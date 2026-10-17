// OpenFill - Metadata: wersja 0.3, data 2026-10-05 15:15
using System.Text.Json.Nodes;
using OpenFill.Core.Model;

namespace OpenFill.Cli;

/// <summary>
/// A scripted "model" imitating the real agent, without a network. Used to test the whole loop:
/// it reads the current state (the last function_call_output) and, in a simple automaton: get_page -> fill in -> finish.
/// It understands only as much as needed to walk the happy path on the test page.
/// </summary>
public sealed class MockModelClient(bool askConfirm = false) : IModelClient
{
    private int _phase;

    public Task<JsonObject> CreateResponseAsync(JsonObject request, CancellationToken ct = default)
    {
        var input = request["input"] as JsonArray ?? new JsonArray();
        var lastOutput = input.OfType<JsonObject>().LastOrDefault(o => o["type"]?.GetValue<string>() == "function_call_output");
        var lastText = lastOutput?["output"]?.GetValue<string>() ?? "";

        JsonObject call;
        switch (_phase)
        {
            case 0:
                call = FunctionCall("c1", "get_page", new JsonObject());
                _phase = 1;
                break;
            case 1:
                // Based on the snapshot, pick the first text field and type a value, then finish.
                var firstId = FindFirstFieldId(lastText);
                if (firstId is null) { _phase = 3; call = FunctionCall("c_end", "finish", new JsonObject { ["status"] = "partial", ["summary"] = "No fields to fill found." }); }
                else
                {
                    call = FunctionCall("c2", "act", new JsonObject { ["id"] = firstId, ["action"] = "set", ["value"] = "OpenFill test" });
                    _phase = 2;
                }
                break;
            case 2 when askConfirm:
                call = FunctionCall("c2b", "confirm_irreversible", new JsonObject { ["what"] = "Saving the test form (mock)." });
                _phase = 4;
                break;
            case 2:
                call = FunctionCall("c3", "finish", new JsonObject { ["status"] = "success", ["summary"] = "Filled the test field (mock)." });
                _phase = 3;
                break;
            case 4:
                var agreed = lastText.Contains("confirmed", StringComparison.OrdinalIgnoreCase) && !lastText.Contains("NOT", StringComparison.Ordinal);
                call = FunctionCall("c4", "finish", agreed
                    ? new JsonObject { ["status"] = "success", ["summary"] = "Filled the test field and got permission (mock)." }
                    : new JsonObject { ["status"] = "partial", ["summary"] = "The user did not give permission (mock)." });
                _phase = 3;
                break;
            default:
                return Task.FromResult(TextResponse("Gotowe."));
        }

        return Task.FromResult(new JsonObject
        {
            ["id"] = "resp_mock_" + _phase,
            ["output"] = new JsonArray(call)
        });
    }

    private static string? FindFirstFieldId(string pageText)
    {
        // Looks for the pattern "[ofN] <type>" with a text type.
        foreach (var line in pageText.Split('\n'))
        {
            var t = line.Trim();
            var m = System.Text.RegularExpressions.Regex.Match(t, @"\[(of\d+)\]\s+(text|textarea|email|search|tel|url)");
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    private static JsonObject FunctionCall(string callId, string name, JsonObject args) => new()
    {
        ["type"] = "function_call",
        ["id"] = "fc_" + callId,
        ["call_id"] = callId,
        ["name"] = name,
        ["arguments"] = args.ToJsonString()
    };

    private static JsonObject TextResponse(string text) => new()
    {
        ["id"] = "resp_text",
        ["output"] = new JsonArray(new JsonObject
        {
            ["type"] = "message",
            ["role"] = "assistant",
            ["content"] = new JsonArray(new JsonObject { ["type"] = "output_text", ["text"] = text })
        })
    };
}
