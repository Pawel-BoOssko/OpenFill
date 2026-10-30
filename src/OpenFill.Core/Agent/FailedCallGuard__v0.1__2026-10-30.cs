// OpenFill SOURCE only: deterministic tool failure guard, 2026-10-30
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public sealed class FailedCallGuard
{
    private readonly Dictionary<string, (int Count, string Error)> _failures = new();
    private static string Key(string name, JsonObject args)
        => name + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(args.ToJsonString())));

    public string? Before(string name, JsonObject args)
    {
        var key = Key(name, args);
        return _failures.TryGetValue(key, out var f) && f.Count >= 2
            ? $"BLOCKED_REPEAT: {name} failed twice with unchanged arguments. Last cause: {f.Error}. Do not repeat the same method; inspect the cause, select another method or finish partial. Clear this failure only after the target state changes."
            : null;
    }

    public void After(string name, JsonObject args, bool success, string output)
    {
        // Successful page movement or a new verified capture may change file/download state.
        if (success && name is "navigate" or "page_capture") _failures.Clear();
        var key = Key(name, args);
        if (success) { _failures.Remove(key); return; }
        var reason = output.Length > 500 ? output[..500] : output;
        if (_failures.TryGetValue(key, out var old) && old.Error == reason)
            _failures[key] = (old.Count + 1, reason);
        else _failures[key] = (1, reason);
    }
}
