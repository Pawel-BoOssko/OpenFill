// OpenFill - Metadata: wersja 0.1, data 2026-10-05 17:09
using System.Text.Json.Nodes;

namespace OpenFill.Core.Cdp;

/// <summary>A connection that knows which browser tab it currently talks to (lets the core set up each tab once).</summary>
public interface ITabAwareConnection
{
    /// <summary>Identifier of the tab the connection is attached to now (null: none).</summary>
    string? ActiveTargetKey { get; }
}

/// <summary>
/// One ICdpConnection that forwards to whichever tab is active now. The core (BrowserController, monitors) subscribes once;
/// the app attaches the connection of the tab a task works in. Events of tabs that are not attached are not forwarded.
/// It does not own the inner connections - whoever created them disposes them.
/// </summary>
public sealed class SwitchableCdpConnection : ICdpConnection, ITabAwareConnection
{
    private readonly object _lock = new();
    private ICdpConnection? _inner;
    private string? _key;
    private bool _disposed;

    public event Action<CdpEvent>? Event;

    public string? ActiveTargetKey { get { lock (_lock) return _key; } }

    public bool IsConnected { get { lock (_lock) return !_disposed && _inner is { IsConnected: true }; } }

    /// <summary>Makes the given connection the active one (the previous one stops delivering events).</summary>
    public void Attach(ICdpConnection inner, string key)
    {
        lock (_lock)
        {
            if (ReferenceEquals(_inner, inner)) { _key = key; return; }
            if (_inner is not null) _inner.Event -= OnInnerEvent;
            _inner = inner;
            _key = key;
            inner.Event += OnInnerEvent;
        }
    }

    /// <summary>Detaches the connection (only if it is the active one), e.g. when its tab is closed.</summary>
    public void Detach(ICdpConnection inner)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(_inner, inner)) return;
            _inner.Event -= OnInnerEvent;
            _inner = null;
            _key = null;
        }
    }

    private void OnInnerEvent(CdpEvent ev)
    {
        Action<CdpEvent>? handler;
        lock (_lock) handler = Event;
        try { handler?.Invoke(ev); } catch { /* a subscriber must not stop the flow */ }
    }

    public Task<JsonNode?> SendAsync(string method, JsonObject? @params = null, string? sessionId = null, CancellationToken ct = default)
    {
        ICdpConnection? inner;
        lock (_lock) inner = _disposed ? null : _inner;
        if (inner is null) throw new CdpException("No browser tab is open.");
        return inner.SendAsync(method, @params, sessionId, ct);
    }

    public ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            _disposed = true;
            if (_inner is not null) _inner.Event -= OnInnerEvent;
            _inner = null;
        }
        return ValueTask.CompletedTask;
    }
}
