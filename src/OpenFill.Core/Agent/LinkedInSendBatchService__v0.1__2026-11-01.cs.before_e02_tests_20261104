// OF-IMP-20261031 D02: staging-only orchestration through the durable executor.
// No public MCP or Luna tool registration before logged-in UI acceptance.
using System.Text.Json.Nodes;
using OpenFill.Core.Browser;

namespace OpenFill.Core.Agent;

public sealed record LinkedInSendTarget(string ProfileUrl, string ThreadUrl, string Message);

// Narrow injection point for deterministic batch acceptance without contacting
// real LinkedIn recipients. Production still performs the same WebView2 checks.
public interface ILinkedInBatchPort
{
    Task<bool> VerifySenderAsync(string sender, LinkedInSendTarget first, CancellationToken ct);
    Task<ProcedureActionResult> PreflightAsync(string sender, LinkedInSendTarget item, CancellationToken ct);
    Task<ProcedureActionResult> SendAsync(string operationId, string sender,
        LinkedInSendTarget item, string evidenceDirectory, CancellationToken ct);
}

public sealed class LinkedInBrowserBatchPort(
    BrowserController browser, OperationalMemoryStore memory) : ILinkedInBatchPort
{
    public async Task<bool> VerifySenderAsync(string sender, LinkedInSendTarget first, CancellationToken ct)
    {
        await browser.NavigateAsync(first.ThreadUrl, ct);
        var check = await new LinkedInThreadPreflight(browser).ObserveAndCheckAsync(
            sender, first.ThreadUrl, first.ProfileUrl, ct);
        return check["status"]?.ToString() == "ready";
    }

    public async Task<ProcedureActionResult> PreflightAsync(
        string sender, LinkedInSendTarget item, CancellationToken ct)
    {
        await browser.NavigateAsync(item.ThreadUrl, ct);
        var check = await new LinkedInThreadPreflight(browser).ObserveAndCheckAsync(
            sender, item.ThreadUrl, item.ProfileUrl, ct);
        return check["status"]?.ToString() == "ready"
            ? new ProcedureActionResult("ready")
            : new ProcedureActionResult("blocked", Reason: check["reason_code"]?.ToString());
    }

    public Task<ProcedureActionResult> SendAsync(string operationId, string sender,
        LinkedInSendTarget item, string evidenceDirectory, CancellationToken ct) =>
        new LinkedInCurrentThreadSendAdapter(browser, memory).SendCurrentAsync(
            operationId, sender, item.ThreadUrl, item.ProfileUrl,
            item.Message, evidenceDirectory, ct);
}

public sealed class LinkedInSendBatchService(
    BrowserController browser, OperationalMemoryStore memory, string evidenceDirectory)
{
    private ILinkedInBatchPort _port = new LinkedInBrowserBatchPort(browser, memory);

    public LinkedInSendBatchService(
        ILinkedInBatchPort port, OperationalMemoryStore memory, string evidenceDirectory)
        : this((BrowserController)null!, memory, evidenceDirectory)
    {
        _port = port ?? throw new ArgumentNullException(nameof(port));
    }
    public static string? Validate(string expectedSender, IReadOnlyList<LinkedInSendTarget> items)
    {
        if (!ExactLinkedIn(expectedSender, "/in/")) return "invalid_sender_profile";
        if (items.Count is < 1 or > 50) return "invalid_target_count";
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (!ExactLinkedIn(item.ProfileUrl, "/in/")) return "invalid_target_profile";
            if (!ExactLinkedIn(item.ThreadUrl, "/messaging/thread/")) return "invalid_thread_url";
            if (string.IsNullOrWhiteSpace(item.Message) || item.Message.Length > 5000)
                return "invalid_message_length";
            if (!seen.Add(item.ProfileUrl.TrimEnd('/'))) return "duplicate_target";
        }
        return null;
    }

    private static bool ExactLinkedIn(string? value, string prefix)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps ||
            (url.Host != "linkedin.com" && url.Host != "www.linkedin.com") ||
            !url.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        var id = url.AbsolutePath[prefix.Length..].Trim('/');
        return id.Length > 0 && !id.Contains('/') && !id.Contains('%') &&
            string.IsNullOrEmpty(url.Query) && string.IsNullOrEmpty(url.Fragment);
    }

    public async Task<JsonObject> SendBatchAsync(
        string expectedSenderProfile, string requestId, IReadOnlyList<LinkedInSendTarget> items,
        string requester, string chat, CancellationToken ct = default, bool stagingOnly = false)
    {
        if (!stagingOnly) return new() { ["status"] = "staging_only" };
        var reason = Validate(expectedSenderProfile, items);
        if (reason is not null) return new() { ["status"] = "blocked", ["reason_code"] = reason };
        if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(requester) ||
            string.IsNullOrWhiteSpace(chat))
            return new() { ["status"] = "missing_request_identity" };

        var byProfile = items.ToDictionary(x => x.ProfileUrl, StringComparer.OrdinalIgnoreCase);
        var targets = items.Select(x => new ProcedureTarget(x.ProfileUrl, x.Message)).ToArray();
        var sender = expectedSenderProfile;

        Task<ProcedureActionResult> Preflight(ProcedureTarget target, CancellationToken token) =>
            _port.PreflightAsync(sender, byProfile[target.TargetId], token);

        // An exact authenticated sender in the first thread is mandatory.
        Task<bool> Auth(CancellationToken token) =>
            _port.VerifySenderAsync(sender, items[0], token);

        return await new ProcedureExecutor(ProcedureCatalog.FromEmbedded(Path.Combine(Path.GetDirectoryName(memory.DatabasePath)!, "procedure-learning")), memory).ExecuteBatchAsync(
            "linkedin.send_batch", sender, requestId, targets, Auth,
            (target, token) => Task.FromResult(new ProcedureActionResult(
                "blocked", Reason: "operation_id_required")),
            null, requester, chat, ct, allowCandidateForStaging: true,
            preflight: Preflight,
            actionWithOperation: (target, operationId, token) =>
                _port.SendAsync(operationId, sender, byProfile[target.TargetId],
                    evidenceDirectory, token));
    }
}
