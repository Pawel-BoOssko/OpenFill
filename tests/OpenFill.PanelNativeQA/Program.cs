using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using OpenFill.Core.Assets;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var path = Path.Combine(Path.GetTempPath(), "of_panel_native_qa_20261105.json");
        var profile = Path.Combine(Path.GetTempPath(), "of_panel_native_qa_profile_20261105");
        var report = new JsonObject { ["status"] = "running" };
        var form = new Form { Width = 900, Height = 650, Text = "OpenFill isolated native panel QA" };
        var view = new WebView2 { Dock = DockStyle.Fill };
        form.Controls.Add(view);
        form.Shown += async (_, _) =>
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, profile);
                await view.EnsureCoreWebView2Async(env);
                var received = new List<JsonObject>();
                view.CoreWebView2.WebMessageReceived += (_, evt) =>
                {
                    try
                    {
                        var row = JsonNode.Parse(evt.WebMessageAsJson) as JsonObject;
                        if (row is not null) received.Add(row);
                    }
                    catch { }
                };
                var ready = new TaskCompletionSource<bool>();
                view.CoreWebView2.NavigationCompleted += (_, e) => ready.TrySetResult(e.IsSuccess);
                view.CoreWebView2.NavigateToString(AssetLoader.Load("panel.html"));
                if (!await ready.Task.WaitAsync(TimeSpan.FromSeconds(20))) throw new Exception("panel_load_failed");

                async Task Until(Func<bool> predicate, string name)
                {
                    for (var i = 0; i < 70; i++)
                    {
                        if (predicate()) return;
                        await Task.Delay(100);
                    }
                    throw new Exception(name);
                }
                async Task Js(string script) => _ = await view.CoreWebView2.ExecuteScriptAsync(script);
                var state = new { kind = "state", runs = new [] { new { n = 1, key = "OF-NATIVE-QA-STOP", task = "Fixture" } }, max = 1 };
                view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(state));
                await Task.Delay(300);
                await Js("document.getElementById('stop').click()");
                await Until(() => received.Any(x => x["kind"]?.ToString() == "stop"), "stop_not_received");
                var stop = received.Last(x => x["kind"]?.ToString() == "stop");
                report["stop_message"] = stop.DeepClone();
                report["stop_pass"] = stop["kind"]?.ToString() == "stop";

                view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new {
                    kind = "ask", id = "native-ask-1", question = "What is your answer?", options = Array.Empty<string>()
                }));
                await Task.Delay(300);
                await Js("""
                    (() => {
                      const c=document.querySelector('[data-card="native-ask-1"]');
                      if(!c)throw Error('card_missing');
                      c.querySelector('input').value='Answer';
                      c.querySelector('textarea.note').value='Note';
                      c.querySelector('textarea.note').focus();
                    })()
                    """);
                // Real Chromium keyboard dispatch against the WebView2 panel.
                async Task Key(string type, int modifiers = 0)
                {
                    var ev = new JsonObject {
                        ["type"] = type, ["key"] = "Enter", ["code"] = "Enter",
                        ["windowsVirtualKeyCode"] = 13, ["nativeVirtualKeyCode"] = 13,
                        ["modifiers"] = modifiers
                    };
                    if (type == "char") {
                        ev["text"] = "\r";
                        ev["unmodifiedText"] = "\r";
                    }
                    await view.CoreWebView2.CallDevToolsProtocolMethodAsync(
                        "Input.dispatchKeyEvent", ev.ToJsonString());
                }
                await Key("keyDown"); await Key("keyUp");
                await Until(() => received.Any(x => x["kind"]?.ToString() == "answer"), "enter_not_received");
                var answers = received.Where(x => x["kind"]?.ToString() == "answer").ToList();
                report["enter_message"] = answers[0].DeepClone();
                report["enter_pass"] = answers.Count == 1 &&
                    answers[0]["id"]?.ToString() == "native-ask-1" &&
                    answers[0]["text"]?.ToString() == "Answer\n\nMessage from the person: Note";

                await Key("keyDown"); await Key("keyUp");
                await Task.Delay(200);
                report["duplicate_pass"] = received.Count(x => x["kind"]?.ToString() == "answer") == 1;

                view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new {
                    kind = "ask", id = "native-ask-2", question = "Second?", options = Array.Empty<string>()
                }));
                await Task.Delay(200);
                await Js("""
                    (() => {
                      const c=document.querySelector('[data-card="native-ask-2"]');
                      c.querySelector('textarea.note').value='First';
                      c.querySelector('textarea.note').focus();
                    })()
                    """);
                await Key("keyDown",8); await Key("char",8); await Key("keyUp",8);
                await Task.Delay(250);
                report["shift_enter_no_submit"] = received.Count(x => x["kind"]?.ToString() == "answer") == 1;
                var value = await view.CoreWebView2.ExecuteScriptAsync(
                    "document.querySelector('[data-card=\\\"native-ask-2\\\"] textarea.note').value");
                report["shift_enter_value"] = JsonSerializer.Deserialize<string>(value) ?? "";
                report["shift_enter_newline"] = (report["shift_enter_value"]?.ToString() ?? "").Contains('\n');
                report["status"] = report["stop_pass"]?.ToString() == "true" &&
                    report["enter_pass"]?.ToString() == "true" &&
                    report["duplicate_pass"]?.ToString() == "true" &&
                    report["shift_enter_no_submit"]?.ToString() == "true" &&
                    report["shift_enter_newline"]?.ToString() == "true"
                    ? "pass" : "partial";
            }
            catch (Exception e)
            {
                report["status"] = "failed";
                report["error"] = e.ToString();
            }
            finally
            {
                File.WriteAllText(path, report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                form.Close();
            }
        };
        Application.Run(form);
    }
}