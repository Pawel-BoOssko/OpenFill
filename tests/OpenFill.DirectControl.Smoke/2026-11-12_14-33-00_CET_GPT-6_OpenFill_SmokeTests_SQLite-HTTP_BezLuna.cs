// 2026-11-12 14:33 CET | GPT-6 | OpenFill | verification of SQLite and HTTP API without Luna.
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using OpenFill.Core.Hosting;
using OpenFill.App;

static void Check(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS " + name);
}

var root = Path.Combine(Path.GetTempPath(), "openfill_direct_smoke_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var store = new ChatGptVisitStore(root);
    const string id = "6af5272a-c674-83eb-89bf-cc67dd9efd2f";
    const string baseUrl = "https://chatgpt.com/c/" + id;
    Check(ChatGptVisitStore.TryParse(baseUrl + "?foo=bar#fragment", out var parsed, out var canonical) &&
        parsed == id && canonical == baseUrl, "UUID key and canonical URL");
    Check(ChatGptVisitStore.TryParse("https://chatgpt.com/g/g-p-123/c/" + id, out parsed, out _) &&
        parsed == id, "project chat URL");
    Check(!ChatGptVisitStore.TryParse("https://other.net/c/" + id, out _, out _) &&
        !ChatGptVisitStore.TryParse("https://chatgpt.com/share/" + id, out _, out _), "reject unrelated URLs");
    var oldTime = DateTimeOffset.UtcNow.AddDays(-1);
    var now = DateTimeOffset.UtcNow;
    Check(store.Record(baseUrl, "ChatGPT", oldTime), "insert placeholder");
    Check(store.Record(baseUrl, "Tytul probny", now), "update real title and last access");
    Check(store.Record(baseUrl, "", oldTime), "do not erase title");
    Check(store.Record(baseUrl, "ChatGPT: Chat, Work, Create & Code with AI", oldTime),
        "ignore generic ChatGPT tab title");
    using (var db = new SqliteConnection("Data Source=" + store.DatabasePath))
    {
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT count(*), max(title), max(last_access_utc) FROM chat_visits WHERE chat_id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        Check(reader.Read() && reader.GetInt64(0) == 1 &&
            reader.GetString(1) == "Tytul probny" &&
            reader.GetString(2) == now.ToUniversalTime().ToString("O"), "SQLite upsert one row, monotonic last access");
    }
    Check(new ChatGptVisitStore(root).DatabasePath == store.DatabasePath, "DB reuse across restart");

    var callCount = 0;
    await using (var server = new DirectBrowserApi(root, command =>
    {
        Interlocked.Increment(ref callCount);
        return Task.FromResult(new JsonObject
        {
            ["ok"] = true, ["action"] = command["action"]?.GetValue<string>(),
            ["model_calls"] = 0
        });
    }, port: 17874))
    {
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false });
        var health = await http.GetAsync(server.Address + "health");
        Check(health.StatusCode == HttpStatusCode.OK, "direct HTTP loopback health");
        var unauthorized = await http.PostAsync(server.Address + "command",
            new StringContent("{\"action\":\"status\"}", Encoding.UTF8, "application/json"));
        Check(unauthorized.StatusCode == HttpStatusCode.Unauthorized, "token required");
        http.DefaultRequestHeaders.Add("X-OpenFill-Token", File.ReadAllText(server.TokenFile).Trim());
        var reply = await http.PostAsync(server.Address + "command",
            new StringContent("{\"action\":\"status\"}", Encoding.UTF8, "application/json"));
        var json = JsonNode.Parse(await reply.Content.ReadAsStringAsync());
        Check(reply.StatusCode == HttpStatusCode.OK && json?["model_calls"]?.GetValue<int>() == 0
            && callCount == 1, "model-free direct command");
    }
    Console.WriteLine("ALL_TESTS_PASSED");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

