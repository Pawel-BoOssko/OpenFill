using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenFill.Core.Agent;
using OpenFill.Core.Logging;
using OpenFill.Core.Mcp;

namespace OpenFill.Core.Hosting;

/// <summary>
/// Token-free telemetry: counts an end-user goal and externally evidenced actions,
/// not model turns. EventLog.WriteUsage provides append-only, crash-readable records.
/// A model's written claim is never represented as an independent provider proof.
/// </summary>
public sealed class BusinessGoalMeter
{
    private readonly EventLog _log;
    private readonly string _goalId;
    private readonly string _taskKey;
    private readonly string? _parentTaskKey;
    private readonly GoalSpec _spec;
    private readonly HashSet<string> _verifiedRecipients = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _verifiedBatches = new(StringComparer.Ordinal);
    private int _modelReportedUnits;
    private int _successfulPageReads;
    private string _modelReportBasis = "none";

    public string GoalId => _goalId;
    public GoalSpec Spec => _spec;
    public int ProviderVerifiedUnits => Math.Max(_verifiedRecipients.Count, _verifiedBatches.Values.Sum());

    public BusinessGoalMeter(EventLog log, string goalId, string taskKey, string? parentTaskKey, string originalGoal)
    {
        _log = log;
        _taskKey = taskKey;
        _parentTaskKey = parentTaskKey;
        _goalId = string.IsNullOrWhiteSpace(goalId) ? taskKey : goalId;
        _spec = GoalSpec.Parse(originalGoal);
    }

    public void Start(string runId, string originalGoal)
    {
        _log.WriteUsage(new
        {
            ts = DateTime.UtcNow, kind = "business_goal_start", runId,
            goalId = _goalId, taskKey = _taskKey, continuedFrom = _parentTaskKey,
            goalType = _spec.Category, requestedUnits = _spec.RequestedUnits,
            unitName = _spec.UnitName, goalText = originalGoal,
            evidencePolicy = "provider_verified_or_agent_reported_separately"
        });
    }

    public void RecordVerified(VerifiedAction action)
    {
        if (string.IsNullOrWhiteSpace(action.Recipient)) return;
        if (!_verifiedRecipients.Add(action.Recipient.Trim())) return;
        _log.WriteUsage(new
        {
            ts = DateTime.UtcNow, kind = "business_goal_effect",
            runId = _log.CurrentRunId, goalId = _goalId, taskKey = _taskKey,
            evidence = "recipient_verified_in_current_ui",
            unitName = "message_or_invitation", units = 1,
            actionKey = "recipient:" + action.Recipient.Trim(),
            page = action.Url, confirmedUtc = action.ConfirmedUtc
        });
    }
    public void RecordToolResult(string toolName, bool ok, string output)
    {
        if (!ok) return;
        if (toolName is "get_page" or "read_text" or "read_view" or "page_capture" or "view_digest" or "navigate")
        {
            _successfulPageReads++;
            return;
        }
        if (toolName is not ("whatsapp_send_batch" or "linkedin_send_batch")) return;
        try
        {
            var json = JsonNode.Parse(output);
            var batch = json?["batch_id"]?.ToString();
            var countNode = json?["counts"]?["verified"];
            if (string.IsNullOrWhiteSpace(batch) || countNode is null ||
                !int.TryParse(countNode.ToString(), out var verified) || verified < 1)
                return;
            var key = toolName + ":" + batch;
            if (!_verifiedBatches.TryAdd(key, verified)) return;
            _log.WriteUsage(new
            {
                ts = DateTime.UtcNow, kind = "business_goal_effect",
                runId = _log.CurrentRunId, goalId = _goalId, taskKey = _taskKey,
                evidence = "provider_batch_ui_verified", unitName = toolName,
                units = verified, actionKey = key
            });
        }
        catch (System.Text.Json.JsonException) { }
        catch (InvalidOperationException) { }
    }

    public void ReportAgentConclusion(string summary, string outcome)
    {
        (_modelReportedUnits, _modelReportBasis) = GoalSpec.ExtractAgentReported(
            _spec.Category, _spec.RequestedUnits, summary, outcome);
    }

    public void Finish(string runId, string outcome, string? stopReason, UsageTotals usage)
    {
        // Different evidence routes may describe the same sent action. Never sum
        // provider batch counts with report_verified_action recipient counts.
        var verified = ProviderVerifiedUnits;
        var readCompleted = _spec.Category == "reading" && _spec.RequestedUnits == 1 &&
            outcome == "success" && _successfulPageReads > 0 ? 1 : 0;
        _log.WriteUsage(new
        {
            ts = DateTime.UtcNow, kind = "business_goal_finish",
            goalId = _goalId, taskKey = _taskKey, runId,
            continuedFrom = _parentTaskKey, goalType = _spec.Category,
            unitName = _spec.UnitName, requestedUnits = _spec.RequestedUnits,
            providerVerifiedUnits = verified,
            successfulPageReads = _successfulPageReads,
            readCompletedUnits = readCompleted,
            agentReportedUnits = _modelReportedUnits,
            agentReportBasis = _modelReportBasis,
            attributableUnits = verified > 0 ? verified : readCompleted > 0 ? readCompleted : _modelReportedUnits,
            attributionEvidence = verified > 0 ? "provider_ui" : readCompleted > 0 ? "success_and_live_read" :
                _modelReportedUnits > 0 ? "agent_summary" : "not_confirmed",
            outcome, stopReason, calls = usage.Calls,
            inputTokens = usage.InputTokens, cachedTokens = usage.CachedTokens,
            outputTokens = usage.OutputTokens, reasoningTokens = usage.ReasoningTokens,
            costUsd = usage.CostUsd
        });
    }
}

public sealed record GoalSpec(string Category, string UnitName, int? RequestedUnits)
{
    private static readonly RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static bool Has(string text, string fragment) =>
        text.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    public static GoalSpec Parse(string? goal)
    {
        var text = goal ?? "";
        var lead = text.TrimStart();
        if (lead.StartsWith("TYLKO ODCZYT", StringComparison.OrdinalIgnoreCase) ||
            lead.StartsWith("READ-ONLY", StringComparison.OrdinalIgnoreCase) ||
            lead.StartsWith("READ ONLY", StringComparison.OrdinalIgnoreCase))
            return new GoalSpec("reading", "page_or_finding", FindExplicitCount(text, "reading"));
        var category = "other";
        var unit = "result";
        if ((Has(text, "zaprosze") || Has(text, "invitation")) &&
            (Has(text, "wyślij") || Has(text, "send") || Has(text, "invite")))
        { category = "invitation"; unit = "invitation"; }
        else if ((Has(text, "wiadomoś") || Has(text, "message") || Has(text, "sms")) &&
                 (Has(text, "wyślij") || Has(text, "send") || Has(text, "odpowiedz")))
        { category = "message"; unit = "message"; }
        else if (Has(text, "rejestracj") || Has(text, "zarejestruj") ||
                 Has(text, "register account") || Has(text, "create account") ||
                 Has(text, "sign up") || Has(text, "załóż konto"))
        { category = "registration"; unit = "account"; }
        else if ((Has(text, "opublikuj") || Has(text, "publikuj") ||
                  Has(text, "publish") || Has(text, "post ")) &&
                 (Has(text, "wpis") || Has(text, "post") || Has(text, "reel") ||
                  Has(text, "komentarz") || Has(text, "comment") || Has(text, "answer")))
        { category = "publication"; unit = "publication"; }
        else if (Has(text, "odczytaj") || Has(text, "otwórz") ||
                 Has(text, "read ") || Has(text, "open ") || Has(text, "sprawdź"))
        { category = "reading"; unit = "page_or_finding"; }
        var requested = category == "registration" ? 1 : FindExplicitCount(text, category);
        return new GoalSpec(category, unit, requested);
    }
    private static int? FindExplicitCount(string text, string category)
    {
        if (category == "reading")
        {
            var urls = Regex.Matches(text, @"https?://[^\s<>\x22]+", Opt);
            return urls.Count == 1 &&
                (Has(text, "title") || Has(text, "tytuł") || Has(text, "nagłówek") ||
                 Has(text, "heading") || Has(text, "akapit")) ? 1 : null;
        }
        string noun = category switch
        {
            "publication" => @"(?:wpis\w*|post\w*|komentarz\w*|comment\w*|reel\w*|answer\w*)",
            "invitation" => @"(?:zaprosze\w*|invitation\w*|contact\w*)",
            "message" => @"(?:wiadomo\w*|message\w*|sms\w*)",
            _ => @"(?!)"
        };
        var m = Regex.Match(text, @"\b(\d{1,3})\s+(?:nowych\s+)?" + noun, Opt,
            TimeSpan.FromMilliseconds(60));
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n is >= 1 and <= 100)
            return n;
        m = Regex.Match(text,
            @"\b(?:najwyżej|maksymalnie|łącznie najwyżej|up to|at most)\s+(\d{1,3})\b", Opt,
            TimeSpan.FromMilliseconds(60));
        if (m.Success && int.TryParse(m.Groups[1].Value, out n) && n is >= 1 and <= 100)
            return n;
        foreach (var (word, value) in new[] {
            ("pięć",5), ("five",5), ("cztery",4), ("four",4), ("trzy",3),
            ("three",3), ("dwie",2), ("dwa",2), ("two",2), ("jedną",1), ("one",1)})
        {
            if (Regex.IsMatch(text, @"\b" + word + @"\s+" + noun, Opt,
                    TimeSpan.FromMilliseconds(60))) return value;
        }
        return category == "message" && (Has(text, "jedną") || Has(text, "one message")) ? 1 : null;
    }

    public static (int Units, string Evidence) ExtractAgentReported(
        string kind, int? requested, string? summary, string outcome)
    {
        if (string.IsNullOrWhiteSpace(summary)) return (0, "no_summary");
        if (kind == "registration")
        {
            var ok = Has(summary, "rejestracja powiodła się") ||
                     Has(summary, "konto aktywne") ||
                     Has(summary, "profil jest już aktywny") ||
                     Has(summary, "registration successful") ||
                     Has(summary, "account is active");
            return ok ? (1, "agent_reports_active_registration") : (0, "unconfirmed");
        }
        if (kind == "message" && requested == 1 && outcome == "success")
        {
            var ok = Has(summary, "wysłano") || Has(summary, "sent exactly one") ||
                     Has(summary, "sent the single") || Has(summary, "delivered");
            return ok ? (1, "agent_reports_message") : (0, "unconfirmed");
        }
        if (kind is not ("publication" or "invitation")) return (0, "no_count_rule");
        var noun = kind == "publication" ?
            @"(?:post\w*|wpis\w*|reel\w*|answer\w*|odpowied\w*)" :
            @"(?:zaprosze\w*|invitation\w*)";
        var m = Regex.Match(summary,
            @"(?:published|opublikowano|opublikowałem|wysłano|sent)\s+" +
            @"(?:and\s+verified\s+|i\s+zweryfikowano\s+|all\s+|wszystkie\s+)?" +
            @"(\d{1,3}|three|trzy|five|pięć|two|dwa|dwie|four|cztery)\s+" +
            @"(?:.{0,24}?)" + noun, Opt, TimeSpan.FromMilliseconds(60));
        if (m.Success)
        {
            var value = m.Groups[1].Value.ToLowerInvariant();
            var count = int.TryParse(value, out var v) ? v : value switch {
                "three" or "trzy" => 3, "five" or "pięć" => 5,
                "four" or "cztery" => 4, "two" or "dwie" or "dwa" => 2, _ => 0 };
            if (count > 0 && count <= 100 && (!requested.HasValue || count <= requested))
                return (count, "agent_reports_explicit_count");
        }
        if (kind == "publication" && outcome == "success" &&
            (Has(summary, "reel opublikowany") || Has(summary, "opublikowano reel") ||
             Has(summary, "published one post")))
            return (1, "agent_reports_single_post");
        return (0, "unconfirmed");
    }
}
