// OF-IMP-20261031 D03: exact, post-attempt price evidence gate.
// Candidate for DEV; never treats a click or an unscoped price as verification.
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public sealed class AirbnbPriceEvidenceGate(OperationalMemoryStore memory)
{
    private static JsonObject Error(string code, string detail) => new()
    {
        ["status"] = "blocked", ["reason_code"] = code, ["reason"] = detail
    };

    public JsonObject ReconcileExact(string operationId, string evidencePath, string evidenceSha256)
    {
        var state = memory.OperationState(operationId);
        if (state is not ("attempted" or "uncertain"))
            return Error("not_awaiting_verification", "Only attempted or uncertain operations may be verified.");
        var raw = memory.OperationPayload(operationId);
        if (string.IsNullOrWhiteSpace(raw))
            return Error("operation_payload_missing", "Stored operation payload is missing.");
        JsonObject intent;
        try { intent = JsonNode.Parse(raw) as JsonObject ?? throw new FormatException(); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { return Error("invalid_operation_payload", "Stored operation payload is not an object."); }

        if (string.IsNullOrWhiteSpace(evidencePath) || !Path.IsPathFullyQualified(evidencePath))
            return Error("evidence_path_invalid", "Evidence must have an absolute file path.");
        var dbDir = Path.GetDirectoryName(memory.DatabasePath)!;
        var root = Path.GetFileName(dbDir).Equals("memory", StringComparison.OrdinalIgnoreCase)
            ? Directory.GetParent(dbDir)!.FullName : dbDir;
        var capturesRoot = Path.GetFullPath(Path.Combine(root, "captures")) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(evidencePath);
        if (!path.StartsWith(capturesRoot, StringComparison.OrdinalIgnoreCase))
            return Error("evidence_outside_captures", "Evidence must be inside this instance's captures directory.");
        if (!File.Exists(path))
            return Error("evidence_missing", "The referenced evidence file does not exist.");
        var info = new FileInfo(path);
        if (info.Length is < 10 or > 1048576)
            return Error("evidence_size_invalid", "Evidence JSON must be 10 bytes to 1 MiB.");
        if (string.IsNullOrWhiteSpace(evidenceSha256) || evidenceSha256.Length != 64 ||
            evidenceSha256.Any(ch => !Uri.IsHexDigit(ch)))
            return Error("evidence_hash_invalid", "Evidence requires a 64-character SHA256.");
        string actualHash;
        JsonObject proof;
        try
        {
            var bytes = File.ReadAllBytes(path);
            actualHash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!string.Equals(actualHash, evidenceSha256, StringComparison.OrdinalIgnoreCase))
                return Error("evidence_hash_mismatch", "Evidence file bytes differ from the supplied SHA256.");
            proof = JsonNode.Parse(bytes) as JsonObject ?? throw new FormatException();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { return Error("evidence_unreadable", ex.GetType().Name); }

        if (proof["provider"]?.ToString() != "airbnb" ||
            proof["source_kind"]?.ToString() != "webview2_dom" ||
            proof["scope"]?.ToString() != "single_listing_day" ||
            !bool.TryParse(proof["complete"]?.ToString(), out var complete) || !complete)
            return Error("evidence_scope_incomplete", "Evidence is not a complete single-day Airbnb WebView2 observation.");
        foreach (var field in new[] { "account_id", "listing_id", "currency", "rate_plan", "date" })
        {
            if (intent[field] is null || proof[field] is null ||
                !string.Equals(proof[field]?.ToString(), intent[field]?.ToString(), StringComparison.Ordinal))
                return Error("evidence_identity_mismatch", "Evidence does not match intended " + field + ".");
        }
        if (!DateTimeOffset.TryParse(proof["observed_at_utc"]?.ToString(),
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var observedAt))
            return Error("evidence_timestamp_invalid", "Evidence has no parseable observed_at_utc.");
        if (observedAt.Offset != TimeSpan.Zero || observedAt > DateTimeOffset.UtcNow.AddMinutes(2))
            return Error("evidence_timestamp_invalid", "Observation must have a plausible UTC timestamp.");
        var attempts = memory.OperationEvents(operationId).OfType<JsonObject>()
            .Where(x => x["state"]?.ToString() == "attempted")
            .Select(x => DateTimeOffset.TryParse(x["at"]?.ToString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var t) ? (DateTimeOffset?)t : null)
            .Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        if (attempts.Length == 0 || observedAt < attempts[^1])
            return Error("evidence_predates_attempt", "Price observation predates the durable attempted event.");
        if (!decimal.TryParse(proof["price"]?.ToString(), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var observedPrice) || observedPrice <= 0)
            return Error("evidence_price_invalid", "Evidence has no positive numeric price.");
        if (!decimal.TryParse(intent["new_price"]?.ToString(), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var expectedPrice))
            return Error("invalid_operation_payload", "Stored operation has no numeric new_price.");

        var matched = observedPrice == expectedPrice;
        var evidenceRef = path + "#sha256=" + actualHash;
        var updated = memory.MarkOutcome(operationId, matched ? "verified" : "uncertain",
            evidenceRef, null, matched ? "observed_exact_scoped_price" : "price_not_matching");
        return new JsonObject
        {
            ["status"] = updated && matched ? "verified" : "uncertain",
            ["operation_id"] = operationId, ["expected_price"] = expectedPrice,
            ["observed_price"] = observedPrice, ["evidence_ref"] = evidenceRef,
            ["reason_code"] = matched ? "observed_exact_scoped_price" : "price_not_matching"
        };
    }
}
