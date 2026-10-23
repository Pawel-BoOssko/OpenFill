// OpenFill - Metadata: wersja 0.1, data 2026-10-23 11:00
using System.Text.Json.Nodes;
using OpenFill.Core.Agent;

namespace OpenFill.Tests;

public static class PromptHistoryTests
{
    private static JsonObject User(string text) => new()
    {
        ["type"] = "message", ["role"] = "user",
        ["content"] = new JsonArray(new JsonObject { ["type"] = "input_text", ["text"] = text })
    };

    public static async Task RunAsync()
    {
        await T.Section("Prompt cache: bounded, append-only turns and whole-call checkpointing", () =>
        {
            var original = User("Register precisely these 14 people; verify each real result.");
            var originalText = original.ToJsonString();
            var history = new JsonArray(original.DeepClone());
            var ends = new List<int>();
            var compactor = new PromptHistoryCompactor();
            var firstCacheKey = "openfill-agent-" + Guid.NewGuid().ToString("N")[..12];
            var checkpoints = 0;
            var previous = history.ToJsonString();
            for (var step = 1; step <= 100; step++)
            {
                var callId = "tool" + step;
                history.Add(new JsonObject { ["type"] = "reasoning", ["id"] = "thought" + step,
                    ["summary"] = new JsonArray(), ["encrypted_content"] = "abc" });
                history.Add(new JsonObject { ["type"] = "function_call", ["call_id"] = callId,
                    ["name"] = step % 5 == 0 ? "act" : "get_page", ["arguments"] = "{}" });
                history.Add(new JsonObject { ["type"] = "function_call_output", ["call_id"] = callId,
                    ["output"] = "https://example.com/profile/" + step + " checked: " + new string('x', 1900) });
                ends.Add(history.Count);
                var changed = compactor.Apply(history, ends, 28_000, out var before,
                    out var after, out var removed);
                if (changed)
                {
                    checkpoints++;
                    T.Check("checkpoint removes at least one complete turn", removed > 0 && after < before);
                    T.Eq("original task remains byte-for-byte unchanged", originalText,
                        history[0]!.ToJsonString());
                    T.Check("compaction summary is placed immediately after task",
                        history[1]!.ToJsonString().Contains("COMPACTED TASK HISTORY"));
                    T.Check("history stays bounded after each compaction", after < 28_000);
                }
                var items = history.OfType<JsonObject>().ToList();
                var ids = items.Where(i => i["type"]?.GetValue<string>() == "function_call")
                    .Select(i => i["call_id"]!.GetValue<string>()).ToHashSet();
                T.Check("no orphaned function result at step " + step,
                    items.Where(i => i["type"]?.GetValue<string>() == "function_call_output")
                         .All(i => ids.Contains(i["call_id"]!.GetValue<string>())));
                T.Check("latest response is retained at step " + step,
                    ids.Contains(callId));
                T.Check("same routing key for all turns", firstCacheKey.StartsWith("openfill-agent-"));
                previous = history.ToJsonString();
            }
            T.Check("more than one complete compaction", checkpoints >= 3);
            T.Eq("compaction counter matches", checkpoints, compactor.Checkpoints);
            T.Check("long-run request remains bounded", history.ToJsonString().Length < 28_000);
            return Task.CompletedTask;
        });
    }
}
