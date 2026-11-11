// OF-ARCHFLOW-20261102: CDP adapter for DEV navigation and textbox-filling slice.
// Not wired to AgentRunner or LIVE. No send/save/submit.
using System.Text.Json.Nodes;
using OpenFill.Core.Cdp;

namespace OpenFill.Core.Agent;

public sealed class LocalPlanCdpBrowser : ILocalPlanFormBrowser
{
    private readonly CdpSession _session;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _lastFingerprint;
    private string? _pendingNavigationFingerprint;
    private long _revision;

    public LocalPlanCdpBrowser(CdpSession session) => _session = session;

    // Capture an accessible, visible subset, with DOM-order refs. Fingerprint is of
    // observed content, URL and document identity, NOT of the number of reads.
    // Navigation-only: refs for actual <a href>; no buttons or JS role=link.
    private const string Capture = """
    (() => {
      const visible = e => {
        if (!e.getClientRects().length) return false;
        const s = getComputedStyle(e);
        return s.display !== 'none' && s.visibility !== 'hidden';
      };
      const nameOf = e => {
        let label = e.getAttribute('aria-label');
        if (!label && e.hasAttribute('aria-labelledby')) {
          label = e.getAttribute('aria-labelledby').split(/\s+/).map(id => document.getElementById(id)?.textContent || '').join(' ');
        }
        if (!label && e.labels?.length) label = [...e.labels].map(l => l.textContent || '').join(' ');
        if (!label) label = e.getAttribute('placeholder') || e.getAttribute('title') || e.textContent || '';
        return label.replace(/\s+/g,' ').trim().slice(0,180);
      };
      const elements = [];
      for (const e of document.querySelectorAll('a[href], input, textarea, [role="textbox"]')) {
        if (elements.length >= 500) break;
        if (!visible(e)) continue;
        let role;
        if (e.matches('a[href]')) role = 'link';
        else if (e.matches('textarea, [role="textbox"]')) role = 'textbox';
        else if (e.matches('input') && !['hidden','checkbox','radio','file','submit','button','password'].includes((e.type || '').toLowerCase())) role = 'textbox';
        else continue;
        const href = role === 'link' ? e.href : null;
        const enabled = !e.matches(':disabled') && e.getAttribute('aria-disabled') !== 'true';
        elements.push({ref:'e'+elements.length,role,name:nameOf(e),enabled,href,value:role==='textbox' && 'value' in e ? e.value : null});
      }
      const fingerprint = JSON.stringify({url:location.href,document:performance.timeOrigin,elements});
      return {host:location.hostname, readyState:document.readyState, fingerprint, elements};
    })()
    """;

    private async Task<JsonObject> CaptureAsync(CancellationToken ct)
    {
        var data = await _session.EvaluateAsync(Capture, ct: ct);
        return data as JsonObject ?? throw new InvalidOperationException("cdp_snapshot_empty");
    }

    private async Task<JsonObject> CaptureAfterNavigationAsync(CancellationToken ct)
    {
        var previous = _pendingNavigationFingerprint;
        if (previous is null) return await CaptureAsync(ct);
        Exception? lastError = null;
        // Wait only for observation, never reissue an action.
        for (var i = 0; i < 24; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var data = await CaptureAsync(ct);
                if (data["fingerprint"]?.GetValue<string>() != previous &&
                    data["readyState"]?.GetValue<string>() == "complete")
                {
                    _pendingNavigationFingerprint = null;
                    return data;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { lastError = ex; }
            await Task.Delay(100, ct);
        }
        try { return await CaptureAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { throw new InvalidOperationException("cdp_navigation_snapshot_unavailable", lastError ?? ex); }
    }

    public async Task<SemanticPlanSnapshot> ReadAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var data = await CaptureAfterNavigationAsync(ct);
            var fingerprint = data["fingerprint"]?.GetValue<string>()
                ?? throw new InvalidOperationException("cdp_snapshot_without_fingerprint");
            if (!string.Equals(fingerprint, _lastFingerprint, StringComparison.Ordinal))
            {
                checked { _revision++; }
                _lastFingerprint = fingerprint;
            }
            var host = data["host"]?.GetValue<string>() ?? "";
            var elements = new List<SemanticPlanElement>();
            if (data["elements"] is not JsonArray rows)
                throw new InvalidOperationException("cdp_snapshot_without_elements");
            foreach (var row in rows.OfType<JsonObject>())
                elements.Add(new SemanticPlanElement(
                    row["ref"]!.GetValue<string>(),
                    row["role"]!.GetValue<string>(),
                    row["name"]!.GetValue<string>(),
                    row["enabled"]!.GetValue<bool>(),
                    row["value"]?.GetValue<string>()));
            return new SemanticPlanSnapshot(_revision, host, elements);
        }
        finally { _gate.Release(); }
    }

    public async Task<SemanticPlanClickResult> ClickNavigationAsync(
        string elementRef, long expectedRevision, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (expectedRevision != _revision || _lastFingerprint is null)
                return new("stale");
            var args = new JsonObject {
                ["ref"] = elementRef, ["fingerprint"] = _lastFingerprint
            }.ToJsonString();
            // Snapshot comparison and location.assign happen in ONE JS execution:
            // unlike separate CDP reads, there is no TOCTOU gap before navigation.
            // No element.click(): an app's click handlers may have business effects.
            var script = "(() => { const arg = " + args + "; const current = " + Capture + @";
              if (current.fingerprint !== arg.fingerprint) return {status:'stale'};
              const item = current.elements.find(e => e.ref === arg.ref);
              if (!item || item.role !== 'link' || !item.enabled || !item.href)
                return {status:'failed_before_action'};
              let destination;
              try { destination = new URL(item.href, location.href); }
              catch { return {status:'failed_before_action'}; }
              if (!['http:', 'https:'].includes(destination.protocol) ||
                  destination.origin !== location.origin ||
                  destination.href === location.href)
                return {status:'failed_before_action'};
              location.assign(destination.href);
              return {status:'applied', evidence:'cdp_location_assign'};
            })()";
            JsonNode? result;
            try { result = await _session.EvaluateAsync(script, ct: ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { return new("uncertain"); }
            var status = result?["status"]?.GetValue<string>() ?? "uncertain";
            if (status == "applied") _pendingNavigationFingerprint = _lastFingerprint;
            return new(status, result?["evidence"]?.GetValue<string>());
        }
        finally { _gate.Release(); }
    }

    // DEV-only form input, never a submit/send/save. Re-resolve the exact DOM
    // under the same fingerprint, in one CDP execution before editing.
    public async Task<SemanticPlanClickResult> FillTextAsync(
        string elementRef, long expectedRevision, string value, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (expectedRevision != _revision || _lastFingerprint is null)
                return new("stale");
            var args = new JsonObject {
                ["ref"] = elementRef, ["fingerprint"] = _lastFingerprint, ["value"] = value
            }.ToJsonString();
            var script = "(() => { const arg = " + args + "; const current = " + Capture + @";
              if (current.fingerprint !== arg.fingerprint) return {status:'stale'};
              const item = current.elements.find(e => e.ref === arg.ref);
              if (!item || item.role !== 'textbox' || !item.enabled ||
                  !/^e[0-9]+$/.test(arg.ref)) return {status:'failed_before_action'};
              const idx = Number(arg.ref.slice(1));
              const visible = e => e.getClientRects().length &&
                getComputedStyle(e).display !== 'none' &&
                getComputedStyle(e).visibility !== 'hidden';
              const nodes = [];
              for (const e of document.querySelectorAll('a[href],input,textarea,[role=textbox]')) {
                if (nodes.length >= 500) break;
                if (!visible(e)) continue;
                if (e.matches('a[href],textarea,[role=textbox]')) nodes.push(e);
                else if (e.matches('input') && !['hidden','checkbox','radio','file','submit','button','password']
                  .includes((e.type || '').toLowerCase())) nodes.push(e);
              }
              const el = nodes[idx];
              if (!el || !(el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement) ||
                  el.disabled || el.readOnly || !el.isConnected || arg.value.length > 2000)
                return {status:'failed_before_action'};
              const proto = el instanceof HTMLTextAreaElement ?
                HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
              const setter = Object.getOwnPropertyDescriptor(proto,'value')?.set;
              if (!setter) return {status:'failed_before_action'};
              setter.call(el,arg.value);
              el.dispatchEvent(new Event('input',{bubbles:true}));
              return {status:'applied',evidence:'cdp_input_value'};
            })()";
            JsonNode? result;
            try { result = await _session.EvaluateAsync(script, ct: ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { return new("uncertain"); }
            return new(result?["status"]?.GetValue<string>() ?? "uncertain",
                result?["evidence"]?.GetValue<string>());
        }
        finally { _gate.Release(); }
    }
}
