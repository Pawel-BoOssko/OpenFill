using System.Text.Json;
using OpenFill.Core.Agent;
using OpenFill.Core.Hosting;
using OpenFill.Core.Logging;
using OpenFill.Core.Mcp;

namespace OpenFill.Tests;

public static class BusinessGoalMeterTests
{
    public static async Task RunAsync()
    {
        await T.Section("Business goal units: deterministic, evidence-aware", () =>
        {
            var five = GoalSpec.Parse("Opublikuj 5 wpisów na Facebooku.");
            T.Eq("five publications kind", "publication", five.Category);
            T.Eq("five publications requested", 5, five.RequestedUnits);
            var invitations = GoalSpec.Parse("Wyślij najwyżej 5 zaproszeń na LinkedIn.");
            T.Eq("five invitations requested", 5, invitations.RequestedUnits);
            var registration = GoalSpec.Parse("Zarejestruj konto w serwisie.");
            T.Eq("registration requested", 1, registration.RequestedUnits);
            var firstRead = GoalSpec.Parse("Otwórz https://example.com i odczytaj tytuł strony.");
            T.Eq("simple page read requested", 1, firstRead.RequestedUnits);
            var onlyRead = GoalSpec.Parse("TYLKO ODCZYT: sprawdź rejestrację na https://example.com i odczytaj tytuł.");
            T.Eq("read-only task is not an account registration", "reading", onlyRead.Category);
            var openResearch = GoalSpec.Parse("Sprawdź cztery portale.");
            T.Check("multi portal research has no made up unit count", openResearch.RequestedUnits is null);
            var (confirmed, evidence) = GoalSpec.ExtractAgentReported(
                "publication", 5, "Opublikowano i zweryfikowano trzy osobne posty.", "success");
            T.Eq("three posts reported", 3, confirmed);
            T.Eq("report is not provider proof", "agent_reports_explicit_count", evidence);
            var (notDone, _) = GoalSpec.ExtractAgentReported(
                "publication", 5, "Przygotowano pięć wersji roboczych.", "partial");
            T.Eq("draft does not count as published", 0, notDone);
            return Task.CompletedTask;
        });
        await T.Section("Business goal usage ledger: provider evidence and cost", () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "of-goalmeter-" + Guid.NewGuid().ToString("N"));
            var logDir = Path.Combine(root, "logs");
            try
            {
                using (var log = new EventLog(logDir, Path.Combine(root, "runs")))
                {
                    var run = log.BeginRun("business-goal-qa");
                    var meter = new BusinessGoalMeter(log, "goal-x", "task-x", null,
                        "Wyślij 5 wiadomości na WhatsApp.");
                    meter.Start(run, "Wyślij 5 wiadomości na WhatsApp.");
                    meter.RecordToolResult("whatsapp_send_batch", true,
                        """{"batch_id":"batch-1","counts":{"verified":3,"uncertain":2}}""");
                    meter.RecordToolResult("whatsapp_send_batch", true,
                        """{"batch_id":"batch-1","counts":{"verified":3,"uncertain":2}}""");
                    T.Eq("only three real provider confirmations", 3, meter.ProviderVerifiedUnits);
                    meter.ReportAgentConclusion("Wysłano pięć wiadomości.", "success");
                    meter.Finish(run, "partial", "model_partial", new UsageTotals {
                        Calls = 7, InputTokens = 1000, CachedTokens = 600,
                        OutputTokens = 200, ReasoningTokens = 75, CostUsd = 0.002 });
                    log.EndRun();
                    var pageRun = log.BeginRun("business-read-qa");
                    var page = new BusinessGoalMeter(log, "goal-read", "task-read", null,
                        "Otwórz https://example.com i odczytaj tytuł strony.");
                    page.Start(pageRun, "Otwórz https://example.com i odczytaj tytuł strony.");
                    page.RecordToolResult("navigate", true, "COMPACT CURRENT VIEW: Example Domain");
                    page.Finish(pageRun, "success", "model_finish", new UsageTotals {
                        Calls = 2, InputTokens = 550, CachedTokens = 200, OutputTokens = 50 });
                    log.EndRun();
                }
                var file = Path.Combine(logDir, "usage_" + DateTime.UtcNow.ToString("yyyyMM") + ".ndjson");
                var events = File.ReadLines(file).Select(line => JsonDocument.Parse(line)).ToArray();
                try
                {
                    var effect = events.Where(x => x.RootElement.GetProperty("kind").GetString() == "business_goal_effect").ToArray();
                    T.Eq("duplicate batch has one effect record", 1, effect.Length);
                    var finished = events.Single(x => x.RootElement.GetProperty("kind").GetString() == "business_goal_finish"
                        && x.RootElement.GetProperty("goalId").GetString() == "goal-x");
                    T.Eq("finish event goal identity", "goal-x", finished.RootElement.GetProperty("goalId").GetString());
                    T.Eq("finish event actual units", 3, finished.RootElement.GetProperty("attributableUnits").GetInt32());
                    T.Eq("finish event input", 1000L, finished.RootElement.GetProperty("inputTokens").GetInt64());
                    T.Eq("finish event output", 200L, finished.RootElement.GetProperty("outputTokens").GetInt64());
                    T.Eq("finish event units are provider verified", "provider_ui", finished.RootElement.GetProperty("attributionEvidence").GetString());
                    var pageEnd = events.Single(x => x.RootElement.GetProperty("kind").GetString() == "business_goal_finish"
                        && x.RootElement.GetProperty("goalId").GetString() == "goal-read");
                    T.Eq("successful real page read is one unit", 1, pageEnd.RootElement.GetProperty("readCompletedUnits").GetInt32());
                    T.Eq("page read evidence is explicitly weaker than provider action", "success_and_live_read",
                        pageEnd.RootElement.GetProperty("attributionEvidence").GetString());
                }
                finally { foreach (var item in events) item.Dispose(); }
            }
            finally { try { Directory.Delete(root, true); } catch { } }
            return Task.CompletedTask;
        });
    }
}
