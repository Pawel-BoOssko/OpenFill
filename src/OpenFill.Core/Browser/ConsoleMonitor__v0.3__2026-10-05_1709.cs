// OpenFill - Metadata: wersja 0.3, data 2026-10-05 17:09
using System.Text;
using System.Text.Json.Nodes;
using OpenFill.Core.Cdp;

namespace OpenFill.Core.Browser;

/// <summary>Collects console errors and messages and unhandled JS exceptions. The model asks for them on demand.</summary>
public sealed class ConsoleMonitor(CdpSession session)
{
    public sealed record ConsoleItem(DateTime TsUtc, string Level, string Text);

    private readonly LinkedList<ConsoleItem> _items = new();
    private readonly object _lock = new();
    private const int Capacity = 300;

    private bool _subscribed;

    public async Task EnableAsync(CancellationToken ct = default)
    {
        if (!_subscribed) { _subscribed = true; session.Connection.Event += OnEvent; }
        await session.SendAsync("Runtime.enable", null, ct);
        await session.SendAsync("Log.enable", null, ct);
    }

    private void Add(string level, string text)
    {
        lock (_lock)
        {
            _items.AddLast(new ConsoleItem(DateTime.UtcNow, level, text));
            while (_items.Count > Capacity) _items.RemoveFirst();
        }
    }

    private void OnEvent(CdpEvent ev)
    {
        if (ev.SessionId is not null && session.SessionId is not null && ev.SessionId != session.SessionId) return;
        try
        {
            switch (ev.Method)
            {
                case "Runtime.consoleAPICalled":
                {
                    var level = ev.Params["type"]?.GetValue<string>() ?? "log";
                    var args = ev.Params["args"] as JsonArray;
                    var sb = new StringBuilder();
                    if (args is not null)
                        foreach (var a in args)
                            sb.Append(DescribeRemote(a as JsonObject)).Append(' ');
                    Add(level, sb.ToString().Trim());
                    break;
                }
                case "Runtime.exceptionThrown":
                {
                    var ex = ev.Params["exceptionDetails"] as JsonObject;
                    var text = ex?["exception"]?["description"]?.GetValue<string>()
                               ?? ex?["text"]?.GetValue<string>() ?? "exception";
                    Add("error", text);
                    break;
                }
                case "Log.entryAdded":
                {
                    var entry = ev.Params["entry"] as JsonObject;
                    Add(entry?["level"]?.GetValue<string>() ?? "log", entry?["text"]?.GetValue<string>() ?? "");
                    break;
                }
            }
        }
        catch { /* same as above */ }
    }

    private static string DescribeRemote(JsonObject? o)
    {
        if (o is null) return "";
        if (o["value"] is { } v) return v.ToJsonString();
        return o["description"]?.GetValue<string>() ?? o["type"]?.GetValue<string>() ?? "";
    }

    public IReadOnlyList<ConsoleItem> Snapshot(bool errorsOnly, int limit)
    {
        lock (_lock)
        {
            IEnumerable<ConsoleItem> q = _items;
            if (errorsOnly) q = q.Where(i => i.Level is "error" or "assert" or "warning");
            return q.Reverse().Take(limit).Reverse().ToList();
        }
    }
}
