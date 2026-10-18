// OpenFill - Metadata: wersja 0.6, data 2026-10-12 22:00
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenFill.Core.Cdp;
using OpenFill.Core.Agent;

namespace OpenFill.Core.Hosting;

/// <summary>A tab as the strip shows it. State: running | waiting | finished | free | user (opened by hand).</summary>
public sealed record TabInfo(string TaskKey, string Label, string? TabId, string? LastUrl, string State, DateTime LastUsedUtc, string? Requester = null, string? Reason = null);

/// <summary>Result of preparing a tab for a task.</summary>
/// <param name="TabId">The tab the task works in (already selected).</param>
/// <param name="Continued">The task continues an earlier one.</param>
/// <param name="Reopened">The earlier task's tab was gone, so a new tab was opened at the last known address.</param>
/// <param name="Url">The address the new tab was opened at (only when Reopened).</param>
public sealed record TabStart(string TabId, bool Continued, bool Reopened, string? Url);

/// <summary>What the window provides for browser tabs. All calls may come from any thread; the implementation marshals to the UI thread.</summary>
public interface IBrowserTabs
{
    /// <summary>Opens a new tab (at url when given), selects it and points the CDP connection at it. Returns the tab id.</summary>
    Task<string> OpenTabAsync(string? url, string label, CancellationToken ct);
    /// <summary>Shows an existing tab and points the CDP connection at it.</summary>
    Task SelectTabAsync(string tabId, CancellationToken ct);
    Task NavigateAsync(string tabId, string url, CancellationToken ct);
    Task CloseTabAsync(string tabId);
    Task<string?> UrlAsync(string tabId);
    bool Exists(string tabId);
    /// <summary>True when every tab has its own CDP connection, so several tasks can work in different tabs at once.</summary>
    bool SupportsParallel => false;
    /// <summary>The tab's own CDP connection (only when SupportsParallel); null means "use the shared, switchable one".</summary>
    ICdpConnection? ConnectionOf(string tabId) => null;
    /// <summary>Redraws the tab strip.</summary>
    void Strip(IReadOnlyList<TabInfo> tabs, string? activeTabId);
}

/// <summary>
/// One task = one browser tab; several tasks may work in different tabs at once. A finished task's tab stays open; when more than Limit tabs would be open,
/// the oldest FINISHED tab is closed (never one with a running task or a task waiting for the person). Returning to a task
/// (continue) switches to its tab, or - if it was closed - opens a new tab at the last known address.
/// The task-to-address map is saved, so returning works after a restart too (the tab is then reopened at the stored address).
/// </summary>
public sealed class TabManager
{
    private sealed class Rec
    {
        public string TaskKey { get; set; } = "";
        public string Label { get; set; } = "";
        public string Short { get; set; } = "";
        public string? Requester { get; set; }
        public string? Reason { get; set; }
        public string? LastUrl { get; set; }
        public string State { get; set; } = "finished";
        public DateTime LastUsedUtc { get; set; }
        [JsonIgnore] public string? TabId { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private const int StoredRecords = 200;

    private readonly IBrowserTabs _ui;
    private readonly string _store;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lock = new();
    private readonly List<Rec> _recs = new();
    private readonly bool _releaseFinishedTabs;
    private string? _activeTabId;

    /// <summary>Maximum number of open tabs (the oldest finished one is closed to make room). At least 1.</summary>
    public int Limit { get => _limit; set => _limit = Math.Max(1, value); }
    private int _limit = 5;

    public TabManager(IBrowserTabs ui, string storeFile, int limit, bool releaseFinishedTabs = false)
    {
        _ui = ui;
        _store = storeFile;
        _releaseFinishedTabs = releaseFinishedTabs;
        Limit = limit;
        Load();
    }

    // ------------------------------------------------------------------ queries

    public IReadOnlyList<TabInfo> Open()
    {
        lock (_lock) return _recs.Where(r => r.TabId is not null).OrderBy(r => r.LastUsedUtc).Select(ToInfo).ToList();
    }

    /// <summary>Stored information about a task (also after its tab was closed).</summary>
    public TabInfo? Find(string taskKey)
    {
        lock (_lock) return _recs.Where(r => r.TaskKey == taskKey).Select(ToInfo).FirstOrDefault();
    }

    public bool IsTabPresent(string taskKey)
    {
        lock (_lock)
        {
            var tabId = _recs.FirstOrDefault(r => r.TaskKey == taskKey)?.TabId;
            return tabId is not null && _ui.Exists(tabId);
        }
    }
    /// <summary>True when tabs have their own CDP connections (several tasks may run at once).</summary>
    public bool SupportsParallel => _ui.SupportsParallel;

    /// <summary>The CDP connection of one tab (null when tabs share a single connection).</summary>
    public ICdpConnection? ConnectionOf(string tabId) => _ui.ConnectionOf(tabId);

    /// <summary>True when the tab that is shown now has a task running (or waiting for the person) in it.</summary>
    public bool ActiveTabBusy()
    {
        lock (_lock)
        {
            if (_activeTabId is null) return false;
            return _recs.Any(r => r.TabId == _activeTabId && r.State is "running" or "waiting");
        }
    }

    private static TabInfo ToInfo(Rec r) => new(r.TaskKey, r.Label, r.TabId, r.LastUrl, r.State, r.LastUsedUtc, r.Requester, r.Reason);

    // ------------------------------------------------------------------ the window's blank starting tab

    /// <summary>A tab that belongs to no task (the one the window starts with). The next task takes it over.</summary>
    public void RegisterFree(string tabId, string label)
    {
        lock (_lock)
        {
            _recs.Add(new Rec { TaskKey = "free:" + tabId, Label = label, State = "free", LastUsedUtc = DateTime.UtcNow, TabId = tabId });
            _activeTabId = tabId;
        }
        Refresh();
    }

    /// <summary>A tab the person opened by hand (e.g. to read a verification code in their e-mail). No task takes it over and none closes it.</summary>
    public void RegisterUser(string tabId, string label)
    {
        lock (_lock)
        {
            _recs.Add(new Rec { TaskKey = "user:" + tabId, Label = label, State = "user", LastUsedUtc = DateTime.UtcNow, TabId = tabId });
            _activeTabId = tabId;
        }
        Refresh();
    }

    // ------------------------------------------------------------------ task life cycle

    // An explicit continuation wins. Otherwise reuse only a finished tab of the same
    // requester/chat and a confidently identified domain, never another chat's tab.
    private Rec? FindPrevious(string? continueKey, string? requester, string? preferredDomain)
    {
        if (continueKey is not null) return _recs.FirstOrDefault(r => r.TaskKey == continueKey);
        if (string.IsNullOrWhiteSpace(requester) || string.IsNullOrWhiteSpace(preferredDomain)) return null;
        return _recs.Where(r => r.State == "finished" && r.Requester == requester &&
            !string.IsNullOrWhiteSpace(r.LastUrl) && CostGuard.DomainOf(r.LastUrl) == preferredDomain)
            .OrderByDescending(r => r.LastUsedUtc).FirstOrDefault();
    }
    public async Task<TabStart> BeginAsync(string taskKey, string label, string? continueKey, CancellationToken ct, string? shortLabel = null, string? requester = null, string? preferredDomain = null)
    {
        while (true)
        {
            await _gate.WaitAsync(ct);
            bool room;
            lock (_lock)
            {
                var previous = continueKey is null ? null : _recs.FirstOrDefault(r => r.TaskKey == continueKey);
                room = (previous?.TabId is { } tab && _ui.Exists(tab))
                    || _recs.Any(r => r.State == "free" && r.TabId is not null && _ui.Exists(r.TabId))
                    || _recs.Count(r => r.TabId is { } id && _ui.Exists(id)) < Limit
                    || _recs.Any(r => r.TabId is { } id && _ui.Exists(id) && (r.State is "finished" or "free"));
            }
            if (room) break;
            _gate.Release();
            await Task.Delay(200, ct);
        }
        try
        {
            Rec? prev;
            lock (_lock) prev = FindPrevious(continueKey, requester, preferredDomain);

            string tabId;
            bool reopened = false;
            bool reusedPhysical = false;
            string? url = null;

            if (prev?.TabId is { } own && _ui.Exists(own))
            {
                // Same session, tab still open: the new task takes the tab over.
                tabId = own;
                reusedPhysical = true;
                await _ui.SelectTabAsync(tabId, ct);
                lock (_lock) prev.TabId = null;
            }
            else
            {
                url = prev?.LastUrl;
                reopened = prev is not null && !string.IsNullOrWhiteSpace(url);
                Rec? free;
                lock (_lock) free = _recs.FirstOrDefault(r => r.State == "free" && r.TabId is not null && _ui.Exists(r.TabId));
                if (free?.TabId is { } freeId)
                {
                    tabId = freeId;
                    await _ui.SelectTabAsync(tabId, ct);
                    if (reopened) await _ui.NavigateAsync(tabId, url!, ct);
                    lock (_lock) _recs.Remove(free);
                }
                else
                {
                    await MakeRoomAsync();
                    tabId = await _ui.OpenTabAsync(reopened ? url : null, label, ct);
                }
            }

            lock (_lock)
            {
                if (prev is not null) prev.TabId = null;
                _recs.RemoveAll(r => r.TaskKey == taskKey);
                _recs.Add(new Rec { TaskKey = taskKey, Label = label, Short = shortLabel ?? label, Requester = requester, LastUrl = reopened ? url : null, State = "running", LastUsedUtc = DateTime.UtcNow, TabId = tabId,
                    Reason = prev is null ? (string.IsNullOrWhiteSpace(preferredDomain) ? "site_domain_unknown" : "no_matching_finished_chat_domain_tab") :
                        reopened ? "previous_tab_closed_reopened_saved_url" : reusedPhysical ? "existing_session_reused" : "previous_session_unavailable_no_saved_url" });
                _activeTabId = tabId;
            }
            Save();
            Refresh();
            return new TabStart(tabId, prev is not null, reopened, reopened ? url : null);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Marks the task's tab as running or waiting for the person.</summary>
    public void SetState(string taskKey, string state)
    {
        lock (_lock)
        {
            var r = _recs.FirstOrDefault(x => x.TaskKey == taskKey);
            if (r is null || r.State == state) return;
            r.State = state;
        }
        Refresh();
    }

    /// <summary>The task is over: its address is remembered, while the physical tab is released to a blank reusable tab so finished web pages do not keep consuming CPU/RAM.</summary>
    public async Task EndAsync(string taskKey)
    {
        await _gate.WaitAsync();
        try
        {
            Rec? r;
            lock (_lock) r = _recs.FirstOrDefault(x => x.TaskKey == taskKey);
            if (r is null) return;

            var tabId = r.TabId;
            string? url = null;
            if (tabId is { } t && _ui.Exists(t))
            {
                try { url = await _ui.UrlAsync(t); } catch { }
            }

            if (!_releaseFinishedTabs)
            {
                lock (_lock)
                {
                    r.State = "finished";
                    r.LastUsedUtc = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(url) && url != "about:blank") r.LastUrl = url;
                }
                Save();
                Refresh();
                return;
            }

            Rec? existingFree;
            bool wasActive;
            lock (_lock)
            {
                r.State = "finished";
                r.LastUsedUtc = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(url) && url != "about:blank") r.LastUrl = url;
                r.TabId = null;
                wasActive = tabId is not null && _activeTabId == tabId;
                existingFree = _recs.FirstOrDefault(x => x.State == "free" && x.TabId is not null &&
                                                        x.TabId != tabId && _ui.Exists(x.TabId));
            }

            if (tabId is { } finishedTab && _ui.Exists(finishedTab))
            {
                if (existingFree?.TabId is { } freeTab)
                {
                    if (wasActive)
                    {
                        try { await _ui.SelectTabAsync(freeTab, CancellationToken.None); } catch { }
                        lock (_lock) _activeTabId = freeTab;
                    }
                    try { await _ui.CloseTabAsync(finishedTab); } catch { lock (_lock) { if (_ui.Exists(finishedTab)) r.TabId = finishedTab; } }
                }
                else
                {
                    // Keep exactly one cheap reusable browser tab while idle. The task's URL is already
                    // stored above, so a continuation reopens it explicitly and knows that transient form
                    // state is gone. This prevents completed sites from running timers/renderers forever.
                    var navigated = false;
                    try { await _ui.NavigateAsync(finishedTab, "about:blank", CancellationToken.None); navigated = true; } catch { }
                    lock (_lock)
                    {
                        if (navigated)
                        {
                            _recs.RemoveAll(x => x.TaskKey == "free:" + finishedTab);
                            _recs.Add(new Rec
                            {
                                TaskKey = "free:" + finishedTab,
                                Label = "Ready",
                                State = "free",
                                LastUsedUtc = DateTime.UtcNow,
                                TabId = finishedTab
                            });
                            if (wasActive) _activeTabId = finishedTab;
                        }
                        else if (_ui.Exists(finishedTab))
                        {
                            // Keep the finished tab tracked if navigation failed; never silently orphan a live renderer.
                            r.TabId = finishedTab;
                        }
                    }
                }
            }

            Save();
            Refresh();
        }
        finally { _gate.Release(); }
    }

    /// <summary>Remembers where the task's tab is now, so a task cut off by a crash or restart can be picked up at that page.</summary>
    public async Task RememberUrlAsync(string taskKey)
    {
        Rec? r;
        lock (_lock) r = _recs.FirstOrDefault(x => x.TaskKey == taskKey);
        if (r?.TabId is not { } t || !_ui.Exists(t)) return;
        string? url = null;
        try { url = await _ui.UrlAsync(t); } catch { }
        if (string.IsNullOrWhiteSpace(url) || url == "about:blank") return;
        bool changed, relabeled = false;
        lock (_lock)
        {
            changed = r.LastUrl != url; r.LastUrl = url;
            // The tab is named after the site it is on now plus the task's short title.
            if (!string.IsNullOrEmpty(r.Short) && Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Host.Length > 0)
            {
                var host = u.Host.StartsWith("www.") ? u.Host[4..] : u.Host;
                var full = host + " - " + r.Short;
                if (full != r.Label) { r.Label = full; relabeled = true; }
            }
        }
        if (changed || relabeled) Save();
        if (relabeled) Refresh();
    }

    /// <summary>The person went to a card that belongs to this task: bring the task's tab to the front.</summary>
    public async Task ShowTaskAsync(string taskKey)
    {
        string? id;
        lock (_lock)
        {
            id = _recs.FirstOrDefault(r => r.TaskKey == taskKey)?.TabId;
            if (id is null || _activeTabId == id) return;
        }
        if (!_ui.Exists(id)) return;
        await _ui.SelectTabAsync(id, CancellationToken.None);
        UserSelected(id);
    }

    /// <summary>The person picked a tab in the strip (the window already showed it).</summary>
    public void UserSelected(string tabId)
    {
        lock (_lock) _activeTabId = tabId;
        Refresh();
    }

    /// <summary>The person closes a tab. Refused (false) while a task is running in it or waits for the person.</summary>
    public async Task<bool> UserCloseAsync(string tabId)
    {
        Rec? r;
        lock (_lock) r = _recs.FirstOrDefault(x => x.TabId == tabId);
        if (r is null) return false;
        if (r.State is "running" or "waiting") return false;
        string? url = null;
        try { url = await _ui.UrlAsync(tabId); } catch { }
        await _ui.CloseTabAsync(tabId);
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(url) && url != "about:blank") r.LastUrl = url;
            r.TabId = null;
            if (r.State is "free" or "user") _recs.Remove(r);
            if (_activeTabId == tabId) _activeTabId = null;
        }
        Save();
        Refresh();
        return true;
    }

    // ------------------------------------------------------------------ internals

    /// <summary>Closes the oldest finished (or free) tabs until a new one fits within the limit.</summary>
    private async Task MakeRoomAsync()
    {
        while (true)
        {
            Rec? victim;
            lock (_lock)
            {
                var open = _recs.Count(r => r.TabId is { } id && _ui.Exists(id)); // all physical tabs count; user tabs are protected from eviction
                if (open < Limit) return;
                victim = _recs.Where(r => r.TabId is { } id && _ui.Exists(id) && (r.State is "finished" or "free"))
                              .OrderBy(r => r.State == "free" ? 0 : 1).ThenBy(r => r.LastUsedUtc).FirstOrDefault();
            }
            if (victim?.TabId is not { } id) throw new IOException("All browser tabs are in use; no safe tab can be evicted.");
            string? url = null;
            try { url = await _ui.UrlAsync(id); } catch { }
            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(url) && url != "about:blank") victim.LastUrl = url;
                victim.TabId = null;
                if (victim.State == "free") _recs.Remove(victim);
            }
            bool closed = false;
            try { await _ui.CloseTabAsync(id); closed = !_ui.Exists(id); } catch { }
            if (!closed)
            {
                lock (_lock) victim.TabId = id; // Do not orphan the still-open renderer.
                throw new IOException("Cannot free a browser tab without losing its session. New tab was not opened.");
            }
        }
    }

    /// <summary>Raised when the tab in front belongs to another task (null: a tab that belongs to no task, or no tab).</summary>
    public event Action<string?>? ActiveTaskChanged;
    private string? _lastActiveTask;

    private void Refresh()
    {
        IReadOnlyList<TabInfo> open;
        string? active;
        string? activeTask;
        bool taskChanged;
        lock (_lock)
        {
            open = _recs.Where(r => r.TabId is not null).OrderBy(r => r.LastUsedUtc).Select(ToInfo).ToList();
            active = _activeTabId;
            var rec = active is null ? null : _recs.FirstOrDefault(r => r.TabId == active);
            activeTask = rec is null || rec.State is "user" or "free" ? null : rec.TaskKey;
            taskChanged = activeTask != _lastActiveTask;
            _lastActiveTask = activeTask;
        }
        try { _ui.Strip(open, active); } catch { }
        if (taskChanged) { try { ActiveTaskChanged?.Invoke(activeTask); } catch { } }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_store)) return;
            var list = JsonSerializer.Deserialize<List<Rec>>(File.ReadAllText(_store));
            if (list is null) return;
            foreach (var r in list)
            {
                if (string.IsNullOrWhiteSpace(r.TaskKey)) continue;
                r.TabId = null;
                if (r.State is "running" or "waiting") r.State = "finished"; // left over from a run that never ended
                _recs.Add(r);
            }
        }
        catch { /* the map is a convenience; a damaged file is ignored */ }
    }

    private void Save()
    {
        try
        {
            List<Rec> copy;
            lock (_lock)
                copy = _recs.Where(r => r.State is not ("free" or "user")).OrderByDescending(r => r.LastUsedUtc).Take(StoredRecords).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(_store)!);
            File.WriteAllText(_store, JsonSerializer.Serialize(copy, JsonOpts));
        }
        catch { }
    }
}
