using System.Text.Json;
using System.Text.Json.Nodes;
using OpenFill.Core.Browser;

namespace OpenFill.Core.Agent;

// A05: one lightweight DOM structure probe, never a full page export.
// Account-bearing sites remain fail-closed until trusted account scope exists.
public static class CaptureRecipeLiveProbe
{
    public static async Task<CaptureRecipe?> SelectAsync(BrowserController browser,
        IReadOnlyList<CaptureRecipe> recipes, Uri expected, CancellationToken ct)
    {
        var linkedin = expected.Host.Equals("www.linkedin.com", StringComparison.OrdinalIgnoreCase);
        if (CaptureRecipeScopeGate.IsSensitiveHost(expected.Host) && !linkedin) return null;
        var candidates = recipes.Where(r =>
            r.Host.Equals(expected.Host, StringComparison.OrdinalIgnoreCase) &&
            r.Path.Equals(expected.AbsolutePath, StringComparison.Ordinal) &&
            r.Operation == "capture" && r.Confidence == "verified" &&
            (linkedin ? !string.IsNullOrWhiteSpace(r.AccountScope) :
                string.IsNullOrWhiteSpace(r.AccountScope)) &&
            !string.IsNullOrWhiteSpace(r.ViewSignature) &&
            r.Mode is "all" or "elements" or "text" &&
            r.Selector.Length is > 0 and <= 400).Take(12).ToArray();
        if (candidates.Length == 0) return null;
        var specs = JsonSerializer.Serialize(candidates.Select(r =>
            new { selector = r.Selector, mode = r.Mode }));
        var js = """
            (async () => {
              const specs = __SPECS__;
              const results = specs.map((s, index) => {
                try {
                  const root = document.querySelector(s.selector);
                  if (!root) return {index, found:false};
                  const nodes = root.querySelectorAll("a[href],button,input,textarea,select,[role=button],[role=link],[role=tab],[role=checkbox],[role=textbox],h1,h2,h3");
                  if (nodes.length > 3000) return {index, found:false, reason:"too_many_elements"};
                  let structure = "";
                  for (const e of nodes) {
                    structure += e.tagName.toLowerCase()+":"+(e.getAttribute("role")||"")+":"+
                      (e.getAttribute("type")||"")+":"+(e.getAttribute("name")||"")+";";
                  }
                  return {index, found:true, structure};
                } catch { return {index, found:false, reason:"invalid_selector"}; }
              });
              const accountSelfLinks = await __LINKEDIN_SCOPE_PROBE__;
              const modernAuthNav = [...document.querySelectorAll('nav')].some(n =>
                !!n.querySelector('a[href*="/mynetwork"]') &&
                !!n.querySelector('a[href*="/messaging"]') &&
                !!n.querySelector('a[href*="/notifications"]') &&
                !!n.querySelector('button[aria-expanded]'));
              const authenticatedMarker =
                !!document.querySelector('nav.global-nav,header.global-nav,#global-nav,[data-test-global-nav],.global-nav__me') ||
                ((location.hostname === 'linkedin.com' || location.hostname.endsWith('.linkedin.com')) && modernAuthNav);
              const loginForm = !!document.querySelector('form[action*="/login"],input[type="password"],input[name="session_key"]');
              return {url:location.href, results, authenticatedMarker, loginForm, accountSelfLinks};
            })()
            """.Replace("__SPECS__", specs)
                .Replace("__LINKEDIN_SCOPE_PROBE__", LinkedInAccountMenuProbe.Javascript);
        try
        {
            var result = await browser.RunJsAsync(js, ct);
            if (result?["ok"]?.GetValue<bool>() != true) return null;
            return SelectFromObservation(candidates, expected, result["value"] as JsonObject);
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; } // A failed probe is a normal uncached capture, never an auth bypass.
    }

    public static CaptureRecipe? SelectFromObservation(
        IReadOnlyList<CaptureRecipe> candidates, Uri expected, JsonObject? observation)
    {
        var linkedin = expected.Host.Equals("www.linkedin.com", StringComparison.OrdinalIgnoreCase);
        if (observation is null || (CaptureRecipeScopeGate.IsSensitiveHost(expected.Host) && !linkedin) ||
            !Uri.TryCreate(observation["url"]?.ToString(), UriKind.Absolute, out var actual) ||
            actual.Scheme != "https" ||
            !actual.Host.Equals(expected.Host, StringComparison.OrdinalIgnoreCase) ||
            actual.AbsolutePath != expected.AbsolutePath ||
            observation["results"] is not JsonArray results) return null;
        var accountScope = linkedin ? CaptureAccountScopeGate.Resolve(observation) : null;
        if (linkedin && string.IsNullOrWhiteSpace(accountScope)) return null;
        var matched = new List<CaptureRecipe>();
        foreach (var node in results)
        {
            if (node is not JsonObject row || row["found"]?.ToString() != "true" ||
                !int.TryParse(row["index"]?.ToString(), out var index) ||
                index < 0 || index >= candidates.Count) continue;
            var candidate = candidates[index];
            if (candidate.Mode is not ("all" or "elements" or "text") ||
                string.IsNullOrWhiteSpace(candidate.ViewSignature) ||
                (linkedin && !string.Equals(candidate.AccountScope, accountScope, StringComparison.Ordinal)) ||
                (!linkedin && !string.IsNullOrWhiteSpace(candidate.AccountScope))) continue;
            var signature = CaptureManifestMetadata.ComputeViewSignatureFromStructure(
                actual.ToString(), candidate.Selector, candidate.Mode, row["structure"]?.ToString() ?? "");
            if (signature.Equals(candidate.ViewSignature, StringComparison.OrdinalIgnoreCase))
                matched.Add(candidate);
        }
        return matched.OrderByDescending(r => r.TaskSuccesses)
            .ThenBy(r => r.TaskFailures).ThenBy(r => r.LastElapsedMs).FirstOrDefault();
    }
}
