// OpenFill - Metadata: wersja 0.10, data 2026-10-05 20:40
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenFill.Core.Config;

/// <summary>Price of one model in USD per 1M tokens. 0 = unknown.</summary>
public sealed class ModelPrice
{
    public double Input { get; set; }
    /// <summary>Cached input price; when 0 the regular input price is used.</summary>
    public double CachedInput { get; set; }
    public double Output { get; set; }
}

/// <summary>Application settings. Each timeout has a single, explicit source of truth.</summary>
public sealed class AppConfig
{
    /// <summary>Model id. The real value comes from config.json / the Settings panel; this is only the fallback for a fresh install.</summary>
    public string Model { get; set; } = "gpt-6-luna";
    /// <summary>low | medium | high | xhigh | max (depends on the model).</summary>
    public string ReasoningEffort { get; set; } = "low";
    public string OpenAIBaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>
    /// Optional prices in USD per 1M tokens, keyed by model id (e.g. "gpt-6-luna"), used only to estimate cost
    /// in the usage log and the panel footer. There are no built-in prices: when the model in use has no entry,
    /// only token counts are shown and no cost is estimated.
    /// </summary>
    public Dictionary<string, ModelPrice> Prices { get; set; } = new();

    /// <summary>Finds the price entry for a model id: exact match first, then the longest key that prefixes the id (dated snapshots).</summary>
    public ModelPrice? FindPrice(params string?[] modelIds)
    {
        foreach (var id in modelIds)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            foreach (var (key, price) in Prices)
                if (string.Equals(key, id, StringComparison.OrdinalIgnoreCase)) return price;
            var best = Prices.Where(p => id.StartsWith(p.Key, StringComparison.OrdinalIgnoreCase))
                             .OrderByDescending(p => p.Key.Length).Select(p => p.Value).FirstOrDefault();
            if (best != null) return best;
        }
        return null;
    }

    /// <summary>Expose OpenFill to other models through MCP (a local server plus a public tunnel).</summary>
    public bool McpEnabled { get; set; } = false;
    /// <summary>Open a temporary public HTTPS address (Cloudflare quick tunnel) for the MCP server.</summary>
    public bool McpTunnel { get; set; } = true;
    /// <summary>Fixed local port of the MCP server; 0 = pick a free one.</summary>
    public int McpPort { get; set; } = 0;
    /// <summary>Your own stable public address (e.g. https://mcp.example.com) when you run a named tunnel yourself; disables the quick tunnel.</summary>
    public string McpPublicBaseUrl { get; set; } = "";

    /// <summary>Per task, USD: past this the task is flagged as unusually expensive (notice in the panel, warning in the MCP status).</summary>
    public double CostInfoUsd { get; set; } = 0.10;
    /// <summary>Per task, USD: past this (and again at every further multiple) the person at the computer is asked whether to go on.</summary>
    public double CostWarnUsd { get; set; } = 0.15;
    /// <summary>Per web domain, cumulative, USD: a hard stop. The domain is blocked until the person lifts it in the panel.</summary>
    public double DomainLimitUsd { get; set; } = 0.40;
    /// <summary>Per web domain, everything ever spent, USD. Lifting a block resets the count towards DomainLimitUsd, but never this ceiling: past it the domain stays blocked until you raise this value.</summary>
    public double DomainLifetimeLimitUsd { get; set; } = 1.00;

    /// <summary>Steps after which a task is checked (progress, loops). A task that makes progress goes on without a question.</summary>
    public int MaxSteps { get; set; } = 60;
    /// <summary>How many times a task that is still making progress may be extended by another MaxSteps rounds.</summary>
    public int MaxStepExtensions { get; set; } = 5;
    public int ModelTimeoutSeconds { get; set; } = 180;
    public int ToolTimeoutSeconds { get; set; } = 60;
    public int TaskTimeoutMinutes { get; set; } = 30;
    public int UserAnswerTimeoutMinutes { get; set; } = 20;
    /// <summary>How long a task waits for the calling model (MCP) to answer a question before it stops and reports where it got to.</summary>
    public int CallerAnswerTimeoutMinutes { get; set; } = 5;

    /// <summary>Character limit of a single tool result; the excess goes to a file.</summary>
    public int ToolOutputLimitChars { get; set; } = 50_000;
    /// <summary>Once the history exceeds this size, old tool results are truncated.</summary>
    public int HistoryCompactThresholdChars { get; set; } = 240_000;

    /// <summary>Before an irreversible step (payment, sending, final booking) the model must ask for consent.</summary>
    public bool ConfirmIrreversible { get; set; } = true;

    public string HomeUrl { get; set; } = "about:blank";

    /// <summary>How many browser tabs stay open (one per task). Past that, the oldest finished tab is closed. 1-12.</summary>
    public int MaxTabs { get; set; } = 5;

    /// <summary>Folder for exchanging files with the calling model (point it at a folder synced with Google Drive). Empty = %USERPROFILE%\OpenFill\shared.</summary>
    public string SharedFolder { get; set; } = "";

    public static readonly JsonSerializerOptions FileJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static AppConfig Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), FileJson) ?? new AppConfig();
        }
        catch
        {
            // A corrupted file does not block startup; it is overwritten on the next save.
        }
        return new AppConfig();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, FileJson));
    }

    public AppConfig Clone() => JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(this, FileJson), FileJson)!;
}
