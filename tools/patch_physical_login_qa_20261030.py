from pathlib import Path
import shutil
r=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
p=r/"tests/OpenFill.WebView2QA/Program.cs"
backup=r/"backups/auth_gate_source_20261030"
backup.mkdir(exist_ok=True)
s=p.read_text(encoding="utf-8-sig")
if "QA_LOGIN_FIRST_E2E_20261030" in s:print("ALREADY");raise SystemExit(0)
shutil.copy2(p,backup/(p.name+".before_login_physical"))
old='''                    var html = "<!doctype html><html><head><title>QA LinkedIn-like fixture</title></head><body>" +'''
new='''                    // QA_LOGIN_FIRST_E2E_20261030: a cookie-gated private site and a real HTML login form.
                    var route = ctx.Request.Url?.AbsolutePath ?? "/";
                    if (route == "/session" && ctx.Request.HttpMethod == "POST")
                    {
                        using var body = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                        var posted = await body.ReadToEndAsync();
                        if (posted.Contains("user=qa", StringComparison.Ordinal)
                            && posted.Contains("pass=qa123", StringComparison.Ordinal))
                        {
                            ctx.Response.SetCookie(new Cookie("ofqa", "passed") { Path="/" });
                            ctx.Response.Redirect(url + "secure");
                        }
                        else ctx.Response.StatusCode = 403;
                        ctx.Response.Close();
                        continue;
                    }
                    if (route == "/secure" && ctx.Request.Cookies["ofqa"]?.Value != "passed")
                    {
                        ctx.Response.Redirect(url + "login");
                        ctx.Response.Close();
                        continue;
                    }
                    if (route == "/login")
                    {
                        var formHtml = "<html><body><h1>Login required</h1><form method='POST' action='/session'>" +
                            "<input name='user' id='qa-user'><input name='pass' type='password' id='qa-pass'>" +
                            "<button id='qa-submit' type='submit'>Sign in</button></form></body></html>";
                        var loginBytes=Encoding.UTF8.GetBytes(formHtml);
                        ctx.Response.ContentType="text/html; charset=utf-8";
                        ctx.Response.ContentLength64=loginBytes.Length;
                        await ctx.Response.OutputStream.WriteAsync(loginBytes);
                        ctx.Response.Close();
                        continue;
                    }
                    var html = "<!doctype html><html><head><title>QA LinkedIn-like fixture</title></head><body>" +'''
assert s.count(old)==1, "server HTML anchor missing"
s=s.replace(old,new,1)
old='''                report["elapsed_ms"]=sw.ElapsedMilliseconds;'''
new='''                // Real browser login -> session cookie -> protected DOM capture. No Luna or external service.
                web.Navigate(url + "secure");
                await Wait(()=>web.Source?.EndsWith("/login",StringComparison.Ordinal)==true,
                    "Protected QA URL did not redirect to login");
                var beforeLogin = await manager.PageSnapshotAsync(taskId, "ChatGPT", "QA",
                    url+"secure", "all", "body", "speaker", null, 20, CancellationToken.None);
                report["before_login_status"]=beforeLogin["status"]?.ToString();
                report["before_login_files"]=beforeLogin["files"]?.AsArray().Count ?? 0;
                await web.ExecuteScriptAsync("""
                (() => { document.querySelector('#qa-user').value='qa';
                         document.querySelector('#qa-pass').value='qa123';
                         document.querySelector('#qa-submit').click(); return true; })()
                """);
                await Wait(()=>string.Equals(web.Source?.TrimEnd('/'),(url+"secure").TrimEnd('/'),StringComparison.OrdinalIgnoreCase),
                    "After login, protected QA page did not open");
                var afterLogin = await manager.PageSnapshotAsync(taskId, "ChatGPT", "QA",
                    url+"secure", "all", "body", "speaker", null, 20, CancellationToken.None);
                report["after_login_status"]=afterLogin["status"]?.ToString();
                report["after_login_files"]=afterLogin["files"]?.AsArray().Count ?? 0;
                report["auth_pipeline_pass"]=beforeLogin["status"]?.ToString()=="authentication_required"
                    && beforeLogin["files"]==null
                    && afterLogin["status"]?.ToString()=="saved"
                    && afterLogin["files"]?.AsArray().Count==4;
                report["elapsed_ms"]=sw.ElapsedMilliseconds;'''
assert s.count(old)==1, "report anchor missing"
s=s.replace(old,new,1)
old='''                report["status"]=report["direct_mcp_success"]?.GetValue<bool>()==true'''
new='''                report["status"]=report["auth_pipeline_pass"]?.GetValue<bool>()==true
                    && report["direct_mcp_success"]?.GetValue<bool>()==true'''
assert s.count(old)==1, "status anchor missing"
s=s.replace(old,new,1)
p.write_text(s,encoding="utf-8")
print("LOCAL_COOKIE_LOGIN_PHYSICAL_QA_ADDED")
