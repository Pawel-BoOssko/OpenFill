using System.Text.Json.Nodes;
using OpenFill.Cli;
using OpenFill.Core.Agent;
using OpenFill.Core.Browser;
using OpenFill.Core.Cdp;
using OpenFill.Core.Config;
using OpenFill.Core.Hosting;
using OpenFill.Core.Logging;

namespace OpenFill.Tests;

// One focused acceptance: eight distinct local recipient pages and eight
// verified invitation submissions using a single, site-independent act_many.
public static class SemanticBatchNavigationTests
{
    public static async Task RunAsync(string fixtureDir)
    {
        await T.Section("A21: eight verified recipients, one multi-page act_many", async () =>
        {
            var names = new [] {
                "Alex Morgan 1", "Taylor Lee 2", "Morgan Chen 3", "Sam Wilson 4",
                "Jordan Fox 5", "Casey Kim 6", "Jamie Reed 7", "Riley Cruz 8"
            };
            using var site = new StaticSite(fixtureDir);
            using var chromium = new ChromiumLauncher();
            var ws = await chromium.LaunchAsync(
                Path.Combine(Path.GetTempPath(), "ofimp_a21_chromium_" + Guid.NewGuid().ToString("N")[..8]),
                headless: true);
            var conn = new WebSocketCdpConnection();
            await conn.ConnectAsync(ws);
            var root = Path.Combine(Path.GetTempPath(), "ofimp_a21_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(root);
            try
            {
                using var log = new EventLog(Path.Combine(root,"logs"), Path.Combine(root,"runs"));
                var cdp = await CdpBootstrap.AttachPageAsync(conn,"about:blank");
                var browser = new BrowserController(cdp,log);
                await browser.InitAsync();
                var paths = new AppPaths("a21",root);
                paths.EnsureCreated();
                var registry = new ToolRegistry(
                    browser,log,new AppConfig(),new OutputLimiter(paths.Overflow,1200),
                    new SiteNotesStore(paths.Notes,log),new HeadlessInteraction(),
                    new GapRecorder(paths.GapsFile,log),
                    files:new SharedFiles(Path.Combine(root,"shared"),Path.Combine(root,"downloads")));
                var batch = registry.Build().Single(t => t.Name == "act_many");
                var steps = new JsonArray();
                for (var i=0; i<8; i++)
                {
                    var recipient = names[i];
                    var message = "OFIMP A21 local invitation " + (i+1);
                    steps.Add(new JsonObject {
                        ["action"]="navigate",
                        ["url"]=site.BaseUrl + "invitations.html?vanityName=contact-" + (i+1).ToString("00"),
                        ["expected_host"]="127.0.0.1",
                        ["verify"]=new JsonObject {
                            ["selector"]="#recipient",["check"]="text_contains",
                            ["expected"]=recipient,["expected_host"]="127.0.0.1",
                            ["timeout_ms"]=3500
                        }
                    });
                    steps.Add(new JsonObject {
                        ["selector"]="#note",["action"]="set",["value"]=message,
                        ["expected_host"]="127.0.0.1",["timeout_ms"]=3500
                    });
                    steps.Add(new JsonObject {
                        ["selector"]="#send-invite",["action"]="click",
                        ["expected_host"]="127.0.0.1",
                        ["verify"]=new JsonObject {
                            ["selector"]="#status",["check"]="text_contains",
                            ["expected"]="Invitation sent to " + recipient,
                            ["expected_host"]="127.0.0.1",["timeout_ms"]=3500
                        }
                    });
                }
                var response = await batch.Handler(
                    new JsonObject{["steps"]=steps},CancellationToken.None);
                T.Check("batch returned ok",response.Ok);
                var parsed = JsonNode.Parse(response.Output) as JsonObject;
                T.Eq("24 local steps",24,parsed?["completed_steps"]?.GetValue<int>() ?? 0);
                T.Eq("all steps completed","completed",parsed?["status"]?.ToString());
                var finalCount = await browser.RunJsAsync("document.querySelector('#invite-total')?.textContent");
                T.Eq("eight local records committed","8",finalCount?["value"]?.ToString());
                T.Check("no automatic repeat flag",
                    parsed?["repeat_action_automatically"]?.GetValue<bool>() == false);
                // A verification result must exist for all eight navigations
                // and all eight submit actions, independently.
                var verified = parsed?["results"]?.AsArray()
                    .Count(x => x?["result"]?["verification_status"]?.ToString() == "observed") ?? 0;
                T.Eq("sixteen separate postconditions",16,verified);
            }
            finally
            {
                await conn.DisposeAsync();
                try {Directory.Delete(root,true);} catch {}
            }
        });
    }
}
