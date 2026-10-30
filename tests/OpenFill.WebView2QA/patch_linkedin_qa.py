from pathlib import Path
p=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\tests\OpenFill.WebView2QA\Program.cs")
s=p.read_text(encoding="utf-8-sig")
anchor='                report["elapsed_ms"]=sw.ElapsedMilliseconds;'
if s.count(anchor)!=1: raise RuntimeError("Missing insertion point")
block='''                if (args.Length > 1 && args[1] == "--linkedin")
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
'''
s=s.replace(anchor,block+anchor,1)
p.write_text(s,encoding="utf-8")
print("LINKEDIN_READONLY_QA_BRANCH_ADDED")
