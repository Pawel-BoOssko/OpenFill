// OF-000026: test configured MCP shared root without starting HTTP or a browser.
using System.Reflection;
using OpenFill.Core.Mcp;

internal static class McpSharedCaptureRootSmoke
{
    public static async Task RunAsync()
    {
        var custom = Path.Combine(Path.GetTempPath(), "of026_custom_shared_" +
            Guid.NewGuid().ToString("N"));
        await using var server = new McpServer(null!, "fixture", port: 48790,
            sharedFolder: custom);
        string Resolve(string name) => (string)(typeof(McpServer).GetProperty(
            name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(server) ?? throw new Exception("OF026_PROPERTY_" + name));

        var expected = Path.GetFullPath(custom);
        if (!string.Equals(Resolve("SharedCaptureDirectory"),
            Path.Combine(expected, "captures"), StringComparison.OrdinalIgnoreCase))
            throw new Exception("OF026_CUSTOM_CAPTURE_PATH_MISMATCH");
        if (!string.Equals(Resolve("SharedFilterDirectory"),
            Path.Combine(expected, "filters"), StringComparison.OrdinalIgnoreCase))
            throw new Exception("OF026_CUSTOM_FILTER_PATH_MISMATCH");

        Console.WriteLine("OF026_CONFIGURED_SHARED_ROOT_2_CHECKS_OK");
    }
}
