// OF-IMP-20261031 D03: exact per-day Airbnb price plans and independent price reconciliation.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public sealed record AirbnbPriceObservation(DateOnly Date, decimal CurrentPrice, string SourceRef);

public sealed class AirbnbPricePlanner(OperationalMemoryStore memory)
{
    private static JsonObject Error(string status, string detail) => new()
    { ["status"] = status, ["reason"] = detail };

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void Part(StringBuilder sb, string value) =>
        sb.Append(value.Length).Append(':').Append(value);

    // Strict entry point for UI-sourced plans. Invalid or stale captures cannot create ledger entries.
    // The legacy Prepare method is retained for existing planning-only MCP compatibility.
    public JsonObject PrepareFromCaptures(string accountId, string listingId, string currency,
        string ratePlan, string requestId, string requester, string chat,
        DateOnly firstDay, DateOnly lastDay, decimal percentChange, int priceDecimals,
        IReadOnlyList<AirbnbPriceCaptureReference> references)
    {
        var (gate, observations) = new AirbnbPriceSourceEvidenceGate(memory).Validate(
            accountId, listingId, currency, ratePlan, firstDay, lastDay, references);
        if (gate["status"]?.ToString() != "ready") return gate;
        return Prepare(accountId, listingId, currency, ratePlan, requestId, requester,
            chat, firstDay, lastDay, percentChange, priceDecimals, observations);
    }

    public JsonObject Prepare(string accountId, string listingId, string currency, string ratePlan,
        string requestId, string requester, string chat, DateOnly firstDay, DateOnly lastDay,
        decimal percentChange, int priceDecimals, IReadOnlyList<AirbnbPriceObservation> observed)
    {
        if (new[] { accountId, listingId, currency, requestId, requester, chat }.Any(string.IsNullOrWhiteSpace))
            return Error("missing_identity", "account/listing/currency/request/requester/chat required.");
        if (currency.Length != 3 || currency.Any(ch => ch is < 'A' or > 'Z'))
            return Error("invalid_currency", "Currency must be an exact uppercase ISO 4217 code.");
        if (ratePlan.Length > 100)
            return Error("invalid_rate_plan", "ratePlan must have at most 100 characters.");
        if (percentChange is <= -100 or > 1000)
            return Error("invalid_change", "Percent change must be greater than -100 and at most 1000.");
        if (priceDecimals is not (0 or 2))
            return Error("invalid_decimals", "Use priceDecimals=0 or 2, matching the listing's actual price precision.");
        var days = lastDay.DayNumber - firstDay.DayNumber + 1;
        if (days is < 1 or > 366)
            return Error("invalid_range", "Date range must cover 1 to 366 inclusive days.");
        if (observed.Count != days)
            return Error("incomplete_observation", $"Expected {days} daily observations, got {observed.Count}.");
        if (observed.Any(x => x.CurrentPrice <= 0 || x.CurrentPrice > 100000000 || string.IsNullOrWhiteSpace(x.SourceRef)))
            return Error("invalid_observation", "Every day requires a positive observed price and evidence source reference.");
        var byDate = observed.GroupBy(x => x.Date).ToDictionary(g => g.Key, g => g.ToArray());
        if (byDate.Values.Any(x => x.Length != 1) ||
            Enumerable.Range(0, days).Any(i => !byDate.ContainsKey(firstDay.AddDays(i))))
            return Error("incomplete_observation", "Every date must occur exactly once; no gaps or duplicates.");
        var sb = new StringBuilder();
        foreach (var s in new[] { "airbnb.change_price_range", "1.0", accountId, listingId, currency,
                     ratePlan, firstDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                     lastDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                     percentChange.ToString(CultureInfo.InvariantCulture), priceDecimals.ToString(CultureInfo.InvariantCulture) })
            Part(sb, s);
        var planned = new List<(DateOnly Date, JsonObject Item, string Payload)>();
        foreach (var day in Enumerable.Range(0, days).Select(i => firstDay.AddDays(i)))
        {
            var original = byDate[day][0];
            var revised = Math.Round(original.CurrentPrice * (1 + percentChange / 100m),
                priceDecimals, MidpointRounding.AwayFromZero);
            if (revised <= 0 || revised > 100000000)
                return Error("price_out_of_range", "Calculated price is outside the supported positive range.");
            var item = new JsonObject
            {
                ["account_id"] = accountId, ["listing_id"] = listingId, ["rate_plan"] = ratePlan,
                ["currency"] = currency, ["date"] = day.ToString("yyyy-MM-dd"),
                ["old_price"] = original.CurrentPrice, ["new_price"] = revised,
                ["source_ref"] = original.SourceRef
            };
            // Fingerprint includes the material price change but not the incidental capture identifier.
            Part(sb, day.ToString("yyyy-MM-dd"));
            Part(sb, original.CurrentPrice.ToString(CultureInfo.InvariantCulture));
            Part(sb, revised.ToString(CultureInfo.InvariantCulture));
            planned.Add((day, item, item.ToJsonString()));
        }
        var batch = memory.BeginBatch("airbnb", accountId,
            requester + "|" + chat + "|" + requestId, "airbnb.change_price_range", "1.0",
            null, requester, chat);
        if (!memory.BindRequestFingerprint(batch, Hash(sb.ToString())))
            return Error("idempotency_conflict", "Same request_id used with a changed listing, date range or price.");
        var items = new JsonArray();
        foreach (var (date, detail, payload) in planned)
        {
            var targetId = listingId + "|" + ratePlan + "|" + currency + "|" + date.ToString("yyyy-MM-dd");
            var stablePayloadKey = targetId + "|" + detail["old_price"] + "|" + detail["new_price"];
            var op = memory.PlanOperation(batch, targetId, "price_update", Hash(stablePayloadKey), payload);
            detail["operation_id"] = op;
            detail["state"] = memory.OperationState(op);
            items.Add(detail);
        }
        var root = Path.Combine(Path.GetDirectoryName(memory.DatabasePath)!, "plans");
        Directory.CreateDirectory(root);
        var planPath = Path.Combine(root, "airbnb_" + batch + ".json");
        var planText = new JsonObject { ["batch_id"] = batch, ["items"] = items }.ToJsonString();
        var tmp = planPath + ".tmp";
        File.WriteAllText(tmp, planText, new UTF8Encoding(false));
        File.Move(tmp, planPath, true);
        return new JsonObject {
            ["status"] = "planned", ["batch_id"] = batch, ["listing_id"] = listingId,
            ["currency"] = currency, ["rate_plan"] = ratePlan,
            ["days"] = days, ["plan_path"] = planPath, ["plan_sha256"] = Hash(planText),
            ["counts"] = memory.BatchStatus(batch),
            ["preview"] = new JsonArray(items.Take(10).Select(x => x?.DeepClone()).ToArray())
        };
    }

    // Unscoped price+reference cannot prove which listing/day/account was observed.
    // Retained only to fail closed for callers compiled against the old signature.
    [Obsolete("Use ReconcileExact with a scoped, hashed, post-attempt WebView2 capture.")]
    public JsonObject Reconcile(string operationId, decimal observedPrice, string sourceRef) =>
        Error("unscoped_evidence_rejected", "Use ReconcileExact with complete account/listing/day evidence.");

    public JsonObject ReconcileExact(string operationId, string evidencePath, string evidenceSha256) =>
        new AirbnbPriceEvidenceGate(memory).ReconcileExact(operationId, evidencePath, evidenceSha256);
}
