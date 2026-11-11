// OF-000015: independent A/B using actual legacy AgentRunner.RunAsync
// and opt-in AgentRunner.RunPlannedDevAsync on the same isolated localhost fixture.
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using OpenFill.App;
using OpenFill.Core.Agent;
using OpenFill.Core.Browser;
using OpenFill.Core.Cdp;
using OpenFill.Core.Config;
using OpenFill.Core.Logging;
using EventLog = OpenFill.Core.Logging.EventLog;
using OpenFill.Core.Model;

internal static class ABBenchmark
{
    private const string Goal =
        "On the currently open LOCAL test website: open the visible link " +
        "'Customer 42'. On the resulting form set the textbox labeled " +
        "'Customer name' to exactly 'AB verified'. Verify that the textbox " +
        "actually contains 'AB verified'. Do not save, submit, send, " +
        "purchase, register or open an external website.";

    private sealed class CountingClient(IModelClient inner) : IModelClient
    {
        public int Calls { get; private set; }
        public long InTokens { get; private set; }
        public long OutTokens { get; private set; }
        public long CachedTokens { get; private set; }
        public long RequestChars { get; private set; }
        public async Task<JsonObject> CreateResponseAsync(JsonObject req, CancellationToken ct = default)
        {
            Calls++;
            RequestChars += req.ToJsonString().Length;
            var resp = await inner.CreateResponseAsync(req, ct);
            var usage = resp["usage"];
            if (long.TryParse(usage?["input_tokens"]?.ToString(), out var input))
                InTokens += input;
            if (long.TryParse(usage?["output_tokens"]?.ToString(), out var output))
                OutTokens += output;
            if (long.TryParse(usage?["input_tokens_details"]?["cached_tokens"]?.ToString(), out var cached))
                CachedTokens += cached;
            return resp;
        }
    }

    private static async Task NavigateAsync(WebView2 view, string url)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<CoreWebView2NavigationCompletedEventArgs>? handler = null;
        handler = (_, ev) =>
        {
            view.CoreWebView2.NavigationCompleted -= handler;
            done.TrySetResult(ev.IsSuccess);
        };
        view.CoreWebView2.NavigationCompleted += handler;
        view.CoreWebView2.Navigate(url);
        if (!await done.Task.WaitAsync(TimeSpan.FromSeconds(12)))
            throw new InvalidOperationException("ab_fixture_navigation_failed");
    }

    private static async Task<bool> VerifyAsync(LocalPlanCdpBrowser browser, WebView2 view)
    {
        var snapshot = await browser.ReadAsync(default);
        return view.CoreWebView2.Source.EndsWith("/form", StringComparison.Ordinal)
            && snapshot.Elements.Count(e => e.Role == "textbox"
                && e.Name == "Customer name" && e.Value == "AB verified") == 1;
    }

    public static async Task RunAsync(WebView2 view, string origin, string root,
        LocalPlanCdpBrowser localBrowser)
    {
        var stable = new AppPaths("stable");
        var dev = new AppPaths("dev");
        var cfg = AppConfig.Load(stable.ConfigFile);
        cfg.MaxSteps = 12;
        cfg.MaxStepExtensions = 0;
        cfg.TaskTimeoutMinutes = 3;
        var key = new SecretStore(dev.SecretFile, new DpapiSecretProtector()).GetApiKey();
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("ab_no_dev_api_key");
        var url = origin + "/list";

        await NavigateAsync(view, url);
        var legacyLogDir = Path.Combine(root, "ab_legacy_logs");
        using var legacyLog = new EventLog(legacyLogDir, Path.Combine(legacyLogDir, "runs"));
        legacyLog.BeginRun("of_000015_legacy");
        var toolNames = new List<string>();
        legacyLog.Published += e =>
        {
            if (e.Source == "tool" && e.EventType == "call" &&
                e.Data?["name"] is JsonNode n) toolNames.Add(n.ToString());
        };
        await using var conn = new WebView2CdpConnection(view, view.CoreWebView2);
        var controller = new BrowserController(new CdpSession(conn), legacyLog);
        await controller.InitAsync();
        var registry = new ToolRegistry(controller, legacyLog, cfg,
            new OutputLimiter(Path.Combine(root, "ab_overflow"), 12000),
            new SiteNotesStore(Path.Combine(root, "ab_notes"), legacyLog),
            new HeadlessInteraction(), new GapRecorder(
                Path.Combine(root, "ab_gaps.ndjson"), legacyLog));
        var oldClient = new CountingClient(new OpenAIClient(key, cfg.OpenAIBaseUrl));
        var oldAgent = new AgentRunner(oldClient, registry, cfg, legacyLog);
        string legacyStatus, legacyReason;
        var swLegacy = Stopwatch.StartNew();
        try
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(150));
            var conclusion = await oldAgent.RunAsync(Goal, limit.Token);
            legacyStatus = conclusion.Status;
            legacyReason = conclusion.Summary.Length > 100
                ? conclusion.Summary[..100] : conclusion.Summary;
        }
        catch (Exception ex)
        {
            legacyStatus = "exception_" + ex.GetType().Name;
            legacyReason = ex.Message.Length > 100 ? ex.Message[..100] : ex.Message;
        }
        swLegacy.Stop();
        bool legacyVerified;
        try { legacyVerified = await VerifyAsync(localBrowser, view); }
        catch { legacyVerified = false; }

        // Identical goal, fresh navigation, same model/account/fixture, no
        // browser model history shared with legacy and independent verification.
        await NavigateAsync(view, url);
        var planClient = new CountingClient(new OpenAIClient(key, cfg.OpenAIBaseUrl));
        var planLogDir = Path.Combine(root, "ab_plan_logs");
        using var planLog = new EventLog(planLogDir, Path.Combine(planLogDir, "runs"));
        planLog.BeginRun("of_000015_planner");
        var planAgent = new AgentRunner(planClient, null!, null!, planLog);
        var swPlanner = Stopwatch.StartNew();
        var planned = await planAgent.RunPlannedDevAsync(cfg.Model, Goal, localBrowser,
            "of-ab-operation-015", "of-ab-task-planner", "of-ab-run-planner");
        swPlanner.Stop();
        bool plannerVerified;
        try { plannerVerified = await VerifyAsync(localBrowser, view); }
        catch { plannerVerified = false; }

        var result = new {
            Test = "OF-000015_AB_SINGLE_PAIR",
            Host = "localhost fixture (127.0.0.1)",
            Model = cfg.Model, Expected = "AB verified",
            Legacy = new {
                AgentStatus = legacyStatus, AgentReason = legacyReason,
                Verified = legacyVerified, ModelCalls = oldClient.Calls,
                InputTokens = oldClient.InTokens, OutputTokens = oldClient.OutTokens,
                CachedTokens = oldClient.CachedTokens, RequestChars = oldClient.RequestChars,
                ElapsedMs = swLegacy.ElapsedMilliseconds,
                ToolCalls = toolNames.Count, Tools = toolNames.ToArray()
            },
            Planner = new {
                planned.Status, planned.Reason, Verified = plannerVerified,
                ModelCalls = planClient.Calls, InputTokens = planClient.InTokens,
                OutputTokens = planClient.OutTokens, CachedTokens = planClient.CachedTokens,
                RequestChars = planClient.RequestChars, ElapsedMs = swPlanner.ElapsedMilliseconds,
                DomReads = planned.TotalDomReads,
                NavigationAttempts = planned.NavigationAttempts, FillAttempts = planned.FillAttempts,
                ConditionsObserved = planned.ObservedConditions
            }
        };
        var json = JsonSerializer.Serialize(result);
        Console.WriteLine("ARCHFLOW_AB_RESULT " + json);
        File.WriteAllText(Path.Combine(
            Path.GetDirectoryName(typeof(ABBenchmark).Assembly.Location)!,
            "ab_of_000015_result.json"), json);
        if (!legacyVerified || !plannerVerified)
            throw new InvalidOperationException("ARCHFLOW_AB_INCOMPLETE");
        Console.WriteLine("ARCHFLOW_AB_BOTH_RESULTS_VERIFIED");
    }
}
