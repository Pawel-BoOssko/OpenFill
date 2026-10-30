from pathlib import Path
import shutil
r=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
backup=r/"backups"/"auth_gate_source_20261030"
backup.mkdir(exist_ok=True)
p=r/"src/OpenFill.Core/Agent/PageCaptureService__v0.1__2026-10-30.cs"
s=p.read_text(encoding="utf-8-sig")
shutil.copy2(p,backup/(p.name+".prior_dom_reorder"))
old='''          if (!root) return { captureError: "SELECTOR_NOT_FOUND", selector: sel };'''
new='''          // Root check is AFTER authentication metadata; login pages may not have the requested selector.'''
assert s.count(old)==1
s=s.replace(old,new,1)
old='''            selector: sel, authenticatedMarker: authMarker, loginForm: signInForm };
          if (mode === "all" || mode === "text")'''
new='''            selector: sel, authenticatedMarker: authMarker, loginForm: signInForm };
          if (!root) return { ...data, captureError: "SELECTOR_NOT_FOUND" };
          if (mode === "all" || mode === "text")'''
assert s.count(old)==1
s=s.replace(old,new,1)
old='''        if (payload["captureError"] is not null) throw new InvalidOperationException(payload["captureError"]!.ToString() + ": " + selector);
        // Gate before creating any export directory/file and before recording a recipe success.
        var auth = PageAuthenticationGate.Check(payload, expectedUrl, requireAuthenticated, allowLoginPage);
        if (auth["status"]?.ToString() != "ready") return auth;
        var folder = ResolveOutputFolder(outputFolder);'''
new='''        // Authentication is checked before selector failure and before writing even one file.
        var auth = PageAuthenticationGate.Check(payload, expectedUrl, requireAuthenticated, allowLoginPage);
        if (auth["status"]?.ToString() != "ready") return auth;
        if (payload["captureError"] is not null) throw new InvalidOperationException(payload["captureError"]!.ToString() + ": " + selector);
        var folder = ResolveOutputFolder(outputFolder);'''
assert s.count(old)==1
s=s.replace(old,new,1)
p.write_text(s,encoding="utf-8")
print("PAGE_AUTH_GATE_EARLY_CHECK_PATCHED")

p=r/"tests/OpenFill.Tests/CapturePipelineTests__v0.1__2026-10-30.cs"
s=p.read_text(encoding="utf-8-sig")
shutil.copy2(p,backup/(p.name+".before_auth_tests"))
anchor='''        await T.Section("Capture recipes: verified methods, scope and reuse", () =>'''
snippet='''        await T.Section("Authentication-first: signed-out LinkedIn never counts as export", () =>
        {
            var redirect = new JsonObject { ["url"]="https://www.linkedin.com/login/?session_redirect=https%3A%2F%2Fwww.linkedin.com%2Fmessaging%2F",
                ["loginForm"]=true, ["authenticatedMarker"]=false, ["captureError"]="SELECTOR_NOT_FOUND" };
            var refusal = PageAuthenticationGate.Check(redirect, expectedUrl:"https://www.linkedin.com/messaging/");
            T.Eq("login redirect reports LOGIN_FIRST", "authentication_required", refusal["status"]!.ToString());
            T.Eq("login redirect exact code", "LOGIN_FIRST", refusal["code"]!.ToString());
            T.Check("login screen not a successful export", refusal["files"] is null);
            var accepted = new JsonObject { ["url"]="https://www.linkedin.com/messaging/",
                ["loginForm"]=false, ["authenticatedMarker"]=true };
            var ready = PageAuthenticationGate.Check(accepted, expectedUrl:"https://www.linkedin.com/messaging/");
            T.Eq("verified authenticated LinkedIn permits capture", "ready", ready["status"]!.ToString());
            var unverified = PageAuthenticationGate.Check(new JsonObject { ["url"]="https://www.linkedin.com/messaging/",
                ["loginForm"]=false, ["authenticatedMarker"]=false });
            T.Eq("missing proof is not success", "authentication_unverified", unverified["status"]!.ToString());
            var unrelated = PageAuthenticationGate.Check(new JsonObject { ["url"]="https://example.org/landing",
                ["loginForm"]=false, ["authenticatedMarker"]=false });
            T.Eq("public page does not require login", "ready", unrelated["status"]!.ToString());
            return Task.CompletedTask;
        });
        await T.Section("Capture recipes: verified methods, scope and reuse", () =>'''
assert s.count(anchor)==1
s=s.replace(anchor,snippet,1)
anchor='''                T.Eq("CDP capture status saved", "saved", result["status"]!.ToString());'''
test='''                // The same actual CDP page is rejected without positive login evidence,
                // then accepted after a verified session marker appears.
                var protectedFolder = Path.Combine(root,"after-login");
                var refused = await capture.CaptureAsync("all", "main", "email", protectedFolder, 20,
                    CancellationToken.None, requireAuthenticated:true, authenticatedSelector:"html[data-qa-auth='yes']");
                T.Eq("unverified login blocks physical DOM export", "authentication_unverified", refused["status"]!.ToString());
                T.Check("no output directory created before authentication", !Directory.Exists(protectedFolder));
                var loggedIn = await browser.RunJsAsync("(document.documentElement.setAttribute('data-qa-auth','yes'), true)");
                T.Check("test page simulated successful login", loggedIn?["ok"]?.GetValue<bool>() == true);
                var afterLogin = await capture.CaptureAsync("all", "body", "email", protectedFolder, 20,
                    CancellationToken.None, requireAuthenticated:true, authenticatedSelector:"html[data-qa-auth='yes']");
                T.Eq("verified login allows physical capture", "saved", afterLogin["status"]!.ToString());
                T.Eq("verified physical capture four files", 4, afterLogin["files"]!.AsArray().Count);
                T.Eq("CDP capture status saved", "saved", result["status"]!.ToString());'''
assert s.count(anchor)==1
s=s.replace(anchor,test,1)
p.write_text(s,encoding="utf-8")
print("AUTH_GATE_TESTS_ADDED")
