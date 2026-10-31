// OpenFill - Metadata: wersja 0.1, data 2026-10-23 11:46
using OpenFill.Core.Hosting;

namespace OpenFill.Tests;

public static class AddressHistoryTests
{
    public static async Task RunAsync()
    {
        await T.Section("Address bar: persistent history, autocomplete and no secret URLs", () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "openfill_address_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "visited.json");
            try
            {
                var history = new AddressHistory(path);
                T.Eq("history initially empty", 0, history.Suggestions.Count);
                T.Check("rejects non-web schemes", !history.Remember("about:blank") &&
                    !history.Remember("file:///C:/private.txt") && !history.Remember("javascript:alert(1)"));
                T.Check("remembers HTTPS website", history.Remember("https://www.example.com/checkout?q=abc#secret"));
                T.Eq("suggestion matches partial hostname typing", "example.com/checkout", history.Suggestions[0]);
                T.Eq("choosing suggestion opens original site without secrets",
                    "https://www.example.com/checkout", history.Resolve("example.com/checkout"));
                T.Check("no query or fragment persisted", !File.ReadAllText(path).Contains("q=abc") &&
                    !File.ReadAllText(path).Contains("#secret"));
                T.Check("same page with new query deduplicated", !history.Remember("https://www.example.com/checkout?q=another"));
                T.Check("handles other address as more recent", history.Remember("https://carrefour.ke/"));
                T.Eq("most recent suggestion first", "carrefour.ke", history.Suggestions[0]);
                T.Eq("resolve partial text unchanged", "carre", history.Resolve("carre"));
                T.Check("rejects userinfo from stored link", history.Remember("https://login:passwd@host.example/order"));
                T.Check("credentials never written to disk", !File.ReadAllText(path).Contains("passwd"));
                T.Check("sensitive recovery URL saved as site origin", history.Remember("https://id.example/oauth/callback?token=xyz"));
                T.Eq("recovery suggestion shows only domain", "id.example", history.Suggestions[0]);
                T.Check("recovery token absent", !File.ReadAllText(path).Contains("xyz") &&
                    !File.ReadAllText(path).Contains("/oauth"));
                var reloaded = new AddressHistory(path);
                T.Eq("history survives browser restart", "id.example", reloaded.Suggestions[0]);
                T.Eq("earlier pages survive restart", "example.com/checkout",
                    reloaded.Suggestions.First(s => s.Contains("example.com/checkout")));
                for (var i = 0; i < 220; i++)
                    reloaded.Remember("https://site" + i + ".example/path");
                T.Eq("history capped at 200 sites", 200, reloaded.Suggestions.Count);
                T.Eq("newest site remains", "site219.example/path", reloaded.Suggestions[0]);
                T.Check("oldest entries are discarded", !reloaded.Suggestions.Contains("example.com/checkout"));
                File.WriteAllText(path, "{ broken");
                T.Eq("invalid JSON does not prevent browser startup", 0, new AddressHistory(path).Suggestions.Count);
            }
            finally { try { Directory.Delete(folder, true); } catch { } }
            return Task.CompletedTask;
        });
    }
}
