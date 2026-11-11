using System.Text.Json;

namespace OpenFill.Core.Agent;

/// <summary>Small, read-only evidence probe for an exact current DOM target. Never a full-page capture.</summary>
public static class ViewEvidenceProbe
{
    public sealed record Prepared(string ReasonCode, string? Script);

    public static Prepared Prepare(string? selector, string? check, string? expected = null,
        string? attribute = null, string? expectedHost = null)
    {
        var css = selector?.Trim() ?? "";
        var mode = check?.Trim().ToLowerInvariant() ?? "";
        var wanted = expected ?? "";
        var attr = attribute?.Trim() ?? "";
        var host = expectedHost?.Trim().ToLowerInvariant() ?? "";
        if (css.Length is < 1 or > 500 || css is "*" or "html" or "body")
            return new("selector_required_or_too_broad", null);
        if (mode is not ("exists" or "absent" or "visible" or "text_contains" or "value_equals" or "attribute_equals"))
            return new("check_invalid", null);
        if (mode is "text_contains" or "value_equals" or "attribute_equals" && wanted.Length is < 1 or > 500)
            return new("expected_required_or_too_long", null);
        if (mode == "attribute_equals" && (attr.Length is < 1 or > 80 || !System.Text.RegularExpressions.Regex.IsMatch(attr, @"^[a-zA-Z_][a-zA-Z0-9_:-]*$")))
            return new("attribute_invalid", null);
        if (host.Length > 253 || host.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '.')) ||
            (host.Length > 0 && (host.StartsWith('-') || host.StartsWith('.') || host.EndsWith('.'))))
            return new("host_invalid", null);

        var script = $$"""
            (() => {
              const selector = {{JsonSerializer.Serialize(css)}};
              const check = {{JsonSerializer.Serialize(mode)}};
              const expected = {{JsonSerializer.Serialize(wanted)}};
              const attribute = {{JsonSerializer.Serialize(attr)}};
              const expectedHost = {{JsonSerializer.Serialize(host)}};
              if (expectedHost && location.hostname.toLowerCase() !== expectedHost)
                return {status:"blocked",reason_code:"host_mismatch",observed_host:location.hostname};
              let elements;
              try { elements = document.querySelectorAll(selector); }
              catch (_) { return {status:"blocked",reason_code:"selector_invalid"}; }
              if (elements.length > 20)
                return {status:"blocked",reason_code:"selector_ambiguous",observed_count:elements.length};
              const valid = el => {
                if (check === "exists") return true;
                if (check === "visible") {
                  const r = el.getBoundingClientRect();
                  const st = getComputedStyle(el);
                  return r.width > 0 && r.height > 0 && st.display !== "none" && st.visibility !== "hidden";
                }
                if (check === "text_contains") return (el.innerText || el.textContent || "").slice(0,4096).includes(expected);
                if (check === "value_equals") return String(el.value ?? "") === expected;
                if (check === "attribute_equals") return el.getAttribute(attribute) === expected;
                return false;
              };
              const met = check === "absent" ? elements.length === 0 : Array.from(elements).some(valid);
              return {status:"ok",reason_code:met?"condition_met":"condition_not_met",
                condition_met:met,observed_count:elements.length,evidence_level:"current_dom_observation_only",
                observed_at:new Date().toISOString(),url:location.href};
            })()
            """;
        return new("ok", script);
    }
}
