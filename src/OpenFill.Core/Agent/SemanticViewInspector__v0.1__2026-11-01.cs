using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

/// <summary>A08: bounded, read-only structural DOM fingerprint. No text, values, cookies or full DOM.</summary>
public static class SemanticViewInspector
{
    public const string Script = """
        (() => {
          const count = s => { try { return Math.min(200, document.querySelectorAll(s).length); } catch (_) { return 0; } };
          const route = location.pathname.split('/').filter(Boolean).slice(0, 8).map(s =>
            (/^\d+$/.test(s) || /^[0-9a-f-]{16,}$/i.test(s) || s.length > 32) ? ':id' : s.toLowerCase()).join('/');
          return {
            host: location.hostname.toLowerCase(), route: '/' + route,
            forms: count('form'), inputs: count('input:not([type=hidden]),textarea,select'),
            tables: count('table,[role=table],[role=grid]'),
            rows: count('tr,[role=row]'), headers: count('th,[role=columnheader]'),
            lists: count('ul,ol,[role=list]'), items: count('li,[role=listitem]'),
            messages: count('[role=log] [role=listitem],[data-message-id],[data-testid*=message]'),
            articles: count('article,[role=article]'),
            cards: count('article,[role=article],.feed-shared-update-v2,[data-urn^="urn:li:activity:"]'),
            links: count('a[href]'), buttons: count('button,[role=button]'),
            nav: count('nav,[role=navigation]'), headings: count('h1,h2,[role=heading]'),
            main: count('main,[role=main]'),
            observed_at: new Date().toISOString()
          };
        })()
        """;

    public static JsonObject Summarize(JsonObject? browserResponse)
    {
        if (browserResponse?["ok"]?.GetValue<bool>() != true ||
            browserResponse["value"] is not JsonObject raw)
            return new JsonObject { ["status"] = "error", ["reason_code"] = "structural_probe_unavailable" };

        int N(string key) => Math.Clamp(raw[key]?.GetValue<int>() ?? 0, 0, 200);
        var host = raw["host"]?.ToString() ?? "";
        var route = raw["route"]?.ToString() ?? "";
        if (host.Length is < 1 or > 253 || route.Length > 512)
            return new JsonObject { ["status"] = "error", ["reason_code"] = "probe_origin_invalid" };

        var forms = N("forms"); var inputs = N("inputs");
        var tables = N("tables"); var rows = N("rows"); var headers = N("headers");
        var lists = N("lists"); var items = N("items"); var messages = N("messages");
        var articles = N("articles"); var cards = N("cards");
        var links = N("links"); var buttons = N("buttons");
        var nav = N("nav"); var headings = N("headings"); var main = N("main");
        // A23: many SPA inboxes render conversations as ordinary list items rather
        // than role=log or data-message-id. Use generic route semantics plus
        // actual rendered rows; never classify by URL alone without list content.
        var conversationRoute = route.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(s => s is "messages" or "messaging" or "inbox" or "conversations" or "chats" or "threads");
        // A real feed contains content cards but also search/post/chat inputs.
        // Classify the content rather than the surrounding interactive chrome.
        var feedRoute = route.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(s => s is "feed" or "timeline" or "activity");
        string view;
        string confidence;
        if ((messages >= 3 && (lists > 0 || nav > 0)) ||
            (conversationRoute && lists > 0 && items >= 3))
            (view, confidence) = ("inbox", "medium");
        else if (tables > 0 && rows >= 3)
            (view, confidence) = ("table", headers > 0 ? "high" : "medium");
        else if ((cards >= 2 && links >= 3) ||
                 (feedRoute && links >= 8 && buttons >= 5) ||
                 (feedRoute && lists > 0 && items >= 5 && links >= 5))
            (view, confidence) = ("list", cards >= 2 ? "high" : "medium");
        else if (inputs >= 2 || (forms > 0 && inputs > 0))
            (view, confidence) = ("form", inputs >= 3 ? "high" : "medium");
        else if (items >= 5 || (articles >= 3 && links >= 3))
            (view, confidence) = ("list", "medium");
        else
            (view, confidence) = ("detail", "low");

        // Bucket counts: avoid invalidating a structural recipe when one item is added.
        static int Bucket(int n) => n == 0 ? 0 : n == 1 ? 1 : n <= 3 ? 3 : n <= 10 ? 10 : n <= 30 ? 30 : 200;
        static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..20].ToLowerInvariant();
        var shape = string.Join(":", new[] { forms, inputs, tables, rows, headers, lists, items,
            messages, articles, cards, links, buttons, nav, headings, main }.Select(Bucket));
        return new JsonObject
        {
            ["status"] = "ok", ["view_type"] = view, ["confidence"] = confidence,
            ["host"] = host, ["route_key"] = Hash(host + "|" + route),
            // This 20-character signature is a semantic routing hint, not the
            // 64-character selector/mode signature persisted by capture recipes.
            ["view_signature"] = Hash(host + "|" + route + "|" + shape), // legacy alias
            ["semantic_signature"] = Hash(host + "|" + route + "|" + shape),
            ["view_signature_kind"] = "semantic_hint_v1",
            ["source_scope"] = "currently_loaded_dom", ["completeness"] = "unverified",
            ["auth_status"] = "unverified", ["account_scope"] = "unknown",
            ["read_only"] = true, ["full_dom_capture"] = false,
            ["recommended_next"] = view is "list" or "table" or "inbox" ? "read_view_filtered" : "get_page_controls",
            ["counts"] = new JsonObject {
                ["forms"] = forms, ["inputs"] = inputs, ["tables"] = tables, ["rows"] = rows,
                ["lists"] = lists, ["items"] = items, ["messages"] = messages,
                ["articles"] = articles, ["cards"] = cards,
                ["links"] = links, ["buttons"] = buttons
            },
            ["observed_at"] = raw["observed_at"]?.ToString()
        };
    }
}
