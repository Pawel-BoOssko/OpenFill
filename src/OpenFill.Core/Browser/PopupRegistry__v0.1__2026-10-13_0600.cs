// OpenFill - Metadata: wersja 0.1, data 2026-10-13 06:00
namespace OpenFill.Core.Browser;

/// <summary>
/// Keeps track of the pop-up windows (sign-in windows and the like). A page that opens the same pop-up again (the same address without
/// the query) replaces the earlier one, and only a few pop-ups stay open at once, so they do not pile up on the taskbar.
/// </summary>
public sealed class PopupRegistry<T> where T : class
{
    private readonly List<(string Key, T Item, string? Owner)> _items = new();
    private readonly object _lock = new();

    /// <summary>The most pop-ups kept open; past that the oldest ones are closed.</summary>
    public int Max { get; set; } = 4;

    /// <summary>Host and path of an address, lower case, without the query. about:blank and non-addresses give "" (never replaced).</summary>
    public static string KeyOf(string? uri)
    {
        if (string.IsNullOrEmpty(uri) || !Uri.TryCreate(uri, UriKind.Absolute, out var u) || !(u.Scheme == "http" || u.Scheme == "https")) return "";
        return (u.Host + u.AbsolutePath).ToLowerInvariant();
    }

    /// <summary>Registers a new pop-up; returns the earlier pop-ups that the caller must close (same key, or over the limit).</summary>
    public List<T> Add(string? uri, T item, string? owner = null)
    {
        var key = KeyOf(uri);
        var close = new List<T>();
        lock (_lock)
        {
            if (key.Length > 0)
                foreach (var e in _items.Where(e => e.Key == key).ToList()) { close.Add(e.Item); _items.Remove(e); }
            _items.Add((key, item, owner));
            while (_items.Count > Math.Max(1, Max)) { close.Add(_items[0].Item); _items.RemoveAt(0); }
        }
        return close;
    }

    public void Remove(T item)
    {
        lock (_lock) _items.RemoveAll(e => ReferenceEquals(e.Item, item));
    }

    /// <summary>Find pop-ups belonging to one browser tab. Keep them tracked until FormClosed confirms disposal.</summary>
    public List<T> ForOwner(string owner)
    {
        lock (_lock) return _items.Where(e => e.Owner == owner).Select(e => e.Item).ToList();
    }
    public int Count { get { lock (_lock) return _items.Count; } }
}
