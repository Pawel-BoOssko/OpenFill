// OF-000022: exercise the actual internal ToolRegistry.Build() without a
// WebView2 session, model request, or external send.
using OpenFill.Core.Agent;
using OpenFill.Core.Config;
using OpenFill.Core.Logging;
using System.Text.Json.Nodes;

internal static class ToolRegistryEffectRegistrationSmoke
{
    private static void Require(bool ok, string reason)
    {
        if (!ok) throw new InvalidOperationException("OF022_REGISTRATION_" + reason);
    }

    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "of022_registry_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var instance in new[] { "stable", "dev" })
            {
                var instanceRoot = Path.Combine(root, instance);
                using var log = new EventLog(Path.Combine(instanceRoot, "logs"),
                    Path.Combine(instanceRoot, "runs"));
                var notes = new SiteNotesStore(Path.Combine(instanceRoot, "notes"), log);
                var limiter = new OutputLimiter(Path.Combine(instanceRoot, "overflow"), 4000);

                // Build() must not require a browser session. Handlers are not
                // executed except for the authorized=false negative gate below.
                var registry = new ToolRegistry(null!, log, new AppConfig(), limiter,
                    notes, null!, null!);
                var definitions = registry.Build();
                if (instance == "stable")
                {
                    var bytes = definitions.Select(t => new { t.Name, Characters = t.ToDefinition().ToJsonString().Length })
                        .OrderByDescending(x => x.Characters).ToArray();
                    Console.WriteLine("OF_TOKEN_TOOL_SCHEMA_PROFILE total=" + bytes.Sum(x => x.Characters) +
                        " top=" + string.Join(",", bytes.Take(12).Select(x => x.Name + ":" + x.Characters)));
                    Console.WriteLine("OF_TOOL_NAMES " + string.Join(",", bytes.OrderBy(x => x.Name).Select(x => x.Name)));
                    var focused = SingleHostToolProfile.Select(definitions, "Wykonaj konfigurację na https://www.lh.pl/panel")!;
                    Require(focused is not null && focused.Length < definitions.Count, "single_host_reduced");
                    Require(!focused!.Any(t => t.Name.StartsWith("airbnb_") || t.Name.StartsWith("linkedin_") || t.Name.StartsWith("whatsapp_")), "single_host_provider_removed");
                    Require(new[] { "act_many", "upload_file", "ask_user", "finish" }.All(n => focused.Any(t => t.Name == n)), "single_host_generic_preserved");
                    Require(SingleHostToolProfile.Select(definitions, "Z https://www.lh.pl do https://www.linkedin.com") is null, "cross_host_full_fallback");
                    Require(SingleHostToolProfile.Select(definitions, "Przetwórz zadanie bez adresu URL") is null, "no_host_full_fallback");
                    var linkedTools = SingleHostToolProfile.Select(definitions, "Otwórz https://www.linkedin.com/messaging")!;
                    Require(linkedTools.Any(t => t.Name == "linkedin_send_batch"), "linkedin_provider_preserved");
                    var metaTools = SingleHostToolProfile.Select(definitions, "Otwórz https://business.facebook.com/latest")!;
                    Require(metaTools.Any(t => t.Name == "google_meta_view_audit"), "meta_provider_preserved");
                    var crossProvider = SingleHostToolProfile.Select(definitions, "Z https://www.lh.pl wyślij na whatsapp")!;
                    Require(crossProvider.Any(t => t.Name == "whatsapp_send_batch"), "mentioned_provider_preserved");
                    Console.WriteLine("OF_SINGLE_HOST_SCHEMA_CHARS=" + focused.Sum(t => t.ToDefinition().ToJsonString().Length));
                }
                var runJs = definitions.Single(t => t.Name == "run_js");
                var invalidCode = await runJs.Handler(new JsonObject { ["code"] = 29 }, CancellationToken.None);
                Require(!invalidCode.Ok && invalidCode.Output.Contains("run_js.code_invalid_type", StringComparison.Ordinal),
                    instance + "_run_js_numeric_code_rejected_without_browser");
                var blankCode = await runJs.Handler(new JsonObject { ["code"] = " " }, CancellationToken.None);
                Require(!blankCode.Ok && blankCode.Output.Contains("run_js.code_invalid_type", StringComparison.Ordinal),
                    instance + "_run_js_empty_code_rejected_without_browser");
                var li = definitions.SingleOrDefault(t => t.Name == "linkedin_send_current_effect");
                var wa = definitions.SingleOrDefault(t => t.Name == "whatsapp_send_effect");
                var batch = definitions.SingleOrDefault(t => t.Name == "linkedin_send_batch");
                Require((li is not null) == (instance == "dev"),
                    instance + "_linkedin_scope");
                Require((wa is not null) == (instance == "dev"),
                    instance + "_whatsapp_scope");
                // D02 candidate batch was deliberately exposed in LIVE with
                // an explicit authorization gate; single-effect tools stay DEV.
                Require(batch is not null, instance + "_linkedin_batch_scope");
                Require(batch!.Parameters["required"] is JsonArray required &&
                    new[] { "authorized", "request_id", "requester", "chat", "items",
                            "sender_profile_url" }.All(v => required.Any(x => x?.ToString() == v)),
                    instance + "_linkedin_batch_required_scope");
                var denied = await batch.Handler(
                    new JsonObject { ["authorized"] = false }, CancellationToken.None);
                Require(!denied.Ok &&
                    denied.Output.Contains("authorization", StringComparison.OrdinalIgnoreCase),
                    instance + "_linkedin_batch_auth_gate");

                if (instance != "dev") continue;
                Require(li!.Parameters["required"] is JsonArray liReq &&
                    liReq.Any(x => x?.ToString() == "authorized") &&
                    liReq.Any(x => x?.ToString() == "effect_id") &&
                    liReq.Any(x => x?.ToString() == "thread_url"),
                    "linkedin_required_scope");
                Require(wa!.Parameters["required"] is JsonArray waReq &&
                    waReq.Any(x => x?.ToString() == "authorized") &&
                    waReq.Any(x => x?.ToString() == "effect_id"),
                    "whatsapp_required_scope");
                Require(batch!.Parameters["required"] is JsonArray batchReq &&
                    new[] { "authorized", "request_id", "requester", "chat", "items",
                            "sender_profile_url" }.All(v => batchReq.Any(x => x?.ToString() == v)),
                    "linkedin_batch_required_scope");
                var noBatchAuth = await batch.Handler(
                    new JsonObject { ["authorized"] = false }, CancellationToken.None);
                Require(!noBatchAuth.Ok &&
                    noBatchAuth.Output.Contains("authorization", StringComparison.OrdinalIgnoreCase),
                    "linkedin_batch_auth_gate");
                var badBatch = await batch.Handler(new JsonObject {
                    ["authorized"] = true,
                    ["sender_profile_url"] = "invalid",
                    ["request_id"] = "of023-invalid",
                    ["requester"] = "test",
                    ["chat"] = "test",
                    ["items"] = new JsonArray(new JsonObject {
                        ["profile_url"] = "invalid",
                        ["thread_url"] = "invalid",
                        ["message"] = "not sent"
                    })
                }, CancellationToken.None);
                Require(badBatch.Ok &&
                    badBatch.Output.Contains("invalid_sender_profile", StringComparison.Ordinal),
                    "linkedin_batch_invalid_scope_no_browser");

                var noLiAuth = await li.Handler(new JsonObject { ["authorized"] = false },
                    CancellationToken.None);
                Require(!noLiAuth.Ok &&
                    noLiAuth.Output.Contains("authorization", StringComparison.OrdinalIgnoreCase),
                    "linkedin_auth_gate");
                var noWaAuth = await wa.Handler(new JsonObject { ["authorized"] = false },
                    CancellationToken.None);
                Require(!noWaAuth.Ok &&
                    noWaAuth.Output.Contains("authoriz", StringComparison.OrdinalIgnoreCase),
                    "whatsapp_auth_gate");

                var liBadScope = await li.Handler(new JsonObject {
                    ["authorized"] = true, ["business_operation_id"] = "of022-test",
                    ["effect_id"] = "send-1", ["task_id"] = "task-022",
                    ["run_id"] = "run-022", ["sender_profile_url"] = "invalid",
                    ["target_profile_url"] = "invalid", ["thread_url"] = "invalid",
                    ["message"] = "Do not send"
                }, CancellationToken.None);
                Require(!liBadScope.Ok, "linkedin_invalid_scope_rejected");
            }
            Console.WriteLine("OF023_LINKEDIN_BATCH_AND_RUN_JS_GATES_18_CHECKS_OK");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
