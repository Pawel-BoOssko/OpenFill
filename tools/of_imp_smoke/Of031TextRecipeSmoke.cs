// OF-000031: verify text-only capture signatures using the existing live-probe
// and race-gate contracts. Does not navigate a browser or send effects.
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;

internal static class Of031TextRecipeSmoke
{
    public static async Task RunAsync()
    {
        var checks = 0;
        void Check(bool yes, string reason)
        {
            if (!yes) throw new InvalidOperationException("OF031_" + reason);
            checks++;
        }

        const string url = "https://example.com/catalog";
        const string shape = "a:::;button:::submit;";
        var payload = new JsonObject
        {
            ["url"] = url,
            ["title"] = "Catalog",
            ["selector"] = "main",
            ["text"] = "Product one\nProduct two",
            ["capturedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["structureComplete"] = true,
            ["structure"] = shape
        };
        var dir = Path.Combine(Path.GetTempPath(), "of031_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = await PageCaptureService.SaveSnapshotForTestAsync(
                payload, "text", "", dir, 5);
            var signature = CaptureManifestMetadata.ComputeViewSignatureFromStructure(
                url, "main", "text", shape);
            Check(signature.Length == 64 && manifest["view_signature"]?.ToString() == signature,
                "text_signature");
            var files = manifest["files"] as JsonArray;
            Check(files is { Count: 1 } &&
                (files[0] as JsonObject)?["kind"]?.ToString() == "text",
                "no_html_or_elements");
            var candidate = new CaptureRecipe
            {
                Host = "example.com", Path = "/catalog", Operation = "capture",
                Mode = "text", Selector = "main", Pattern = "Product",
                ViewSignature = signature, Confidence = "verified"
            };
            JsonObject Observation(string structure) => new()
            {
                ["url"] = url,
                ["results"] = new JsonArray(
                    new JsonObject { ["index"] = 0, ["found"] = true,
                        ["structure"] = structure })
            };
            var selected = CaptureRecipeLiveProbe.SelectFromObservation(
                [candidate], new Uri(url), Observation(shape));
            Check(ReferenceEquals(selected, candidate), "live_probe_selects_text");
            Check(CaptureRecipeLiveProbe.SelectFromObservation(
                [candidate], new Uri(url), Observation("button:::different;")) is null,
                "live_probe_rejects_changed_shape");
            Check(CaptureRecipeRaceGate.Check(payload, url, "main", "text",
                signature) == "ready", "race_accepts_current_shape");
            payload["structure"] = "button:::different;";
            Check(CaptureRecipeRaceGate.Check(payload, url, "main", "text",
                signature) == "recipe_view_changed", "race_rejects_changed_shape");
            payload["structureComplete"] = false;
            Check(CaptureRecipeRaceGate.Check(payload, url, "main", "text",
                signature) == "recipe_structure_incomplete", "incomplete_shape_rejected");
            var semantic = new JsonObject
            {
                ["status"] = "ok", ["host"] = "example.com",
                ["view_type"] = "list", ["view_signature"] = "abcdef1234567890abcd"
            };
            var plan = AutoViewReadPlanner.Choose(semantic, url, candidate, true);
            Check(plan.ReasonCode == "verified_live_recipe" &&
                plan.RecipeMode == "text" &&
                plan.RecipeSignature == signature, "planner_preserves_exact_text_mode");
            Check(AutoViewReadPlanner.Choose(semantic, url, candidate).ReasonCode ==
                "generic_live_filter", "unproven_recipe_not_reused");

            // Actual LinkedIn feed has content cards alongside search/post/chat inputs.
            JsonObject Summarize(string raw) => SemanticViewInspector.Summarize(
                new JsonObject { ["ok"] = true, ["value"] = JsonNode.Parse(raw) });
            var feed = Summarize(
                """{"host":"www.linkedin.com","route":"/feed","forms":1,"inputs":4,"lists":3,"items":12,"cards":2,"links":25,"buttons":15}""");
            Check(feed["view_type"]?.ToString() == "list" &&
                  feed["recommended_next"]?.ToString() == "read_view_filtered",
                  "feed_not_misclassified_as_form");
            Check(Summarize(
                """{"host":"example.com","route":"/edit","forms":1,"inputs":4,"lists":0,"items":0,"cards":0,"links":2,"buttons":2}""")
                ["view_type"]?.ToString() == "form", "actual_form_preserved");
            Check(Summarize(
                """{"host":"www.linkedin.com","route":"/feed","forms":1,"inputs":3,"cards":0,"links":4,"buttons":1}""")
                ["view_type"]?.ToString() == "form", "empty_feed_form_preserved");
            Check(Summarize(
                """{"host":"www.linkedin.com","route":"/messaging","inputs":3,"lists":2,"items":15,"cards":2,"links":20,"buttons":10}""")
                ["view_type"]?.ToString() == "inbox", "inbox_precedence_preserved");

            // OF-000033: actual modern "Me" menu uses stable data-view-name
            // attributes. Scope stays distinct from mere authenticated nav.
            var ownProfile = "https://www.linkedin.com/in/example-user/";
            var linkedSnapshot = new JsonObject {
                ["url"] = "https://www.linkedin.com/feed/",
                ["authenticatedMarker"] = true, ["loginForm"] = false,
                ["accountSelfLinks"] = new JsonArray(JsonValue.Create(ownProfile))
            };
            Check(CaptureAccountScopeGate.Resolve(linkedSnapshot) ==
                "linkedin:in/example-user", "menu_link_identity_scoped");
            linkedSnapshot["accountSelfLinks"] = new JsonArray();
            Check(CaptureAccountScopeGate.Resolve(linkedSnapshot) is null,
                "closed_menu_without_link_not_trusted");
            linkedSnapshot["accountSelfLinks"] = new JsonArray(JsonValue.Create(
                "https://www.linkedin.com/in/example-user/"));
            linkedSnapshot["authenticatedMarker"] = false;
            Check(CaptureAccountScopeGate.Resolve(linkedSnapshot) is null,
                "profile_link_without_auth_not_trusted");
            Check(LinkedInAccountMenuProbe.Javascript.Contains(
                "nav-me-settings-view-profile", StringComparison.Ordinal) &&
                LinkedInAccountMenuProbe.Javascript.Contains(
                "navigation-settings", StringComparison.Ordinal) &&
                LinkedInAccountMenuProbe.Javascript.Contains(
                "aria-expanded", StringComparison.Ordinal),
                "current_menu_probe_present");
            // OF-000034: the new variant omits data-view-name on the Me
            // button and menu link. A signed nav recipe must never displace
            // a list/content read, even if the nav SHA matches exactly.
            Check(LinkedInAccountMenuProbe.Javascript.Contains(
                "toggles.length === 1", StringComparison.Ordinal),
                "modern_nav_toggle_fallback");
            Check(LinkedInAccountMenuProbe.Javascript.Contains(
                "aria-disabled=\"false\"", StringComparison.Ordinal),
                "modern_menu_unique_profile_link");
            var navRecipe = new CaptureRecipe {
                Host = "example.com", Path = "/catalog", Operation = "capture",
                Confidence = "verified", Selector = "nav:has(button[aria-expanded])",
                Mode = "text", Pattern = "", ViewSignature = signature
            };
            Check(!AutoViewReadPlanner.IsContentRecipe(navRecipe),
                "navigation_recipe_is_not_content");
            Check(AutoViewReadPlanner.Choose(semantic, url, navRecipe, true).ReasonCode ==
                "generic_live_filter", "auto_list_does_not_reuse_nav_capture");
            Check(AutoViewReadPlanner.IsContentRecipe(candidate),
                "main_content_recipe_remains_eligible");
            Console.WriteLine($"OF034_MENU_AND_CONTENT_{checks}_CHECKS_OK");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
