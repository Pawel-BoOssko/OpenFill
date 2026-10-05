// OpenFill - Metadata: wersja 0.3, data 2026-10-05 15:15
using System.Text.Json.Nodes;
using OpenFill.Core.Cdp;

namespace OpenFill.Cli;

/// <summary>
/// After connecting to /devtools/browser, creates or takes over a tab (page target) and returns a ready CDP session.
/// Uses the "flat" mode (flatten=true): one socket serves many sessions through sessionId.
/// </summary>
public static class CdpBootstrap
{
    public static async Task<CdpSession> AttachPageAsync(WebSocketCdpConnection conn, string startUrl, CancellationToken ct = default)
    {
        // New tab with an explicit size (in headless mode the default tab is sometimes tiny, and resizing it
        // through Emulation can crash Chromium in a container). Outside headless the size is ignored.
        var created = await conn.SendAsync("Target.createTarget", new JsonObject { ["url"] = "about:blank", ["width"] = 1280, ["height"] = 900, ["newWindow"] = true }, ct: ct);
        string? targetId = created?["targetId"]?.GetValue<string>();
        if (targetId is null) throw new InvalidOperationException("Could not create a tab.");

        var attached = await conn.SendAsync("Target.attachToTarget", new JsonObject
        {
            ["targetId"] = targetId,
            ["flatten"] = true
        }, ct: ct);
        var sessionId = attached?["sessionId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("No sessionId after attachToTarget.");

        var session = new CdpSession(conn) { SessionId = sessionId };

        // Close the empty start tabs of the browser, so that windowed mode has no useless tabs.
        try
        {
            var targets = await conn.SendAsync("Target.getTargets", ct: ct);
            if (targets?["targetInfos"] is JsonArray infos)
                foreach (var t in infos.OfType<JsonObject>())
                {
                    var id = t["targetId"]?.GetValue<string>();
                    var url = t["url"]?.GetValue<string>() ?? "";
                    if (t["type"]?.GetValue<string>() == "page" && id != targetId &&
                        (url == "about:blank" || url.StartsWith("chrome://newtab") || url.StartsWith("edge://newtab") || url.StartsWith("chrome://new-tab-page")))
                        await conn.SendAsync("Target.closeTarget", new JsonObject { ["targetId"] = id }, ct: ct);
                }
        }
        catch { /* best effort */ }

        // When a specific URL is given (not about:blank), go to it - the taken-over tab
        // may have been sitting on the browser start page.
        if (!string.IsNullOrWhiteSpace(startUrl) && startUrl != "about:blank")
        {
            await session.SendAsync("Page.enable", ct: ct);
            await session.SendAsync("Page.navigate", new JsonObject { ["url"] = startUrl }, ct);
            await Task.Delay(400, ct);
        }

        return session;
    }

    /// <summary>
    /// Headless Chromium can start a tab with a viewport only a few dozen pixels high,
    /// so elements are "not visible" and real clicks land in the void. We force a sensible size.
    /// </summary>
    public static async Task EnsureViewportAsync(CdpSession session, CancellationToken ct = default)
    {
        try
        {
            var h = await session.EvaluateAsync("innerHeight", awaitPromise: false, ct: ct);
            if (h is not null && h.GetValue<double>() < 300)
                await session.SendAsync("Emulation.setDeviceMetricsOverride", new JsonObject
                { ["width"] = 1280, ["height"] = 900, ["deviceScaleFactor"] = 1, ["mobile"] = false }, ct);
        }
        catch { /* best effort */ }
    }

    /// <summary>Opens a new tab with the given address and returns its session (e.g. the panel next to the target page).</summary>
    public static async Task<CdpSession> CreatePageAsync(WebSocketCdpConnection conn, string url, int width = 1280, int height = 900, CancellationToken ct = default)
    {
        var created = await conn.SendAsync("Target.createTarget", new JsonObject { ["url"] = url, ["width"] = width, ["height"] = height, ["newWindow"] = true }, ct: ct);
        var targetId = created?["targetId"]?.GetValue<string>() ?? throw new InvalidOperationException("Could not create a tab.");
        var attached = await conn.SendAsync("Target.attachToTarget", new JsonObject { ["targetId"] = targetId, ["flatten"] = true }, ct: ct);
        var sessionId = attached?["sessionId"]?.GetValue<string>() ?? throw new InvalidOperationException("No sessionId.");
        return new CdpSession(conn) { SessionId = sessionId };
    }
}
