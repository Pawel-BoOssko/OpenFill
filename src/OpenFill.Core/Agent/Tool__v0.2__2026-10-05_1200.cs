// OpenFill - Metadata: wersja 0.2, data 2026-10-05 12:00
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

/// <summary>Tool result: text for the model, status and optional data for the panel.</summary>
public sealed record ToolResult(string Output, bool Ok = true, object? PanelData = null)
{
    /// <summary>Optional image (JPEG base64) attached to the model context as a separate message.</summary>
    public string? ImageJpegBase64 { get; init; }

    public static ToolResult Text(string s) => new(s);
    public static ToolResult Error(string s) => new(s, Ok: false);
}

/// <summary>One internal tool available to the model in the app.</summary>
public sealed class Tool
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonObject Parameters { get; init; }
    public required Func<JsonObject, CancellationToken, Task<ToolResult>> Handler { get; init; }

    /// <summary>Definition in the Responses API tool format.</summary>
    public JsonObject ToDefinition() => new()
    {
        ["type"] = "function",
        ["name"] = Name,
        ["description"] = Description,
        ["parameters"] = Parameters.DeepClone(),
        ["strict"] = false
    };
}
