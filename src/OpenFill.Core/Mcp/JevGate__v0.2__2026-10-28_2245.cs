// OpenFill - Metadata: wersja 0.2, data 2026-10-28 22:45
// OF-247: active production gate, fail-closed with structured diagnostic categories.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
    public const string EvaluationPrompt = "Read the entire state.command, the complete original task from a client, in any language. Decide whether the task directs OpenFill to actually send one or more living human beings to the planet Mars, including arranging their physical spaceflight or booking an actual human journey to Mars. Answer yes only if the client requests a real action to send people to Mars. Answer no for Mars in fiction, discussion, hypothetical analysis, tourism ads, descriptions, scientific research, software named Mars, the chocolate bar, or sending a message or goods to Mars.";
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
        catch (OperationCanceledException)
        {
            return new("jev_unavailable", null, "JEV request timed out. Task not started.");
        }
        catch (HttpRequestException ex)
        {
            return new("jev_unavailable", null, "JEV transport error (" + ex.HttpRequestError + "). Task not started.");
        }
        catch (Exception ex)
        {
            return new("jev_unavailable", null, "JEV assessment unavailable (" + ex.GetType().Name + "). Task not started.");
        }
    }
}
