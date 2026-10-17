// OpenFill - Metadata: wersja 0.3, data 2026-10-13 10:35
using System.Text.Json.Nodes;

namespace OpenFill.Core.Cdp;

/// <summary>A connection that knows which browser tab it currently talks to (lets the core set up each tab once).</summary>
public interface ITabAwareConnection
{
    /// <summary>Identifier of the page the connection is attached to now (null: none). A pop-up window has its own identifier.</summary>
    string? ActiveTargetKey { get; }
}

/// <summary>A task's page may have a pop-up window (a sign-in window) open; the task can work in either of them.</summary>
public interface IWindowSwitch
{
    bool HasPopup { get; }
    /// <summary>True while the connection talks to the pop-up.</summary>
    bool OnPopup { get; }
    /// <summary>Switches to the pop-up (true) or back to the page that opened it (false). False when there is no pop-up.</summary>
    bool Select(bool popup);
}

/// <summary>Real (operating-system) input for the page the connection is attached to: some browser UI (the saved-password list) ignores keys sent through CDP.</summary>
public interface INativeInput
{
    /// <summary>Brings the window of the page to the front with keyboard focus. True when it has focus now.</summary>
    Task<bool> FocusWindowAsync(CancellationToken ct);
    /// <summary>Presses real keys (ArrowDown, ArrowUp, Enter, Tab, Escape) in the focused window. False when the window does not have focus (nothing is sent).</summary>
    Task<bool> SendKeysAsync(IReadOnlyList<string> keys, CancellationToken ct);
}

/// <summary>
/// One ICdpConnection that forwards to whichever page is active now. The core (BrowserController, monitors) subscribes once;
/// the app attaches the connection of the tab a task works in, and - while one is open - of the pop-up window that tab opened
/// (the task works in the pop-up until it closes, or until it switches back). Events of pages that are not active are not forwarded.
/// It does not own the inner connections - whoever created them disposes them.
/// </summary>
public sealed class SwitchableCdpConnection : ICdpConnection, ITabAwareConnection, IWindowSwitch, INativeInput
{
    private readonly object _lock = new();
    private ICdpConnection? _main, _popup, _active;
    private string? _mainKey, _popupKey;
    private bool _onPopup;
    private bool _disposed;

    public event Action<CdpEvent>? Event;

    public string? ActiveTargetKey { get { lock (_lock) return _onPopup && _popup is not null ? _popupKey : _mainKey; } }

    public bool IsConnected { get { lock (_lock) return !_disposed && _active is { IsConnected: true }; } }

    public bool HasPopup { get { lock (_lock) return _popup is not null; } }
    public bool OnPopup { get { lock (_lock) return _onPopup && _popup is not null; } }

    /// <summary>Makes the given connection the tab's own one (the previous one stops delivering events).</summary>
    public void Attach(ICdpConnection inner, string key)
    {
        lock (_lock)
        {
            if (ReferenceEquals(_main, inner)) { _mainKey = key; return; }
            _main = inner; _mainKey = key;
            Refresh();
        }
    }

    /// <summary>Detaches the tab's own connection (only if it is the attached one), e.g. when its tab is closed.</summary>
    public void Detach(ICdpConnection inner)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(_main, inner)) return;
            _main = null; _mainKey = null;
            Refresh();
        }
    }

    /// <summary>A pop-up window of the tab opened: from now on the connection talks to it.</summary>
    public void AttachPopup(ICdpConnection popup, string key)
    {
        lock (_lock)
        {
            _popup = popup; _popupKey = key; _onPopup = true;
            Refresh();
        }
    }

    /// <summary>The pop-up window closed: back to the tab's own page.</summary>
    public void DetachPopup(ICdpConnection popup)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(_popup, popup)) return;
            _popup = null; _popupKey = null; _onPopup = false;
            Refresh();
        }
    }

    public bool Select(bool popup)
    {
        lock (_lock)
        {
            if (popup && _popup is null) return false;
            _onPopup = popup;
            Refresh();
            return true;
        }
    }

    // Call with the lock held.
    private void Refresh()
    {
        var want = _onPopup && _popup is not null ? _popup : _main;
        if (ReferenceEquals(want, _active)) return;
        if (_active is not null) _active.Event -= OnInnerEvent;
        _active = want;
        if (want is not null) want.Event += OnInnerEvent;
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
        lock (_lock) inner = _disposed ? null : _active;
        if (inner is null) throw new CdpException("No browser tab is open.");
        return inner.SendAsync(method, @params, sessionId, ct);
    }

    private INativeInput? Native { get { lock (_lock) return _disposed ? null : _active as INativeInput; } }

    public Task<bool> FocusWindowAsync(CancellationToken ct) => Native?.FocusWindowAsync(ct) ?? Task.FromResult(false);

    public Task<bool> SendKeysAsync(IReadOnlyList<string> keys, CancellationToken ct) => Native?.SendKeysAsync(keys, ct) ?? Task.FromResult(false);

    public ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            _disposed = true;
            if (_active is not null) _active.Event -= OnInnerEvent;
            _active = null; _main = null; _popup = null;
        }
        return ValueTask.CompletedTask;
    }
}