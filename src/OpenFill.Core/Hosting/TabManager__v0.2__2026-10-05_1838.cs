// OpenFill - Metadata: wersja 0.2, data 2026-10-05 18:38
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenFill.Core.Hosting;

/// <summary>A tab as the strip shows it. State: running | waiting | finished | free.</summary>
public sealed record TabInfo(string TaskKey, string Label, string? TabId, string? LastUrl, string State, DateTime LastUsedUtc);

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
    /// <summary>Redraws the tab strip.</summary>
    void Strip(IReadOnlyList<TabInfo> tabs, string? activeTabId);
}

/// <summary>
/// One task = one browser tab, one task at a time. A finished task's tab stays open; when more than Limit tabs would be open,
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
    private string? _activeTabId;

    /// <summary>Maximum number of open tabs (the oldest finished one is closed to make room). At least 1.</summary>
    public int Limit { get => _limit; set => _limit = Math.Max(1, value); }
    private int _limit = 5;

    public TabManager(IBrowserTabs ui, string storeFile, int limit)
    {
        _ui = ui;
        _store = storeFile;
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

    private static TabInfo ToInfo(Rec r) => new(r.TaskKey, r.Label, r.TabId, r.LastUrl, r.State, r.LastUsedUtc);

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

    // ------------------------------------------------------------------ task life cycle

    public async Task<TabStart> BeginAsync(string taskKey, string label, string? continueKey, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            Rec? prev;
            lock (_lock) prev = continueKey is null ? null : _recs.FirstOrDefault(r => r.TaskKey == continueKey);

            string tabId;
            bool reopened = false;
            string? url = null;

            if (prev?.TabId is { } own && _ui.Exists(own))
            {
                // Same session, tab still open: the new task takes the tab over.
                tabId = own;
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
                _recs.Add(new Rec { TaskKey = taskKey, Label = label, LastUrl = reopened ? url : null, State = "running", LastUsedUtc = DateTime.UtcNow, TabId = tabId });
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

    /// <summary>The task is over: the tab stays open, its address is remembered.</summary>
    public async Task EndAsync(string taskKey)
    {
        Rec? r;
        lock (_lock) r = _recs.FirstOrDefault(x => x.TaskKey == taskKey);
        if (r is null) return;
        string? url = null;
        if (r.TabId is { } t && _ui.Exists(t))
        {
            try { url = await _ui.UrlAsync(t); } catch { }
        }
        lock (_lock)
        {
            r.State = "finished";
            r.LastUsedUtc = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(url) && url != "about:blank") r.LastUrl = url;
        }
        Save();
        Refresh();
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
        bool changed;
        lock (_lock) { changed = r.LastUrl != url; r.LastUrl = url; }
        if (changed) Save();
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
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(url) && url != "about:blank") r.LastUrl = url;
            r.TabId = null;
            if (r.State == "free") _recs.Remove(r);
            if (_activeTabId == tabId) _activeTabId = null;
        }
        await _ui.CloseTabAsync(tabId);
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
                var open = _recs.Count(r => r.TabId is not null);
                if (open < Limit) return;
                victim = _recs.Where(r => r.TabId is not null && r.State is "finished" or "free")
                              .OrderBy(r => r.State == "free" ? 0 : 1).ThenBy(r => r.LastUsedUtc).FirstOrDefault();
            }
            if (victim?.TabId is not { } id) return; // everything open is busy: allow going over the limit
            string? url = null;
            try { url = await _ui.UrlAsync(id); } catch { }
            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(url) && url != "about:blank") victim.LastUrl = url;
                victim.TabId = null;
                if (victim.State == "free") _recs.Remove(victim);
            }
            try { await _ui.CloseTabAsync(id); } catch { }
        }
    }

    private void Refresh()
    {
        IReadOnlyList<TabInfo> open;
        string? active;
        lock (_lock)
        {
            open = _recs.Where(r => r.TabId is not null).OrderBy(r => r.LastUsedUtc).Select(ToInfo).ToList();
            active = _activeTabId;
        }
        try { _ui.Strip(open, active); } catch { }
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
                copy = _recs.Where(r => r.State != "free").OrderByDescending(r => r.LastUsedUtc).Take(StoredRecords).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(_store)!);
            File.WriteAllText(_store, JsonSerializer.Serialize(copy, JsonOpts));
        }
        catch { }
    }
}
