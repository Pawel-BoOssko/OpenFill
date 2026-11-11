using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Hosting;

/// <summary>Local-only NDJSON request-response IPC. Every connection handles one command.</summary>
public sealed class MobileCardPipeServer : IAsyncDisposable
{
    public const string DefaultPipeName = "OpenFillMobileCards";
    private readonly MobileCardGateway _cards;
    private readonly CancellationTokenSource _stop = new();
    private Task? _runner;
    public MobileCardPipeServer(MobileCardGateway cards) => _cards = cards;
    public void Start() => _runner ??= Task.Run(RunAsync);

    private async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(DefaultPipeName, PipeDirection.InOut,
                    1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_stop.Token);
                using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
                await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true)
                { AutoFlush = true };
                var line = await reader.ReadLineAsync(_stop.Token);
                if (line is null || line.Length > 65536) continue;
                string reply;
                try { reply = Handle(line); }
                catch (Exception) { reply = "{\"error\":\"invalid_request\"}"; }
                await writer.WriteLineAsync(reply);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
            catch (IOException) { await Task.Delay(100, _stop.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing); }
        }
    }

    public string Handle(string request)
    {
        var json = JsonNode.Parse(request) as JsonObject ?? throw new FormatException();
        var kind = json["kind"]?.GetValue<string>();
        object result = kind switch
        {
            "health" => new { instanceId = _cards.InstanceId, lastSequence = _cards.LastSequence, status = "ready" },
            "list" => _cards.OpenCards(),
            "events" => _cards.ReadEvents(json["after"]?.GetValue<long>() ?? 0),
            "answer" => _cards.Submit(JsonSerializer.Deserialize<MobileAnswer>(
                json["action"]?.ToJsonString() ?? throw new FormatException(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new FormatException()),
            _ => new { error = "unknown_command" }
        };
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        if (_runner is not null) { try { await _runner; } catch (OperationCanceledException) { } }
        _stop.Dispose();
    }
}
