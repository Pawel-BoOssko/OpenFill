using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenFill.Core.Agent;

/// <summary>One-call A09: recognized structure -> small live read or interactive controls.</summary>
public static class AutoViewReadPlanner
{
    // SemanticSignature is a short routing hint, never a persisted recipe identity.
    // RecipeSignature is the full SHA256 of selector/mode/DOM structure, validated
    // by CaptureRecipeLiveProbe against the currently loaded page.
    public sealed record Plan(string Target, string ViewType, string Operation, string Query,
        string Selector, string ReasonCode, string? SemanticSignature,
        string? RecipeSignature = null, string? RecipeAccountScope = null,
        string? RecipeMode = null);

    // OF-000034: a verified navigation capture is not a verified feed/list
    // capture. Keep these recipes available to explicit page_capture, but never
    // auto-route a list/table/inbox read to a header or menu.
    public static bool IsContentRecipe(CaptureRecipe? recipe)
    {
        if (recipe is null || string.IsNullOrWhiteSpace(recipe.Selector)) return false;
        var selector = recipe.Selector.Trim();
        return !Regex.IsMatch(selector, @"^(nav|header|footer|aside)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(50))
            && !selector.Contains("role=navigation", StringComparison.OrdinalIgnoreCase)
            && !selector.Contains("role=\"navigation\"", StringComparison.OrdinalIgnoreCase)
            && !selector.Contains("role='navigation'", StringComparison.OrdinalIgnoreCase);
    }

    public static Plan Choose(JsonObject? manifest, string? currentUrl,
        CaptureRecipe? recipe = null, bool exactLiveVerified = false)
    {
        if (manifest?["status"]?.ToString() != "ok" ||
            !Uri.TryCreate(currentUrl, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals(manifest["host"]?.ToString(), StringComparison.OrdinalIgnoreCase))
            return new("get_page", "unknown", "", "", "body", "structure_unavailable", null);
        var view = manifest["view_type"]?.ToString() ?? "detail";
        var semantic = manifest["view_signature"]?.ToString();
        if (view is not ("list" or "table" or "inbox"))
            return new("get_page", view, "", "", "body", "interactive_or_detail", semantic);

        var candidate = recipe is { Confidence: "verified", Operation: "capture" } &&
            IsContentRecipe(recipe) &&
            recipe.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase) &&
            recipe.Path.Equals(uri.AbsolutePath, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(recipe.Selector) && recipe.Selector.Length <= 300 &&
            recipe.Pattern.Length <= 400;
        // Only an exact full-length signature confirmed by the live DOM probe
        // permits signed reuse. For a sensitive site require proven account scope.
        var exact = candidate && exactLiveVerified && (recipe!.Mode is "all" or "text") &&
            recipe.ViewSignature.Length == 64 &&
            (!CaptureRecipeScopeGate.IsSensitiveHost(uri.Host) ||
                !string.IsNullOrWhiteSpace(recipe.AccountScope));
        // Keep the existing unsigned route-only fallback on neutral sites.
        var legacy = candidate && !exact &&
            !CaptureRecipeScopeGate.IsSensitiveHost(uri.Host) &&
            recipe!.Mode is "text" or "all" &&
            string.IsNullOrWhiteSpace(recipe.ViewSignature) &&
            string.IsNullOrWhiteSpace(recipe.AccountScope);
        var trusted = exact || legacy;
        var selector = trusted ? recipe!.Selector :
            (manifest["counts"]?["main"]?.GetValue<int>() > 0 ? "main" : "body");
        var query = trusted && !string.IsNullOrWhiteSpace(recipe!.Pattern) ? recipe.Pattern : ".+";
        return new("observe_query", view, "text_regex", query, selector,
            exact ? "verified_live_recipe" : legacy ? "verified_route_recipe" : "generic_live_filter",
            semantic, exact ? recipe!.ViewSignature : null,
            exact ? recipe!.AccountScope : null, exact ? recipe!.Mode : null);
    }
}
