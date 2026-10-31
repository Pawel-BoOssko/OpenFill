// OpenFill - Metadata: version 0.1, 2026-10-29 23:20
using OpenFill.App;
using OpenFill.Cli;
using OpenFill.Core.Browser;
using OpenFill.Core.Cdp;
using OpenFill.Core.Hosting;
using OpenFill.Core.Logging;
namespace OpenFill.Tests;

public static class WhatsAppTests
{
    public static async Task RunAsync(string siteDir, Func<string,string> page)
    {
        await T.Section("WhatsApp DOM fast path: phone, previews, messages, batch",async() =>
        {
            T.Check("WhatsApp URL recognized", WhatsAppBrowser.IsWhatsAppUrl("https://web.whatsapp.com/send?phone=48123456789"));
            T.Check("other site not WhatsApp", !WhatsAppBrowser.IsWhatsAppUrl("https://fake-whatsapp.example/"));
            T.Eq("direct phone URL","https://web.whatsapp.com/send?phone=48123456789",WhatsAppBrowser.DirectUrl("+48123456789"));
            T.Eq("phone JID maps to direct route","https://web.whatsapp.com/send?phone=48123456789",WhatsAppBrowser.DirectUrl("48123456789@c.us"));
            T.Eq("whatsapp.net phone JID maps to direct route","https://web.whatsapp.com/send?phone=48123456789",WhatsAppBrowser.DirectUrl("48123456789@s.whatsapp.net"));
            T.Check("name never triggers list search", WhatsAppBrowser.DirectUrl("Anna Test") is null);
            T.Check("opaque unsupported identifier not guessed", WhatsAppBrowser.DirectUrl("12345@g.us") is null);
            var oldView = System.Text.Json.Nodes.JsonNode.Parse("""{"messages":[{"id":"a","text":"Earlier message","direction":"out"}]}""");
            var newView = System.Text.Json.Nodes.JsonNode.Parse("""{"messages":[{"id":"a","text":"Earlier message","direction":"out"},{"id":"b","text":"Diagnostic test  sent 08:04","direction":"out"}]}""");
            T.Check("new outgoing bubble is confirmed by text and identity", WhatsAppBrowser.HasNewOutgoingMessage(oldView,newView,"Diagnostic test sent"));
            T.Check("previous bubble never confirms a new send", !WhatsAppBrowser.HasNewOutgoingMessage(oldView,oldView,"Earlier message"));
            T.Check("missing bubble never confirms a send", !WhatsAppBrowser.HasNewOutgoingMessage(oldView,newView,"Unseen message"));
            var sameTextOld = System.Text.Json.Nodes.JsonNode.Parse("""{"messages":[{"id":"a","text":"Repeated test","direction":"out"}]}""");
            var sameTextNew = System.Text.Json.Nodes.JsonNode.Parse("""{"messages":[{"id":"b","text":"Repeated test","direction":"out"}]}""");
            T.Check("new identity confirms identical repeat in virtualized list", WhatsAppBrowser.HasNewOutgoingMessage(sameTextOld,sameTextNew,"Repeated test"));
            T.Check("same identity does not falsely confirm repeat", !WhatsAppBrowser.HasNewOutgoingMessage(sameTextOld,sameTextOld,"Repeated test"));
            using var site=new StaticSite(siteDir);
            using var chromium=new ChromiumLauncher();
            var ws=await chromium.LaunchAsync(Path.Combine(Path.GetTempPath(),"openfill_wa_test_"+Guid.NewGuid().ToString("N")[..6]),headless:true);
            var conn=new WebSocketCdpConnection();
            await conn.ConnectAsync(ws);
            try
            {
                var cdp=await CdpBootstrap.AttachPageAsync(conn,"about:blank");
                var paths=new OpenFill.Core.Config.AppPaths("test",Path.Combine(Path.GetTempPath(),"wa_test_root_"+Guid.NewGuid().ToString("N")[..6]));
                paths.EnsureCreated();
                using var log=new EventLog(paths.Logs,paths.Runs);
                log.BeginRun("wa_dom_test");
                var browser=new BrowserController(cdp,log);
                await browser.InitAsync();
                await browser.NavigateAsync(site.BaseUrl+page("whatsapp_mock"));
                var wa=new WhatsAppBrowser(browser);
                var view=await wa.ReadVisibleAsync(default);
                T.Eq("four rendered chat previews",4,view?["preview_count"]?.GetValue<int>()??-1);
                T.Check("last text appears",view?.ToJsonString().Contains("Do zobaczenia")==true);
                T.Check("read receipt meaning appears",view?.ToJsonString().Contains("Odczytano")==true);
                T.Check("two loaded messages only",view?["messages"]?.AsArray().Count==2);
                T.Eq("nested outgoing DOM is classified out", "out", view?["messages"]?[1]?["direction"]?.GetValue<string>());
                T.Eq("outgoing message DOM id preserved", "true_msg2", view?["messages"]?[1]?["id"]?.GetValue<string>());
                T.Check("composer visible",view?["composer_visible"]?.GetValue<bool>()==true);
                await browser.RunJsAsync("(() => { const p=document.querySelector('#pane-side'); p.style.display='none'; setTimeout(()=>p.style.display='',950); return true; })()");
                var premature=await wa.ReadVisibleAsync(default);
                T.Eq("premature list has zero rendered previews",0,premature?["preview_count"]?.GetValue<int>()??-1);
                var waitTimer=System.Diagnostics.Stopwatch.StartNew();
                var ready=await wa.ReadReadyAsync(default,3500);
                T.Eq("delayed list becomes available for first model turn",4,ready?["preview_count"]?.GetValue<int>()??-1);
                T.Check("readiness avoids the premature snapshot",waitTimer.ElapsedMilliseconds>=650);
                var all=await wa.ReadAllPreviewsAsync(default);
                T.Check("whole preview list read in one tool operation",all?["complete"]?.GetValue<bool>()==true);
                T.Eq("all previews preserved",4,all?["preview_count"]?.GetValue<int>()??-1);
                T.Eq("no conversation opened during list",0,all?["opened_conversations"]?.GetValue<int>()??-1);
                await browser.RunJsAsync("(() => { const p=document.querySelector('#pane-side'); p.style.display='none'; setTimeout(()=>p.style.display='',850); return true; })()");
                var delayedAll=await wa.ReadAllPreviewsAsync(default);
                T.Eq("full list waits until DOM panel is rendered",4,delayedAll?["preview_count"]?.GetValue<int>()??-1);
                T.Check("full list succeeds after deferred rendering",delayedAll?["complete"]?.GetValue<bool>()==true);
                var noSend=await wa.OpenAsync("Anna Test",default);
                T.Eq("unsupported target does not search", "unsupported_identifier_for_direct_open", noSend?["error"]?.GetValue<string>());
            }
            finally {await conn.DisposeAsync();}
        });
    }
}
