// OpenFill - Metadata: wersja 0.1, data 2026-10-28 21:19
using System.Text.Json.Nodes;
using OpenFill.Core.Mcp;

namespace OpenFill.Tests;

internal sealed class ScriptedJevScorer : IJevScorer
{
    public IReadOnlyDictionary<string, double>? Scores { get; set; } = new Dictionary<string, double>();
    public bool Throw { get; set; }
    public int Calls { get; private set; }

    public Task<IReadOnlyDictionary<string, double>> ScoreAsync(string taskText, CancellationToken ct)
    {
        Calls++;
        if (Throw) throw new InvalidOperationException("simulated JEV failure");
        return Task.FromResult(Scores ?? (IReadOnlyDictionary<string, double>)new Dictionary<string, double>());
    }
}

public static class JevGateTests
{
    public static async Task RunAsync()
    {
        await T.Section("OF-247: JEV exact threshold and rejection", async () =>
        {
            var scorer = new ScriptedJevScorer();
            var gate = new JevGate(scorer);
            foreach (var score in new[] { 0d, 0.8499d, 0.85d })
            {
                scorer.Scores = new Dictionary<string, double> { ["p1"] = score };
                var v = await gate.ReviewAsync("sample", default);
                T.Eq("allow at " + score, "allow", v.Status);
                T.Check("CanStart at " + score, v.CanStart);
            }
            foreach (var score in new[] { 0.8501d, 1d })
            {
                scorer.Scores = new Dictionary<string, double> { ["p1"] = score };
                var v = await gate.ReviewAsync("sample", default);
                T.Eq("reject above " + score, "jev_rejected", v.Status);
                T.Check("cannot start above " + score, !v.CanStart);
                T.Eq("placeholder message above " + score, JevGate.RejectionMessage, v.Message);
            }
            scorer.Scores = new Dictionary<string, double> { ["p1"] = 0.2, ["p2"] = 0.95 };
            T.Eq("maximum category determines rejection", "jev_rejected", (await gate.ReviewAsync("sample", default)).Status);
        });
        await T.Section("OF-247: JEV provider and invalid scoring", async () =>
        {
            var scorer = new ScriptedJevScorer();
            var gate = new JevGate(scorer);
            scorer.Scores = new Dictionary<string, double>();
            T.Eq("empty assessment not silently allowed", "jev_unavailable", (await gate.ReviewAsync("sample", default)).Status);
            scorer.Scores = new Dictionary<string, double> { ["p1"] = double.NaN };
            T.Eq("NaN assessment not allowed", "jev_unavailable", (await gate.ReviewAsync("sample", default)).Status);
            scorer.Scores = new Dictionary<string, double> { ["p1"] = 1.01 };
            T.Eq("out of range assessment not allowed", "jev_unavailable", (await gate.ReviewAsync("sample", default)).Status);
            scorer.Throw = true;
            T.Eq("provider failure not interpreted as zero", "jev_unavailable", (await gate.ReviewAsync("sample", default)).Status);
        });
        await T.Section("OF-247: JEV blocks browser before side effects", async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "openfill_jev_" + Guid.NewGuid().ToString("N")[..8]);
            var scorer = new ScriptedJevScorer { Scores = new Dictionary<string, double> { ["p1"] = 0.851 } };
            var fake = new FakeRunHost();
            var manager = new McpTaskManager(fake, dir, TimeSpan.FromSeconds(2), new JevGate(scorer));
            var rejected = await manager.StartAsync("SLOW sample", 0, default, requester: "ChatGPT", chat: "Development");
            T.Eq("rejected before execution", "jev_rejected", rejected["status"]?.GetValue<string>());
            T.Eq("no host start on rejection", 0, fake.Started);
            T.Eq("response addressed to requester", "ChatGPT", rejected["requester"]?.GetValue<string>());
            T.Eq("response addressed to chat", "Development", rejected["chat"]?.GetValue<string>());
            T.Eq("no active task created on rejection", 0, manager.ShortInfo()["working"]!.AsArray().Count);
            scorer.Throw = true;
            var offline = await manager.StartAsync("SLOW another", 0, default, requester: "ChatGPT", chat: "Development");
            T.Eq("unavailable model blocks start", "jev_unavailable", offline["status"]?.GetValue<string>());
            T.Eq("no host start on provider failure", 0, fake.Started);
            scorer.Throw = false;
            scorer.Scores = new Dictionary<string, double> { ["p1"] = 0.85 };
            var allowed = await manager.StartAsync("SLOW example", 0, default, requester: "ChatGPT", chat: "Development");
            T.Eq("accepted task starts host", 1, fake.Started);
            T.Check("accepted task has id", !string.IsNullOrEmpty(allowed["task_id"]?.GetValue<string>()));
            if (allowed["task_id"] is JsonValue key)
                manager.Cancel(key.GetValue<string>());
            try { Directory.Delete(dir, true); } catch { }
        });
    }
}
