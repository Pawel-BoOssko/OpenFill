// OpenFill - Metadata: wersja 0.1, data 2026-10-28 21:18
// OF-247: decision boundary staged in SOURCE. Not enabled until a real model scorer and prompt are configured.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFill.Core.Mcp;

public interface IJevScorer
{
    Task<IReadOnlyDictionary<string, double>> ScoreAsync(string taskText, CancellationToken cancellationToken);
}

public sealed record JevDecision(string Status, double? Score, string? Message)
{
    public bool CanStart => Status == "allow";

    public JsonObject ToMcpResult(string requester, string chat) => new()
    {
        ["status"] = Status,
        ["requester"] = requester,
        ["chat"] = chat,
        ["score"] = Score,
        ["message"] = Message
    };
}

public sealed class JevGate
{
    public const double RejectAbove = 0.85;
    public const string EvaluationPrompt = "PLACEHOLDER_JEV_EVALUATION_PROMPT";
    public const string RejectionMessage = "PLACEHOLDER_JEV_REJECTION_MESSAGE";

    private readonly IJevScorer _scorer;

    public JevGate(IJevScorer scorer) => _scorer = scorer ?? throw new ArgumentNullException(nameof(scorer));

    public async Task<JevDecision> ReviewAsync(string taskText, CancellationToken ct)
    {
        try
        {
            var scores = await _scorer.ScoreAsync(taskText, ct).ConfigureAwait(false);
            if (scores is null || scores.Count == 0 || scores.Values.Any(
                    v => !double.IsFinite(v) || v < 0 || v > 1))
                return new("jev_unavailable", null, "JEV returned no valid assessment. Task not started.");
            var maximum = scores.Values.Max();
            return maximum > RejectAbove
                ? new("jev_rejected", maximum, RejectionMessage)
                : new("allow", maximum, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new("jev_unavailable", null, "JEV assessment unavailable. Task not started.");
        }
    }
}
