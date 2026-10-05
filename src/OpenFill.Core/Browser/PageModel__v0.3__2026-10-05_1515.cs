// OpenFill - Metadata: wersja 0.3, data 2026-10-05 15:15
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Browser;

/// <summary>
/// Mirrors the extractor result on the C# side: renders a compact text view
/// and computes the difference between two snapshots (what changed after an action).
/// </summary>
public sealed class PageModel
{
    public string Url { get; init; } = "";
    public string Title { get; init; } = "";
    public JsonObject Raw { get; init; } = new();

    public static PageModel From(JsonNode? node)
    {
        var obj = node as JsonObject ?? new JsonObject();
        return new PageModel
        {
            Url = obj["url"]?.GetValue<string>() ?? "",
            Title = obj["title"]?.GetValue<string>() ?? "",
            Raw = obj
        };
    }

    private static string FieldLine(JsonObject f)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(f["id"]?.GetValue<string>()).Append("] ");
        var type = f["type"]?.GetValue<string>() ?? "text";
        sb.Append(type);
        var label = f["label"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(label)) sb.Append(" \"").Append(label).Append('"');
        if (f["required"]?.GetValue<bool>() == true) sb.Append(" *");
        if (f["disabled"]?.GetValue<bool>() == true) sb.Append(" (disabled)");
        if (f["autocomplete"]?.GetValue<bool>() == true) sb.Append(" (autocomplete)");

        if (f["options"] is JsonArray opts)
        {
            var labels = opts.Take(8).Select(o => o?["label"]?.GetValue<string>() ?? o?["value"]?.GetValue<string>() ?? "").Where(s => s.Length > 0);
            sb.Append(" options={").Append(string.Join(" | ", labels)).Append('}');
        }
        if (f["checked"] is JsonNode chk) sb.Append(chk.GetValue<bool>() ? " =checked" : " =unchecked");
        else
        {
            var val = f["value"];
            if (val is JsonValue && !string.IsNullOrEmpty(val.ToString())) sb.Append(" =\"").Append(val.ToString()).Append('"');
            else if (val is JsonArray arr && arr.Count > 0) sb.Append(" =[").Append(string.Join(", ", arr.Select(x => x?.ToString()))).Append(']');
        }
        if (f["invalid"]?.GetValue<bool>() == true)
            sb.Append(" !ERROR").Append(f["error"] is JsonNode err ? ": " + err.GetValue<string>() : "");
        return sb.ToString();
    }

    private static string ButtonLine(JsonObject b)
    {
        var sb = new StringBuilder();
        var kind = b["kind"]?.GetValue<string>();
        sb.Append(kind is "option" ? "option [" : kind is "toggle" or "radio" ? kind + " [" : kind is "tab" ? "tab [" : "button [")
          .Append(b["id"]?.GetValue<string>()).Append("] \"").Append(b["text"]?.GetValue<string>()).Append('"');
        if (kind == "submit") sb.Append(" submit");
        if (b["state"] is JsonNode st) sb.Append(st.GetValue<bool>() ? " =on" : " =off");
        if (b["disabled"]?.GetValue<bool>() == true) sb.Append(" (disabled)");
        return sb.ToString();
    }

    /// <summary>Compact text for the model: forms with fields and buttons, loose fields, buttons, a few links.</summary>
    public string Render(int maxLinks = 15)
    {
        var sb = new StringBuilder();
        sb.Append("URL: ").Append(Url).Append('\n');
        if (!string.IsNullOrEmpty(Title)) sb.Append("Title: ").Append(Title).Append('\n');
        if (Raw["headings"] is JsonArray hs && hs.Count > 0)
            sb.Append("Headings: ").Append(string.Join(" | ", hs.Select(h => h?.GetValue<string>()))).Append('\n');

        if (Raw["dialogs"] is JsonArray dlg && dlg.Count > 0)
        {
            sb.Append("\nOPEN DIALOG (handle it first):\n");
            foreach (var d in dlg.OfType<JsonObject>())
                sb.Append("  dialog [").Append(d["id"]?.GetValue<string>()).Append("] \"").Append(d["name"]?.GetValue<string>()).Append("\"\n");
        }

        if (Raw["messages"] is JsonArray msgs && msgs.Count > 0)
        {
            sb.Append("\nPAGE MESSAGES:\n");
            foreach (var m in msgs.OfType<JsonObject>())
                sb.Append("  ").Append(m["kind"]?.GetValue<string>() == "alert" ? "! " : "- ").Append(m["text"]?.GetValue<string>()).Append('\n');
        }

        if (Raw["forms"] is JsonArray forms && forms.Count > 0)
        {
            foreach (var fo in forms.OfType<JsonObject>())
            {
                sb.Append("\nFORM [").Append(fo["id"]?.GetValue<string>()).Append(']');
                var nm = fo["name"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(nm)) sb.Append(" \"").Append(nm).Append('"');
                sb.Append('\n');
                foreach (var f in (fo["fields"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                    sb.Append("  ").Append(FieldLine(f)).Append('\n');
                foreach (var b in (fo["buttons"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                    sb.Append("  (").Append(ButtonLine(b)).Append(")\n");
            }
        }

        if (Raw["looseFields"] is JsonArray loose && loose.Count > 0)
        {
            sb.Append("\nFIELDS (no form)\n");
            foreach (var f in loose.OfType<JsonObject>()) sb.Append("  ").Append(FieldLine(f)).Append('\n');
        }

        if (Raw["buttons"] is JsonArray btns && btns.Count > 0)
        {
            sb.Append("\nBUTTONS\n");
            foreach (var group in btns.OfType<JsonObject>().GroupBy(b => b["form"]?.GetValue<string>() ?? ""))
            {
                if (group.Key.Length > 0) sb.Append("  w [").Append(group.Key).Append("]:\n");
                foreach (var b in group) sb.Append(group.Key.Length > 0 ? "    " : "  ").Append(ButtonLine(b)).Append('\n');
            }
        }

        if (Raw["links"] is JsonArray links && links.Count > 0)
        {
            sb.Append("\nLINKS\n");
            foreach (var l in links.OfType<JsonObject>().Take(maxLinks))
                sb.Append("  [").Append(l["id"]?.GetValue<string>()).Append("] \"").Append(l["text"]?.GetValue<string>()).Append("\"\n");
        }

        if (Raw["truncated"]?.GetValue<bool>() == true)
            sb.Append("\n[note] the field list was truncated — the page has more fields than the limit.\n");

        return sb.ToString().TrimEnd();
    }

    /// <summary>Map id -> compact description of a field/button, used to compute differences.</summary>
    private Dictionary<string, string> FlattenControls()
    {
        var map = new Dictionary<string, string>();
        void AddField(JsonObject f) { var id = f["id"]?.GetValue<string>(); if (id != null) map[id] = FieldLine(f); }
        if (Raw["forms"] is JsonArray forms)
            foreach (var fo in forms.OfType<JsonObject>())
            {
                foreach (var f in (fo["fields"] as JsonArray ?? new()).OfType<JsonObject>()) AddField(f);
                foreach (var b in (fo["buttons"] as JsonArray ?? new()).OfType<JsonObject>())
                { var id = b["id"]?.GetValue<string>(); if (id != null) map[id] = ButtonLine(b); }
            }
        if (Raw["looseFields"] is JsonArray loose) foreach (var f in loose.OfType<JsonObject>()) AddField(f);
        if (Raw["buttons"] is JsonArray btns)
            foreach (var b in btns.OfType<JsonObject>())
            { var id = b["id"]?.GetValue<string>(); if (id != null) map[id] = ButtonLine(b); }
        if (Raw["dialogs"] is JsonArray dlg)
            foreach (var d in dlg.OfType<JsonObject>())
            { var id = d["id"]?.GetValue<string>(); if (id != null) map["dlg:" + id] = "dialog \"" + d["name"]?.GetValue<string>() + "\""; }
        if (Raw["messages"] is JsonArray msgs)
            foreach (var m in msgs.OfType<JsonObject>())
            { var t = m["text"]?.GetValue<string>() ?? ""; map["msg:" + t] = "message: " + t; }
        return map;
    }

    /// <summary>Text difference against the previous snapshot: URL change, new/disappeared/changed controls.</summary>
    public string DiffFrom(PageModel? prev)
    {
        if (prev is null) return Render();
        var sb = new StringBuilder();
        if (prev.Url != Url) sb.Append("URL changed to: ").Append(Url).Append('\n');

        var before = prev.FlattenControls();
        var after = FlattenControls();

        var added = after.Keys.Where(k => !before.ContainsKey(k)).ToList();
        var removed = before.Keys.Where(k => !after.ContainsKey(k)).ToList();
        var changed = after.Keys.Where(k => before.ContainsKey(k) && before[k] != after[k]).ToList();

        if (added.Count > 0) { sb.Append("Added:\n"); foreach (var k in added.Take(30)) sb.Append("  + ").Append(after[k]).Append('\n'); }
        if (changed.Count > 0) { sb.Append("Changed:\n"); foreach (var k in changed.Take(30)) sb.Append("  ~ ").Append(after[k]).Append('\n'); }
        if (removed.Count > 0) { sb.Append("Removed:\n"); foreach (var k in removed.Take(30)) sb.Append("  - ").Append(before[k]).Append('\n'); }

        if (sb.Length == 0) return "No changes in the page controls.";
        return sb.ToString().TrimEnd();
    }
}
