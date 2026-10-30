// OpenFill SOURCE only: native WebView2 browser permissions -> matching task tab, 2026-10-30
using System.Collections.Concurrent;

namespace OpenFill.Core.Browser;

public static class BrowserNotificationBus
{
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<string>> Queues = new(StringComparer.Ordinal);

    public static void Post(string tabId, string message)
    {
        if (string.IsNullOrWhiteSpace(tabId) || string.IsNullOrWhiteSpace(message)) return;
        var queue = Queues.GetOrAdd(tabId, _ => new ConcurrentQueue<string>());
        queue.Enqueue(message);
        while (queue.Count > 30) queue.TryDequeue(out _);
    }

    public static IReadOnlyList<string> Take(string? tabId)
    {
        if (string.IsNullOrWhiteSpace(tabId) || !Queues.TryGetValue(tabId, out var queue)) return Array.Empty<string>();
        var notices = new List<string>();
        while (notices.Count < 30 && queue.TryDequeue(out var one)) notices.Add(one);
        return notices;
    }

    public static void Clear(string tabId) => Queues.TryRemove(tabId, out _);
}
