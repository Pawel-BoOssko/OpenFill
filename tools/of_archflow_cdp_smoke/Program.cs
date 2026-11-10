// Real Chromium/CDP integration smoke, isolated from OpenFill stable and dev sessions.
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;
using OpenFill.Core.Cdp;

static void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("ARCHFLOW_CDP_FAIL: " + label);
}

static int FreePort()
{
    var candidate = new TcpListener(IPAddress.Loopback, 0);
    candidate.Start();
    var port = ((IPEndPoint)candidate.LocalEndpoint).Port;
    candidate.Stop();
    return port;
}

static async Task ServeAsync(TcpListener listener, CancellationToken ct)
{
    try
    {
        while (!ct.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(ct);
            _ = Task.Run(async () => {
                using (client)
                {
                    try
                    {
                        using var stream = client.GetStream();
                        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                        var first = await reader.ReadLineAsync(ct) ?? "";
                        string? header;
                        do { header = await reader.ReadLineAsync(ct); }
                        while (!string.IsNullOrEmpty(header));
                        var path = first.Split(' ').ElementAtOrDefault(1) ?? "/";
                        var status = "200 OK";
                        var extra = "";
                        string body;
                        switch (path)
                        {
                            case "/list":
                                body = "<!doctype html><title>Customers</title><h1>Customers</h1><a href='/form'>Customer 42</a>";
                                break;
                            case "/redirect-list":
                                body = "<!doctype html><title>Redirect list</title><a href='/redirect-go'>Customer 42</a>";
                                break;
                            case "/redirect-go":
                                status = "302 Found"; extra = "Location: /form\r\n"; body = "";
                                break;
                            case "/form":
                                body = "<!doctype html><title>Customer</title><label for='customer'>Customer name</label><input id='customer' type='text' value='Alice'>";
                                break;
                            case "/delayed-list":
                                body = "<!doctype html><title>Delayed</title><a href='/delayed-form'>Customer 42</a>";
                                break;
                            case "/delayed-form":
                                body = "<!doctype html><title>Delayed form</title><main id='root'></main><script>setTimeout(()=>{document.querySelector('#root').innerHTML=\"<label for='customer'>Customer name</label><input id='customer' type='text'>\"},350)</script>";
                                break;
                            case "/external":
                                body = "<!doctype html><a href='https://example.com/'>Customer 42</a>";
                                break;
                            default:
                                status = "404 Not Found"; body = "<!doctype html>missing";
                                break;
                        }
                        var bytes = Encoding.UTF8.GetBytes(body);
                        var response = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n" + extra + "\r\n");
                        await stream.WriteAsync(response, ct);
                        await stream.WriteAsync(bytes, ct);
                    }
                    catch (IOException) { }
                    catch (OperationCanceledException) { }
                }
            }, ct);
        }
    }
    catch (OperationCanceledException) { }
}

static async Task<string> WaitForDebuggerAsync(int port)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(1200) };
    for (int i = 0; i < 80; i++)
    {
        try
        {
            var json = JsonNode.Parse(await http.GetStringAsync("http://127.0.0.1:" + port + "/json/version"));
            var url = json?["webSocketDebuggerUrl"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(url)) return url;
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        await Task.Delay(150);
    }
    throw new InvalidOperationException("CDP_DEBUGGER_NOT_AVAILABLE");
}

static async Task WaitForPathAsync(CdpSession session, string expected)
{
    for (int i = 0; i < 70; i++)
    {
        try
        {
            var path = (await session.EvaluateAsync("location.pathname", ct: default))?.ToString();
            if (path == expected) return;
        }
        catch (CdpException) { }
        await Task.Delay(100);
    }
    throw new InvalidOperationException("CDP_NAVIGATION_TIMEOUT: " + expected);
}

var edge = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
Check(File.Exists(edge), "edge_missing");
var profile = Path.Combine(Path.GetTempPath(), "of_archflow_cdp_" + Guid.NewGuid().ToString("N"));
var fixture = new TcpListener(IPAddress.Loopback, 0);
fixture.Start();
using var stop = new CancellationTokenSource();
var serveTask = ServeAsync(fixture, stop.Token);
var localPort = ((IPEndPoint)fixture.LocalEndpoint).Port;
var cdpPort = FreePort();
var origin = "http://127.0.0.1:" + localPort;
var psi = new ProcessStartInfo(edge)
{
    Arguments = "--headless=new --disable-gpu --no-first-run --no-default-browser-check --disable-extensions --user-data-dir=\"" + profile + "\" --remote-debugging-port=" + cdpPort + " about:blank",
    UseShellExecute = false,
    CreateNoWindow = true
};
using var chrome = Process.Start(psi) ?? throw new InvalidOperationException("EDGE_PROCESS_NOT_STARTED");
try
{
    var websocket = await WaitForDebuggerAsync(cdpPort);
    await using var connection = new WebSocketCdpConnection();
    await connection.ConnectAsync(websocket);
    var session = new CdpSession(connection);
    var target = await session.SendAsync("Target.createTarget", new JsonObject { ["url"] = origin + "/list" });
    var targetId = target?["targetId"]?.GetValue<string>() ?? throw new Exception("CDP_TARGET_CREATE_FAILED");
    var attach = await session.SendAsync("Target.attachToTarget", new JsonObject { ["targetId"] = targetId, ["flatten"] = true });
    session.SessionId = attach?["sessionId"]?.GetValue<string>() ?? throw new Exception("CDP_TARGET_ATTACH_FAILED");
    await WaitForPathAsync(session, "/list");

    var adapter = new LocalPlanCdpBrowser(session);
    var first = await adapter.ReadAsync(default);
    Check(first.Host == "127.0.0.1" && first.Elements.Any(e => e.Role == "link" && e.Name == "Customer 42"), "real_dom_snapshot");
    var same = await adapter.ReadAsync(default);
    Check(same.Revision == first.Revision, "identical_reads_must_not_advance_revision");

    await session.EvaluateAsync("document.querySelector('a').textContent = 'Customer 43'");
    var mutated = await adapter.ReadAsync(default);
    Check(mutated.Revision > first.Revision, "dom_change_must_advance_revision");
    var oldRef = first.Elements.First(e => e.Role == "link").Ref;
    var stale = await adapter.ClickNavigationAsync(oldRef, first.Revision, default);
    Check(stale.Status == "stale", "old_revision_rejected");
    Check((await session.EvaluateAsync("location.pathname"))?.ToString() == "/list", "stale_must_not_navigate");
    await session.EvaluateAsync("document.querySelector('a').textContent = 'Customer 42'");
    var beforeRace = await adapter.ReadAsync(default);
    await session.EvaluateAsync("document.querySelector('a').textContent = 'Customer 44'");
    var race = await adapter.ClickNavigationAsync(beforeRace.Elements.First(e => e.Role == "link").Ref, beforeRace.Revision, default);
    Check(race.Status == "stale", "atomic_cdp_dom_race_rejected");
    await session.EvaluateAsync("document.querySelector('a').textContent = 'Customer 42'");

    static ValidatedSemanticPlan Plan() => PlanProtocolValidator.Parse(JsonNode.Parse("""
    {"protocol":"of.plan.v1","expected_host":"127.0.0.1",
     "nodes":[
       {"id":"locate","op":"locate","role":"link","name":"Customer 42","bind":"target"},
       {"id":"navigate","op":"click","ref":"$target","effect":"navigation"},
       {"id":"verify","op":"assert_visible","role":"textbox","name":"Customer name"}
     ]}
    """)!.AsObject());

    var executor = new LocalPlanExecutor();
    var result = await executor.ExecuteAsync(Plan(), adapter);
    if (result.Status != "completed")
        Console.Error.WriteLine("CDP_DIAG_AFTER_NAV=" + (await session.EvaluateAsync("JSON.stringify({url:location.href,ready:document.readyState,body:document.body?.innerHTML})"))?.ToString());
    Check(result.Status == "completed", "list_to_form_" + result.Status + "_" + result.Reason);
    Check(result.Nodes.Count == 3 && result.Nodes[2].Status == "observed", "postcondition");
    await WaitForPathAsync(session, "/form");

    await session.SendAsync("Page.navigate", new JsonObject { ["url"] = origin + "/redirect-list" });
    await WaitForPathAsync(session, "/redirect-list");
    var redirected = await executor.ExecuteAsync(Plan(), adapter);
    Check(redirected.Status == "completed", "http_redirect_" + redirected.Status + "_" + redirected.Reason);
    await WaitForPathAsync(session, "/form");

    // A late-rendering SPA-style form must be observed locally, without
    // asking the model again or replaying the navigation.
    await session.SendAsync("Page.navigate", new JsonObject { ["url"] = origin + "/delayed-list" });
    await WaitForPathAsync(session, "/delayed-list");
    var waitedPlan = PlanProtocolValidator.Parse(JsonNode.Parse("""
    {"protocol":"of.plan.v1","expected_host":"127.0.0.1",
     "nodes":[
       {"id":"locate","op":"locate","role":"link","name":"Customer 42","bind":"row"},
       {"id":"click","op":"click","ref":"$row","effect":"navigation"},
       {"id":"wait","op":"wait_until","role":"textbox","name":"Customer name","timeout_ms":2000}
     ]}
    """)!.AsObject());
    var waited = await executor.ExecuteAsync(waitedPlan, adapter);
    Check(waited.Status == "completed", "delayed_form_" + waited.Status + "_" + waited.Reason);
    Check(waited.Nodes.Count == 3 && waited.Nodes[2].Status == "observed", "delayed_form_observed");
    await WaitForPathAsync(session, "/delayed-form");

    var fillPlan = PlanProtocolValidator.Parse(JsonNode.Parse("""
    {"protocol":"of.plan.v1","expected_host":"127.0.0.1",
     "nodes":[
       {"id":"locate","op":"locate","role":"textbox","name":"Customer name","bind":"field"},
       {"id":"fill","op":"fill","ref":"$field","value":"Charlie"},
       {"id":"verify","op":"assert_value","ref":"$field","value":"Charlie"}
     ]}
    """)!.AsObject());
    var filled = await executor.ExecuteAsync(fillPlan, adapter);
    Check(filled.Status == "completed", "fill_form_" + filled.Status + "_" + filled.Reason);
    Check(filled.Nodes[^1].Status == "observed", "filled_value_observed");
    Check((await session.EvaluateAsync("document.querySelector('input').value"))?.ToString() == "Charlie",
        "filled_dom_value");

    var beforeStaleFill = await adapter.ReadAsync(default);
    await session.EvaluateAsync("document.querySelector('input').value = 'Other'");
    var staleFill = await adapter.FillTextAsync(
        beforeStaleFill.Elements.Single(e => e.Role == "textbox").Ref,
        beforeStaleFill.Revision, "Unwanted", default);
    Check(staleFill.Status == "stale", "fill_atomic_stale_check");
    Check((await session.EvaluateAsync("document.querySelector('input').value"))?.ToString() == "Other",
        "stale_fill_did_not_overwrite");

    await session.SendAsync("Page.navigate", new JsonObject { ["url"] = origin + "/external" });
    await WaitForPathAsync(session, "/external");
    var external = await executor.ExecuteAsync(Plan(), adapter);
    Check(external.Status == "needs_replan" && external.Reason == "action_failed_before_action", "external_link_rejected");
    Check((await session.EvaluateAsync("location.pathname"))?.ToString() == "/external", "no_external_navigation");
    Console.WriteLine("ARCHFLOW_CDP_REAL_BROWSER_19_CHECKS_OK");
}
finally
{
    stop.Cancel();
    fixture.Stop();
    try { if (!chrome.HasExited) chrome.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
    try { await chrome.WaitForExitAsync(); } catch { }
    try { await serveTask; } catch (SocketException) { }
    for (var cleanupTry = 0; cleanupTry < 3; cleanupTry++)
    {
        try { Directory.Delete(profile, true); break; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Edge child processes may briefly retain Chromium profile handles.
            if (cleanupTry == 2) Console.Error.WriteLine("CDP_PROFILE_CLEANUP_DEFERRED: " + ex.Message);
            else await Task.Delay(200);
        }
    }
}
