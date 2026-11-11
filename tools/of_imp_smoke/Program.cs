using OpenFill.Core.Agent;
if (args.Contains("d04-domain-read-only", StringComparer.OrdinalIgnoreCase))
{
    D04DomainReadSmoke.Run();
    return;
}
if (args.Contains("e02-procedure-learning-only", StringComparer.OrdinalIgnoreCase))
{
    ProcedureLearningSmoke.Run();
    return;
}
if (args.Contains("localplan-auto-routing-only", StringComparer.OrdinalIgnoreCase))
{
    LocalPlanAutoRoutingSmoke.Run();
    return;
}
if (args.Contains("of031-text-recipe-only", StringComparer.OrdinalIgnoreCase))
{
    await Of031TextRecipeSmoke.RunAsync();
    return;
}
if (args.Contains("of027-nested-capture-only", StringComparer.OrdinalIgnoreCase))
{
    await NestedCaptureQuerySmoke.RunAsync();
    return;
}
if (args.Contains("of026-mcp-shared-root-only", StringComparer.OrdinalIgnoreCase))
{
    await McpSharedCaptureRootSmoke.RunAsync();
    return;
}
if (args.Contains("d02-linkedin-batch-only", StringComparer.OrdinalIgnoreCase))
{
    await LinkedInBatchEffectSmoke.RunAsync();
    return;
}
if (args.Contains("of025-whatsapp-batch-only", StringComparer.OrdinalIgnoreCase))
{
    await WhatsAppBatchEffectSmoke.RunAsync();
    return;
}
if (args.Contains("of022-effect-registry-only", StringComparer.OrdinalIgnoreCase))
{
    await ToolRegistryEffectRegistrationSmoke.RunAsync();
    return;
}
if (args.Contains("archflow-whatsapp-effect-only", StringComparer.OrdinalIgnoreCase))
{
    await WhatsAppEffectSmoke.RunAsync();
    return;
}
if (args.Contains("archflow-effect-executor-only", StringComparer.OrdinalIgnoreCase))
{
    await LocalPlanEffectExecutorSmoke.RunAsync();
    return;
}
if (args.Contains("archflow-integrated-flow-only", StringComparer.OrdinalIgnoreCase))
{
    await LocalPlanIntegratedFlowSmoke.RunAsync();
    return;
}
if (args.Contains("archflow-planner-only", StringComparer.OrdinalIgnoreCase))
{
    await LocalPlanModelSmoke.RunAsync();
    return;
}
if (args.Contains("archflow-flow-only", StringComparer.OrdinalIgnoreCase))
{
    await LocalPlanFlowSmoke.RunAsync();
    return;
}
if (args.Contains("archflow-fill-only", StringComparer.OrdinalIgnoreCase))
{
    await LocalPlanFillSmoke.RunAsync();
    return;
}
if (args.Contains("archflow-wait-only", StringComparer.OrdinalIgnoreCase))
{
    await LocalPlanWaitSmoke.RunAsync();
    return;
}
if (args.Contains("archflow-effect-journal-only", StringComparer.OrdinalIgnoreCase))
{
    EffectJournalSmoke.Run();
    return;
}
if (args.Contains("archflow-local-plan-only", StringComparer.OrdinalIgnoreCase))
{
    await LocalPlanSmoke.RunAsync();
    return;
}
if (args.Contains("archflow-coverage-only", StringComparer.OrdinalIgnoreCase))
{
    var measured = new System.Text.Json.Nodes.JsonObject
    {
        ["status"] = "ok",
        ["locator_coverage"] = new System.Text.Json.Nodes.JsonObject
        {
            ["records_considered"] = 4, ["records_with_selector"] = 1,
            ["controls_considered"] = 5, ["controls_with_selector"] = 2,
            ["controls_returned"] = 1
        }
    };
    var coverage = SemanticViewDigest.LocatorCoverage(measured);
    if (coverage["status"]?.ToString() != "measured" ||
        coverage["record_coverage_pct"]?.GetValue<double>() != 25.0 ||
        coverage["control_coverage_pct"]?.GetValue<double>() != 40.0)
        throw new Exception("ARCHFLOW coverage percentages are incorrect");
    var empty = new System.Text.Json.Nodes.JsonObject
    {
        ["status"] = "ok",
        ["locator_coverage"] = new System.Text.Json.Nodes.JsonObject
        {
            ["records_considered"] = 0, ["records_with_selector"] = 0,
            ["controls_considered"] = 0, ["controls_with_selector"] = 0,
            ["controls_returned"] = 0
        }
    };
    var zero = SemanticViewDigest.LocatorCoverage(empty);
    if (zero["status"]?.ToString() != "measured" ||
        zero["record_coverage_pct"] is not null ||
        zero["control_coverage_pct"] is not null)
        throw new Exception("ARCHFLOW zero denominator must be unknown, not 0 percent");
    var invalid = measured.DeepClone().AsObject();
    invalid["locator_coverage"]!["records_with_selector"] = 6;
    if (SemanticViewDigest.LocatorCoverage(invalid)["status"]?.ToString() != "invalid")
        throw new Exception("ARCHFLOW invalid numerator was accepted");
    if (SemanticViewDigest.LocatorCoverage(new System.Text.Json.Nodes.JsonObject { ["status"] = "ok" })
        ["status"]?.ToString() != "unavailable")
        throw new Exception("ARCHFLOW missing instrumentation treated as measured");
    var supported = new System.Text.Json.Nodes.JsonArray(
        new System.Text.Json.Nodes.JsonObject { ["id"] = "of1", ["action"] = "set", ["value"] = "x" },
        new System.Text.Json.Nodes.JsonObject { ["id"] = "of2", ["action"] = "click" });
    if (!ToolRegistry.LegacyBatchStepsSupported(supported))
        throw new Exception("ARCHFLOW valid legacy batch rejected");
    var unsupported = new System.Text.Json.Nodes.JsonArray(
        new System.Text.Json.Nodes.JsonObject { ["id"] = "of1", ["action"] = "click" },
        new System.Text.Json.Nodes.JsonObject { ["id"] = "of2", ["action"] = "realClick" });
    if (ToolRegistry.LegacyBatchStepsSupported(unsupported))
        throw new Exception("ARCHFLOW realClick would be silently sent to JS legacy batch");
    var missingId = new System.Text.Json.Nodes.JsonArray(
        new System.Text.Json.Nodes.JsonObject { ["id"] = "", ["action"] = "click" });
    if (ToolRegistry.LegacyBatchStepsSupported(missingId))
        throw new Exception("ARCHFLOW missing legacy element ID accepted");
    Console.WriteLine("ARCHFLOW_LOCATOR_COVERAGE_4_CHECKS_OK");
    Console.WriteLine("ARCHFLOW_LEGACY_BATCH_VALIDATION_3_CHECKS_OK");
    return;
}
if (args.Contains("history-interrupted-only", StringComparer.OrdinalIgnoreCase))
{
    var tempHistoryDir = Path.Combine(Path.GetTempPath(), "of_history_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempHistoryDir);
    try
    {
        var file = Path.Combine(tempHistoryDir, "tasks.ndjson");
        var history = new OpenFill.Core.Hosting.HistoryStore(file);
        var start = DateTime.UtcNow.AddHours(-2);
        history.Append(new OpenFill.Core.Hosting.HistoryRecord("old-running", "mcp", "test", start, null, "running", null, "run_01"));
        history.Append(new OpenFill.Core.Hosting.HistoryRecord("completed", "mcp", "test", start, start.AddMinutes(5), "success", "ok", "run_02"));
        if (history.PersistInterruptedFromPreviousProcess() != 1)
            throw new Exception("P1 history: expected one stale running task");
        var rows = history.Load(10, Array.Empty<string>());
        if (rows.Single(r => r.Id == "old-running").Status != "interrupted" ||
            rows.Single(r => r.Id == "old-running").EndedUtc is not null ||
            rows.Single(r => r.Id == "completed").Status != "success")
            throw new Exception("P1 history: incorrect status or invented end timestamp");
        if (history.PersistInterruptedFromPreviousProcess() != 0)
            throw new Exception("P1 history: duplicate terminal append after restart");
        Console.WriteLine("HISTORY_P1_RECONCILE_3_CHECKS_OK");
    }
    finally { Directory.Delete(tempHistoryDir, true); }
    return;
}
if (args.Contains("airbnb-source-scope-only", StringComparer.OrdinalIgnoreCase))
{
    var trusted = new[] {
        "https://www.airbnb.com/hosting/calendar",
        "https://pl.airbnb.com/multicalendar",
        "https://airbnb.com/multicalendar/123"
    };
    var rejected = new[] {
        "http://pl.airbnb.com/multicalendar",
        "https://pl.airbnb.com.evil.example/multicalendar",
        "https://airbnb.com/account-settings",
        "https://airbnb.com/multicalendar-evil"
    };
    if (trusted.Any(u => !AirbnbPriceSourceEvidenceGate.IsTrustedCalendarUrl(u)) ||
        rejected.Any(u => AirbnbPriceSourceEvidenceGate.IsTrustedCalendarUrl(u)))
        throw new Exception("D03 calendar evidence URL boundary rejected valid or accepted invalid URL");
    var temp = Path.Combine(Path.GetTempPath(), "of_d03_scope_" + Guid.NewGuid().ToString("N"));
    var captures = Path.Combine(temp, "captures");
    Directory.CreateDirectory(captures);
    try
    {
        var sourceScopeMemory = new OperationalMemoryStore(Path.Combine(temp, "memory", "operational.db"));
        var date = new DateOnly(2026, 11, 2);
        var proof = new System.Text.Json.Nodes.JsonObject {
            ["provider"]="airbnb", ["source_kind"]="webview2_dom",
            ["scope"]="single_listing_day", ["complete"]=true,
            ["url"]="https://pl.airbnb.com/multicalendar",
            ["account_id"]="account-1", ["listing_id"]="123",
            ["currency"]="PLN", ["rate_plan"]="",
            ["date"]=date.ToString("yyyy-MM-dd"),
            ["price"]=250m, ["observed_at_utc"]=DateTimeOffset.UtcNow.ToString("O")
        };
        var bytes = System.Text.Encoding.UTF8.GetBytes(proof.ToJsonString());
        var path = Path.Combine(captures, "day.json");
        File.WriteAllBytes(path, bytes);
        var reference = new AirbnbPriceCaptureReference(date, path,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
        var result = new AirbnbPriceSourceEvidenceGate(sourceScopeMemory).Validate(
            "account-1", "123", "PLN", "", date, date, new[] { reference });
        if (result.Result["status"]?.ToString() != "ready" ||
            result.Observations.Count != 1 || result.Observations[0].CurrentPrice != 250m)
            throw new Exception("D03 valid localized no-rate-plan evidence was rejected");
    }
    finally
    {
        try { Directory.Delete(temp, true); } catch (IOException) { }
    }
    Console.WriteLine("D03_AIRBNB_SOURCE_SCOPE_8_CHECKS_OK");
    return;
}
if (args.Contains("whatsapp-recent-only", StringComparer.OrdinalIgnoreCase))
{
    var sample = new System.Text.Json.Nodes.JsonArray();
    sample.Add(new System.Text.Json.Nodes.JsonObject { ["text"]="old", ["direction"]="in" });
    sample.Add(new System.Text.Json.Nodes.JsonObject { ["text"]="recent", ["direction"]="out" });
    sample.Add(new System.Text.Json.Nodes.JsonObject { ["text"]=new string('x', 450), ["direction"]="in" });
    var last = WhatsAppBatchService.ProjectRecentMessages(sample, 2);
    if (last.Count != 2 || last[0]?["text"]?.ToString() != "recent" ||
        last[1]?["text"]?.ToString().Length != 400 ||
        WhatsAppBatchService.ProjectRecentMessages(sample, 0).Count != 0)
        throw new Exception("A24 compact WhatsApp batch projection failed");
    Console.WriteLine("A24_WHATSAPP_BATCH_RECENT_4_CHECKS_OK");
    return;
}
if (args.Contains("airbnb-calendar-only", StringComparer.OrdinalIgnoreCase))
{
    var cells = new System.Text.Json.Nodes.JsonArray(
        new System.Text.Json.Nodes.JsonObject {
            ["date"]="2026-11-02",["text"]="250 zł",["in_viewport"]=true
        },
        new System.Text.Json.Nodes.JsonObject {
            ["date"]="2026-11-03",["text"]="300 zł",["in_viewport"]=false
        });
    var raw = new System.Text.Json.Nodes.JsonObject {
        ["status"]="ok",["listing_id"]="123",
        ["url"]="https://pl.airbnb.com/multicalendar",
        ["cells"]=cells
    };
    var result=AirbnbCalendarReadService.Summarize(raw,"123",
        new DateOnly(2026,11,2),new DateOnly(2026,11,4),35m,0);
    if (result["status"]?.ToString()!="ok" ||
        result["observed_dates"]?.GetValue<int>()!=2 ||
        result["missing_dates"]?.GetValue<int>()!=1 ||
        result["all_prices_verified"]?.GetValue<bool>()!=false ||
        AirbnbCalendarReadService.Summarize(raw,"999",
            new DateOnly(2026,11,2),new DateOnly(2026,11,4))["status"]?.ToString()!="listing_mismatch")
        throw new Exception("D03 current calendar date observation failed");
    Console.WriteLine("D03_AIRBNB_CALENDAR_READ_5_CHECKS_OK");
    return;
}
if (args.Contains("capture-cache-only", StringComparer.OrdinalIgnoreCase))
{
    var temp = Path.Combine(Path.GetTempPath(), "of_a27_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(temp);
    try
    {
        var body = "A27 unchanged current text";
        var now = DateTimeOffset.UtcNow;
        var manifest = await PageCaptureService.SaveSnapshotForTestAsync(
            new System.Text.Json.Nodes.JsonObject {
                ["text"]=body, ["url"]="https://example.test/page?tab=2",
                ["title"]="Current", ["selector"]="body",
                ["capturedUtc"]=now.ToString("O")
            }, "text", "", temp, 10);
        bool Match(string url, string value) =>
            CurrentTextCaptureCache.IsExactMatch(manifest,url,"body",value,temp,now);
        if (!Match("https://example.test/page?tab=2",body) ||
            Match("https://example.test/page?tab=1",body) ||
            Match("https://example.test/page?tab=2",body + " CHANGED") ||
            CurrentTextCaptureCache.IsExactMatch(manifest,"https://www.linkedin.com/page","body",
                body,temp,now) ||
            CurrentTextCaptureCache.IsExactMatch(manifest,"https://example.test/page?tab=2",
                "body",body,temp,now.AddMinutes(31)))
            throw new Exception("A27 current text cache scope mismatch");
        var src=(manifest["files"] as System.Text.Json.Nodes.JsonArray)![0]!["path"]!.ToString();
        File.AppendAllText(src,"modified");
        if (Match("https://example.test/page?tab=2",body))
            throw new Exception("A27 modified cached bytes were reused");
        Console.WriteLine("A27_CURRENT_TEXT_CACHE_6_CHECKS_OK");
    }
    finally { Directory.Delete(temp,true); }
    return;
}
var root=Path.Combine(Path.GetTempPath(),"of_imp_smoke_"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var text=Path.Combine(root,"sample.txt");
var json=Path.Combine(root,"sample.json");
var nd=Path.Combine(root,"sample.ndjson");
await File.WriteAllTextAsync(text,"Ala ma kota\nAla ma psa\nZosia ma kota\n");
await File.WriteAllTextAsync(json,"{\"records\":[{\"name\":\"Ala\"},{\"name\":\"Zosia\"}]}");
await File.WriteAllTextAsync(nd,"{\"name\":\"Ala\"}\n{\"name\":\"Zosia\"}\n");
Console.WriteLine("availability="+FileFilterService.Availability().ToJsonString());
foreach (var (path,op,q) in new[]{(text,"text_literal","Ala"),(text,"text_count","Ala"),(text,"text_literal","BRAK"),(json,"json_select",".records[].name"),(nd,"ndjson_select",".name")})
{
    var r=await FileFilterService.QueryAsync(path,op,q,root);
    Console.WriteLine(op+" status="+r["status"]+" preview="+r["preview"]+" output="+r["output_path"]);
    var expected=op=="text_literal"&&q=="BRAK"?"zero_matches":"ok";
    if(r["status"]?.ToString()!=expected)throw new Exception("MISMATCH "+op+" expected "+expected+" actual "+r["status"]);
}
var capture = await PageCaptureService.SaveSnapshotForTestAsync(
    new System.Text.Json.Nodes.JsonObject { ["text"]="Example capture", ["url"]="https://example.test/path", ["title"]="Example", ["selector"]="body", ["capturedUtc"]=DateTime.UtcNow.ToString("O") },
    "text", "", root, 50);
var manifestPath = capture["manifest_path"]?.ToString();
if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath)) throw new Exception("Capture manifest missing");
Console.WriteLine("CAPTURE_MANIFEST_OK "+capture["capture_id"]);
if(capture["source_kind"]?.ToString()!="webview2_dom" || capture["normalized_route"]?.ToString()!="/path")
    throw new Exception("Capture provenance missing");
if(capture["provider"]?.ToString()!="other" || capture["completeness"]?.ToString()!="unverified")
    throw new Exception("Capture scope overstated");
if(capture["account_scope"] is not null || capture["all_pages_complete"]?.GetValue<bool>()!=false)
    throw new Exception("Unverified account or pagination was marked complete");
if(capture["view_signature"]?.ToString().Length!=64 || capture["content_fingerprint"]?.ToString().Length!=64)
    throw new Exception("Capture fingerprints missing");
var viewA=new System.Text.Json.Nodes.JsonObject { ["url"]="https://www.linkedin.com/messaging/thread/abc?tracking=1",["selector"]="main" };
var viewB=new System.Text.Json.Nodes.JsonObject { ["url"]="https://www.linkedin.com/messaging/thread/abc?tracking=2",["selector"]="main" };
var metaA=new System.Text.Json.Nodes.JsonObject { ["files"]=new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject{["kind"]="text",["sha256"]="a"}) };
var metaB=(System.Text.Json.Nodes.JsonObject)metaA.DeepClone();
CaptureManifestMetadata.Apply(metaA,viewA,"text");
CaptureManifestMetadata.Apply(metaB,viewB,"text");
if(metaA["provider"]?.ToString()!="linkedin" || metaA["view_signature"]?.ToString()!=metaB["view_signature"]?.ToString())
    throw new Exception("View signature changed on query string only");
metaB["files"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject{["kind"]="text",["sha256"]="b"});
CaptureManifestMetadata.Apply(metaB,viewB,"text");
if(metaA["content_fingerprint"]?.ToString()==metaB["content_fingerprint"]?.ToString())
    throw new Exception("Different capture content has same fingerprint");
Console.WriteLine("CAPTURE_SCOPE_METADATA_8_CHECKS_OK");
// A06: repeat local queries by capture_id, with manifest/path/hash integrity and no browser.
var a06Id=capture["capture_id"]?.ToString() ?? "";
var a06Resolved=CaptureArtifactQueryService.Resolve(root,a06Id,"text");
if(a06Resolved["status"]?.ToString()!="ready") throw new Exception("A06 capture ID not resolved");
var a06Query=await CaptureArtifactQueryService.QueryAsync(root,a06Id,"text",
    "text_literal","Example",root,10,CancellationToken.None);
if(a06Query["status"]?.ToString()!="ok" || a06Query["reused_snapshot"]?.GetValue<bool>()!=true)
    throw new Exception("A06 existing capture not queried");
var a06Again=await CaptureArtifactQueryService.QueryAsync(root,a06Id,"text",
    "text_count","capture",root,10,CancellationToken.None);
if(a06Again["status"]?.ToString()!="ok" || a06Again["capture_id"]?.ToString()!=a06Id)
    throw new Exception("A06 second local query failed");
if(CaptureArtifactQueryService.Resolve(root,"../bad","text")["reason_code"]?.ToString()!="capture_id_invalid")
    throw new Exception("A06 unsafe capture ID accepted");
if(CaptureArtifactQueryService.Resolve(root,a06Id,"elements")["reason_code"]?.ToString()!="capture_kind_missing_or_ambiguous")
    throw new Exception("A06 missing kind accepted");
if((await CaptureArtifactQueryService.QueryAsync(root,a06Id,"text",
    "json_select",".",root,10,CancellationToken.None))["reason_code"]?.ToString()!="capture_operation_kind_mismatch")
    throw new Exception("A06 incompatible query accepted");
var a06Path=a06Resolved["source_path"]!.ToString();
var a06Original=await File.ReadAllTextAsync(a06Path);
await File.WriteAllTextAsync(a06Path,a06Original+" tampered");
if(CaptureArtifactQueryService.Resolve(root,a06Id,"text")["reason_code"]?.ToString()!="capture_hash_mismatch")
    throw new Exception("A06 tampered capture accepted");
await File.WriteAllTextAsync(a06Path,a06Original);
if(CaptureArtifactQueryService.Resolve(root,a06Id,"text")["status"]?.ToString()!="ready")
    throw new Exception("A06 restored capture not accepted");
var a06ManifestOriginal=await File.ReadAllTextAsync(manifestPath!);
var a06Manifest=System.Text.Json.Nodes.JsonNode.Parse(a06ManifestOriginal)!.AsObject();
a06Manifest["capture_id"]="capture_20000101_000000_000_ffffff";
await File.WriteAllTextAsync(manifestPath!,a06Manifest.ToJsonString());
if(CaptureArtifactQueryService.Resolve(root,a06Id,"text")["reason_code"]?.ToString()!="capture_manifest_id_mismatch")
    throw new Exception("A06 wrong manifest identity accepted");
await File.WriteAllTextAsync(manifestPath!,a06ManifestOriginal);
Console.WriteLine("A06_CAPTURE_ID_QUERY_INTEGRITY_9_CHECKS_OK");
// A06 typed view routing: no repeated DOM for capture_id; forms keep action IDs.
var vr1=ViewReadRouter.Select("list",null,null,null);
if(vr1.Target!="observe_query" || vr1.Operation!="text_regex" || vr1.Query!=".+")
    throw new Exception("A06 list did not use local filter");
if(ViewReadRouter.Select("table",null,"json_select",".records").Target!="observe_query")
    throw new Exception("A06 table not routed to filter");
if(ViewReadRouter.Select("inbox",null,null,null).Target!="observe_query")
    throw new Exception("A06 inbox not routed to filter");
if(ViewReadRouter.Select("form",null,null,null).Target!="get_page")
    throw new Exception("A06 form lost interactive controls");
if(ViewReadRouter.Select("detail",null,null,null).Target!="get_page")
    throw new Exception("A06 detail lost interactive controls");
if(ViewReadRouter.Select("auto",null,null,null).Target!="get_page")
    throw new Exception("A06 unknown view incorrectly captured");
if(ViewReadRouter.Select("auto",null,"text_literal","hello").Target!="observe_query")
    throw new Exception("A06 auto query not routed to filter");
if(ViewReadRouter.Select("list",a06Id,"text_count","Example").Target!="file_query")
    throw new Exception("A06 stored capture was not reused");
if(ViewReadRouter.Select("form",a06Id,"text_count","Example").ReasonCode!="capture_not_interactive")
    throw new Exception("A06 old snapshot allowed for form controls");
if(ViewReadRouter.Select("list",a06Id,null,null).ReasonCode!="stored_capture_query_required")
    throw new Exception("A06 missing stored query accepted");
if(ViewReadRouter.Select("invalid",null,null,null).ReasonCode!="view_type_invalid")
    throw new Exception("A06 invalid view type accepted");
if(ViewReadRouter.Select("table",null,"json_select",null).ReasonCode!="query_required")
    throw new Exception("A06 empty jq query accepted");
Console.WriteLine("A06_TYPED_VIEW_ROUTING_12_CHECKS_OK");
if(ViewReadRouter.Select("list",null,"ndjson_select",".name").ReasonCode!="operation_requires_stored_ndjson")
    throw new Exception("A06 unsupported live NDJSON caused an unnecessary DOM read");
if(ViewReadRouter.ResolveFileKind(null,"json_select")!="elements")
    throw new Exception("A06 JSON query did not select elements artifact");
if(ViewReadRouter.ResolveFileKind(null,"text_count")!="text")
    throw new Exception("A06 text query did not select text artifact");
if(ViewReadRouter.ResolveFileKind("html","json_select")!="html")
    throw new Exception("A06 explicit file kind was overwritten");
Console.WriteLine("A06_FILE_KIND_AND_LIVE_NDJSON_4_CHECKS_OK");
// A06: invalid requests must be rejected before any WebView2 capture or local file read.
var a06Preflight = new (string? View, string? Capture, string? Operation, string? Query, string? FileKind, string Expected)[]
{
    ("list", null, "bad_filter", "x", null, "operation_invalid"),
    ("list", null, "json_select", ".", "text", "operation_file_kind_mismatch"),
    ("list", null, "text_count", "x", "elements", "operation_file_kind_mismatch"),
    ("list", null, "text_literal", "x", "html", "live_file_kind_unsupported"),
    ("list", null, "text_literal", "x", "csv", "file_kind_invalid"),
    ("auto", null, null, null, "elements", "file_kind_requires_filter"),
    ("list", null, null, null, "elements", "file_kind_requires_operation"),
    ("list", "capture_20261101_103734_240_a157ee", "ndjson_select", ".", null, "capture_ndjson_not_supported"),
    ("list", "capture_20261101_103734_240_a157ee", "json_select", ".", "html", "operation_file_kind_mismatch")
};
foreach (var check in a06Preflight)
{
    var decision = ViewReadRouter.Select(check.View, check.Capture, check.Operation, check.Query, check.FileKind);
    if (decision.Target != "error" || decision.ReasonCode != check.Expected)
        throw new Exception($"A06 preflight {check.Expected}: {decision.Target}/{decision.ReasonCode}");
}
if (ViewReadRouter.Select("list", null, "json_select", ".[].label", "elements").Target != "observe_query" ||
    ViewReadRouter.Select("list", "capture_20261101_103734_240_a157ee", "text_count", "domain", "text").Target != "file_query" ||
    ViewReadRouter.Select("form", null, null, null).Target != "get_page")
    throw new Exception("A06 preflight blocked valid live/stored/interactive routes");
if (ViewReadRouter.ResolveFileKind(" ELEMENTS ", "json_select") != "elements")
    throw new Exception("A06 file kind normalization failed");
Console.WriteLine("A06_PRE_CAPTURE_VALIDATION_13_CHECKS_OK");
// A07 targeted, read-only DOM evidence: invalid selectors and comparisons fail before WebView2.
var probe = ViewEvidenceProbe.Prepare("main a[href]", "visible", expectedHost:"example.com");
if (probe.ReasonCode != "ok" || probe.Script is null ||
    !probe.Script.Contains("querySelectorAll") || !probe.Script.Contains("host_mismatch"))
    throw new Exception("A07 precise live DOM probe missing");
if (ViewEvidenceProbe.Prepare("body", "exists").ReasonCode != "selector_required_or_too_broad" ||
    ViewEvidenceProbe.Prepare("*", "exists").ReasonCode != "selector_required_or_too_broad" ||
    ViewEvidenceProbe.Prepare("", "exists").ReasonCode != "selector_required_or_too_broad")
    throw new Exception("A07 broad selectors accepted");
if (ViewEvidenceProbe.Prepare("#send", "incorrect").ReasonCode != "check_invalid" ||
    ViewEvidenceProbe.Prepare("#send", "text_contains").ReasonCode != "expected_required_or_too_long" ||
    ViewEvidenceProbe.Prepare("#send", "attribute_equals", "sent", "[]").ReasonCode != "attribute_invalid" ||
    ViewEvidenceProbe.Prepare("#send", "exists", expectedHost:"https://example.com").ReasonCode != "host_invalid")
    throw new Exception("A07 invalid evidence rules accepted");
if (ViewEvidenceProbe.Prepare("#send", "absent").Script is null ||
    ViewEvidenceProbe.Prepare("#send", "value_equals", "ok").Script is null ||
    ViewEvidenceProbe.Prepare("#send", "attribute_equals", "ok", "aria-label").Script is null)
    throw new Exception("A07 valid evidence rules blocked");
var injected = ViewEvidenceProbe.Prepare("a[title=\"</script>\"]", "text_contains", "\";return true;//");
if (injected.Script is null || injected.Script.Contains("</script>") ||
    !injected.Script.Contains("const expected = "))
    throw new Exception("A07 JS serialization boundary unsafe");
Console.WriteLine("A07_TARGETED_DOM_PROBE_11_CHECKS_OK");
// A05: account and live view must be exact before a stored selector can be reused.
var recipe = new CaptureRecipe {
    Host="www.linkedin.com",Path="/messaging/thread/abc",Operation="capture",
    Mode="elements",Selector="main",Confidence="verified",AccountScope="sender-A",
    ViewSignature=new string('a',64),TaskSuccesses=3
};
var scope = new CaptureRecipeScope("www.linkedin.com","/messaging/thread/abc",
    AccountScope:"sender-A",ViewSignature:new string('a',64),Authenticated:true);
if(CaptureRecipeScopeGate.Select(new[]{recipe},scope)!=recipe)
    throw new Exception("Exact scoped recipe not selected");
if(CaptureRecipeScopeGate.Select(new[]{recipe},scope with { AccountScope="sender-B" })!=null)
    throw new Exception("Cross-account recipe reused");
if(CaptureRecipeScopeGate.Select(new[]{recipe},scope with { ViewSignature=new string('b',64) })!=null)
    throw new Exception("Changed view recipe reused");
if(CaptureRecipeScopeGate.Select(new[]{recipe},scope with { AccountScope=null })!=null)
    throw new Exception("Unverified account recipe reused");
recipe.Confidence="candidate";
if(CaptureRecipeScopeGate.Select(new[]{recipe},scope)!=null)
    throw new Exception("Candidate recipe reused as verified");
recipe.Confidence="verified";
var neutral = new CaptureRecipe { Host="example.com",Path="/",Operation="capture",
    Confidence="verified",Mode="text",Selector="body" };
if(CaptureRecipeScopeGate.Select(new[]{neutral},new CaptureRecipeScope("example.com","/"))!=neutral)
    throw new Exception("Verified neutral route not selected");
var recipeStore=new CaptureRecipeStore(Path.Combine(root,"a05_recipes"));
recipeStore.Record("www.linkedin.com","/messaging/thread/abc","text","body","",1);
recipeStore.RecordTaskOutcome("www.linkedin.com","/messaging/thread/abc","text","body","","success");
if(recipeStore.Best("www.linkedin.com","/messaging/thread/abc")!=null)
    throw new Exception("Legacy unscoped LinkedIn recipe reused");
recipeStore.Record("example.com","/","text","body","",1);
recipeStore.RecordTaskOutcome("example.com","/","text","body","","success");
if(recipeStore.Best("example.com","/")==null)
    throw new Exception("Legacy verified neutral recipe migration failed");
// A05 v2: scoped persistence, task-outcome promotion and real-view signature matching.
var scopedStore=new CaptureRecipeStore(Path.Combine(root,"a05_scoped_recipes"));
var neutralSignature=CaptureManifestMetadata.ComputeViewSignatureFromStructure(
    "https://example.com/","main","elements","a:link:::;"); // expected structure is opaque.
scopedStore.RecordScoped("example.com","/","elements","main","",2,neutralSignature,null);
if(scopedStore.Best("example.com","/")!=null)
    throw new Exception("Candidate was reused before task success");
scopedStore.RecordTaskOutcomeScoped("example.com","/","elements","main","",
    neutralSignature,null,"success");
var verifiedScoped=scopedStore.BestScoped(new CaptureRecipeScope("example.com","/",
    ViewSignature:neutralSignature));
if(verifiedScoped is null || verifiedScoped.Confidence!="verified")
    throw new Exception("Scoped successful recipe was not persisted");
scopedStore.RecordScoped("example.com","/","elements","main","",3,new string('b',64),null);
if(scopedStore.BestScoped(new CaptureRecipeScope("example.com","/",
    ViewSignature:new string('b',64)))!=null)
    throw new Exception("New changed view was promoted without success");
scopedStore.RecordScoped("www.linkedin.com","/messaging/","elements","main","",2,
    new string('a',64),null);
scopedStore.RecordTaskOutcomeScoped("www.linkedin.com","/messaging/","elements",
    "main","",new string('a',64),null,"success");
if(scopedStore.Get("www.linkedin.com").Any(r=>r.Confidence=="verified"))
    throw new Exception("Unscoped account-bearing capture promoted");
var liveStructure="a:link:::;";
var liveObservation=new System.Text.Json.Nodes.JsonObject {
    ["url"]="https://example.com/?tracking=1",
    ["results"]=new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject {
        ["index"]=0,["found"]=true,["structure"]=liveStructure })
};
if(CaptureRecipeLiveProbe.SelectFromObservation(new[]{verifiedScoped},
    new Uri("https://example.com/"),liveObservation)!=verifiedScoped)
    throw new Exception("Verified live view not reused");
liveObservation["results"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject {
    ["index"]=0,["found"]=true,["structure"]="button::::;" });
if(CaptureRecipeLiveProbe.SelectFromObservation(new[]{verifiedScoped},
    new Uri("https://example.com/"),liveObservation)!=null)
    throw new Exception("Changed live view reused");
liveObservation["url"]="https://example.org/";
if(CaptureRecipeLiveProbe.SelectFromObservation(new[]{verifiedScoped},
    new Uri("https://example.com/"),liveObservation)!=null)
    throw new Exception("Redirected live view reused");
if(CaptureRecipeLiveProbe.SelectFromObservation(new[]{recipe},
    new Uri("https://www.linkedin.com/messaging/thread/abc"),liveObservation)!=null)
    throw new Exception("Sensitive view probed without account verification");
Console.WriteLine("A05_SCOPED_PERSISTENCE_AND_LIVE_PROBE_9_CHECKS_OK");
// A05: a trusted LinkedIn self-menu identity must survive the probe/capture boundary.
var linkedUrl="https://www.linkedin.com/messaging/thread/abc";
var linkedStructure="a:link:::;";
var linkedSignature=CaptureManifestMetadata.ComputeViewSignatureFromStructure(
    linkedUrl,"main","elements",linkedStructure);
var linkedRecipe=new CaptureRecipe { Host="www.linkedin.com",Path="/messaging/thread/abc",
    Operation="capture",Mode="elements",Selector="main",Confidence="verified",
    AccountScope="linkedin:in/sender-a",ViewSignature=linkedSignature,TaskSuccesses=2 };
var linkedObservation=new System.Text.Json.Nodes.JsonObject {
    ["url"]=linkedUrl,["authenticatedMarker"]=true,["loginForm"]=false,
    ["accountSelfLinks"]=new System.Text.Json.Nodes.JsonArray("https://www.linkedin.com/in/sender-a/"),
    ["results"]=new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject {
        ["index"]=0,["found"]=true,["structure"]=linkedStructure })
};
string? LinkedAccount()=>CaptureAccountScopeGate.Resolve(linkedObservation);
CaptureRecipe? LinkedSelect()=>CaptureRecipeLiveProbe.SelectFromObservation(
    new[]{linkedRecipe},new Uri(linkedUrl),linkedObservation);
if(LinkedAccount()!="linkedin:in/sender-a" || LinkedSelect()!=linkedRecipe)
    throw new Exception("Verified self-menu LinkedIn recipe not selected");
var linkedManifest=new System.Text.Json.Nodes.JsonObject { ["files"]=new System.Text.Json.Nodes.JsonArray() };
CaptureManifestMetadata.Apply(linkedManifest,linkedObservation,"elements");
if(linkedManifest["account_scope"]?.ToString()!="linkedin:in/sender-a" ||
   linkedManifest["account_scope_source"]?.ToString()!="linkedin_signed_in_self_menu")
    throw new Exception("Trusted LinkedIn account not persisted in manifest");
linkedObservation["accountSelfLinks"]=new System.Text.Json.Nodes.JsonArray("https://www.linkedin.com/in/sender-b/");
if(LinkedAccount()!="linkedin:in/sender-b" || LinkedSelect()!=null)
    throw new Exception("Cross-account LinkedIn recipe reused");
linkedObservation["accountSelfLinks"]=new System.Text.Json.Nodes.JsonArray(
    "https://www.linkedin.com/in/sender-a/","https://www.linkedin.com/in/sender-b/");
if(LinkedAccount()!=null || LinkedSelect()!=null)
    throw new Exception("Ambiguous self-menu accepted");
linkedObservation["accountSelfLinks"]=new System.Text.Json.Nodes.JsonArray("https://evil.example/in/sender-a/");
if(LinkedAccount()!=null)throw new Exception("External self-link accepted");
linkedObservation["accountSelfLinks"]=new System.Text.Json.Nodes.JsonArray("https://www.linkedin.com/in/sender-a/");
linkedObservation["authenticatedMarker"]=false;
if(LinkedAccount()!=null || LinkedSelect()!=null)
    throw new Exception("Unverified login marker accepted");
linkedObservation["authenticatedMarker"]=true;
linkedObservation["loginForm"]=true;
if(LinkedAccount()!=null || LinkedSelect()!=null)
    throw new Exception("Login form accepted as signed-in identity");
linkedObservation["loginForm"]=false;
linkedObservation["url"]="https://www.linkedin.com/login";
if(LinkedSelect()!=null)throw new Exception("Redirected LinkedIn view reused");
Console.WriteLine("A05_LINKEDIN_TRUSTED_ACCOUNT_SCOPE_8_CHECKS_OK");
// A05: a view may change after the lightweight probe and before capture.
var raceObservation = new System.Text.Json.Nodes.JsonObject {
    ["url"]="https://example.com/?tracking=2", ["elementsComplete"]=true,
    ["elements"]=new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject {
        ["tag"]="a",["role"]="link",["type"]="",["name"]="" })
};
var raceSignature=CaptureManifestMetadata.ComputeViewSignature("https://example.com/",
    "main","elements",raceObservation["elements"] as System.Text.Json.Nodes.JsonArray);
string RaceCheck()=>CaptureRecipeRaceGate.Check(raceObservation,"https://example.com/",
    "main","elements",raceSignature);
if(RaceCheck()!="ready")throw new Exception("Stable view rejected after probe");
raceObservation["elements"] = new System.Text.Json.Nodes.JsonArray(
    new System.Text.Json.Nodes.JsonObject { ["tag"]="button",["role"]="",["type"]="",["name"]="" });
if(RaceCheck()!="recipe_view_changed")throw new Exception("Changed view reused after probe");
raceObservation["elementsComplete"]=false;
if(RaceCheck()!="recipe_structure_incomplete")throw new Exception("Truncated DOM accepted");
raceObservation["elementsComplete"]=true;
raceObservation["url"]="https://example.org/";
if(RaceCheck()!="recipe_url_changed")throw new Exception("Redirected view accepted");
raceObservation["url"]="https://example.com/";
if(CaptureRecipeRaceGate.Check(raceObservation,"","main","elements",neutralSignature)
    !="recipe_expected_scope_missing")throw new Exception("Missing expected scope accepted");
Console.WriteLine("A05_POST_PROBE_RACE_5_CHECKS_OK");
var cachedFile=Path.Combine(root,"a05_cache.txt");
await File.WriteAllTextAsync(cachedFile,"safe cached text");
var cacheHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
    await File.ReadAllBytesAsync(cachedFile)));
var cacheManifest=new System.Text.Json.Nodes.JsonObject {
    ["source_url"]="https://www.linkedin.com/messaging/thread/abc",
    ["view_signature"]=new string('a',64),["content_fingerprint"]=new string('c',64),
    ["account_scope"]="sender-A",["completeness"]="unverified",
    ["all_pages_complete"]=false,["virtualized"]=false,["lazy_not_loaded"]=false,
    ["observed_at_utc"]=DateTimeOffset.UtcNow.ToString("O"),
    ["files"]=new System.Text.Json.Nodes.JsonArray(
        new System.Text.Json.Nodes.JsonObject { ["path"]=cachedFile,["sha256"]=cacheHash })
};
var cacheScope=scope with { ContentFingerprint=new string('c',64) };
string CacheCheck()=>CaptureRecipeScopeGate.CheckCachedCapture(cacheManifest,cacheScope,root,TimeSpan.FromMinutes(5));
if(CacheCheck()!="cache_scope_incomplete")throw new Exception("Partial capture reused");
cacheManifest["completeness"]="complete";
cacheManifest["all_pages_complete"]=true;
if(CacheCheck()!="ready")throw new Exception("Complete scoped cache rejected: "+CacheCheck());
cacheManifest["account_scope"]="sender-B";
if(CacheCheck()!="cache_account_unverified_or_changed")throw new Exception("Wrong account cache reused");
cacheManifest["account_scope"]="sender-A";
cacheScope=cacheScope with { ContentFingerprint=new string('d',64) };
if(CacheCheck()!="cache_content_changed_or_unverified")throw new Exception("Changed content cache reused");
cacheScope=cacheScope with { ContentFingerprint=new string('c',64) };
await File.WriteAllTextAsync(cachedFile,"tampered");
if(CacheCheck()!="cache_file_hash_mismatch")throw new Exception("Tampered file cache reused");
await File.WriteAllTextAsync(cachedFile,"safe cached text");
cacheManifest["observed_at_utc"]=DateTimeOffset.UtcNow.AddHours(-1).ToString("O");
if(CacheCheck()!="cache_stale_or_invalid_time")throw new Exception("Stale cache reused");
Console.WriteLine("CAPTURE_RECIPE_SCOPE_CACHE_14_CHECKS_OK");
var memory = new OperationalMemoryStore(Path.Combine(root,"operational.db"));
memory.UpsertAccount("sender1","linkedin","sender-external");
memory.UpsertIdentity("linkedin","sender1","person123","https://linkedin.example/in/person123","Person 123","capture1");
if(memory.FindIdentities("linkedin","sender1","person123").Count!=1)throw new Exception("Identity lookup failed");
var batch=memory.BeginBatch("linkedin","sender1","req1","linkedin.send_batch","1",null,"ChatGPT","QA");
if(memory.BeginBatch("linkedin","sender1","req1","linkedin.send_batch","1",null,"ChatGPT","QA")!=batch)throw new Exception("Batch identity changed");
var operation=memory.PlanOperation(batch,"person123","send","hash123");
if(memory.PlanOperation(batch,"person123","send","hash123")!=operation)throw new Exception("Operation identity changed");
if(!memory.MarkAttempted(operation))throw new Exception("Attempt not saved");
if(!memory.MarkOutcome(operation,"uncertain",null,null,"retry_not_allowed"))throw new Exception("Uncertain not saved");
if(memory.MarkAttempted(operation))throw new Exception("Uncertain was re-attempted");
if(!memory.MarkOutcome(operation,"verified","proof1","msg1",null))throw new Exception("Reconcile failed");
if(memory.MarkAttempted(operation))throw new Exception("Verified was re-attempted");
var events = memory.OperationEvents(operation);
if(events.Count != 4)throw new Exception("Event ledger expected 4 entries, got "+events.Count);
Console.WriteLine("LEDGER_EVENTS_OK "+events.Count);
var counts=memory.BatchStatus(batch);
if(counts["verified"]?.GetValue<int>()!=1)throw new Exception("Batch counts wrong");
Console.WriteLine("SQLITE_LEDGER_OK "+counts.ToJsonString());
var thread=memory.UpsertConversation("linkedin","sender1","thread123","https://linkedin.example/messaging/thread123",null,"capture1");
memory.SaveMessage("message1",thread,"out","Witaj, kolego","body_hash","capture1");
if(memory.SearchMessages("linkedin","sender1","Witaj").Count!=1)throw new Exception("FTS message search failed");
Console.WriteLine("FTS_MESSAGE_LOOKUP_OK");
memory.UpsertIdentity("linkedin","sender1","person123","https://www.linkedin.com/in/person123/","Person 123","capture1");
memory.UpsertConversation("linkedin","sender1","person123","https://www.linkedin.com/messaging/thread/abc",null,"capture1");
var linkedinPlanner=new LinkedInBatchPlanner(memory);
var linkedinTargets=new List<ProcedureTarget>{new("person123","Hello Person"),new("unknown001","Hello Unknown")};
var linkedinFirst=linkedinPlanner.Prepare("sender1","linkedin-batch-1",linkedinTargets,"ChatGPT","QA");
var linkedinRetry=linkedinPlanner.Prepare("sender1","linkedin-batch-1",linkedinTargets,"ChatGPT","QA");
if(linkedinFirst["status"]?.ToString()!="planned" || linkedinFirst["thread_ready"]?.GetValue<int>()!=1)
    throw new Exception("LinkedIn known thread not resolved in a batch");
if(linkedinRetry["batch_id"]?.ToString()!=linkedinFirst["batch_id"]?.ToString())
    throw new Exception("LinkedIn idempotency not stable");
var linkedinChanged=linkedinPlanner.Prepare("sender1","linkedin-batch-1",
    new List<ProcedureTarget>{new("person123","CHANGED"),new("unknown001","Hello Unknown")},"ChatGPT","QA");
if(linkedinChanged["status"]?.ToString()!="idempotency_conflict")
    throw new Exception("LinkedIn request collision not rejected");
Console.WriteLine("LINKEDIN_BATCH_RESOLVE_AND_LEDGER_OK");
var linkedinObservation=new System.Text.Json.Nodes.JsonObject
{
    ["url"]="https://www.linkedin.com/messaging/thread/abc?trk=messaging",
    ["sender_profiles"]=new System.Text.Json.Nodes.JsonArray("https://www.linkedin.com/in/own-profile/"),
    ["target_profiles"]=new System.Text.Json.Nodes.JsonArray("https://www.linkedin.com/in/person123/"),
    ["composer_count"]=1,
    ["observed_at"]="2026-11-01T00:00:00Z"
};
var senderUrl="https://www.linkedin.com/in/own-profile/";
var targetUrl="https://www.linkedin.com/in/person123/";
var threadUrl="https://www.linkedin.com/messaging/thread/abc";
string Preflight(string sender,string thread,string target) =>
    LinkedInThreadPreflight.Check(linkedinObservation,sender,thread,target)["reason_code"]?.ToString() ?? "";
if(LinkedInThreadPreflight.Check(linkedinObservation,senderUrl,threadUrl,targetUrl)["status"]?.ToString()!="ready")
    throw new Exception("Exact LinkedIn preflight not accepted");
if(Preflight("https://www.linkedin.com/in/other/",threadUrl,targetUrl)!="account_unverified_or_mismatch")
    throw new Exception("Wrong sender not blocked");
if(Preflight(senderUrl,"https://www.linkedin.com/messaging/thread/other",targetUrl)!="thread_mismatch")
    throw new Exception("Wrong thread not blocked");
if(Preflight(senderUrl,threadUrl,"https://www.linkedin.com/in/other/")!="target_unverified_or_mismatch")
    throw new Exception("Wrong recipient not blocked");
if(Preflight(senderUrl,threadUrl,"https://evil.example/in/person123")!="invalid_expected_identity")
    throw new Exception("Non-LinkedIn identity accepted");
linkedinObservation["composer_count"]=0;
if(LinkedInThreadPreflight.Check(linkedinObservation,senderUrl,threadUrl,targetUrl)["reason_code"]?.ToString()!="composer_missing_or_ambiguous")
    throw new Exception("Missing composer not blocked");
linkedinObservation["composer_count"]=1;
linkedinObservation["target_profiles"]=new System.Text.Json.Nodes.JsonArray(targetUrl,"https://www.linkedin.com/in/other/");
if(LinkedInThreadPreflight.Check(linkedinObservation,senderUrl,threadUrl,targetUrl)["status"]?.ToString()!="blocked")
    throw new Exception("Ambiguous recipient not blocked");
Console.WriteLine("LINKEDIN_THREAD_PREFLIGHT_7_CASES_OK");
var airbnbPlanner=new AirbnbPricePlanner(memory);
var airbnbStart=new DateOnly(2026,11,1);
var airbnbEnd=new DateOnly(2026,11,2);
var airbnbObserved=new List<AirbnbPriceObservation> {
    new(airbnbStart,100m,"capture-airbnb-1"),new(airbnbEnd,150m,"capture-airbnb-1") };
var airbnbPlan=airbnbPlanner.Prepare("airbnb-acct","listing123","USD","standard",
    "airbnb-request-1","ChatGPT","QA",airbnbStart,airbnbEnd,35m,2,airbnbObserved);
if(airbnbPlan["status"]?.ToString()!="planned" || airbnbPlan["days"]?.GetValue<int>()!=2)
    throw new Exception("Airbnb price plan failed");
var airbnbPreview=(System.Text.Json.Nodes.JsonArray)airbnbPlan["preview"]!;
if(airbnbPreview[0]?["new_price"]?.GetValue<decimal>()!=135m ||
   airbnbPreview[1]?["new_price"]?.GetValue<decimal>()!=202.50m)
    throw new Exception("Airbnb percentage calculation failed");
var airbnbRetry=airbnbPlanner.Prepare("airbnb-acct","listing123","USD","standard",
    "airbnb-request-1","ChatGPT","QA",airbnbStart,airbnbEnd,35m,2,
    new List<AirbnbPriceObservation>{new(airbnbStart,100m,"capture-new"),new(airbnbEnd,150m,"capture-new")});
if(airbnbRetry["batch_id"]?.ToString()!=airbnbPlan["batch_id"]?.ToString() ||
   airbnbRetry["counts"]?["total"]?.GetValue<int>()!=2)
    throw new Exception("Airbnb duplicate operation added on replan");
var airbnbConflict=airbnbPlanner.Prepare("airbnb-acct","listing123","USD","standard",
    "airbnb-request-1","ChatGPT","QA",airbnbStart,airbnbEnd,40m,2,airbnbObserved);
if(airbnbConflict["status"]?.ToString()!="idempotency_conflict")
    throw new Exception("Airbnb conflict not rejected");
var airbnbGap=airbnbPlanner.Prepare("airbnb-acct","listing123","USD","standard",
    "airbnb-gap","ChatGPT","QA",airbnbStart,airbnbEnd,35m,2,
    new List<AirbnbPriceObservation>{new(airbnbStart,100m,"capture")});
if(airbnbGap["status"]?.ToString()!="incomplete_observation")
    throw new Exception("Airbnb missing day not blocked");
var priceOperation=airbnbPreview[0]?["operation_id"]?.ToString() ?? throw new Exception("Operation ID missing");
(string Path,string Hash) PriceProof(decimal price,string account="airbnb-acct",string listing="listing123",
    string date="2026-11-01",bool complete=true,DateTimeOffset? observedAt=null)
{
    var dir=Path.Combine(root,"captures","airbnb");
    Directory.CreateDirectory(dir);
    var path=Path.Combine(dir,Guid.NewGuid().ToString("N")+".json");
    var proof=new System.Text.Json.Nodes.JsonObject {
        ["provider"]="airbnb",["source_kind"]="webview2_dom",["scope"]="single_listing_day",
        ["complete"]=complete,["account_id"]=account,["listing_id"]=listing,
        ["currency"]="USD",["rate_plan"]="standard",["date"]=date,["price"]=price,
        ["observed_at_utc"]=(observedAt??DateTimeOffset.UtcNow).ToString("O")
    };
    var bytes=System.Text.Encoding.UTF8.GetBytes(proof.ToJsonString());
    File.WriteAllBytes(path,bytes);
    return (path,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
}
var beforeAttempt=PriceProof(135m);
if(airbnbPlanner.ReconcileExact(priceOperation,beforeAttempt.Path,beforeAttempt.Hash)["reason_code"]?.ToString()!="not_awaiting_verification")
    throw new Exception("Airbnb price falsely confirmed before attempt");
if(!memory.MarkAttempted(priceOperation))throw new Exception("Price attempt not recorded");
if(airbnbPlanner.Reconcile(priceOperation,135m,"unscoped")["status"]?.ToString()!="unscoped_evidence_rejected")
    throw new Exception("Legacy unscoped Airbnb evidence accepted");
var wrongListing=PriceProof(135m,listing:"other-listing");
if(airbnbPlanner.ReconcileExact(priceOperation,wrongListing.Path,wrongListing.Hash)["reason_code"]?.ToString()!="evidence_identity_mismatch")
    throw new Exception("Airbnb wrong listing accepted");
var wrongAccount=PriceProof(135m,account:"other-account");
if(airbnbPlanner.ReconcileExact(priceOperation,wrongAccount.Path,wrongAccount.Hash)["reason_code"]?.ToString()!="evidence_identity_mismatch")
    throw new Exception("Airbnb wrong account accepted");
var wrongDay=PriceProof(135m,date:"2026-11-02");
if(airbnbPlanner.ReconcileExact(priceOperation,wrongDay.Path,wrongDay.Hash)["reason_code"]?.ToString()!="evidence_identity_mismatch")
    throw new Exception("Airbnb wrong day accepted");
var partial=PriceProof(135m,complete:false);
if(airbnbPlanner.ReconcileExact(priceOperation,partial.Path,partial.Hash)["reason_code"]?.ToString()!="evidence_scope_incomplete")
    throw new Exception("Airbnb partial capture accepted");
var stale=PriceProof(135m,observedAt:DateTimeOffset.UtcNow.AddMinutes(-5));
if(airbnbPlanner.ReconcileExact(priceOperation,stale.Path,stale.Hash)["reason_code"]?.ToString()!="evidence_predates_attempt")
    throw new Exception("Airbnb stale capture accepted");
var hashMismatch=PriceProof(135m);
if(airbnbPlanner.ReconcileExact(priceOperation,hashMismatch.Path,new string('0',64))["reason_code"]?.ToString()!="evidence_hash_mismatch")
    throw new Exception("Airbnb evidence hash mismatch accepted");
var mismatch=PriceProof(134m);
if(airbnbPlanner.ReconcileExact(priceOperation,mismatch.Path,mismatch.Hash)["status"]?.ToString()!="uncertain")
    throw new Exception("Mismatched Airbnb price falsely confirmed");
if(memory.MarkAttempted(priceOperation))throw new Exception("Uncertain Airbnb price was re-attempted");
var matching=PriceProof(135m);
if(airbnbPlanner.ReconcileExact(priceOperation,matching.Path,matching.Hash)["status"]?.ToString()!="verified")
    throw new Exception("Airbnb matching scoped price not confirmed");
if(memory.MarkAttempted(priceOperation))throw new Exception("Verified Airbnb price was re-attempted");
Console.WriteLine("AIRBNB_SCOPED_EVIDENCE_12_CHECKS_OK");
Console.WriteLine("AIRBNB_PLAN_RECONCILE_OK");
// D03: pre-plan source captures must be exact, fresh, complete and tamper-evident.
AirbnbPriceCaptureReference SourceCapture(DateOnly date, decimal price,
    string account="airbnb-acct", string listing="listing123", string currency="USD",
    string rate="standard", bool complete=true, string url="https://www.airbnb.com/hosting/calendar",
    DateTimeOffset? at=null)
{
    var dir=Path.Combine(root,"captures","airbnb","source");
    Directory.CreateDirectory(dir);
    var path=Path.Combine(dir,Guid.NewGuid().ToString("N")+".json");
    var proof=new System.Text.Json.Nodes.JsonObject {
        ["provider"]="airbnb", ["source_kind"]="webview2_dom",
        ["scope"]="single_listing_day", ["complete"]=complete,
        ["url"]=url, ["account_id"]=account, ["listing_id"]=listing,
        ["currency"]=currency, ["rate_plan"]=rate,
        ["date"]=date.ToString("yyyy-MM-dd"), ["price"]=price,
        ["observed_at_utc"]=(at??DateTimeOffset.UtcNow).ToString("O")
    };
    var bytes=System.Text.Encoding.UTF8.GetBytes(proof.ToJsonString());
    File.WriteAllBytes(path,bytes);
    return new AirbnbPriceCaptureReference(date,path,
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
}
var source1=SourceCapture(airbnbStart,100m);
var source2=SourceCapture(airbnbEnd,150m);
var sources=new List<AirbnbPriceCaptureReference>{source1,source2};
string SourceCheck(IReadOnlyList<AirbnbPriceCaptureReference> refs) =>
    new AirbnbPriceSourceEvidenceGate(memory).Validate("airbnb-acct","listing123",
        "USD","standard",airbnbStart,airbnbEnd,refs).Result["reason_code"]?.ToString() ?? "ready";
if(SourceCheck(sources)!="ready")throw new Exception("Valid initial Airbnb evidence rejected");
if(SourceCheck(new[]{SourceCapture(airbnbStart,100m,listing:"wrong"),source2})!="source_identity_mismatch:listing_id")
    throw new Exception("Wrong listing initial evidence accepted");
if(SourceCheck(new[]{SourceCapture(airbnbStart,100m,account:"wrong"),source2})!="source_identity_mismatch:account_id")
    throw new Exception("Wrong account initial evidence accepted");
if(SourceCheck(new[]{SourceCapture(airbnbStart,100m,currency:"EUR"),source2})!="source_identity_mismatch:currency")
    throw new Exception("Wrong currency initial evidence accepted");
if(SourceCheck(new[]{SourceCapture(airbnbStart,100m,rate:"other"),source2})!="source_identity_mismatch:rate_plan")
    throw new Exception("Wrong rate plan initial evidence accepted");
if(SourceCheck(new[]{SourceCapture(airbnbStart,100m,complete:false),source2})!="source_scope_incomplete")
    throw new Exception("Partial initial evidence accepted");
if(SourceCheck(new[]{SourceCapture(airbnbStart,100m,url:"https://evil.example/hosting/calendar"),source2})!="source_url_unverified")
    throw new Exception("Non-Airbnb initial evidence accepted");
if(SourceCheck(new[]{SourceCapture(airbnbStart,100m,at:DateTimeOffset.UtcNow.AddHours(-1)),source2})!="source_stale_or_invalid_time")
    throw new Exception("Stale initial evidence accepted");
if(SourceCheck(new[]{source1 with { Sha256=new string('0',64) },source2})!="source_hash_mismatch")
    throw new Exception("Tampered initial evidence accepted");
if(SourceCheck(new[]{source1,source1})!="source_date_coverage_incomplete")
    throw new Exception("Duplicate day initial evidence accepted");
if(SourceCheck(new[]{source1})!="source_date_coverage_incomplete")
    throw new Exception("Missing day initial evidence accepted");
var blockedSourcePlan=airbnbPlanner.PrepareFromCaptures("airbnb-acct","listing123",
    "USD","standard","verified-source-1","ChatGPT","QA",airbnbStart,airbnbEnd,
    35m,2,new[]{source1 with { Sha256=new string('0',64) },source2});
if(blockedSourcePlan["reason_code"]?.ToString()!="source_hash_mismatch")
    throw new Exception("Unsafe source created an Airbnb plan");
var safeSourcePlan=airbnbPlanner.PrepareFromCaptures("airbnb-acct","listing123",
    "USD","standard","verified-source-1","ChatGPT","QA",airbnbStart,airbnbEnd,35m,2,sources);
if(safeSourcePlan["status"]?.ToString()!="planned" || safeSourcePlan["days"]?.GetValue<int>()!=2)
    throw new Exception("Valid source could not create a plan");
var sourceRetry=airbnbPlanner.PrepareFromCaptures("airbnb-acct","listing123",
    "USD","standard","verified-source-1","ChatGPT","QA",airbnbStart,airbnbEnd,
    35m,2,new[]{SourceCapture(airbnbStart,100m),SourceCapture(airbnbEnd,150m)});
if(sourceRetry["batch_id"]?.ToString()!=safeSourcePlan["batch_id"]?.ToString())
    throw new Exception("New capture duplicated an existing Airbnb plan");
Console.WriteLine("AIRBNB_SOURCE_EVIDENCE_14_CHECKS_OK");
// Resolve the registry relative to the test assembly, not the shell's
// current directory (which varies between IDE, CI and CLI invocation).
var registry=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
    "..","..","..","..","..","procedures","registry.json"));
var catalog=new ProcedureCatalog(registry);
if(catalog.Get("whatsapp.send_batch") is null)throw new Exception("Procedure registry invalid");
var executor=new ProcedureExecutor(catalog,memory);
var targets=new List<ProcedureTarget>{new("555111","test one"),new("555222","test two")};
var sendCount=0;
Func<ProcedureTarget,CancellationToken,Task<ProcedureActionResult>> fakeAction=(x,ct)=>{
    sendCount++;
    return Task.FromResult(new ProcedureActionResult("verified","smoke-proof",null,null));
};
var first=await executor.ExecuteBatchAsync("whatsapp.send_batch","sender1","smoke-batch",targets,
    ct=>Task.FromResult(true),fakeAction,null,"ChatGPT","QA",allowCandidateForStaging:true);
var second=await executor.ExecuteBatchAsync("whatsapp.send_batch","sender1","smoke-batch",targets,
    ct=>Task.FromResult(true),fakeAction,null,"ChatGPT","QA",allowCandidateForStaging:true);
if(sendCount!=2)throw new Exception("Duplicate batch executed twice: "+sendCount);
var changed=await executor.ExecuteBatchAsync("whatsapp.send_batch","sender1","smoke-batch",
    new List<ProcedureTarget>{new("555111","CHANGED CONTENT"),new("555222","test two")},
    ct=>Task.FromResult(true),fakeAction,null,"ChatGPT","QA",allowCandidateForStaging:true);
if(changed["status"]?.ToString()!="idempotency_conflict" || sendCount!=2)
    throw new Exception("Request collision was not rejected");
if(ProcedureCatalog.FromEmbedded().Get("whatsapp.send_batch") is null)
    throw new Exception("Embedded procedure catalog unavailable");
Console.WriteLine("IDEMPOTENCY_CONFLICT_OK");
Console.WriteLine("PROCEDURE_CATALOG_EMBEDDED_OK");
var externalTools=OpenFill.Core.Mcp.McpServer.ToolList();
if (!externalTools.Any(x=>x?["name"]?.ToString()=="openfill_query_capture"))
    throw new Exception("MCP query tool missing");
Console.WriteLine("MCP_QUERY_CAPTURE_REGISTERED_OK");
Console.WriteLine("PROCEDURE_RESUME_OK "+first["counts"]);
memory.RecordObservation("capture-smoke-1", "sender1", "example.com", "https://example.com/", "currently_loaded_dom", false, text, "hash-smoke");
using (var observationDb = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + memory.DatabasePath))
{
    observationDb.Open();
    using var observationQuery = observationDb.CreateCommand();
    observationQuery.CommandText = "SELECT COUNT(*) FROM observations WHERE capture_id='capture-smoke-1' AND scope='currently_loaded_dom' AND complete=0;";
    if (Convert.ToInt64(observationQuery.ExecuteScalar()) != 1) throw new Exception("Observation persistence failed");
}
if (memory.RecentObservations("example.com", "sender1", "example.com").Count != 1)
    throw new Exception("Observation lookup failed");
Console.WriteLine("OBSERVATION_INDEX_OK");
var operationalMetrics = memory.Metrics();
if(operationalMetrics["status"]?.ToString()!="ok" ||
   operationalMetrics["operation_count"]?.GetValue<long>() < 5)
    throw new Exception("Operational metrics failed");
var metricGroups = (System.Text.Json.Nodes.JsonArray?)operationalMetrics["groups"];
if(metricGroups is null ||
   !metricGroups.OfType<System.Text.Json.Nodes.JsonObject>().Any(m=>m["provider"]?.ToString()=="airbnb") ||
   !metricGroups.OfType<System.Text.Json.Nodes.JsonObject>().Any(m=>m["provider"]?.ToString()=="linkedin"))
    throw new Exception("Operational metrics missing provider groups");
Console.WriteLine("OPERATION_METRICS_OK "+operationalMetrics["operation_count"]);
// D02/C03: a read-only preflight failure must never consume an attempt.
var guardedTargets = new List<ProcedureTarget> { new("guarded-person", "message") };
var guardedCalls = 0;
Func<ProcedureTarget,CancellationToken,Task<ProcedureActionResult>> guardedAction = (target, token) =>
{
    guardedCalls++;
    return Task.FromResult(new ProcedureActionResult("verified", "verified-outgoing-ui"));
};
var gateBlocked = await executor.ExecuteBatchAsync("whatsapp.send_batch", "sender1", "preflight-gate-1",
    guardedTargets, token => Task.FromResult(true), guardedAction, null, "ChatGPT", "QA",
    allowCandidateForStaging: true,
    preflight: (target, token) => Task.FromResult(new ProcedureActionResult("blocked", Reason: "thread_mismatch")));
var guardedOp = gateBlocked["items"]?[0]?["operation_id"]?.ToString() ?? throw new Exception("Guard operation missing");
if (guardedCalls != 0 || memory.OperationState(guardedOp) != "planned" ||
    gateBlocked["items"]?[0]?["preflight_status"]?.ToString() != "blocked")
    throw new Exception("Preflight rejection incorrectly consumed the operation");
var gateRetry = await executor.ExecuteBatchAsync("whatsapp.send_batch", "sender1", "preflight-gate-1",
    guardedTargets, token => Task.FromResult(true), guardedAction, null, "ChatGPT", "QA",
    allowCandidateForStaging: true,
    preflight: (target, token) => Task.FromResult(new ProcedureActionResult("ready")));
if (guardedCalls != 1 || memory.OperationState(guardedOp) != "verified")
    throw new Exception("Corrected preflight did not resume exactly once");
var gateRepeat = await executor.ExecuteBatchAsync("whatsapp.send_batch", "sender1", "preflight-gate-1",
    guardedTargets, token => Task.FromResult(true), guardedAction, null, "ChatGPT", "QA",
    allowCandidateForStaging: true,
    preflight: (target, token) => throw new Exception("Verified operation should not reach preflight"));
if (guardedCalls != 1 || gateRepeat["items"]?[0]?["executed"]?.GetValue<bool>() != false)
    throw new Exception("Verified operation was repeated");
var gateException = await executor.ExecuteBatchAsync("whatsapp.send_batch", "sender1", "preflight-gate-exception",
    guardedTargets, token => Task.FromResult(true), guardedAction, null, "ChatGPT", "QA",
    allowCandidateForStaging: true,
    preflight: (target, token) => throw new InvalidOperationException("preflight-failed"));
var exceptionOp = gateException["items"]?[0]?["operation_id"]?.ToString() ?? throw new Exception("Exception operation missing");
if (guardedCalls != 1 || memory.OperationState(exceptionOp) != "planned" ||
    gateException["items"]?[0]?["reason"]?.ToString() != "preflight_exception:InvalidOperationException")
    throw new Exception("Preflight exception caused an action or lost its reason");
Console.WriteLine("PROCEDURE_PREFLIGHT_LEDGER_4_CASES_OK");
// D02: candidate UI adapter must require exact targets and durable proof.
var linkedTargets = new List<LinkedInSendTarget> {
    new("https://www.linkedin.com/in/person123/", "https://www.linkedin.com/messaging/thread/abc", "Hello")
};
if (LinkedInSendBatchService.Validate(senderUrl, linkedTargets) is not null)
    throw new Exception("Valid LinkedIn action input rejected");
if (LinkedInSendBatchService.Validate("https://evil.example/in/sender", linkedTargets) != "invalid_sender_profile")
    throw new Exception("Invalid LinkedIn sender accepted");
if (LinkedInSendBatchService.Validate(senderUrl, new List<LinkedInSendTarget> { linkedTargets[0], linkedTargets[0] }) != "duplicate_target")
    throw new Exception("Duplicate LinkedIn recipient accepted");
if (LinkedInSendBatchService.Validate(senderUrl, new List<LinkedInSendTarget> {
    new(targetUrl, "https://evil.example/messaging/thread/abc", "Hello")
}) != "invalid_thread_url")
    throw new Exception("Non-LinkedIn thread accepted");
if (LinkedInSendBatchService.Validate(senderUrl, new List<LinkedInSendTarget> {
    new(targetUrl, threadUrl, "")
}) != "invalid_message_length")
    throw new Exception("Empty LinkedIn message accepted");
Console.WriteLine("LINKEDIN_ACTION_INPUT_5_CASES_OK");
var outgoingBefore = new System.Text.Json.Nodes.JsonObject {
    ["url"] = threadUrl, ["outgoing_match_count"] = 1
};
var outgoingAfter = new System.Text.Json.Nodes.JsonObject {
    ["url"] = threadUrl, ["outgoing_match_count"] = 2
};
if (LinkedInCurrentThreadSendAdapter.AssessEvidence(outgoingBefore, outgoingAfter, true, "proof.json").Status != "verified")
    throw new Exception("New outgoing UI message not confirmed");
if (LinkedInCurrentThreadSendAdapter.AssessEvidence(outgoingBefore, outgoingBefore, true, "proof.json").Status != "uncertain")
    throw new Exception("Existing outgoing message falsely verified");
if (LinkedInCurrentThreadSendAdapter.AssessEvidence(outgoingBefore, outgoingAfter, false, "proof.json").Status != "uncertain")
    throw new Exception("Unverified sender falsely verified");
if (LinkedInCurrentThreadSendAdapter.AssessEvidence(outgoingBefore, outgoingAfter, true, null).Status != "uncertain")
    throw new Exception("Missing evidence file falsely verified");
var wrongOutgoing = new System.Text.Json.Nodes.JsonObject {
    ["url"] = "https://www.linkedin.com/messaging/thread/wrong", ["outgoing_match_count"] = 2
};
if (LinkedInCurrentThreadSendAdapter.AssessEvidence(outgoingBefore, wrongOutgoing, true, "proof.json").Status != "uncertain")
    throw new Exception("Wrong thread falsely verified");
if (LinkedInCurrentThreadSendAdapter.AssessEvidence(outgoingBefore, null, true, "proof.json").Status != "uncertain")
    throw new Exception("Missing observation falsely verified");
Console.WriteLine("LINKEDIN_OUTGOING_EVIDENCE_6_CASES_OK");
var operationAwareCalls = 0;
var operationAware = await executor.ExecuteBatchAsync(
    "linkedin.send_batch", senderUrl, "operation-aware-test",
    new List<ProcedureTarget> { new(targetUrl, "Hello") },
    token => Task.FromResult(true),
    (target, token) => throw new Exception("Fallback action must never execute"),
    null, "ChatGPT", "QA", allowCandidateForStaging: true,
    preflight: (target, token) => Task.FromResult(new ProcedureActionResult("ready")),
    actionWithOperation: (target, operationId, token) => {
        operationAwareCalls++;
        if (memory.OperationState(operationId) != "attempted")
            throw new Exception("Action invoked before durable attempted");
        if (memory.OperationPayload(operationId) != target.Payload)
            throw new Exception("Action payload differs from durable ledger");
        return Task.FromResult(new ProcedureActionResult("uncertain", Reason: "simulated_no_ui_proof"));
    });
var operationAwareRetry = await executor.ExecuteBatchAsync(
    "linkedin.send_batch", senderUrl, "operation-aware-test",
    new List<ProcedureTarget> { new(targetUrl, "Hello") },
    token => Task.FromResult(true),
    (target, token) => throw new Exception("Fallback action must never execute"),
    null, "ChatGPT", "QA", allowCandidateForStaging: true,
    preflight: (target, token) => throw new Exception("Uncertain must not be retried"),
    actionWithOperation: (target, operationId, token) => throw new Exception("Uncertain action retried"));
if (operationAwareCalls != 1 ||
    operationAware["items"]?[0]?["state"]?.ToString() != "uncertain" ||
    operationAwareRetry["items"]?[0]?["executed"]?.GetValue<bool>() != false)
    throw new Exception("Operation-aware action idempotency failed");
Console.WriteLine("LINKEDIN_ACTION_LEDGER_3_CASES_OK");
// A08: universal structural view classification, without DOM text or user data.
System.Text.Json.Nodes.JsonObject InspectFixture(string raw) =>
    SemanticViewInspector.Summarize(new System.Text.Json.Nodes.JsonObject {
        ["ok"] = true, ["value"] = System.Text.Json.Nodes.JsonNode.Parse(raw)
    });
var a08Table = InspectFixture("""{"host":"example.com","route":"/orders","tables":1,"rows":8,"headers":3}""");
var a08Form = InspectFixture("""{"host":"example.com","route":"/edit","forms":1,"inputs":4}""");
var a08Inbox = InspectFixture("""{"host":"example.com","route":"/inbox","messages":8,"lists":1,"nav":1}""");
var a08List = InspectFixture("""{"host":"example.com","route":"/catalog","lists":1,"items":12}""");
var a08Detail = InspectFixture("""{"host":"example.com","route":"/item","headings":1,"links":2}""");
if (a08Table["view_type"]?.ToString() != "table" ||
    a08Form["view_type"]?.ToString() != "form" ||
    a08Inbox["view_type"]?.ToString() != "inbox" ||
    a08List["view_type"]?.ToString() != "list" ||
    a08Detail["view_type"]?.ToString() != "detail")
    throw new Exception("A08 structural classification failed");
if (a08List["recommended_next"]?.ToString() != "read_view_filtered" ||
    a08Form["recommended_next"]?.ToString() != "get_page_controls")
    throw new Exception("A08 recommended routing failed");
if (a08List["completeness"]?.ToString() != "unverified" ||
    a08List["account_scope"]?.ToString() != "unknown" ||
    a08List["full_dom_capture"]?.GetValue<bool>() != false)
    throw new Exception("A08 unsafe completeness/account claim");
var a08ListPlusOne = InspectFixture("""{"host":"example.com","route":"/catalog","lists":1,"items":13}""");
if (a08List["view_signature"]?.ToString() != a08ListPlusOne["view_signature"]?.ToString())
    throw new Exception("A08 signature changed for a single added list item");
if (a08List["view_signature"]?.ToString() == a08Table["view_signature"]?.ToString())
    throw new Exception("A08 different structures share a signature");
if (SemanticViewInspector.Summarize(new System.Text.Json.Nodes.JsonObject {
    ["ok"] = false
})["reason_code"]?.ToString() != "structural_probe_unavailable")
    throw new Exception("A08 missing DOM response was accepted");
if (a08List.ToJsonString().Contains("innerText") || a08List.ToJsonString().Contains("password"))
    throw new Exception("A08 model manifest leaked page content");
Console.WriteLine("A08_SEMANTIC_VIEW_MANIFEST_11_CHECKS_OK");
// A09: one-call auto classification/read and verified per-route method selection.
var autoList = AutoViewReadPlanner.Choose(a08List, "https://example.com/catalog");
if (autoList.Target != "observe_query" || autoList.Operation != "text_regex" ||
    autoList.Query != ".+" || autoList.ViewType != "list")
    throw new Exception("A09 auto list routing failed");
if (AutoViewReadPlanner.Choose(a08Form, "https://example.com/edit").Target != "get_page" ||
    AutoViewReadPlanner.Choose(a08Detail, "https://example.com/item").Target != "get_page")
    throw new Exception("A09 form/detail controls lost");
if (AutoViewReadPlanner.Choose(a08List, "https://different.example/catalog").ReasonCode != "structure_unavailable")
    throw new Exception("A09 host mismatch accepted");
var autoRecipe = new CaptureRecipe { Host = "example.com", Path = "/catalog",
    Mode = "text", Selector = "main", Pattern = "product", Confidence = "verified" };
var recipePlan = AutoViewReadPlanner.Choose(a08List, "https://example.com/catalog", autoRecipe);
if (recipePlan.ReasonCode != "verified_route_recipe" ||
    recipePlan.Selector != "main" || recipePlan.Query != "product")
    throw new Exception("A09 verified local recipe ignored");
autoRecipe.Confidence = "candidate";
if (AutoViewReadPlanner.Choose(a08List, "https://example.com/catalog", autoRecipe).ReasonCode != "generic_live_filter")
    throw new Exception("A09 unverified local recipe accepted");
var linkedInPlan = AutoViewReadPlanner.Choose(InspectFixture(
    """{"host":"www.linkedin.com","route":"/feed","lists":1,"items":12}"""),
    "https://www.linkedin.com/feed", new CaptureRecipe {
        Host = "www.linkedin.com", Path = "/feed", Mode = "text",
        Selector = "main", Pattern = "contacts", Confidence = "verified"
    });
if (linkedInPlan.ReasonCode != "generic_live_filter")
    throw new Exception("A09 sensitive-site recipe used without account proof");
Console.WriteLine("A09_AUTOMATIC_VIEW_ROUTING_6_CHECKS_OK");
// OF-000030: the 20-char semantic routing hint must never be confused with
// the SHA256 exact DOM recipe identity confirmed by CaptureRecipeLiveProbe.
if (a08List["semantic_signature"]?.ToString().Length != 20 ||
    a08List["view_signature_kind"]?.ToString() != "semantic_hint_v1")
    throw new Exception("OF030 semantic signature contract missing");
var signedRecipe = new CaptureRecipe { Host = "example.com", Path = "/catalog",
    Mode = "all", Selector = "main", Pattern = "product", Operation = "capture",
    Confidence = "verified", ViewSignature = new string('a', 64) };
var signedPlan = AutoViewReadPlanner.Choose(a08List, "https://example.com/catalog",
    signedRecipe, exactLiveVerified: true);
if (signedPlan.ReasonCode != "verified_live_recipe" ||
    signedPlan.RecipeSignature != signedRecipe.ViewSignature ||
    signedPlan.Selector != "main")
    throw new Exception("OF030 validated exact recipe not selected");
if (AutoViewReadPlanner.Choose(a08List, "https://example.com/catalog",
    signedRecipe).ReasonCode != "generic_live_filter")
    throw new Exception("OF030 signed recipe selected without live proof");
signedRecipe.Host = "www.linkedin.com";
signedRecipe.Path = "/feed";
var of030LinkedManifest = InspectFixture(
    """{"host":"www.linkedin.com","route":"/feed","lists":1,"items":12}""");
if (AutoViewReadPlanner.Choose(of030LinkedManifest, "https://www.linkedin.com/feed",
    signedRecipe, exactLiveVerified: true).ReasonCode != "generic_live_filter")
    throw new Exception("OF030 unscoped LinkedIn recipe was selected");
signedRecipe.AccountScope = "linkedin:/in/test-account";
if (AutoViewReadPlanner.Choose(of030LinkedManifest, "https://www.linkedin.com/feed",
    signedRecipe, exactLiveVerified: true).ReasonCode != "verified_live_recipe")
    throw new Exception("OF030 live-proven scoped LinkedIn recipe ignored");
Console.WriteLine("OF030_SIGNATURE_CONTRACT_5_CHECKS_OK");
// A23: generic inbox route containing ordinary rendered HTML list items.
// Based on a real authenticated messaging SPA with 49 <li> rows but no role=log.
var a22Inbox = InspectFixture("""{"host":"www.linkedin.com","route":"/messaging/thread/:id","lists":6,"items":49,"nav":1,"messages":0}""");
var a22NotReady = InspectFixture("""{"host":"www.linkedin.com","route":"/messaging/thread/:id","lists":0,"items":0,"messages":0}""");
if (a22Inbox["view_type"]?.ToString() != "inbox" ||
    a22NotReady["view_type"]?.ToString() == "inbox")
    throw new Exception("A23 generic rendered inbox recognition failed");
Console.WriteLine("A23_RENDERED_INBOX_2_CHECKS_OK");
Console.WriteLine("ALL_SMOKES_PASS");
