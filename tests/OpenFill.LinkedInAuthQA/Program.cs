// Isolated LinkedIn authenticated QA: no changes to LIVE profile or account data.
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using OpenFill.App;
using OpenFill.Core.Config;
using OpenFill.Core.Hosting;
using OpenFill.Core.Mcp;

internal static class Program
{
[STAThread]
public static void Main()
{
ApplicationConfiguration.Initialize();
var root = @"C:\Users\pboru\OpenFill\data\qa_linkedin_auth_20261030";
Directory.CreateDirectory(root);
var reportPath = Path.Combine(root, "linkedin-auth-result.json");
var report = new JsonObject { ["started_utc"] = DateTime.UtcNow.ToString("O"), ["status"] = "running" };
var paths = new AppPaths("qa_linkedin_auth_20261030", root);
paths.EnsureCreated();
var config = new AppConfig { HomeUrl = "https://www.linkedin.com/messaging/", McpEnabled = false,
    McpTunnel = false, MaxTabs = 2, MaxParallel = 1, SharedFolder = Path.Combine(root, "shared") };
var form = new MainForm(paths, config, new SecretStore(paths.SecretFile));
form.Shown += async (_, _) =>
{
    try
    {
        async Task Wait(Func<bool> predicate, int seconds, string explanation)
        {
            for(var i = 0; i < seconds * 5; i++)
            {
                if(predicate()) return;
                await Task.Delay(200);
            }
            throw new TimeoutException(explanation);
        }
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var hostField = typeof(MainForm).GetField("_host", flags)!;
        var tabField = typeof(MainForm).GetField("_tabMgr", flags)!;
        var activeField = typeof(MainForm).GetField("_active", flags)!;
        await Wait(() => hostField.GetValue(form) != null && tabField.GetValue(form) != null,
            25, "QA MainForm did not initialize");
        var host = (AppHost)hostField.GetValue(form)!;
        var tabs = (TabManager)tabField.GetValue(form)!;
        const string taskId = "OF-QA-LINKEDIN-AUTH-20261030";
        var tab = await tabs.BeginAsync(taskId, "QA LinkedIn read-only auth", null,
            CancellationToken.None, requester:"ChatGPT · QA");
        await Wait(() => activeField.GetValue(form) != null, 15, "QA tab did not start");
        var active = activeField.GetValue(form)!;
        var view = (WebView2)active.GetType().GetProperty("View")!.GetValue(active)!;
        await Wait(() => view.CoreWebView2 != null, 20, "WebView2 is not initialized");
        var web = view.CoreWebView2!;
        web.Navigate("https://www.linkedin.com/messaging/");
        await Wait(() => web.Source != null && web.Source.StartsWith("https://www.linkedin.com/"),
            25, "LinkedIn navigation did not start");
        await Task.Delay(7500);
        report["initial_url"] = web.Source;
        report["qa_tab_id"] = tab.TabId;
        var signedIn = web.Source.Contains("/messaging", StringComparison.OrdinalIgnoreCase);
        report["initial_authenticated_url"] = signedIn;
        if (!signedIn)
        {
            var ff = FirefoxLogins.FindProfile() ?? throw new InvalidOperationException("Firefox saved logins profile unavailable");
            var logins = FirefoxLogins.List(ff).Where(x => x.Host.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.LastUsedMillis).ToList();
            report["firefox_linkedin_candidates"] = logins.Count;
            if (logins.Count == 0) throw new InvalidOperationException("No stored LinkedIn login");
            using var nss = new FirefoxNssSession(ff);
            if (nss.RequiresPrimaryPassword) throw new InvalidOperationException("Firefox primary password required; QA stopped");
            var chosen = logins[0];
            var username = nss.Decrypt(chosen.EncryptedUsername);
            var password = nss.Decrypt(chosen.EncryptedPassword);
            report["credential_source"] = "Firefox most recently used LinkedIn entry";
            report["credentials_read"] = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);
            var userJs = JsonSerializer.Serialize(username);
            var passwordJs = JsonSerializer.Serialize(password);
            var script = """
            (() => {
                const u = document.querySelector('input[name="session_key"],#username,input[autocomplete="username"]');
                const p = document.querySelector('input[name="session_password"],#password,input[type="password"]');
                if (!u || !p) return {found:false};
                const setter=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;
                setter.call(u,__USER__); setter.call(p,__PASS__);
                for(const e of [u,p]) e.dispatchEvent(new Event('input',{bubbles:true}));
                const submit = document.querySelector('button[type="submit"]');
                if(submit) submit.click(); else p.form?.requestSubmit();
                return {found:true,submitted:true};
            })()
            """.Replace("__USER__",userJs).Replace("__PASS__",passwordJs);
            var submitResult = await web.ExecuteScriptAsync(script);
            report["login_form_submitted"] = submitResult.Contains("submitted", StringComparison.Ordinal);
            username = ""; password = ""; script = "";
            if (report["login_form_submitted"]?.GetValue<bool>() != true)
                throw new InvalidOperationException("LinkedIn login form was not found in QA browser");
            await Task.Delay(12000);
            report["post_login_url"] = web.Source;
            signedIn = web.Source.Contains("/messaging",StringComparison.OrdinalIgnoreCase)
                || web.Source.Contains("/feed",StringComparison.OrdinalIgnoreCase);
        }
        if (!signedIn)
        {
            report["status"] = "authentication_required";
            report["reason"] = "LinkedIn remained on login/checkpoint page after single isolated QA attempt";
        }
        else
        {
            web.Navigate("https://www.linkedin.com/messaging/");
            await Task.Delay(9000);
            report["capture_page_url"] = web.Source;
            var rec = new McpTaskRecord { Id = taskId, Requester="ChatGPT · QA",
                TaskText="Read-only LinkedIn auth QA", Status="done", Outcome="success",
                CreatedUtc=DateTime.UtcNow, UpdatedUtc=DateTime.UtcNow };
            File.WriteAllText(Path.Combine(paths.Tasks,taskId+".json"),JsonSerializer.Serialize(rec));
            var manager = new McpTaskManager(host,paths.Tasks,TimeSpan.FromMinutes(1));
            var captured = await manager.PageSnapshotAsync(taskId,"ChatGPT","QA",
                "https://www.linkedin.com/messaging/", "all", "body",
                "message|connection|invitation|linkedin",null,30,CancellationToken.None);
            report["capture_status"] = captured["status"]?.ToString();
            report["capture_code"] = captured["code"]?.ToString();
            report["capture_file_count"] = captured["files"]?.AsArray().Count ?? 0;
            report["authentication_verified"] = captured["authentication_verified"]?.DeepClone();
            report["status"] = captured["status"]?.ToString() == "saved"
                && captured["authentication_verified"]?.GetValue<bool>() == true
                && captured["files"]?.AsArray().Count == 4 ? "pass" : "pending";
        }
    }
    catch(Exception e)
    {
        report["status"]="error";
        report["error_type"]=e.GetType().Name;
        report["error_message"]=e.Message;
    }
    finally
    {
        report["finished_utc"]=DateTime.UtcNow.ToString("O");
        File.WriteAllText(reportPath,report.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
        form.Close();
    }
};
Application.Run(form);
}
}
