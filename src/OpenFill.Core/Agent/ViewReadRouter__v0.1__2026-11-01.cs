namespace OpenFill.Core.Agent;

/// <summary>Deterministic A06 routing. A stored capture is never refreshed implicitly.</summary>
public static class ViewReadRouter
{
    public sealed record Decision(string Target, string ReasonCode, string Operation, string Query);

    public static string ResolveFileKind(string? requested, string operation) =>
        !string.IsNullOrWhiteSpace(requested) ? requested.Trim().ToLowerInvariant() :
        operation == "json_select" ? "elements" : "text";

    // Refuse invalid operations and incompatible file kinds before touching WebView2.
    public static Decision Select(string? viewType, string? captureId, string? operation, string? query,
        string? fileKind = null)
    {
        var kind = (viewType ?? "auto").Trim().ToLowerInvariant();
        var op = (operation ?? "").Trim();
        var q = query ?? "";
        if (kind is not ("auto" or "list" or "table" or "inbox" or "form" or "detail"))
            return new("error", "view_type_invalid", op, q);
        var stored = !string.IsNullOrWhiteSpace(captureId);
        if (op.Length > 0 && op is not ("text_literal" or "text_regex" or "text_count" or "json_select" or "ndjson_select"))
            return new("error", "operation_invalid", op, q);
        if (stored && op == "ndjson_select")
            return new("error", "capture_ndjson_not_supported", op, q);
        if (!string.IsNullOrWhiteSpace(fileKind))
        {
            var requestedKind = fileKind.Trim().ToLowerInvariant();
            if (requestedKind is not ("text" or "elements" or "html"))
                return new("error", "file_kind_invalid", op, q);
            if (!stored && requestedKind == "html")
                return new("error", "live_file_kind_unsupported", op, q);
            if ((op == "json_select" && requestedKind != "elements") ||
                (op is "text_literal" or "text_regex" or "text_count" && requestedKind == "elements"))
                return new("error", "operation_file_kind_mismatch", op, q);
            if (!stored && op.Length == 0 && kind == "auto")
                return new("error", "file_kind_requires_filter", op, q);
            if (!stored && op.Length == 0 && requestedKind != "text")
                return new("error", "file_kind_requires_operation", op, q);
        }
        if (stored)
        {
            if (kind is "form" or "detail")
                return new("error", "capture_not_interactive", op, q);
            if (string.IsNullOrWhiteSpace(op) || string.IsNullOrWhiteSpace(q))
                return new("error", "stored_capture_query_required", op, q);
            return new("file_query", "stored_capture_no_dom", op, q);
        }
        if (kind is "form" or "detail")
            return new("get_page", "interactive_controls", op, q);
        // Current WebView2 capture stores a JSON array, not NDJSON. Refuse before any DOM read.
        if (op == "ndjson_select")
            return new("error", "operation_requires_stored_ndjson", op, q);
        if (kind is "list" or "table" or "inbox" ||
            (kind == "auto" && !string.IsNullOrWhiteSpace(op)))
        {
            if (string.IsNullOrWhiteSpace(op)) op = "text_regex";
            if (string.IsNullOrWhiteSpace(q) && op == "text_regex") q = ".+";
            if (string.IsNullOrWhiteSpace(q))
                return new("error", "query_required", op, q);
            return new("observe_query", "new_capture_local_filter", op, q);
        }
        return new("get_page", "unknown_view_preserve_controls", op, q);
    }
}
