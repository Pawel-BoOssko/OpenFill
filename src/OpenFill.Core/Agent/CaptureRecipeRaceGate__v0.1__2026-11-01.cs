using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

// A05: prevent a verified selector from being applied to a view that changed
// between the lightweight live probe and the actual WebView2 capture.
public static class CaptureRecipeRaceGate
{
    public static string Check(JsonObject observation, string expectedUrl,
        string selector, string mode, string expectedSignature)
    {
        if (string.IsNullOrWhiteSpace(expectedUrl) ||
            string.IsNullOrWhiteSpace(expectedSignature) ||
            !Uri.TryCreate(expectedUrl, UriKind.Absolute, out var expected) ||
            expected.Scheme != "https")
            return "recipe_expected_scope_missing";
        if (!Uri.TryCreate(observation["url"]?.ToString(), UriKind.Absolute, out var actual) ||
            actual.Scheme != "https" ||
            !actual.Host.Equals(expected.Host, StringComparison.OrdinalIgnoreCase) ||
            actual.AbsolutePath != expected.AbsolutePath)
            return "recipe_url_changed";
        string observed;
        if (mode == "text")
        {
            if (observation["structureComplete"]?.ToString() != "true" ||
                observation["structure"] is null)
                return "recipe_structure_incomplete";
            observed = CaptureManifestMetadata.ComputeViewSignatureFromStructure(
                actual.ToString(), selector, mode, observation["structure"]!.ToString());
        }
        else
        {
            if (mode is not ("all" or "elements") ||
                observation["elementsComplete"]?.ToString() != "true" ||
                observation["elements"] is not JsonArray elements)
                return "recipe_structure_incomplete";
            observed = CaptureManifestMetadata.ComputeViewSignature(
                actual.ToString(), selector, mode, elements);
        }
        return observed.Equals(expectedSignature, StringComparison.OrdinalIgnoreCase)
            ? "ready" : "recipe_view_changed";
    }
}
