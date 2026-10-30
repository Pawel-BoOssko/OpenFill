// OpenFill isolated physical WebView2 QA, 2026-10-30. Never touches stable instance.
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using OpenFill.App;
using OpenFill.Core.Agent;
using OpenFill.Core.Config;
using OpenFill.Core.Hosting;
using OpenFill.Core.Mcp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var root = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "of_webview2_qa_" + Guid.NewGuid().ToString("N")[..7]);
        Directory.CreateDirectory(root);
        var resultPath = Path.Combine(root, "physical-qa-result.json");
        var report = new JsonObject { ["started_utc"] = DateTime.UtcNow.ToString("O"), ["status"] = "running" };
        var listener = new HttpListener();
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start(); var port = ((IPEndPoint)tcp.LocalEndpoint).Port; tcp.Stop();
        var url = "http://127.0.0.1:" + port + "/";
        listener.Prefixes.Add(url);
        listener.Start();
        var server = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); } catch { break; }
                try
                {
                    var html = "<!doctype html><html><head><title>QA LinkedIn-like fixture</title></head><body>" +
                        "<header>Menu and noise</header><main id='content'><h1>Conference invitation</h1>" +
                        "<section>Speaker: Alice Example</section><section>Speaker: Bob Example</section>" +
                        "<a href='/contact'>Contact speaker</a><button id='export'>Export</button>" +
                        "</main><footer>Footer, privacy, navigation noise</footer></body></html>";
                    var bytes = Encoding.UTF8.GetBytes(html);
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    ctx.Response.ContentLength64 = bytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(bytes);
                    ctx.Response.Close();
                }
                catch { try { ctx.Response.Abort(); } catch { } }
            }
        });
        var paths = new AppPaths("qa_webview2_physical_20261030", root);
        paths.EnsureCreated();
        var config = new AppConfig { HomeUrl = url, McpEnabled = false, McpTunnel = false, MaxTabs = 3, MaxParallel = 1, SharedFolder = Path.Combine(root, "shared") };
        var form = new MainForm(paths, config, new SecretStore(paths.SecretFile));
        form.Shown += async (_, _) =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                async Task Wait(Func<bool> condition, string reason, int maxSec=25)
                {
                    for (int i=0; i<maxSec*10; i++)
                    {
                        if(condition()) return;
                        await Task.Delay(100);
                    }
                    throw new TimeoutException(reason);
                }
                var flags = BindingFlags.NonPublic|BindingFlags.Instance;
                var hf = typeof(MainForm).GetField("_host", flags)!;
                var tf = typeof(MainForm).GetField("_tabMgr", flags)!;
                var af = typeof(MainForm).GetField("_active", flags)!;
                await Wait(()=>hf.GetValue(form)!=null && tf.GetValue(form)!=null && af.GetValue(form)!=null,"OpenFill real WebView2 failed to initialize");
                var host=(AppHost)hf.GetValue(form)!;
                var tabs=(TabManager)tf.GetValue(form)!;
                var taskId="OF-QA-PHYSICAL-20261030";
                var start=await tabs.BeginAsync(taskId,"QA physical capture",null,CancellationToken.None, requester:"ChatGPT · QA");
                var active=af.GetValue(form)!;
                var view=(WebView2)active.GetType().GetProperty("View")!.GetValue(active)!;
                var web=view.CoreWebView2!;
                await Wait(()=>string.Equals(web.Source?.TrimEnd('/'),url.TrimEnd('/'),StringComparison.OrdinalIgnoreCase),"QA page did not load");
                report["qa_tab_id"]=start.TabId;
                report["page_url"]=web.Source;
                report["task_state"]=tabs.Find(taskId)?.State;
                var rec = new McpTaskRecord {
                    Id=taskId, Requester="ChatGPT · QA", TaskText="Test page capture for QA",
                    Status="done", Outcome="success", CreatedUtc=DateTime.UtcNow, UpdatedUtc=DateTime.UtcNow
                };
                File.WriteAllText(Path.Combine(paths.Tasks,taskId+".json"), JsonSerializer.Serialize(rec));
                var manager = new McpTaskManager(host, paths.Tasks,TimeSpan.FromMinutes(1));
                await using(var mcp=new McpServer(manager,"qa_physical_20261030",0))
                {
                    mcp.Start();
                    using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(15)};
                    async Task<JsonObject> Rpc(string requester, string chat)
                    {
                        var payload = new JsonObject{
                            ["jsonrpc"]="2.0",["id"]=99,["method"]="tools/call",
                            ["params"]=new JsonObject{
                                ["name"]="openfill_page_snapshot",
                                ["arguments"]=new JsonObject {
                                    ["task_id"]=taskId, ["requester"]=requester, ["chat"]=chat,
                                    ["url"]=url, ["mode"]="all", ["selector"]="main",
                                    ["regex"]="speaker|conference", ["max_matches"]=30
                                }
                            }
                        };
                        using var res=await http.PostAsync("http://127.0.0.1:"+mcp.Port+"/mcp/qa_physical_20261030",
                            new StringContent(payload.ToJsonString(),Encoding.UTF8,"application/json"));
                        res.EnsureSuccessStatusCode();
                        var response=JsonNode.Parse(await res.Content.ReadAsStringAsync())!.AsObject();
                        var output=response["result"]?["content"]?[0]?["text"]?.GetValue<string>();
                        if(output is null) throw new InvalidOperationException("MCP HTTP returned no result content: "+response);
                        return JsonNode.Parse(output)!.AsObject();
                    }
                    var unauthorized=await Rpc("Another Chat","QA");
                    report["foreign_status"]=unauthorized["status"]?.ToString();
                    report["foreign_refused"]=unauthorized["status"]?.ToString()=="error";
                    var capture=await Rpc("ChatGPT","QA");
                    report["direct_mcp_result"]=capture.DeepClone();
                    var fileRows=capture["files"]?.AsArray();
                    report["direct_mcp_files"]=fileRows?.Count ?? 0;
                    report["direct_mcp_success"]=capture["status"]?.ToString()=="saved"
                        && fileRows?.Count==4
                        && fileRows.All(x=>File.Exists(x?["path"]?.ToString()))
                        && capture["matches"]?.GetValue<int>()>0;
                }
                var permissions=new List<string>();
                var downloads=new List<string>();
                web.PermissionRequested+=(_,e)=>permissions.Add(e.PermissionKind+":"+e.State+":"+e.Handled);
                web.DownloadStarting+=(_,e)=>
                {
                    downloads.Add(Path.GetFileName(e.ResultFilePath));
                    e.DownloadOperation.StateChanged+=(_,_)=>downloads.Add(Path.GetFileName(e.ResultFilePath)+":"+e.DownloadOperation.State);
                };
                await web.ExecuteScriptAsync("""
                (() => {
                  const names = ['qa-one.txt', 'qa-two.txt', 'qa-three.txt'];
                  for(let i=0;i<names.length;i++) setTimeout(()=>{
                    const blob = new Blob(['QA export ' + names[i]],{type:'text/plain'});
                    const a=document.createElement('a');a.href=URL.createObjectURL(blob);
                    a.download=names[i];document.body.appendChild(a);a.click();
                    setTimeout(()=>{URL.revokeObjectURL(a.href);a.remove();},1200);
                  }, i*250);
                })()
                """);
                await Task.Delay(9500);
                report["permissions"]=new JsonArray(permissions.Select(x=>(JsonNode)x).ToArray());
                report["downloads_events"]=new JsonArray(downloads.Select(x=>(JsonNode)x).ToArray());
                var localFiles=Directory.Exists(paths.Downloads) ? Directory.GetFiles(paths.Downloads,"qa-*.txt") : [];
                report["download_files"]=new JsonArray(localFiles.Select(x=>(JsonNode)Path.GetFileName(x)).ToArray());
                report["three_downloads"]=localFiles.Length==3;
                report["permission_event_observed"]=permissions.Any(x=>x.StartsWith("MultipleAutomaticDownloads:"));
                report["permission_auto_approved"]=permissions.Any(x=>x.StartsWith("MultipleAutomaticDownloads:Allow:True"));
                if (args.Length > 1 && args[1] == "--linkedin")
                {
                    web.Navigate("https://www.linkedin.com/messaging/");
                    await Task.Delay(11000);
                    report["linkedin_current_url"]=web.Source;
                    var lr=await manager.PageSnapshotAsync(taskId,"ChatGPT","QA",null,
                        "all","body","message|invitation|connect|linkedin",null,30,CancellationToken.None);
                    report["linkedin_capture_status"]=lr["status"]?.ToString();
                    report["linkedin_capture_error"]=lr["message"]?.ToString();
                    report["linkedin_files"]=lr["files"]?.AsArray().Count ?? 0;
                    report["linkedin_capture_ms"]=lr["elapsed_ms"]?.DeepClone();
                    if (lr["files"] is JsonArray lf && lf.Count>0)
                    {
                        var textPath=lf[0]?["path"]?.ToString();
                        if (textPath is not null && File.Exists(textPath))
                        {
                            var bodyText=File.ReadAllText(textPath);
                            report["linkedin_text_chars"]=bodyText.Length;
                            report["linkedin_has_messaging_text"]=bodyText.Contains("Messaging",StringComparison.OrdinalIgnoreCase);
                            report["linkedin_login_text_detected"]=bodyText.Contains("Sign in",StringComparison.OrdinalIgnoreCase) ||
                                bodyText.Contains("Join now",StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
                report["elapsed_ms"]=sw.ElapsedMilliseconds;
                report["status"]=report["direct_mcp_success"]?.GetValue<bool>()==true
                    && report["foreign_refused"]?.GetValue<bool>()==true
                    && report["three_downloads"]?.GetValue<bool>()==true
                    && report["permission_auto_approved"]?.GetValue<bool>()==true ? "pass" : "partial";
            }
            catch(Exception e)
            {
                report["status"]="failed";
                report["exception"]=e.ToString();
            }
            finally
            {
                report["finished_utc"]=DateTime.UtcNow.ToString("O");
                File.WriteAllText(resultPath,report.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
                form.Close();
            }
        };
        Application.Run(form);
        try {listener.Stop();listener.Close();}catch{}
    }
}

