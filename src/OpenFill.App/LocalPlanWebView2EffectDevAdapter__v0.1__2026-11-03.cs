// OF-000017: DEV-only WebView2 effect adapter for a two-button LOCAL fixture.
// No production or external-site routing; no real send/save/submit integration.
#if DEBUG
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using OpenFill.Core.Agent;

namespace OpenFill.App;

public sealed class LocalPlanWebView2EffectDevAdapter : ILocalPlanEffectAdapter
{
    private readonly WebView2 _view;
    private readonly Uri _expected;

    public LocalPlanWebView2EffectDevAdapter(WebView2 view, Uri expectedOrigin)
    {
        if (expectedOrigin.Scheme != Uri.UriSchemeHttp ||
            expectedOrigin.Host != "127.0.0.1" || expectedOrigin.Port <= 0 ||
            !string.Equals(expectedOrigin.AbsolutePath, "/", StringComparison.Ordinal))
            throw new ArgumentException("effect_dev_local_origin_required");
        _view = view;
        _expected = expectedOrigin;
    }

    private bool Valid(LocalPlanEffectKey key)
    {
        if (key.Provider != "of-local-fixture" || key.AccountId != "dev-session" ||
            key.Action != "save" || key.EffectId != "save-" + key.TargetId ||
            key.TargetId is not ("a" or "b"))
            return false;
        var hash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes("value-" + key.TargetId)));
        return string.Equals(key.PayloadSha256, hash, StringComparison.OrdinalIgnoreCase);
    }

    private void CheckLocation()
    {
        if (_view.CoreWebView2 is null)
            throw new InvalidOperationException("effect_dev_webview_unavailable");
        var page = new Uri(_view.CoreWebView2.Source);
        if (page.Scheme != _expected.Scheme || page.Host != _expected.Host ||
            page.Port != _expected.Port || page.AbsolutePath != "/effects")
            throw new InvalidOperationException("effect_dev_page_changed");
    }

    public async Task<LocalPlanEffectAttemptResult> AttemptAsync(
        LocalPlanEffectKey key, CancellationToken ct)
    {
        if (!Valid(key)) return new("invalid_identity");
        ct.ThrowIfCancellationRequested();
        CheckLocation();
        // Closed, hard-coded targets. No model-generated JavaScript or selectors.
        var script = key.TargetId == "a"
            ? "(() => { let b = document.getElementById('save-a'); if (!b) return 'missing'; b.click(); return 'applied'; })()"
            : "(() => { let b = document.getElementById('save-b'); if (!b) return 'missing'; b.click(); return 'applied'; })()";
        var raw = await _view.ExecuteScriptAsync(script);
        return new(JsonSerializer.Deserialize<string>(raw) == "applied" ? "applied" : "unknown");
    }

    public async Task<string?> ObserveAsync(LocalPlanEffectKey key, CancellationToken ct)
    {
        if (!Valid(key)) return null;
        ct.ThrowIfCancellationRequested();
        CheckLocation();
        var script = key.TargetId == "a"
            ? "(() => { let n=document.getElementById('count-a'); let s=document.getElementById('result-a'); return n?.dataset.count === '1' && s?.textContent === 'Saved a' ? 'local-effect:a:count=1' : null; })()"
            : "(() => { let n=document.getElementById('count-b'); let s=document.getElementById('result-b'); return n?.dataset.count === '1' && s?.textContent === 'Saved b' ? 'local-effect:b:count=1' : null; })()";
        var raw = await _view.ExecuteScriptAsync(script);
        return JsonSerializer.Deserialize<string?>(raw);
    }
}
#endif
