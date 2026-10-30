from pathlib import Path
import shutil
r=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
bk=r/"backups"/"source_capture_20261030"
def patch(rel,changes):
    p=r/rel
    s=p.read_text(encoding="utf-8-sig")
    if "WEBVIEW2_PERMISSIONS_20261030" in s:print("ALREADY",rel);return
    shutil.copy2(p,bk/p.name)
    for old,new in changes:
        if s.count(old)!=1:raise RuntimeError(rel+" got "+str(s.count(old))+" for "+old[:90])
        s=s.replace(old,new,1)
    p.write_text("// WEBVIEW2_PERMISSIONS_20261030\n"+s,encoding="utf-8")
    print("PATCHED",rel)

b="src/OpenFill.Core/Browser/BrowserController__v0.10__2026-10-29_2314.cs"
patch(b,[
    ("        while (_dialogs.TryDequeue(out var d)) list.Add(d);\n        return list;",
     "        while (_dialogs.TryDequeue(out var d)) list.Add(d);\n        list.AddRange(BrowserNotificationBus.Take(TargetKey));\n        return list;")
])
m="src/OpenFill.App/MainForm__v0.27__2026-10-28_2222.cs"
method='''    /// <summary>Permission policy belongs to the owning WebView2 tab, not the model.</summary>
    private void HandlePermissionRequest(string tabId, CoreWebView2PermissionRequestedEventArgs e)
    {
        var taskTab = _tabMgr?.Open().Any(x => x.TabId == tabId &&
            (x.State == "running" || x.State == "waiting")) == true;
        var allowMultiple = taskTab && e.PermissionKind == CoreWebView2PermissionKind.MultipleAutomaticDownloads;
        if (allowMultiple)
        {
            e.State = CoreWebView2PermissionState.Allow;
            e.Handled = true;
        }
        var msg = "WebView2 permission " + e.PermissionKind + " from " + e.Uri +
            " (userInitiated=" + e.IsUserInitiated + ", decision=" + (allowMultiple ? "allow_task_downloads" : "default") + ")";
        _host?.Session.Log.Write("browser", "permission", msg, status: allowMultiple ? "ok" : "warn");
        OpenFill.Core.Browser.BrowserNotificationBus.Post(tabId, msg);
    }

'''
patch(m,[
    ("    private async Task OpenPopupAsync(CoreWebView2NewWindowRequestedEventArgs e,",
       method+"    private async Task OpenPopupAsync(CoreWebView2NewWindowRequestedEventArgs e,"),
    ('            pc.Settings.IsPasswordAutosaveEnabled = true;',
     '            pc.PermissionRequested += (_, p) => HandlePermissionRequest(opener.Id, p);\n            pc.Settings.IsPasswordAutosaveEnabled = true;'),
    ('        core.Settings.IsPasswordAutosaveEnabled = true;',
     '        core.PermissionRequested += (_, p) => HandlePermissionRequest(tab.Id, p);\n        core.Settings.IsPasswordAutosaveEnabled = true;'),
    ('        core.BasicAuthenticationRequested += (_, e) =>\n        {',
     '''        core.BasicAuthenticationRequested += (_, e) =>
        {
            _host?.Session.Log.Write("browser", "basic-auth-request", "HTTP Basic challenge: " + e.Uri + " realm=" + e.Challenge, status:"warn");
            OpenFill.Core.Browser.BrowserNotificationBus.Post(tab.Id, "HTTP Basic authentication requested from " + e.Uri);'''),
    ('''            _host?.Session.Log.Write("browser", "download", "Download: " + name, new { path = e.ResultFilePath });
        };''',
     '''            _host?.Session.Log.Write("browser", "download", "Download: " + name, new { path = e.ResultFilePath });
            OpenFill.Core.Browser.BrowserNotificationBus.Post(tab.Id, "Download started: " + name);
            e.DownloadOperation.StateChanged += (_, _) =>
            {
                var state = e.DownloadOperation.State.ToString();
                _host?.Session.Log.Write("browser", "download-state", name + ": " + state, status: state == "Completed" ? "ok" : "info");
                OpenFill.Core.Browser.BrowserNotificationBus.Post(tab.Id, "Download " + name + ": " + state);
            };
        };'''),
    ("        ClosePopupsForTab(tabId);",
     "        OpenFill.Core.Browser.BrowserNotificationBus.Clear(tabId);\n        ClosePopupsForTab(tabId);")
])
print("PERMISSION_PATCH_DONE")
