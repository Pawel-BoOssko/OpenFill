from pathlib import Path
root=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
p=root/"src/OpenFill.Core/Agent/PageCaptureService__v0.1__2026-10-30.cs"
c=root/"src/OpenFill.Core/Agent/CurrentTextCaptureCache__v0.1__2026-11-02.cs"
a=p.read_text(encoding="utf-8-sig")
b=c.read_text(encoding="utf-8-sig")
def patch(s,old,new,label):
    n=s.count(old)
    if n!=1: raise RuntimeError(f"{label}: expected 1 occurrence, found {n}")
    return s.replace(old,new)
a=patch(a,
'''            !allowLoginPage && string.IsNullOrWhiteSpace(expectedViewSignature) &&
            string.IsNullOrWhiteSpace(expectedAccountScope))''',
'''            !allowLoginPage && string.IsNullOrWhiteSpace(expectedAccountScope))''',"cache gate")
a=patch(a,
'''                    Path.Combine(sharedDirectory, "captures"), selector, ct);''',
'''                    Path.Combine(sharedDirectory, "captures"), selector, ct,
                    expectedUrl, expectedViewSignature);''',"cache call")
b=patch(b,
'''      return { url:location.href, title:document.title,
        login_form:!!document.querySelector('input[type="password"]'),
        text:root?(root.innerText||root.textContent||""):null };''',
'''      const verifyStructure=__VERIFY_STRUCTURE__;
      let structure=null;
      if(root && verifyStructure) {
        const nodes=root.querySelectorAll("a[href],button,input,textarea,select,[role=button],[role=link],[role=tab],[role=checkbox],[role=textbox],h1,h2,h3");
        if(nodes.length<=3000) {
          structure="";
          for(const e of nodes) {
            structure+=e.tagName.toLowerCase()+":"+(e.getAttribute("role")||"")+":"+
              (e.getAttribute("type")||"")+":"+(e.getAttribute("name")||"")+";";
          }
        }
      }
      return { url:location.href, title:document.title,
        login_form:!!document.querySelector('input[type="password"]'),
        text:root?(root.innerText||root.textContent||""):null, structure };''',"js probe")
b=patch(b,
'''        BrowserController browser, string capturesRoot, string selector, CancellationToken ct)''',
'''        BrowserController browser, string capturesRoot, string selector, CancellationToken ct,
        string? expectedUrl = null, string? expectedViewSignature = null)''',"signature")
b=patch(b,
'''            CaptureRecipeScopeGate.IsSensitiveHost(uri.Host)) return null;''',
'''            CaptureRecipeScopeGate.IsSensitiveHost(uri.Host) ||
            (!string.IsNullOrWhiteSpace(expectedUrl) &&
             !string.Equals(expectedUrl, currentUrl, StringComparison.Ordinal))) return null;''',"expected url")
b=patch(b,
'''                    manifest["source_scope"]?.ToString() != "currently_loaded_dom" ||
                    !DateTimeOffset.TryParse''',
'''                    manifest["source_scope"]?.ToString() != "currently_loaded_dom" ||
                    (!string.IsNullOrWhiteSpace(expectedViewSignature) &&
                     !string.Equals(manifest["view_signature"]?.ToString(), expectedViewSignature,
                         StringComparison.OrdinalIgnoreCase)) ||
                    !DateTimeOffset.TryParse''',"manifest view")
b=patch(b,
'''        var js = ProbeScript.Replace("__SELECTOR__",
            System.Text.Json.JsonSerializer.Serialize(selector));''',
'''        var js = ProbeScript.Replace("__SELECTOR__",
            System.Text.Json.JsonSerializer.Serialize(selector))
            .Replace("__VERIFY_STRUCTURE__", string.IsNullOrWhiteSpace(expectedViewSignature) ? "false" : "true");''',"js bound")
b=patch(b,
'''            live["url"]?.ToString() != currentUrl) return null;
        var text = live["text"]!.ToString();''',
'''            live["url"]?.ToString() != currentUrl) return null;
        if (!string.IsNullOrWhiteSpace(expectedViewSignature) &&
            (live["structure"] is null ||
             !string.Equals(CaptureManifestMetadata.ComputeViewSignatureFromStructure(
                 currentUrl, selector, "text", live["structure"]!.ToString()),
                 expectedViewSignature, StringComparison.OrdinalIgnoreCase)))
            return null;
        var text = live["text"]!.ToString();''',"live signature")
for file,src in [(p,a),(c,b)]:
    backup=Path(str(file)+".before_a27_default_cache_20261104")
    if backup.exists(): raise RuntimeError("Backup already exists: "+str(backup))
    backup.write_bytes(file.read_bytes())
    file.write_text(src,encoding="utf-8")
    print("PATCHED",file,"BACKUP",backup)
