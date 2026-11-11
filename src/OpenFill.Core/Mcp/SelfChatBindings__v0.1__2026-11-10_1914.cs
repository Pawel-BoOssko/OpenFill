using System.Text;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Mcp;

/// <summary>
/// Native chat URL binding for an opaque MCP calling session.
/// Creates a mapping ONLY after the browser agent has returned a machine-readable
/// proof of exact visible fallback marker and one native conversation URL.
/// No guesses from titles, pages open in other tabs or transport session IDs.
/// </summary>
public sealed class SelfChatBindingStore
{
    private readonly string? _path;
    private readonly object _sync = new();

    // Durable data: creation timestamp is preserved as the file is updated.
    public const string FileName =
        "2026-11-10_19-14-00_CET_GPT-6_OpenFill_SelfChat_SessionNativeUrlBindings_AbyUniknacPowtornegoWyszukiwania.json";

    public SelfChatBindingStore(string? operationalDatabasePath)
    {
        if (!string.IsNullOrWhiteSpace(operationalDatabasePath))
            _path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(operationalDatabasePath))!, FileName);
    }

    public bool TryGet(string? sessionFingerprint, string platform, out string? nativeId)
    {
        nativeId = null;
        if (!IsKeyValid(sessionFingerprint, platform) || _path is null) return false;
        lock (_sync)
        {
            var root = ReadOrEmpty();
            var entry = root["bindings"]?[platform + ":" + sessionFingerprint] as JsonObject;
            if (entry is null) return false;
            var value = entry["native_chat_id"]?.GetValue<string>();
            if (Guid.TryParse(value, out var parsed) && value?.Length == 36)
            {
                nativeId = parsed.ToString("D");
                return true;
            }
            return false;
        }
    }

    public static string? CandidateNativeId(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary)) return null;
        const string prefix = "OPENFILL_SELF_CHAT_RESULT:";
        var index = summary.LastIndexOf(prefix, StringComparison.Ordinal);
        if (index < 0) return null;
        var line = summary[(index + prefix.Length)..].TrimStart();
        var end = line.IndexOfAny(new[] { '\r', '\n' });
        if (end >= 0) line = line[..end];
        if (line.Length is < 20 or > 8192) return null;
        try
        {
            var proof = JsonNode.Parse(line) as JsonObject;
            var candidate = proof?["native_chat_id"]?.GetValue<string>();
            return Guid.TryParse(candidate, out var parsed) && candidate?.Length == 36
                ? parsed.ToString("D") : null;
        }
        catch { return null; }
    }

    public bool TryRecord(SelfChatSteering.Context context, string? summary, string? outcome,
                          bool independentlyVerified = false)
    {
        // A malformed model summary or an unavailable cache file must not turn an
        // already completed browser task into an MCP exception.
        try { return independentlyVerified && TryRecordCore(context, summary, outcome); }
        catch { return false; }
    }

    private bool TryRecordCore(SelfChatSteering.Context context, string? summary, string? outcome)
    {
        if (_path is null || outcome != "success" ||
            !IsKeyValid(context.SelfSessionHash, context.Platform) ||
            context.FallbackId is null || string.IsNullOrWhiteSpace(summary))
            return false;

        // The marker itself is never treated as an instruction. The agent must
        // have actually matched the visible token in the identified conversation.
        const string prefix = "OPENFILL_SELF_CHAT_RESULT:";
        var index = summary.LastIndexOf(prefix, StringComparison.Ordinal);
        if (index < 0) return false;
        var line = summary[(index + prefix.Length)..].TrimStart();
        var end = line.IndexOfAny(new[] { '\r', '\n' });
        if (end >= 0) line = line[..end];
        if (line.Length is < 20 or > 8192) return false;
        JsonObject? proof;
        try { proof = JsonNode.Parse(line) as JsonObject; }
        catch { return false; }
        if (proof is null) return false;
        bool True(string field) => proof[field] is JsonValue v &&
            v.TryGetValue<bool>(out var flag) && flag;
        string? Field(string field) => proof[field]?.GetValue<string>();

        var platform = Field("platform");
        var id = Field("native_chat_id");
        var url = Field("verified_url");
        var marker = Field("fallback_id");
        var status = Field("status");

        if (status != "ok" || platform != context.Platform ||
            marker != context.FallbackId || !True("fallback_visible") ||
            !True("same_conversation") || !True("account_verified") ||
            !Guid.TryParse(id, out var parsed) || id?.Length != 36 ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // The ChatGPT native ID is a GUID in /c/. Another domain or path fails.
        var expected = context.Platform == "chatgpt"
            ? "https://chatgpt.com/c/" + parsed.ToString("D")
            : "https://claude.ai/chat/" + parsed.ToString("D");
        if (!string.Equals(uri.GetLeftPart(UriPartial.Path).TrimEnd('/'), expected,
                           StringComparison.OrdinalIgnoreCase))
            return false;

        lock (_sync)
        {
            var data = ReadOrEmpty();
            var records = data["bindings"] as JsonObject;
            if (records is null)
            {
                records = new JsonObject();
                data["bindings"] = records;
            }
            records[context.Platform + ":" + context.SelfSessionHash] = new JsonObject
            {
                ["platform"] = context.Platform,
                ["native_chat_id"] = parsed.ToString("D"),
                ["verified_url"] = expected,
                ["last_verified_utc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["proof"] = "exact_visible_fallback_marker",
                ["fallback_id_sha256"] = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        Encoding.UTF8.GetBytes(marker))).ToLowerInvariant()
            };
            var tmp = _path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(tmp, data.ToJsonString(new System.Text.Json.JsonSerializerOptions
                { WriteIndented = true }), new UTF8Encoding(false));
            File.Move(tmp, _path, overwrite:true);
            return true;
        }
    }

    private JsonObject ReadOrEmpty()
    {
        if (_path is not null && File.Exists(_path))
        {
            try
            {
                var parsed = JsonNode.Parse(File.ReadAllText(_path, Encoding.UTF8)) as JsonObject;
                if (parsed is not null && parsed["schema"]?.ToString() == "openfill.self-chat.bindings.v1")
                    return parsed;
            }
            catch { /* Missing/corrupt cache means fail closed: never select a chat. */ }
        }
        return new JsonObject
        {
            ["schema"] = "openfill.self-chat.bindings.v1",
            ["bindings"] = new JsonObject()
        };
    }

    private static bool IsKeyValid(string? sessionFingerprint, string platform) =>
        !string.IsNullOrWhiteSpace(sessionFingerprint) &&
        sessionFingerprint.Length == 16 &&
        sessionFingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') &&
        platform is "chatgpt" or "claude";
}
