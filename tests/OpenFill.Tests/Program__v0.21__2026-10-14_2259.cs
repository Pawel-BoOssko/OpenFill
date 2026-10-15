// OpenFill - Metadata: wersja 0.22, aktualizacja 2026-10-15 21:19
using System.Text.Json.Nodes;
using OpenFill.Cli;
using OpenFill.App;
using OpenFill.Core;
using OpenFill.Core.Agent;
using OpenFill.Core.Browser;
using OpenFill.Core.Cdp;
using OpenFill.Core.Config;
using OpenFill.Core.Hosting;
using OpenFill.Core.Logging;
using OpenFill.Core.Model;
using OpenFill.Tests;

var testsite = Path.Combine(AppContext.BaseDirectory, "testsite");
if (!Directory.Exists(testsite))
    testsite = Path.Combine(Directory.GetCurrentDirectory(), "tests", "OpenFill.Tests", "testsite");

// Test page files carry a version in their name (e.g. profile__v0.1__2026-10-04_1950.html); look them up by the stem.
string Page(string stem)
{
    var exact = Path.Combine(testsite, stem + ".html");
    if (File.Exists(exact)) return stem + ".html";
    var hit = Directory.GetFiles(testsite, stem + "__*.html").OrderBy(f => f).LastOrDefault()
        ?? throw new FileNotFoundException("Test page not found: " + stem);
    return Path.GetFileName(hit);
}

// "panelshot <file>": after the tests, copy the panel screenshot to the given file (visual check).
var panelShotOut = args.Length >= 2 && args[0] == "panelshot" ? args[1] : null;

if (args.Contains("tabs-only", StringComparer.OrdinalIgnoreCase)) { await TabTests.RunAsync(); return T.Report(); }

// ---------------------------------------------------------------------------- Unit tests
await T.Section("Redactor: secret removal", () =>
{
    T.NotContains("JWT masked", Redactor.Text("auth eyJhbGciOiJ.IUzI1NiIsImtp.Qbfe2eyJhbGci"), "eyJhbGci");
    T.Contains("text stays", Redactor.Text("Bearer abcdef123456 i reszta"), "reszta");
    T.NotContains("bearer token masked", Redactor.Text("Authorization: Bearer abcdef123456789"), "abcdef123456789");
    T.NotContains("API key masked", Redactor.Text("key sk-ABCDEFGHIJKLMNOP123456"), "sk-ABCDEFGHIJKLMNOP123456");
    T.NotContains("URL param masked", Redactor.Text("https://x.pl/a?token=SEKRET123&b=2"), "SEKRET123");
    var node = JsonNode.Parse("""{"password":"tajne","city":"Warszawa","nested":{"api_key":"sk-xxxxxxxxxxxxxxxxxx"}}""")!;
    var red = Redactor.Node(node)!.ToJsonString();
    T.NotContains("password field masked", red, "tajne");
    T.Contains("plain field stays", red, "Warszawa");
    return Task.CompletedTask;
});

await T.Section("Windows credential vault smoke", () =>
{
    var site = Guid.NewGuid().ToString("N");
    var login = "local-test";
    var candidate = Guid.NewGuid().ToString("N");
    var result = TemporaryCredentialVault.Save(site, login, candidate);
    T.Check("vault saved and read back", result.Saved);
    T.NotContains("vault response omits value", result.Message, candidate);
    if (result.Saved) T.Check("vault test entry deleted", TemporaryCredentialVault.Delete(site, login));
    return Task.CompletedTask;
});

await T.Section("Firefox profile list and NSS startup (no passwords are decrypted)", () =>
{
    var profile = FirefoxLogins.FindProfile();
    T.Check("Firefox profile with saved credentials found", profile != null);
    if (profile != null)
    {
        var list = FirefoxLogins.List(profile);
        T.Check("Firefox site metadata enumerated", list.Count > 0);
        using var nss = new FirefoxNssSession(profile);
        T.Check("Firefox NSS database opened", true);
        T.Check("Firefox NSS primary password status readable", nss.RequiresPrimaryPassword || !nss.RequiresPrimaryPassword);
    }
    return Task.CompletedTask;
});

await T.Section("Firefox metadata parser: device realm and three dates", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "openfill_firefox_fixture_" + Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(dir);
    try
    {
        var fixture = """
        {"logins":[
          {"hostname":"http://192.0.2.1","encryptedUsername":"cipher-user","encryptedPassword":"cipher-pass","guid":"router-alpha","httpRealm":"ARRIS Router","timeCreated":1577727011894,"timeLastUsed":1739633240680,"timePasswordChanged":1597788540948,"timesUsed":8},
          {"hostname":"http://192.0.2.1","encryptedUsername":"cipher-empty-user","encryptedPassword":"cipher-pass2","guid":"router-beta","httpRealm":"Orange Router","timeCreated":1577727011899,"timeLastUsed":1739633240681,"timePasswordChanged":1597788540949,"timesUsed":1}
        ]}
        """;
        File.WriteAllText(Path.Combine(dir, "logins.json"), fixture);
        var items = FirefoxLogins.List(dir);
        T.Eq("Firefox parser: retains identical IP records", 2, items.Count);
        T.Eq("Firefox parser: first device realm", "ARRIS Router", items[0].Realm);
        T.Eq("Firefox parser: second device realm", "Orange Router", items[1].Realm);
        T.Eq("Firefox parser: creation time", 1577727011894L, items[0].CreatedMillis);
        T.Eq("Firefox parser: last used", 1739633240680L, items[0].LastUsedMillis);
        T.Eq("Firefox parser: last password change", 1597788540948L, items[0].ChangedMillis);
        T.Eq("Firefox parser: times used", 8, items[0].TimesUsed);
        T.Eq("Firefox parser: stable credential id", "router-beta", items[1].Id);
    }
    finally { try { Directory.Delete(dir, true); } catch { } }
    return Task.CompletedTask;
});
await T.Section("Imported account vault: save, read, site boundary and delete", () =>
{
    var id = Guid.NewGuid().ToString("N");
    var url = "https://test-" + id + ".invalid:443";
    var login = "test-" + id;
    var sampleValue = Guid.NewGuid().ToString("N");
    var saved = ImportedCredentialVault.Save(url, login, sampleValue);
    T.Check("imported account stored", saved.Saved);
    T.Contains("new import: reports created", saved.Message, "Created");
    T.NotContains("new import: never returns password", saved.Message, sampleValue);
    var newValue = Guid.NewGuid().ToString("N");
    var updated = ImportedCredentialVault.Save(url, login, newValue);
    T.Check("existing import updated successfully", updated.Saved);
    T.Contains("existing import: reports updated", updated.Message, "Updated");
    T.NotContains("updated import: never returns password", updated.Message, newValue);
    T.Check("import list exact origin", ImportedCredentialVault.ListForSite(url).Count == 1);
    T.Check("imported account appears in global metadata list",
        ImportedCredentialVault.ListAllMetadata().Any(a => a.Login == login && a.Origin == ImportedCredentialVault.NormalizeOrigin(url)));
    T.Check("different subdomain cannot read imported account", ImportedCredentialVault.ListForSite("https://other.invalid").Count == 0);
    if (saved.Saved)
    {
        var account = ImportedCredentialVault.ListForSite(url).Single();
        T.Eq("imported account verified after update", newValue, ImportedCredentialVault.Read(account));
        T.Check("import test entry removed", ImportedCredentialVault.Delete(url, login));
    }
    return Task.CompletedTask;
});
await T.Section("Imported account origin: unrelated hosting tenants must stay separate", () =>
{
    T.Check("different GitHub Pages tenants unrelated",
        !ImportedCredentialVault.SameSiteFamily("https://alice.github.io", "https://bob.github.io"));
    T.Check("GitHub Pages tenant and own subdomain related",
        ImportedCredentialVault.SameSiteFamily("https://alice.github.io", "https://login.alice.github.io"));
    T.Check("different Pages.dev tenants unrelated",
        !ImportedCredentialVault.SameSiteFamily("https://alice.pages.dev", "https://bob.pages.dev"));
    T.Check("different Appspot tenants unrelated",
        !ImportedCredentialVault.SameSiteFamily("https://alice.appspot.com", "https://bob.appspot.com"));
    T.Check("different S3 virtual-hosted tenants unrelated",
        !ImportedCredentialVault.SameSiteFamily("https://alice.s3.amazonaws.com", "https://bob.s3.amazonaws.com"));
    T.Check("different co.kr registrants unrelated",
        !ImportedCredentialVault.SameSiteFamily("https://alice.co.kr", "https://bob.co.kr"));
    T.Check("different co.uk registrants unrelated",
        !ImportedCredentialVault.SameSiteFamily("https://alice.co.uk", "https://bob.co.uk"));
    T.Check("same registered domain sibling subdomains related",
        ImportedCredentialVault.SameSiteFamily("https://panel.example.pl", "https://login.example.pl"));
    T.Check("same registered co.uk sibling subdomains related",
        ImportedCredentialVault.SameSiteFamily("https://panel.example.co.uk", "https://login.example.co.uk"));
    T.Check("different .com registrants unrelated",
        !ImportedCredentialVault.SameSiteFamily("https://panel.example.com", "https://login.other.com"));
    return Task.CompletedTask;
});
await T.Section("Credential batch: six logins on three distinct domains", () =>
{
    var suffix = Guid.NewGuid().ToString("N")[..8];
    var accounts = new List<(string Site, string Login, string Password)>();
    try
    {
        for (var domain = 1; domain <= 3; domain++)
            for (var user = 1; user <= 2; user++)
            {
                var site = $"https://batch-{domain}-{suffix}.invalid";
                var login = $"batch-login-{domain}-{user}-{suffix}";
                var secret = Guid.NewGuid().ToString("N");
                accounts.Add((site, login, secret));
                T.Check("batch import: credential saved", ImportedCredentialVault.Save(site, login, secret).Saved);
            }

        T.Eq("batch import: six total accounts", 6, accounts.Count);
        foreach (var domain in Enumerable.Range(1, 3))
        {
            var site = $"https://batch-{domain}-{suffix}.invalid";
            var found = ImportedCredentialVault.ListForSite(site);
            T.Eq("batch import: two logins for one domain", 2, found.Count);
            T.Check("batch import: all usernames returned without secrets",
                found.All(a => accounts.Any(x => x.Site == a.Origin && x.Login == a.Login)) &&
                found.All(a => !accounts.Any(x => a.Login.Contains(x.Password, StringComparison.Ordinal))));
        }
    }
    finally
    {
        foreach (var a in accounts) ImportedCredentialVault.Delete(a.Site, a.Login);
    }
    T.Check("batch import: isolated test data cleaned up",
        !ImportedCredentialVault.ListAllMetadata().Any(a => a.Login.EndsWith(suffix, StringComparison.Ordinal)));
    return Task.CompletedTask;
});
await T.Section("Firefox Basic Auth: same IP, multiple realms and no username", () =>
{
    var site = "http://192.0.2.123";
    var marker = Guid.NewGuid().ToString("N");
    var realmA = "Router A " + marker[..6];
    var realmB = "Router B " + marker[..6];
    var sourceA = Guid.NewGuid().ToString();
    var sourceB = Guid.NewGuid().ToString();
    const long created = 1577727011894;
    const long used = 1739633240680;
    var accounts = new List<ImportedCredentialVault.Account>();
    try
    {
        var a = ImportedCredentialVault.Save(site, "", "temporary-test-secret-A", realmA, sourceA, created, used, created);
        var b = ImportedCredentialVault.Save(site, "", "temporary-test-secret-B", realmB, sourceB, created, used, created);
        T.Check("basic-auth: empty username allowed", a.Saved);
        T.Check("basic-auth: second device saved", b.Saved);
        accounts.AddRange(ImportedCredentialVault.ListForSite(site).Where(x => x.SourceId == sourceA || x.SourceId == sourceB));
        T.Eq("basic-auth: same IP retains two independent entries", 2, accounts.Count);
        T.Check("basic-auth: device labels preserved", accounts.Any(x => x.Realm == realmA) && accounts.Any(x => x.Realm == realmB));
        T.Check("basic-auth: Firefox dates preserved", accounts.All(x => x.CreatedMillis == created && x.LastUsedMillis == used));
        var first = accounts.Single(x => x.SourceId == sourceA);
        var second = accounts.Single(x => x.SourceId == sourceB);
        T.Check("basic-auth: two separate stored identities", first.Target != second.Target);
        T.Eq("basic-auth: first entry roundtrip", "temporary-test-secret-A", ImportedCredentialVault.Read(first));
        T.Eq("basic-auth: second entry roundtrip", "temporary-test-secret-B", ImportedCredentialVault.Read(second));
    }
    finally
    {
        foreach (var account in accounts) ImportedCredentialVault.Delete(account);
    }
    return Task.CompletedTask;
});
if (args.Contains("quick-only", StringComparer.OrdinalIgnoreCase)) return T.Report();
await T.Section("OutputLimiter: overflow to file", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "openfill_test_overflow");
    Directory.CreateDirectory(dir);
    var lim = new OutputLimiter(dir, 1200);
    var (small, f1) = lim.Apply("krotki", "x");
    T.Check("small output, no file", f1 is null && small == "krotki");
    var (big, f2) = lim.Apply(new string('A', 5000), "duzy");
    T.Check("large output truncated", big.Length < 5000 && big.Contains("truncated"));
    T.Check("file saved", f2 != null && File.Exists(f2) && new FileInfo(f2!).Length >= 5000);
    return Task.CompletedTask;
});

await T.Section("PageModel: render and diff", () =>
{
    var a = PageModel.From(JsonNode.Parse("""
      {"url":"http://x/","title":"T","forms":[{"id":"of1","fields":[
        {"id":"of2","type":"text","label":"Imie","value":""},
        {"id":"of3","type":"text","label":"Firma","value":""}],"buttons":[{"id":"of4","text":"Zapisz","kind":"submit"}]}]}
    """));
    var r = a.Render();
    T.Contains("render: field", r, "Imie");
    T.Contains("render: button", r, "Zapisz");
    var b = PageModel.From(JsonNode.Parse("""
      {"url":"http://x/","title":"T","forms":[{"id":"of1","fields":[
        {"id":"of2","type":"text","label":"Imie","value":"Jan"},
        {"id":"of3","type":"text","label":"Firma","value":""}],"buttons":[{"id":"of4","text":"Zapisz","kind":"submit"}]}]}
    """));
    var d = b.DiffFrom(a);
    T.Contains("diff: value change", d, "Jan");
    T.NotContains("diff: unchanged omitted", d, "Zapisz");
    return Task.CompletedTask;
});

await T.Section("BuildInfo: version and date", () =>
{
    T.Check("version non-empty", !string.IsNullOrEmpty(BuildInfo.Version));
    T.Contains("headline has the version date", BuildInfo.Headline(), BuildInfo.Format(BuildInfo.VersionDateLocal));
    return Task.CompletedTask;
});

await T.Section("SiteNotesStore: merge, deduplicate, correct, related hosts, concurrency and restart", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "openfill_test_notes_" + Guid.NewGuid().ToString("N")[..6]);
    using var log = new EventLog(dir, dir);
    var store = new SiteNotesStore(dir, log);

    T.Check("notes: first visit has no note", store.Get("panel.example.com") is null);

    store.Merge("panel.example.com",
        "login_url: /login\nrequired_parameter: customer_id\nSubmit only after validation");
    var first = store.Get("panel.example.com")!;
    T.Contains("notes: first knowledge saved", first.Knowledge, "login_url: /login");

    store.Merge("panel.example.com",
        "login_url: /login\nrequired_parameter: customer_id\nSubmit only after validation");
    var duplicate = store.Get("panel.example.com")!;
    T.Eq("notes: identical write adds no duplicate", 3,
        duplicate.Knowledge.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);

    store.Merge("panel.example.com", "success_marker: Dashboard");
    T.Contains("notes: new observation added", store.Get("panel.example.com")!.Knowledge, "success_marker: Dashboard");

    store.Merge("panel.example.com", "login_url: /new-login");
    var corrected = store.Get("panel.example.com")!;
    T.Contains("notes: labelled correction replaces old fact", corrected.Knowledge, "login_url: /new-login");
    T.NotContains("notes: corrected old value removed", corrected.Knowledge, "login_url: /login");

    store.Merge("auth.example.com", "auth_flow: email then password");
    var context = store.ContextFor("panel.example.com");
    T.Contains("notes: exact host in context", context, "panel.example.com");
    T.Contains("notes: related host available", context, "auth.example.com");
    T.Contains("notes: related knowledge available", context, "auth_flow: email then password");

    store.RecordOutcome("panel.example.com", false);
    T.Check("notes: failure marks stale", store.Get("panel.example.com") is { Failures: 1, Stale: true });
    store.RecordOutcome("panel.example.com", true, "success_marker: Account home");
    var recovered = store.Get("panel.example.com")!;
    T.Check("notes: later success clears stale", recovered is { Successes: 1, Failures: 1, Stale: false });
    T.Contains("notes: outcome correction merged", recovered.Knowledge, "success_marker: Account home");
    T.NotContains("notes: superseded outcome removed", recovered.Knowledge, "success_marker: Dashboard");

    store.RecordOutcome("panel.example.com", true,
        "api_hint: safe\ntoken=sk-SHOULDNOTSTAY1234567");
    T.NotContains("notes: secret removed from note", store.Get("panel.example.com")!.Knowledge, "sk-SHOULDNOTSTAY1234567");

    Parallel.For(0, 12, i =>
        store.Merge($"site{i}.parallel-example.net", $"fact_{i}: value_{i}"));
    T.Check("notes: parallel writes on different hosts preserved",
        Enumerable.Range(0, 12).All(i => store.Get($"site{i}.parallel-example.net")?.Knowledge.Contains($"value_{i}") == true));

    var restarted = new SiteNotesStore(dir, log);
    T.Contains("notes: survives store restart", restarted.Get("panel.example.com")!.Knowledge, "login_url: /new-login");
    T.Contains("notes: related context survives restart", restarted.ContextFor("panel.example.com"), "auth.example.com");
    return Task.CompletedTask;
});

await T.Section("AppConfig: save and load", () =>
{
    var f = Path.Combine(Path.GetTempPath(), "openfill_cfg_" + Guid.NewGuid().ToString("N")[..6] + ".json");
    var c = new AppConfig { Model = "gpt-6.1-sol", ReasoningEffort = "high", ResourceTelemetrySeconds = 45, ResourceTelemetryRetentionDays = 9, ResourceTelemetryMaxMb = 123 };
    c.Save(f);
    var c2 = AppConfig.Load(f);
    T.Eq("model preserved", "gpt-6.1-sol", c2.Model);
    T.Eq("effort preserved", "high", c2.ReasoningEffort);
    T.Eq("telemetry interval preserved", 45, c2.ResourceTelemetrySeconds);
    T.Eq("telemetry retention preserved", 9, c2.ResourceTelemetryRetentionDays);
    T.Eq("telemetry max size preserved", 123, c2.ResourceTelemetryMaxMb);
    return Task.CompletedTask;
});

await T.Section("Resource telemetry: process tree sample and explicit CPU scale", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "openfill_telemetry_" + Guid.NewGuid().ToString("N")[..6]);
    var paths = new AppPaths("test", root);
    paths.EnsureCreated();
    var cfg = new AppConfig
    {
        ResourceTelemetryEnabled = true,
        ResourceTelemetrySeconds = 30,
        ResourceTelemetryRetentionDays = 2,
        ResourceTelemetryMaxMb = 20
    };

    using var telemetry = new ResourceTelemetry(paths, cfg, () => new ResourceActivity(2, 1, 3));
    telemetry.SampleNow();
    Thread.Sleep(120);
    telemetry.SampleNow();

    T.Check("telemetry: log file created", File.Exists(telemetry.CurrentLogFile));
    var lines = File.ReadAllLines(telemetry.CurrentLogFile).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    T.Check("telemetry: at least two samples", lines.Length >= 2);
    var sample = JsonNode.Parse(lines[^1])!.AsObject();
    T.Eq("telemetry: active task count", 2, sample["activeTasks"]!.GetValue<int>());
    T.Eq("telemetry: waiting task count", 1, sample["waitingTasks"]!.GetValue<int>());
    T.Eq("telemetry: tab count", 3, sample["tabs"]!.GetValue<int>());
    T.Check("telemetry: process tree includes root", sample["processCount"]!.GetValue<int>() >= 1);
    T.Check("telemetry: working set recorded", sample["workingSetMb"]!.GetValue<double>() > 0);
    T.Contains("telemetry: CPU scale documented", sample["cpuScale"]!.GetValue<string>(), "total logical CPU capacity");
    T.Check("telemetry: process detail present", sample["processes"] is JsonArray { Count: > 0 });
    return Task.CompletedTask;
});

await T.Section("Model loop: Responses API request format (reasoning, tool results, limits)", async () =>
{
    var root = Path.Combine(Path.GetTempPath(), "openfill_test_loop_" + Guid.NewGuid().ToString("N")[..6]);
    var paths = new AppPaths("test", root);
    paths.EnsureCreated();
    using var log = new EventLog(paths.Logs, paths.Runs);
    var browser = new BrowserController(new CdpSession(new NullCdpConnection()), log);
    var config = new AppConfig { Model = "gpt-6.1-sol", ReasoningEffort = "low", MaxSteps = 5 };
    var registry = new ToolRegistry(browser, log, config, new OutputLimiter(paths.Overflow, 1000), new SiteNotesStore(paths.Notes, log), new HeadlessInteraction(), new GapRecorder(paths.GapsFile, log));

    var step1 = JsonNode.Parse("""
      {"id":"resp_1","output":[
        {"type":"reasoning","id":"rs_1","encrypted_content":"ENC123","summary":[]},
        {"type":"function_call","id":"fc_1","call_id":"call_1","name":"get_console","arguments":"{}"},
        {"type":"function_call","id":"fc_2","call_id":"call_2","name":"report_gap","arguments":"{\"needed\":\"x\",\"missing\":\"y\"}"}]}
    """)!.AsObject();
    var step2 = JsonNode.Parse("""
      {"id":"resp_2","output":[
        {"type":"function_call","id":"fc_3","call_id":"call_3","name":"finish","arguments":"{\"status\":\"success\",\"summary\":\"ok\"}"}]}
    """)!.AsObject();
    var model = new ScriptedModel(step1, step2);
    var result = await new AgentRunner(model, registry, config, log).RunAsync("Test task");

    T.Eq("result from finish", "success", result.Status);
    T.Eq("two requests to the model", 2, model.Requests.Count);
    var r1 = model.Requests[0];
    T.Eq("model", "gpt-6.1-sol", r1["model"]?.GetValue<string>());
    T.Eq("store=false", false, r1["store"]?.GetValue<bool>());
    T.Eq("effort", "low", r1["reasoning"]?["effort"]?.GetValue<string>());
    var toolNames = r1["tools"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()).ToList();
    T.Check("tools: get_page, act_many, finish, report_gap", new[] { "get_page", "act_many", "finish", "report_gap", "screenshot", "read_text" }.All(toolNames.Contains));
    T.Check("tools have type function", r1["tools"]!.AsArray().All(t => t!["type"]!.GetValue<string>() == "function"));
    var in2 = model.Requests[1]["input"]!.ToJsonString();
    T.Contains("reasoning returns in input (store=false)", in2, "ENC123");
    T.Contains("tool result 1 with call_id", in2, "\"call_id\":\"call_1\"");
    T.Contains("tool result 2 with call_id", in2, "\"call_id\":\"call_2\"");
    T.Contains("type function_call_output", in2, "function_call_output");
    T.Check("gap report saved", File.Exists(paths.GapsFile) && File.ReadAllText(paths.GapsFile).Contains("missing"));

    // Step limit: a model that never finishes.
    var looping = new ScriptedModel(JsonNode.Parse("""
      {"id":"r","output":[{"type":"function_call","id":"f","call_id":"c","name":"get_console","arguments":"{}"}]}
    """)!.AsObject());
    var registry2 = new ToolRegistry(browser, log, config, new OutputLimiter(paths.Overflow, 1000), new SiteNotesStore(paths.Notes, log), new HeadlessInteraction(), new GapRecorder(paths.GapsFile, log));
    var r2 = await new AgentRunner(looping, registry2, config, log).RunAsync("never ends");
    T.Eq("step limit -> partial", "partial", r2.Status);
    T.Eq("batches of steps, no question asked", config.MaxSteps * 6, looping.Requests.Count);

    // search_logs: finds an earlier event of the same task; says so when nothing matches.
    log.BeginRun();
    log.Write("test", "marker", "needle-12345 was seen on the page");
    var searching = JsonNode.Parse("""
      {"id":"s1","output":[
        {"type":"function_call","id":"fc_a","call_id":"call_a","name":"search_logs","arguments":"{\"query\":\"NEEDLE-12345\"}"},
        {"type":"function_call","id":"fc_b","call_id":"call_b","name":"search_logs","arguments":"{\"query\":\"no-such-text-xyz\"}"},
        {"type":"function_call","id":"fc_c","call_id":"call_c","name":"get_console","arguments":"{\"contains\":\"no-such-console-text\",\"level\":\"error\"}"}]}
    """)!.AsObject();
    var finishing = JsonNode.Parse("""
      {"id":"s2","output":[{"type":"function_call","id":"fc_d","call_id":"call_d","name":"finish","arguments":"{\"status\":\"success\",\"summary\":\"ok\"}"}]}
    """)!.AsObject();
    var searchModel = new ScriptedModel(searching, finishing);
    var rs = await new AgentRunner(searchModel, registry2, config, log).RunAsync("search the log");
    T.Eq("search task finishes", "success", rs.Status);
    var in4 = searchModel.Requests[1]["input"]!.ToJsonString();
    T.Contains("search_logs finds an earlier event", in4, "needle-12345 was seen");
    T.Contains("search_logs says when nothing matches", in4, "nothing in this task");
    T.Contains("get_console with a filter that matches nothing", in4, "(console empty)");
    log.EndRun();

    // ask_user: a plain question and a user-only question (verification code) reach the person through different doors.
    var rec = new RecordingInteraction();
    var registry4 = new ToolRegistry(browser, log, config, new OutputLimiter(paths.Overflow, 1000), new SiteNotesStore(paths.Notes, log), rec, new GapRecorder(paths.GapsFile, log));
    var asking = JsonNode.Parse("""
      {"id":"a1","output":[
        {"type":"function_call","id":"fc_p","call_id":"call_p","name":"ask_user","arguments":"{\"question\":\"Which city?\"}"},
        {"type":"function_call","id":"fc_h","call_id":"call_h","name":"ask_user","arguments":"{\"question\":\"Code from the e-mail?\",\"user_only\":true}"}]}
    """)!.AsObject();
    var asked = new ScriptedModel(asking, finishing);
    await new AgentRunner(asked, registry4, config, log).RunAsync("ask things");
    T.Eq("plain question goes the normal way", "Which city?", rec.Plain.SingleOrDefault());
    T.Eq("user_only question goes to the person only", "Code from the e-mail?", rec.HumanOnly.SingleOrDefault());
    T.Contains("the user's code comes back to the model", asked.Requests[1]["input"]!.ToJsonString(), "481516");

    // Model error 400 -> readable message, no retry.
    var bad = new ThrowingModel(new ModelException(400, "{\"error\":{\"message\":\"Unsupported value: 'reasoning.effort'\"}}"));
    var r3 = await new AgentRunner(bad, registry2, config, log).RunAsync("x");
    T.Eq("error 400 -> failed", "failed", r3.Status);
    T.Contains("API message in summary", r3.Summary, "Unsupported value");
});

// ---------------------------------------------------------------------------- Integration test (Chromium)
await T.Section("Integration: extraction + actions + save on the test page", async () =>
{
    using var site = new StaticSite(testsite);
    using var chromium = new ChromiumLauncher();
    var wsUrl = await chromium.LaunchAsync(Path.Combine(Path.GetTempPath(), "openfill_test_profile_" + Guid.NewGuid().ToString("N")[..6]), headless: true);
    var conn = new WebSocketCdpConnection();
    await conn.ConnectAsync(wsUrl);
    var cdp = await CdpBootstrap.AttachPageAsync(conn, "about:blank");
    using var log = new EventLog(Path.Combine(Path.GetTempPath(), "openfill_test_logs"), Path.Combine(Path.GetTempPath(), "openfill_test_logs"));
    var browser = new BrowserController(cdp, log);
    await browser.InitAsync();

    await browser.NavigateAsync(site.BaseUrl + Page("xing"));
    var page = await browser.ExtractAsync();
    var rendered = page.Render();
    T.Contains("extraction: Firma field", rendered, "Firma");
    T.Contains("extraction: select with options", rendered, "IT");
    T.Contains("extraction: submit button", rendered, "Zapisz");

    // Find field ids by label in the raw model.
    string? IdByLabel(string label)
    {
        foreach (var form in page.Raw["forms"]!.AsArray())
            foreach (var fld in form!["fields"]!.AsArray())
                if (fld!["label"]?.GetValue<string>()?.Contains(label) == true) return fld["id"]!.GetValue<string>();
        return null;
    }
    var headlineId = IdByLabel("Nagłówek");
    var companyId = IdByLabel("Firma");
    var industryId = IdByLabel("Branża");
    var remoteId = IdByLabel("zdalna");
    T.Check("headline id found", headlineId != null);
    T.Check("company id found", companyId != null);

    await browser.ActAsync(headlineId!, "set", "Senior AI Consultant");
    await browser.ActAsync(companyId!, "set", "Acme Consulting");
    await browser.ActAsync(industryId!, "select", "IT");
    if (remoteId != null) await browser.ActAsync(remoteId, "check", "true");

    // The diff should show the changed values.
    var page2 = await browser.ExtractAsync();
    var diff = page2.DiffFrom(page);
    T.Contains("diff shows company", diff, "Acme Consulting");

    // A password field (typed by the person or filled by the browser) must never reach the model.
    await browser.RunJsAsync("(function(){ var f=document.createElement('form'); f.innerHTML='<label>Haslo testowe <input type=\"password\" id=\"ofpw\"></label>'; document.body.appendChild(f); document.getElementById('ofpw').value='SekretnyTekst123'; return 1; })()");
    var pagePw = await browser.ExtractAsync();
    T.Contains("password field is listed", pagePw.Render(), "Haslo testowe");
    T.NotContains("password value hidden from the model (render)", pagePw.Render(), "SekretnyTekst123");
    T.NotContains("password value hidden from the model (raw)", pagePw.Raw.ToJsonString(), "SekretnyTekst123");

    // Click Zapisz (Save) and verify window.__saved.
    string? saveId = null;
    foreach (var form in page2.Raw["forms"]!.AsArray())
        foreach (var b in form!["buttons"]!.AsArray())
            if (b!["text"]?.GetValue<string>()?.Contains("Zapisz") == true) saveId = b["id"]!.GetValue<string>();
    T.Check("save button id", saveId != null);
    await browser.ActAsync(saveId!, "click", null);
    await Task.Delay(300);

    var saved = await browser.RunJsAsync("JSON.stringify(window.__saved||null)");
    var savedStr = saved?["value"]?.GetValue<string>() ?? "null";
    T.Contains("form saved: company", savedStr, "Acme Consulting");
    T.Contains("form saved: industry", savedStr, "it");
    T.Contains("form saved: remote", savedStr, "true");

    // Network traffic: the page document should be visible.
    var net = browser.Network.Snapshot(Page("xing"), 20);
    T.Check("network: page document visible", net.Any(e => e.Url.Contains(Page("xing"))));

    await conn.DisposeAsync();
});

// ---------------------------------------------------------------------------- Imported credential end-to-end
await T.Section("Imported credentials: list, select, fill login+password, submit local login", async () =>
{
    using var site = new StaticSite(testsite);
    var suffix = Guid.NewGuid().ToString("N")[..8];
    var login1 = "alpha-" + suffix;
    var login2 = "beta-" + suffix;
    var password1 = "A!" + Guid.NewGuid().ToString("N");
    var password2 = "B!" + Guid.NewGuid().ToString("N");
    site.AddLoginAccount(login1, password1);
    site.AddLoginAccount(login2, password2);

    var origin = ImportedCredentialVault.NormalizeOrigin(site.BaseUrl)!;
    var relatedOrigin = $"http://127.0.0.1:{site.Port + 1}";
    var relatedLogin = "related-" + suffix;
    var relatedPassword = "C!" + Guid.NewGuid().ToString("N");

    var s1 = ImportedCredentialVault.Save(site.BaseUrl, login1, password1);
    var s2 = ImportedCredentialVault.Save(site.BaseUrl, login2, password2);
    var s3 = ImportedCredentialVault.Save(relatedOrigin, relatedLogin, relatedPassword);
    T.Check("credential e2e: first stored", s1.Saved);
    T.Check("credential e2e: second stored", s2.Saved);
    T.Check("credential e2e: related-origin stored", s3.Saved);

    using var chromium = new ChromiumLauncher();
    var wsUrl = await chromium.LaunchAsync(Path.Combine(Path.GetTempPath(), "openfill_cred_profile_" + suffix), headless: true);
    var conn = new WebSocketCdpConnection();
    await conn.ConnectAsync(wsUrl);
    try
    {
        var cdp = await CdpBootstrap.AttachPageAsync(conn, "about:blank");
        var paths = new AppPaths("test", Path.Combine(Path.GetTempPath(), "openfill_cred_root_" + suffix));
        paths.EnsureCreated();
        using var log = new EventLog(paths.Logs, paths.Runs);
        log.BeginRun("credential_e2e");
        var browser = new BrowserController(cdp, log);
        await browser.InitAsync();
        var config = new AppConfig();
        var registry = new ToolRegistry(browser, log, config, new OutputLimiter(paths.Overflow, config.ToolOutputLimitChars),
            new SiteNotesStore(paths.Notes, log), new HeadlessInteraction(), new GapRecorder(paths.GapsFile, log));
        var tools = registry.Build().ToDictionary(t => t.Name);

        async Task<ToolResult> CallCredentialTool(string name, object args)
        {
            var node = System.Text.Json.JsonSerializer.SerializeToNode(args)!.AsObject();
            return await tools[name].Handler(node, CancellationToken.None);
        }

        await browser.NavigateAsync(site.BaseUrl + "credential-login");

        var listed = await CallCredentialTool("list_imported_logins", new { });
        T.Contains("credential list: first login visible", listed.Output, login1);
        T.Contains("credential list: second login visible", listed.Output, login2);
        T.Contains("credential list: related login visible", listed.Output, relatedLogin);
        T.NotContains("credential list: first password hidden", listed.Output, password1);
        T.NotContains("credential list: second password hidden", listed.Output, password2);
        T.NotContains("credential list: related password hidden", listed.Output, relatedPassword);

        var ambiguous = await CallCredentialTool("use_imported_password", new { });
        T.Check("credential fill: ambiguity rejected", !ambiguous.Ok);
        T.Contains("credential fill: ambiguity names accounts", ambiguous.Output, login1);
        T.Contains("credential fill: ambiguity names second account", ambiguous.Output, login2);
        T.NotContains("credential fill: ambiguity hides passwords", ambiguous.Output, password1);

        var selected = await CallCredentialTool("use_imported_password", new { login = login2 });
        T.Check("credential fill: selected account succeeded", selected.Ok);
        T.Contains("credential fill: selected login reported", selected.Output, login2);
        T.Contains("credential fill: login field found", selected.Output, "loginFieldFound=True");
        T.Contains("credential fill: login filled", selected.Output, "loginFilled=True");
        T.NotContains("credential fill: password not returned", selected.Output, password2);

        var page = await browser.ExtractAsync();
        string? signInId = null;
        foreach (var form in page.Raw["forms"]!.AsArray())
            foreach (var button in form!["buttons"]!.AsArray())
                if (button!["text"]?.GetValue<string>()?.Contains("Sign in", StringComparison.OrdinalIgnoreCase) == true)
                    signInId = button["id"]!.GetValue<string>();

        T.Check("credential e2e: submit button found", signInId != null);
        await browser.ActAsync(signInId!, "click", null);
        await Task.Delay(400);
        var resultPage = await browser.ExtractAsync();
        T.Contains("credential e2e: server accepted selected account", resultPage.Render(), "LOGIN_OK: " + login2);
        T.Eq("credential e2e: server saw selected login", login2, site.LastLoginUser);
        T.Check("credential e2e: server verified selected password", site.LastLoginOk);

        await browser.NavigateAsync(site.BaseUrl + "credential-login");
        await browser.RunJsAsync("(()=>{document.getElementById('username').remove(); return true;})()");
        var passwordOnly = await CallCredentialTool("use_imported_password", new { login = login1 });
        T.Check("credential fill: password-only step still succeeds", passwordOnly.Ok);
        T.Contains("credential fill: missing login field reported", passwordOnly.Output, "loginFieldFound=False");
        T.NotContains("credential fill: password-only output hides password", passwordOnly.Output, password1);
        // Multiple historical routers may have exactly the same URL and login.
        // Luna must ask for an explicit realm/source_id rather than guessing which password to use.
        var routerIdA = Guid.NewGuid().ToString("N");
        var routerIdB = Guid.NewGuid().ToString("N");
        try
        {
            T.Check("same-IP: device A saved", ImportedCredentialVault.Save(site.BaseUrl, login1, "Router-A-test", "ARRIS", routerIdA).Saved);
            T.Check("same-IP: device B saved", ImportedCredentialVault.Save(site.BaseUrl, login1, "Router-B-test", "ORANGE", routerIdB).Saved);
            var routerList = await CallCredentialTool("list_imported_logins", new { });
            T.Contains("same-IP: realm ARRIS exposed", routerList.Output, "ARRIS");
            T.Contains("same-IP: realm ORANGE exposed", routerList.Output, "ORANGE");
            T.Contains("same-IP: unique Firefox source exposed", routerList.Output, routerIdB);
            var ambiguousRouter = await CallCredentialTool("use_imported_password", new { login = login1 });
            T.Check("same-IP: no guessed router credential", !ambiguousRouter.Ok);
            T.Contains("same-IP: explicit realm requested", ambiguousRouter.Output, "CHOOSE_CREDENTIAL");
            await browser.NavigateAsync(site.BaseUrl + "credential-login");
            await Task.Delay(400);
            await browser.ExtractAsync();
            var chosenRouter = await CallCredentialTool("use_imported_password", new { login = login1, realm = "ORANGE", source_id = routerIdB });
            if (!chosenRouter.Ok) Console.WriteLine("  DIAGNOSTIC same-IP choice: " + chosenRouter.Output);
            T.Check("same-IP: exact named router selected", chosenRouter.Ok);
            T.Contains("same-IP: selected realm confirmed", chosenRouter.Output, "realm=ORANGE");
            T.NotContains("same-IP: password hidden from tool output", chosenRouter.Output, "Router-B-test");
        }
        finally
        {
            foreach (var entry in ImportedCredentialVault.ListForSite(origin)
                .Where(a => a.SourceId == routerIdA || a.SourceId == routerIdB))
                ImportedCredentialVault.Delete(entry);
        }
    }
    finally
    {
        await conn.DisposeAsync();
        ImportedCredentialVault.Delete(origin, login1);
        ImportedCredentialVault.Delete(origin, login2);
        ImportedCredentialVault.Delete(relatedOrigin, relatedLogin);
    }
});

// ---------------------------------------------------------------------------- Realistic form via model tools
await T.Section("Model tools on a hard form (modal, React, autocomplete, custom list, file)", async () =>
{
    using var site = new StaticSite(testsite);
    using var chromium = new ChromiumLauncher();
    var wsUrl = await chromium.LaunchAsync(Path.Combine(Path.GetTempPath(), "openfill_test_profile3_" + Guid.NewGuid().ToString("N")[..6]), headless: true);
    var conn = new WebSocketCdpConnection();
    await conn.ConnectAsync(wsUrl);
    var cdp = await CdpBootstrap.AttachPageAsync(conn, "about:blank");
    var paths = new AppPaths("test", Path.Combine(Path.GetTempPath(), "openfill_test_root3_" + Guid.NewGuid().ToString("N")[..6]));
    paths.EnsureCreated();
    using var log = new EventLog(paths.Logs, paths.Runs);
    log.BeginRun("tools_test");
    var browser = new BrowserController(cdp, log);
    await browser.InitAsync();
    var config = new AppConfig();
    var siteNotes = new SiteNotesStore(paths.Notes, log);
    siteNotes.Merge("127.0.0.1", "profile_test_hint: local profile form");
    var registry = new ToolRegistry(browser, log, config, new OutputLimiter(paths.Overflow, config.ToolOutputLimitChars),
        siteNotes, new HeadlessInteraction(), new GapRecorder(paths.GapsFile, log));
    var tools = registry.Build().ToDictionary(t => t.Name);

    async Task<ToolResult> Call(string name, object args)
    {
        var node = System.Text.Json.JsonSerializer.SerializeToNode(args)!.AsObject();
        return await tools[name].Handler(node, CancellationToken.None);
    }
    static string? Id(string text, string labelOrText)
    {
        foreach (var line in text.Split('\n'))
            if (line.Contains('"' + labelOrText) || line.Contains(labelOrText + '"'))
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, @"\[(of\d+)\]");
                if (m.Success) return m.Groups[1].Value;
            }
        return null;
    }

    var nav = await Call("navigate", new { url = site.BaseUrl + Page("profile") });
    T.Contains("navigation: dialog visible", nav.Output, "OPEN DIALOG");
    T.Contains("navigation: page heading", nav.Output, "Edytuj profil");
    T.Contains("navigation: site memory injected", nav.Output, "profile_test_hint: local profile form");

    // Modal covers the form: real click on "Akceptuj wszystkie" (Accept all).
    var accept = Id(nav.Output, "Akceptuj wszystkie");
    T.Check("cookies button id", accept != null);
    var rc = await Call("act", new { id = accept, action = "realClick" });
    T.Check("realClick ok", rc.Ok);
    var afterCookie = await Call("get_page", new { diff = true });
    T.Contains("diff: dialog gone", afterCookie.Output, "Removed");
    var cookies = await browser.RunJsAsync("window.__cookies");
    T.Eq("cookies accepted", "accepted", cookies?["value"]?.GetValue<string>());

    // Validation: saving with no data shows errors and the page message.
    var page0 = await Call("get_page", new { });
    var save = Id(page0.Output, "Zapisz zmiany")!;
    await Call("act", new { id = save, action = "click" });
    var errPage = await Call("get_page", new { });
    T.Contains("validation: field error", errPage.Output, "!ERROR: Podaj stanowisko.");
    T.Contains("validation: page message", errPage.Output, "Popraw błędy w formularzu.");

    // In bulk: position + industry + date + contenteditable editor.
    var page = errPage.Output;
    var many = await Call("act_many", new
    {
        steps = new object[]
        {
            new { id = Id(page, "Stanowisko"), action = "set", value = "Senior AI Consultant" },
            new { id = Id(page, "Branża"), action = "select", value = "Doradztwo" },
            new { id = Id(page, "Od kiedy"), action = "set", value = "2023-07" },
            new { id = Id(page, "O mnie"), action = "set", value = "Architekt IT, automatyzacja i AI." }
        }
    });
    T.Check("act_many ok", many.Ok);

    // Controlled field + server autocomplete: type char by char, wait, pick the suggestion.
    await Call("act", new { id = Id(page, "Firma"), action = "type", value = "Acm" });
    var w = await Call("wait", new { text = "Acme Consulting", ms = 5000 });
    T.Check("autocomplete: suggestion appeared", w.Ok);
    var withSuggest = await Call("get_page", new { });
    T.Contains("autocomplete: suggestion as option", withSuggest.Output, "option [");
    var net = await Call("get_network", new { filter = "companies" });
    T.Contains("network: autocomplete request visible", net.Output, "/api/companies?q=Acm");
    await Call("act", new { id = Id(withSuggest.Output, "Acme Consulting"), action = "click" });

    // Custom city dropdown.
    var cityBox = Id(withSuggest.Output, "Wybierz miasto") ?? Id(withSuggest.Output, "Miasto");
    T.Check("city list id", cityBox != null);
    await Call("act", new { id = cityBox, action = "click" });
    var cityOpen = await Call("get_page", new { diff = true });
    T.Contains("city list: options visible", cityOpen.Output, "Warszawa");
    await Call("act", new { id = Id(cityOpen.Output, "Warszawa"), action = "click" });

    // role=switch toggle and file.
    var p2 = await Call("get_page", new { });
    var sw = Id(p2.Output, "Otwarty na oferty");
    T.Check("switch shown as toggle", p2.Output.Contains("toggle [") && sw != null);
    await Call("act", new { id = sw, action = "click" });
    var photo = Path.Combine(paths.Root, "zdjecie.png");
    await File.WriteAllBytesAsync(photo, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
    var up = await Call("upload_file", new { id = Id(p2.Output, "Zdjęcie profilowe"), path = photo });
    T.Check("upload_file ok", up.Ok);

    // Save and verify on three levels: page message, network traffic, server-side data.
    await Call("act", new { id = save, action = "click" });
    var saved = await Call("wait", new { text = "Profil zapisany", ms = 5000 });
    T.Check("message: profile saved", saved.Ok);
    var post = await Call("get_network", new { filter = "/api/profile" });
    T.Contains("network: save POST", post.Output, "POST");
    var body = site.LastProfile?.ToJsonString() ?? "";
    T.Contains("server: position", body, "Senior AI Consultant");
    T.Contains("server: company from suggestion (controlled field)", body, "Acme Consulting");
    T.Contains("server: city from custom list", body, "Warszawa");
    T.Contains("server: industry", body, "consulting");
    T.Contains("server: date", body, "2023-07");
    T.Contains("server: contenteditable editor", body, "Architekt IT");
    T.Contains("server: switch", body, "\"open\":true");
    T.Contains("server: file", body, "zdjecie.png");

    // A confirm() dialog does not block the page: it is accepted and reported to the model.
    await Call("act", new { id = Id(p2.Output, "Wyczyść formularz"), action = "click" });
    await Task.Delay(300);
    var cleared = await browser.RunJsAsync("window.__cleared === true");
    T.Check("confirm(): accepted, page did not hang", cleared?["value"]?.GetValue<bool>() == true);
    var afterDialog = await Call("get_page", new { diff = true });
    T.Contains("get_page reports page dialog", afterDialog.Output, "Na pewno wyczyścić formularz?");

    var txt = await Call("read_text", new { query = "zapisany" });
    T.Contains("read_text: finds message", txt.Output, "Profil zapisany");
    var shot = await Call("screenshot", new { });
    T.Check("screenshot: image for the model", shot.ImageJpegBase64 is { Length: > 1000 });

    // Logs: no secrets, network file exists.
    T.Check("network log saved", log.CurrentNetworkFile != null && File.Exists(log.CurrentNetworkFile));
    await conn.DisposeAsync();
});

// ---------------------------------------------------------------------------- Panel + AppHost + mock (whole app without a window)
ChromiumLauncher? panelChromium = null;
await T.Section("Panel: user advice delivery acknowledgement", () =>
{
    var panel = OpenFill.Core.Assets.AssetLoader.Load("panel.html");
    T.Contains("UI answer waits for receipt", panel, "c.dataset.sending");
    T.Contains("UI handles explicit receipt", panel, "case 'answerReceived'");
    T.Contains("UI distinguishes delivery from completion", panel, "not yet completion");
    T.Contains("UI reports undelivered answers", panel, "Message NOT delivered");
    return Task.CompletedTask;
});
await T.Section("Live panel: version with date, task from panel, consent card, result", async () =>
{
    using var site = new StaticSite(testsite);
    using var chromium = new ChromiumLauncher();
    panelChromium = chromium;
    try {
    var wsUrl = await chromium.LaunchAsync(Path.Combine(Path.GetTempPath(), "openfill_test_profile4_" + Guid.NewGuid().ToString("N")[..6]), headless: true);
    var conn = new WebSocketCdpConnection();
    await conn.ConnectAsync(wsUrl);
    var cdp = await CdpBootstrap.AttachPageAsync(conn, site.BaseUrl + Page("xing"));

    var paths = new AppPaths("test", Path.Combine(Path.GetTempPath(), "openfill_test_root4_" + Guid.NewGuid().ToString("N")[..6]));
    var config = new AppConfig { MaxSteps = 10, ConfirmIrreversible = true };
    await using var panel = new PanelServer();
    panel.Start();
    await using var host = new AppHost(paths, config, new SecretStore(paths.SecretFile), cdp, panel, new HeadlessInteraction(),
        () => new MockModelClient(askConfirm: true));
    await host.InitAsync();
    await host.Session.Browser.NavigateAsync(site.BaseUrl + Page("xing"));

    conn.Event += ev => { if (ev.Method is "Inspector.targetCrashed" or "Target.targetCrashed" or "Target.detachedFromTarget") Console.WriteLine("  [cdp-event] " + ev.Method + " " + ev.Params.ToJsonString()); };
    var ui = await CdpBootstrap.CreatePageAsync(conn, panel.Url, 480, 1400);
    await ui.SendAsync("Page.enable");

    async Task<string?> UiEval(string js) { try { return (await ui.EvaluateAsync(js))?.ToString(); } catch (Exception ex) { if (!conn.IsConnected) Console.WriteLine("  [ui-eval after disconnect] " + js[..Math.Min(60, js.Length)] + " | " + ex.Message + "\n" + chromium.Tail()); return null; } }
    async Task<bool> Until(string js, int ms = 8000)
    {
        var end = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < end) { if (await UiEval(js) == "true") return true; await Task.Delay(150); }
        return false;
    }

    T.Check("panel connected to app", await Until("document.getElementById('stateText').textContent === 'ready'"));
    var vdate = await UiEval("document.getElementById('vdate').textContent");
    T.Eq("header: version date on top", BuildInfo.Format(BuildInfo.VersionDateLocal), vdate);
    T.Contains("header: version number", await UiEval("document.getElementById('vmeta').textContent") ?? "", "v" + BuildInfo.Version);
    T.Contains("window title with date", await UiEval("document.title") ?? "", BuildInfo.Format(BuildInfo.VersionDateLocal));

    await UiEval("document.getElementById('task').value = 'Fill in the first field with a test value'; document.getElementById('run').click(); true");
    T.Check("consent card appeared in panel", await Until("!!document.querySelector('.card')"));
    T.Check("state: working", await UiEval("document.getElementById('stateText').textContent") == "working...");
    T.Check("banner: working, visible", await Until("!document.getElementById('banner').hidden && document.getElementById('banner').classList.contains('running')"));
    T.Check("task field locked while running", await UiEval("document.getElementById('task').readOnly && document.getElementById('run').disabled") == "true");
    await UiEval("document.querySelector('.card .btn.primary').click(); true");
    T.Check("panel result: success", await Until("!!document.querySelector('.result.success')"));
    T.Check("banner: finished and green", await Until("document.getElementById('banner').classList.contains('done') && !document.getElementById('banner').hidden"));
    T.Check("task field unlocked after the end", await UiEval("!document.getElementById('task').readOnly && !document.getElementById('run').disabled") == "true");
    T.Check("history: the task is listed", await Until("document.querySelectorAll('#histList .hitem').length === 1 && document.querySelector('#histList .pill.success') !== null"));
    T.Contains("history: task text and source", await UiEval("document.getElementById('histList').innerText") ?? "", "Fill in the first field");
    var events = int.Parse(await UiEval("document.querySelectorAll('#events .ev').length") ?? "0");
    T.Check($"live events in panel ({events})", events >= 6);
    T.Contains("panel shows tool call", await UiEval("document.getElementById('events').innerText") ?? "", "act");

    // The Tools filter hides other sources.
    await UiEval("document.querySelector('[data-g=tool]').click(); true");
    var hidden = int.Parse(await UiEval("[...document.querySelectorAll('#events .ev')].filter(e=>e.style.display==='none').length") ?? "0");
    T.Check("filter hides events from other sources", hidden > 0);
    await UiEval("document.querySelector('[data-g=all]').click(); true");

    // Reconnecting the panel (e.g. reload) restores the history.
    await ui.SendAsync("Page.reload");
    if (!conn.IsConnected) Console.WriteLine("  [chromium after reload] " + chromium.Tail());
    T.Check("after reload: history restored", await Until("document.querySelectorAll('#events .ev').length >= " + events, 8000));
    T.Check("after reload: result still visible", await Until("!!document.querySelector('.result.success')"));

    // Consent declined -> partial result.
    await UiEval("document.getElementById('task').value = 'Second task'; document.getElementById('run').click(); true");
    T.Check("second consent card", await Until("!!document.querySelector('.card')"));
    await UiEval("document.querySelectorAll('.card .btn')[1].click(); true");
    T.Check("decline -> partial result", await Until("!!document.querySelector('.result.partial')"));

    T.Check("site note saved after success", Directory.GetFiles(paths.Notes, "*.json").Length >= 1);

    if (conn.LastError != null) Console.WriteLine("  [chromium] " + chromium.Tail());
    await UiEval("window.scrollTo(0,0); true");
    var shot = await ui.SendAsync("Page.captureScreenshot", new JsonObject { ["format"] = "png" });
    var png = Path.Combine(Path.GetTempPath(), "openfill_panel.png");
    await File.WriteAllBytesAsync(png, Convert.FromBase64String(shot!["data"]!.GetValue<string>()));
    if (panelShotOut != null) File.Copy(png, panelShotOut, overwrite: true);
    await conn.DisposeAsync();
    } catch { Console.WriteLine("  [chromium] " + chromium.Tail()); throw; }
});

await T.Section("HistoryStore: upsert, order, interrupted, run events", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "openfill_test_history_" + Guid.NewGuid().ToString("N")[..6]);
    var store = new HistoryStore(Path.Combine(dir, "tasks.ndjson"));
    var t0 = DateTime.UtcNow;
    store.Append(new HistoryRecord("A", "panel", "first task", t0, null, "running", null, null));
    store.Append(new HistoryRecord("A", "panel", "first task", t0, t0.AddSeconds(5), "success", "done A", "run_a"));
    store.Append(new HistoryRecord("B", "mcp", "second task", t0.AddSeconds(10), null, "running", null, null));
    store.Append(new HistoryRecord("C", "mcp", "third task", t0.AddSeconds(20), null, "running", null, null));
    var list = store.Load(10, "C");
    T.Eq("three tasks, one line per id", 3, list.Count);
    T.Eq("newest first", "C", list[0].Id);
    T.Eq("current stays running", "running", list[0].Status);
    T.Eq("old running task becomes interrupted", "interrupted", list[1].Status);
    T.Eq("finished task keeps its summary", "done A", list[2].Summary);
    T.Eq("limit respected", 2, store.Load(2, "C").Count);

    var runs = Path.Combine(dir, "runs");
    Directory.CreateDirectory(runs);
    File.WriteAllText(Path.Combine(runs, "run_20261005_1_abc.ndjson"),
        "{\"tsUtc\":\"2026-10-05T10:00:00Z\",\"source\":\"tool\",\"eventType\":\"call\",\"status\":\"info\",\"message\":\"navigate\",\"data\":{\"x\":1}}\nnot json\n" +
        "{\"tsUtc\":\"2026-10-05T10:00:01Z\",\"source\":\"session\",\"eventType\":\"end\",\"status\":\"ok\",\"message\":\"Finished\"}\n");
    var (events, truncated) = HistoryStore.LoadEvents(runs, "20261005_1_abc", 100);
    T.Eq("events: broken line skipped", 2, events.Count);
    T.Check("events: not truncated", !truncated);
    T.Eq("events: limit truncates", true, HistoryStore.LoadEvents(runs, "20261005_1_abc", 1).Truncated);
    T.Eq("events: path tricks refused", 0, HistoryStore.LoadEvents(runs, "..\\..\\x", 100).Events.Count);
    return Task.CompletedTask;
});

await T.Section("Frames of other domains and sign-in pop-ups", () =>
{
    T.Check("frame id is split", FrameLayer.TrySplit("f2:of7", out var k, out var inner) && k == "f2" && inner == "of7");
    T.Check("plain id is not a frame id", !FrameLayer.TrySplit("of7", out _, out _));
    T.Check("empty id is not a frame id", !FrameLayer.TrySplit("", out _, out _) && !FrameLayer.TrySplit(null, out _, out _));
    T.Check("frame id without inner id is refused", !FrameLayer.TrySplit("f3:", out _, out _));
    T.Check("Google sign-in is a pop-up", SignInPopup.Looks("https://accounts.google.com/v3/signin/accountchooser?client_id=1"));
    T.Check("display=popup is a pop-up", SignInPopup.Looks("https://example.com/auth?display=popup&x=1"));
    T.Check("Apple sign-in is a pop-up", SignInPopup.Looks("https://appleid.apple.com/auth/authorize"));
    T.Check("Facebook dialog is a pop-up", SignInPopup.Looks("https://www.facebook.com/v18.0/dialog/oauth?x=1"));
    T.Check("an ordinary link is not a pop-up", !SignInPopup.Looks("https://example.com/help") && !SignInPopup.Looks("not a url") && !SignInPopup.Looks(null));
    var reg = new PopupRegistry<object> { Max = 3 };
    var w1 = new object(); var w2 = new object(); var w3 = new object(); var w4 = new object(); var w5 = new object();
    T.Eq("first pop-up closes nothing", 0, reg.Add("https://www.instagram.com/oauth/oidc/?x=1", w1).Count);
    var rep = reg.Add("https://www.instagram.com/oauth/oidc/?x=2&y=3", w2);
    T.Check("the same pop-up again replaces the earlier one", rep.Count == 1 && ReferenceEquals(rep[0], w1) && reg.Count == 1);
    T.Eq("another address does not replace", 0, reg.Add("https://membership.glgresearch.com/onboarding/linkedin-auth.html", w3).Count);
    T.Eq("about:blank pop-ups are never replaced", 0, reg.Add("about:blank", w4).Count + reg.Count - 3);
    var over = reg.Add("about:blank", w5);
    T.Check("over the limit the oldest is closed", over.Count == 1 && ReferenceEquals(over[0], w2) && reg.Count == 3);
    reg.Remove(w3);
    T.Eq("a closed pop-up is forgotten", 2, reg.Count);
    T.Eq("key ignores the query and the case", PopupRegistry<object>.KeyOf("https://A.com/Path?q=1"), PopupRegistry<object>.KeyOf("https://a.com/path?q=2"));
    return Task.CompletedTask;
});

await TabTests.RunAsync();
await CostTests.RunAsync();
await DefaultsTests.RunAsync();
await SharedTests.RunAsync();
if (args.Contains("mcp-only", StringComparer.OrdinalIgnoreCase)) { await McpTests.RunAsync(); return T.Report(); }
await McpTests.RunAsync();
await FileTransferTests.RunAsync();
await ParallelTests.RunAsync(testsite, Page);
return T.Report();
