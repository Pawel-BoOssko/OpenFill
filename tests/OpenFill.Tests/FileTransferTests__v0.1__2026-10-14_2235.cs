// OpenFill - Metadata: wersja 0.1, data 2026-10-14 22:35
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using OpenFill.Core.Mcp;

namespace OpenFill.Tests;

public static class FileTransferTests
{
    public static async Task RunAsync()
    {
        await T.Section("MCP file transfer: scoped GET / PUT", async () =>
        {
            var shared = Path.Combine(Path.GetTempPath(), "openfill_transfer_" + Guid.NewGuid().ToString("N")[..10]);
            var tasksDir = Path.Combine(shared, "tasks");
            Directory.CreateDirectory(shared);
            var sourceBytes = Encoding.UTF8.GetBytes("OpenFill file transport integration test");
            File.WriteAllBytes(Path.Combine(shared, "source.bin"), sourceBytes);
            var secret = "test-secret-" + Guid.NewGuid().ToString("N");
            await using var server = new McpServer(
                new McpTaskManager(new FakeRunHost(), tasksDir, TimeSpan.FromSeconds(5)),
                secret, port: 0, sharedFolder: shared);
            server.Start();
            var baseUrl = "http://127.0.0.1:" + server.Port + "/mcp/" + secret;
            server.LinkBase = baseUrl;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            async Task<JsonObject> Prepare(string mode, string file)
            {
                var request = new JsonObject {
                    ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call",
                    ["params"] = new JsonObject {
                        ["name"] = "openfill_file_transfer",
                        ["arguments"] = new JsonObject { ["mode"] = mode, ["file"] = file }
                    }
                };
                using var response = await http.PostAsync(baseUrl, new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
                return JsonNode.Parse(json["result"]!["content"]![0]!["text"]!.GetValue<string>())!.AsObject();
            }

            var invalid = await Prepare("upload", "../escape.bin");
            T.Eq("path traversal rejected", "error", invalid["status"]?.GetValue<string>());
            var missing = await Prepare("download", "unknown.bin");
            T.Eq("missing download rejected", "error", missing["status"]?.GetValue<string>());

            var get = await Prepare("download", "source.bin");
            T.Eq("download link created", "ok", get["status"]?.GetValue<string>());
            T.Eq("download says GET", "GET", get["http_method"]?.GetValue<string>());
            T.NotContains("file URL does not reveal MCP master secret", get["url"]!.GetValue<string>(), secret);
            var downloaded = await http.GetByteArrayAsync(get["url"]!.GetValue<string>());
            T.Check("GET returns identical raw bytes", downloaded.SequenceEqual(sourceBytes));

            var put = await Prepare("upload", "payload.bin");
            T.Eq("upload link created", "ok", put["status"]?.GetValue<string>());
            T.Eq("upload says PUT", "PUT", put["http_method"]?.GetValue<string>());
            var content = new ByteArrayContent(sourceBytes);
            using var uploaded = await http.PutAsync(put["url"]!.GetValue<string>(), content);
            T.Eq("PUT returns Created", HttpStatusCode.Created, uploaded.StatusCode);
            T.Check("PUT persisted identical raw bytes", File.ReadAllBytes(put["local_path"]!.GetValue<string>()).SequenceEqual(sourceBytes));
            using var replay = await http.PutAsync(put["url"]!.GetValue<string>(), new ByteArrayContent(sourceBytes));
            T.Eq("upload link cannot be used twice", HttpStatusCode.NotFound, replay.StatusCode);

            using var noToken = await http.GetAsync(baseUrl + "/files/" + new string('0', 48));
            T.Eq("unissued GET token rejected", HttpStatusCode.NotFound, noToken.StatusCode);
            try { Directory.Delete(shared, true); } catch { }
        });
    }
}
