using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenFill.Core.Mcp;

/// <summary>
/// Self only: the stable OpenAI session token can distinguish MCP callers but does
/// not provide their native chat URL. The only fallback lookup key is one plain
/// alphanumeric token that the caller MUST print visibly in its own conversation.
/// </summary>
public static class SelfChatSteering
{
    public sealed record Context(
        string Platform, string? NativeChatId, string? AccountHint,
        string IdentitySource, string? FallbackId, string? SelfSessionHash)
    {
        public bool HasNativeId => NativeChatId is not null;
        public bool HasFallbackId => FallbackId is not null;
    }

    public static Context Resolve(IReadOnlyDictionary<string, string?> headers, JsonObject? args) =>
        Resolve(headers, args, null);

    public static Context Resolve(
        IReadOnlyDictionary<string, string?> headers, JsonObject? args, JsonObject? requestMeta)
    {
        static string? Clean(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();
        headers.TryGetValue("X-OpenFill-Self-Platform", out var headerPlatform);
        headers.TryGetValue("X-OpenFill-Self-Conversation-Id", out var native);
        headers.TryGetValue("X-OpenFill-Self-Account", out var account);
        var rawSession = Clean(requestMeta?["openai/session"]?.GetValue<string>());
        if (rawSession is { Length: > 300 }) rawSession = null;
        var sessionHash = rawSession is null ? null :
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawSession)))[..16].ToLowerInvariant();

        var platform = (Clean(headerPlatform) ?? Clean(args?["platform"]?.GetValue<string>())
            ?? (sessionHash is not null ? "chatgpt" : "")).ToLowerInvariant();
        platform = platform switch
        {
            "openai" or "chatgpt" => "chatgpt",
            "claude" or "anthropic" => "claude",
            _ => ""
        };
        var nativeId = Clean(native);
        if (!Guid.TryParse(nativeId, out var parsed) || nativeId?.Length != 36)
            nativeId = null;
        else
            nativeId = parsed.ToString("D");

        var fallback = Clean(args?["fallback_id"]?.GetValue<string>());
        if (!ValidFallback(fallback)) fallback = null;
        return new Context(platform, nativeId, Clean(account),
            sessionHash is not null ? "openai/session" :
                nativeId is not null ? "http_header_unverified" : "none",
            fallback, sessionHash);
    }

    public static Context Resolve(HttpListenerRequest req, JsonObject? args, JsonObject? requestMeta = null)
    {
        var h = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (string? key in req.Headers.AllKeys)
            if (key is not null) h[key] = req.Headers[key];
        return Resolve(h, args, requestMeta);
    }

    // One search-friendly identifier: 20-32 ASCII letters/digits, no spaces or punctuation.
    public static bool ValidFallback(string? value) =>
        value is { Length: >= 20 and <= 32 } &&
        Regex.IsMatch(value, @"^[A-Za-z0-9]+$", RegexOptions.CultureInvariant);

    public static JsonObject IdentityStatus(Context ctx)
    {
        if (ctx.Platform.Length == 0)
            return Error("platform_unknown", "Platform unavailable: chatgpt or claude.");

        if (ctx.SelfSessionHash is not null)
            return new JsonObject
            {
                ["status"] = "transport_identity_available",
                ["platform"] = ctx.Platform,
                ["self_chat_id"] = "of-chat-" + ctx.SelfSessionHash,
                ["identity_source"] = "tools/call params._meta.openai/session",
                ["native_chat_id"] = ctx.NativeChatId,
                ["native_url_bound"] = false,
                ["message"] = "Stable per-chat MCP session token is available, but its value is not the UUID used in chatgpt.com/c/ URL."
            };

        if (ctx.NativeChatId is not null)
            return new JsonObject
            {
                ["status"] = "native_id_claim_unverified",
                ["platform"] = ctx.Platform,
                ["native_chat_id"] = ctx.NativeChatId,
                ["native_url_bound"] = false
            };

        return Error("identity_unavailable",
            "No native URL ID in MCP metadata. For browser lookup generate one random 20-32 character alphanumeric fallback_id, DISPLAY IT visibly in this chat, then call again.");
    }

    public static JsonObject? ValidateAction(Context ctx, string action, JsonObject? args)
    {
        if (ctx.Platform.Length == 0)
            return Error("platform_unknown", "Supported platforms: chatgpt, claude.");

        // Client-supplied native headers are not independently authenticated.
        // Until a verified server-side binding exists, every UI action requires the
        // printed fallback marker as an independent self-chat assertion.
        if (!ctx.HasFallbackId)
            return Error("fallback_id_required",
                "Generate and PRINT a random 20-32 character alphanumeric fallback_id as a visible message in this very chat BEFORE calling this tool. Pass exactly the same identifier in fallback_id. No page action was started.");

        if (action == "rename")
        {
            var name = args?["new_name"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || name.Any(char.IsControl))
                return Error("invalid_name", "new_name must have 1-120 printable characters.");
        }
        if (action == "move_project")
        {
            var project = args?["project_name"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(project) || project.Length > 150 || project.Any(char.IsControl))
                return Error("invalid_project", "project_name must have 1-150 printable characters.");
        }
        return null;
    }

    /// <summary>
    /// One navigation + one meaningful DOM read for direct URLs; otherwise one
    /// search of the exact marker, one navigation, one read. No prose/semantic search.
    /// </summary>
    public static string BuildBrowserTask(Context ctx, string action, JsonObject? args)
    {
        if (ctx.Platform.Length == 0 || !ctx.HasFallbackId)
            throw new InvalidOperationException("Self-chat lacks platform or exact fallback identifier.");
        var url = ctx.NativeChatId is null ? null : ctx.Platform == "chatgpt"
            ? "https://chatgpt.com/c/" + ctx.NativeChatId
            : "https://claude.ai/chat/" + ctx.NativeChatId;
        var actionText = action switch
        {
            "identity" => "Read the native ID and full URL, without modifying the chat.",
            "name" => "Read the current chat title, without changing it.",
            "rename" => "Set the chat title to " + JsonSerializer.Serialize(args?["new_name"]?.GetValue<string>()?.Trim()) + ".",
            "archive" => "Archive the matched chat.",
            "move_project" => "Move the matched chat to the unique project named " +
                JsonSerializer.Serialize(args?["project_name"]?.GetValue<string>()?.Trim()) + ".",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        var route = url is not null
            ? "FAST PATH: Go directly to " + url + " and verify the exact marker is visible in that conversation."
            : ctx.Platform == "chatgpt"
                ? "FALLBACK: Open https://chatgpt.com; use Search chats ONCE with the exact marker, open the single result, read its URL and verify the marker."
                : "FALLBACK: Open https://claude.ai; use the built-in chat search ONCE with the exact marker, open the unique result, read its URL and verify the marker.";

        return $"""
            OPENFILL SELF-CHAT OPERATION {action}, platform={ctx.Platform}.
            {route}
            Exact literal alphanumeric marker printed in THIS invoking conversation: {ctx.FallbackId}
            MCP caller session fingerprint, not URL ID: {ctx.SelfSessionHash ?? "not supplied"}
            Account hint if any: {JsonSerializer.Serialize(ctx.AccountHint)}
            ACTION AFTER IDENTIFICATION ONLY: {actionText}

            Prefer deterministic browser actions and one combined read of title, URL, marker, account;
            keep the number of inner-model calls minimal. Wait for actual DOM readiness once instead
            of reloading repeatedly. Search only the exact marker, NEVER long message fragments.
            If zero or multiple matches, account mismatch, or marker is not visible in opened chat,
            stop and return precise failure. The invoking chat's displayed marker MUST match exactly.
            Before mutation verify the URL and marker belong to that same chat, never select by title.
            After mutation, read the same URL once to confirm the requested result.
            Return precise native_chat_id, full chat URL, title, action status and before/after.
            Do not claim completion if merely queued or not verified.
            """;
    }

    public static string TaskChatLabel(Context ctx)
    {
        var key = ctx.Platform + "|" + (ctx.SelfSessionHash ?? ctx.FallbackId ?? ctx.NativeChatId ?? "");
        return "self-chat-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..12].ToLowerInvariant();
    }

    public static JsonObject Error(string status, string reason) => new()
    {
        ["status"] = status, ["changed"] = false, ["verified"] = false, ["message"] = reason
    };
}
