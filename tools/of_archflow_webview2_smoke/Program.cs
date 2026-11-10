// OF-000012: isolated local WebView2 acceptance, no login, no external effects.
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using OpenFill.App;
using OpenFill.Core.Agent;
using OpenFill.Core.Logging;
using OpenFill.Core.Model;
using OpenFill.Core.Config;
using OpenFill.Core.Cdp;

internal static class Program
{
    private static int _checks;
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("WEBVIEW2_SMOKE_" + label);
        _checks++;
    }

    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var cts = new CancellationTokenSource();
        var profile = Path.Combine(Path.GetTempPath(), "of_wv2_dev_" + Guid.NewGuid().ToString("N"));
        using var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        var port = ((IPEndPoint)server.LocalEndpoint).Port;
        var serverTask = ServeAsync(server, cts.Token);
        using var form = new Form {
            Width = 720, Height = 480, Location = new Point(-20000, -20000),
            ShowInTaskbar = false, Text = "OF local WebView2 smoke"
        };
        using var view = new WebView2 { Dock = DockStyle.Fill };
        form.Controls.Add(view);
        var success = false;
        var realPlanner = args.Contains("--real-planner", StringComparer.OrdinalIgnoreCase);
        var compareAb = args.Contains("--ab", StringComparer.OrdinalIgnoreCase);
        var effectDev = args.Contains("--effect-dev", StringComparer.OrdinalIgnoreCase);
        using var watchdog = new System.Windows.Forms.Timer { Interval = compareAb ? 240000 : (realPlanner || effectDev ? 90000 : 45000) };
        watchdog.Tick += (_, _) => {
            watchdog.Stop();
            Console.Error.WriteLine("ARCHFLOW_WEBVIEW2_TIMEOUT");
            form.Close();
        };
        form.Shown += async (_, _) =>
        {
            watchdog.Start();
            try
            {
                await RunAsync(view, port, profile, realPlanner, compareAb, effectDev);
                success = true;
                Console.WriteLine("ARCHFLOW_WEBVIEW2_EMBEDDED_" + _checks + "_CHECKS_OK");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ARCHFLOW_WEBVIEW2_FAIL: " + ex);
            }
            finally
            {
                watchdog.Stop();
                form.Close();
            }
        };
        Application.Run(form);
        cts.Cancel();
        server.Stop();
        try { serverTask.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        catch (SocketException) { }
        try { Directory.Delete(profile, recursive: true); }
        catch (IOException ex) { Console.Error.WriteLine("ARCHFLOW_WEBVIEW2_PROFILE_DEFERRED: " + ex.Message); }
        catch (UnauthorizedAccessException ex) { Console.Error.WriteLine("ARCHFLOW_WEBVIEW2_PROFILE_DEFERRED: " + ex.Message); }
        Environment.ExitCode = success ? 0 : 1;
    }

    private static async Task RunAsync(WebView2 view, int port, string profile, bool realPlanner, bool compareAb, bool effectDev)
    {
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: profile);
        await view.EnsureCoreWebView2Async(env);
        Check(view.CoreWebView2 is not null, "runtime_available");
        var origin = "http://127.0.0.1:" + port;
        var navigated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<CoreWebView2NavigationCompletedEventArgs>? handler = null;
        handler = (_, args) => { navigated.TrySetResult(args.IsSuccess); view.CoreWebView2.NavigationCompleted -= handler; };
        view.CoreWebView2.NavigationCompleted += handler;
        view.CoreWebView2.Navigate(origin + "/list");
        Check(await navigated.Task.WaitAsync(TimeSpan.FromSeconds(15)), "fixture_navigated");

        await using var devSession = LocalPlanWebView2DevSession.Attach(view);
        var adapter = devSession.Browser;
        using var eventLog = new EventLog(Path.Combine(profile, "logs"), Path.Combine(profile, "runs"));
        eventLog.BeginRun("of_000013_dev_localplan");
        // These collaborators are deliberately null: the DEV route must not touch
        // model, full registry, or ordinary task configuration.
        var agent = new AgentRunner(null!, null!, null!, eventLog);
        var before = await adapter.ReadAsync(default);
        Check(before.Host == "127.0.0.1", "actual_embedded_host");
        Check(before.Elements.Any(e => e.Role == "link" && e.Name == "Customer 42"), "link_observed");
        var stable = await adapter.ReadAsync(default);
        Check(stable.Revision == before.Revision, "stable_revision");
        // Exercise bounded read-only branches and foreach in the actual embedded WebView2.
        var flow = JsonNode.Parse("""
        {"protocol":"of.flow.v1","expected_host":"127.0.0.1","nodes":[
           {"id":"choice","op":"if","role":"link","name":"Customer 42",
             "then":[{"id":"yes","op":"assert_visible","role":"link","name":"Customer 42"}],
             "else":[{"id":"no","op":"assert_absent","role":"link","name":"Customer 42"}]},
           {"id":"iterate","op":"foreach","role":"link","name_prefix":"Customer ",
             "max_items":5,
             "body":[{"id":"check","op":"assert_visible","role":"link","name":"$item"}]}
        ]}
        """)!.AsObject();
        var flowResult = await agent.RunLocalPlanDevAsync(flow, adapter,
            "of-dev-flow-013", "task-local-flow-013", "run-local-flow-013");
        Check(flowResult.Status == "completed", "flow_completed_" + flowResult.Reason);
        Check(flowResult.ForeachItems == 1, "flow_one_visible_row");
        Check(flowResult.SnapshotCalls >= 4, "flow_local_dom_reads");
        Check(flowResult.ModelCalls == 0 && flowResult.BrowserActionAttempts == 0,
            "flow_no_model_or_browser_mutations");
        var plan = JsonNode.Parse("""
        {"protocol":"of.plan.v1","expected_host":"127.0.0.1","nodes":[
          {"id":"branch","op":"if","role":"link","name":"Customer 42",
           "then":[{"id":"assert_link","op":"assert_visible","role":"link","name":"Customer 42"}],
           "else":[{"id":"assert_missing","op":"assert_absent","role":"link","name":"Customer 42"}]},
          {"id":"loop","op":"foreach","role":"link","name_prefix":"Customer ",
           "max_items":3,"body":[{"id":"assert_item","op":"assert_visible","role":"link","name":"$item"}]},
          {"id":"n1","op":"locate","role":"link","name":"Customer 42","bind":"row"},
          {"id":"n2","op":"click","ref":"$row","effect":"navigation"},
          {"id":"n3","op":"wait_until","role":"textbox","name":"Customer name","timeout_ms":3000},
          {"id":"n4","op":"locate","role":"textbox","name":"Customer name","bind":"field"},
          {"id":"n5","op":"fill","ref":"$field","value":"WebView2 verified"},
          {"id":"n6","op":"assert_value","ref":"$field","value":"WebView2 verified"}
        ]}
        """)!.AsObject();
        var result = await agent.RunLocalPlanDevAsync(plan, adapter,
            "of-dev-form-013", "task-local-form-013", "run-local-form-013");
        Check(result.Status == "completed", "local_plan_" + result.Status + "_" + result.Reason);
        Check(result.NavigationAttempts == 1 && result.FillAttempts == 1,
            "one_navigation_one_fill");
        Check(result.NavigationApplied == 1 && result.FillApplied == 1,
            "adapter_reports_both_applied");
        Check(result.ObservedConditions >= 4, "postconditions_observed");
        Check(result.SnapshotCalls >= 10, "integrated_flow_measured_dom_reads");
        Check(result.ModelCalls == 0 && agent.Usage.Calls == 0,
            "dev_no_responses_api_calls");
        var after = await adapter.ReadAsync(default);
        Check(after.Elements.Single(e => e.Name == "Customer name").Value == "WebView2 verified",
            "actual_dom_value");
        Check(after.Revision > before.Revision, "revision_advanced");
        Check(view.CoreWebView2.Source.EndsWith("/form", StringComparison.Ordinal),
            "embedded_form_navigated");
        Check(flowResult.BusinessOperationId == "of-dev-flow-013" &&
              result.TaskId == "task-local-form-013" &&
              result.RunId == "run-local-form-013", "correlation_preserved");
        var appLog = Directory.GetFiles(Path.Combine(profile, "logs"), "app_*.ndjson").Single();
        using var logStream = new FileStream(appLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var logReader = new StreamReader(logStream);
        var logBody = logReader.ReadToEnd();
        Check(logBody.Split('\n').Count(x => x.Contains("local_plan_dev", StringComparison.Ordinal)) == 2,
            "both_agent_telemetry_events_logged");
        Console.WriteLine("ARCHFLOW_DEV_METRICS " + System.Text.Json.JsonSerializer.Serialize(new {
            flowReads=flowResult.SnapshotCalls, flowModelCalls=flowResult.ModelCalls,
            flowIterations=flowResult.ForeachItems, planReads=result.SnapshotCalls,
            planModelCalls=result.ModelCalls, planNavigations=result.NavigationAttempts,
            planFills=result.FillAttempts, observed=result.ObservedConditions
        }));

        if (effectDev)
        {
            await EffectDevSmoke.RunAsync(view, origin, profile);
            await EffectPlanDevSmoke.RunAsync(view, origin, profile, adapter, agent);
        }

        if (compareAb)
            await ABBenchmark.RunAsync(view, origin, profile, adapter);

        if (realPlanner)
        {
            // The real API call is opt-in. All browser effects remain on the
            // localhost fixture and only the bounded DEV plan parser can act.
            var stablePaths = new AppPaths("stable");
            var devPaths = new AppPaths("dev");
            var config = AppConfig.Load(stablePaths.ConfigFile);
            var secrets = new SecretStore(devPaths.SecretFile, new DpapiSecretProtector());
            var key = secrets.GetApiKey();
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("ARCHFLOW_REAL_KEY_UNAVAILABLE");
            var pageReady = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<CoreWebView2NavigationCompletedEventArgs>? readyHandler = null;
            readyHandler = (_, args) => {
                pageReady.TrySetResult(args.IsSuccess);
                view.CoreWebView2.NavigationCompleted -= readyHandler;
            };
            view.CoreWebView2.NavigationCompleted += readyHandler;
            view.CoreWebView2.Navigate(origin + "/list");
            Check(await pageReady.Task.WaitAsync(TimeSpan.FromSeconds(10)), "real_fixture_ready");
            IModelClient realModel = new DiagnosticModelClient(new OpenAIClient(key, config.OpenAIBaseUrl));
            var plannerAgent = new AgentRunner(realModel, null!, null!, eventLog);
            var planned = await plannerAgent.RunPlannedDevAsync(
                config.Model,
                "On this LOCAL test website, open the link 'Customer 42', " +
                "then set the textbox 'Customer name' to exactly 'Planner verified' " +
                "and assert the new value. The only allowed navigation is the " +
                "visible Customer 42 link and no save/submit/send is permitted.",
                adapter, "of-real-plan-014", "task-real-014", "run-real-014");
            Console.WriteLine("ARCHFLOW_REAL_PLANNER_RESULT " +
                System.Text.Json.JsonSerializer.Serialize(new {
                    planned.Status, planned.Reason, planned.ModelCalls,
                    planned.InputTokens, planned.OutputTokens, planned.TotalDomReads,
                    planned.NavigationAttempts, planned.FillAttempts,
                    planned.ObservedConditions, planned.ElapsedMs, planned.RequestChars
                }));
            Check(planned.ModelCalls == 1, "real_model_called_once");
            if (planned.Status != "completed")
            {
                var current = await adapter.ReadAsync(default);
                Console.WriteLine("ARCHFLOW_REAL_CURRENT_SNAPSHOT " +
                    System.Text.Json.JsonSerializer.Serialize(new {
                        current.Host, current.Revision,
                        elements = current.Elements.Select(e => new {e.Role,e.Name}).ToArray()
                    }));
            }
            Check(planned.Status == "completed", "real_plan_" + planned.Reason);
            var actual = await adapter.ReadAsync(default);
            Check(actual.Elements.Single(e => e.Name == "Customer name").Value == "Planner verified",
                "real_plan_value_verified_in_webview2");
        }
    }

    private sealed class DiagnosticModelClient(IModelClient inner) : IModelClient
    {
        public async Task<JsonObject> CreateResponseAsync(JsonObject request, CancellationToken ct = default)
        {
            var response = await inner.CreateResponseAsync(request, ct);
            try
            {
                var item = response["output"]?.AsArray().OfType<JsonObject>()
                    .FirstOrDefault(o => o["type"]?.ToString() == "function_call");
                var args = JsonNode.Parse(item?["arguments"]?.GetValue<string>() ?? "{}");
                var plan = JsonNode.Parse(args?["plan_json"]?.GetValue<string>() ?? "{}");
                var nodes = plan?["nodes"]?.AsArray().OfType<JsonObject>()
                    .Select(n => string.Join(":", n["op"]?.ToString(),
                        n["role"]?.ToString(), n["name"]?.ToString(), n["ref"]?.ToString()));
                Console.WriteLine("ARCHFLOW_REAL_PLAN_NODES " +
                    string.Join("|", nodes ?? Enumerable.Empty<string>()));
            }
            catch (Exception ex)
            { Console.WriteLine("ARCHFLOW_REAL_PLAN_INSPECT_" + ex.GetType().Name); }
            return response;
        }
    }

    private static async Task ServeAsync(TcpListener listener, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(ct);
                try
                {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var first = await reader.ReadLineAsync(ct) ?? "";
                string? line;
                do { line = await reader.ReadLineAsync(ct); }
                while (!string.IsNullOrEmpty(line));
                var path = first.Split(' ').ElementAtOrDefault(1) ?? "";
                var html = path switch {
                    "/list" => "<!doctype html><title>Fixture</title><a href='/form'>Customer 42</a>",
                    "/form" => "<!doctype html><title>Form</title><label for='n'>Customer name</label><input id='n' type='text' value='Initial'>",
                    "/effects" => "<!doctype html><title>Local effects fixture</title>" +
                        "<button id='save-a'>Save a</button><p id='count-a' data-count='0'></p><p id='result-a'></p>" +
                        "<button id='save-b'>Save b</button><p id='count-b' data-count='0'></p><p id='result-b'></p>" +
                        "<script>['a','b'].forEach(k => document.getElementById('save-'+k).addEventListener('click', () => {" +
                        "const c=document.getElementById('count-'+k);" +
                        "c.dataset.count=String(Number(c.dataset.count)+1);" +
                        "document.getElementById('result-'+k).textContent='Saved '+k;" +
                        "}));</script>",
                    _ => "<!doctype html><title>Missing</title>"
                };
                var bytes = Encoding.UTF8.GetBytes(html);
                var head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " +
                    bytes.Length + "\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(head, ct);
                await stream.WriteAsync(bytes, ct);
                }
                catch (IOException) { /* Browser closed this fixture connection. */ }
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (ct.IsCancellationRequested) { }
    }
}
