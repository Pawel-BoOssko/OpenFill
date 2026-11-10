using OpenFill.Core.Agent;
using OpenFill.Core.Browser;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

internal static class WhatsAppEffectSmoke
{
    private sealed class Port : IWhatsAppEffectPort
    {
        public bool SenderVerified = true;
        public bool UiConfirmed = true;
        public int Sends;
        public Task<bool> AccountMatchesAsync(string account, CancellationToken ct) =>
            Task.FromResult(SenderVerified);
        public Task<JsonNode?> SendAsync(string target, string message, CancellationToken ct)
        {
            Sends++;
            JsonNode result = new JsonObject {
                ["status"] = UiConfirmed ? "sent_ui" : "unconfirmed",
                ["confirmation"] = UiConfirmed
                    ? "new_outgoing_bubble_visible" : "outgoing_bubble_not_confirmed",
                ["view"] = new JsonObject {
                    ["messages"] = new JsonArray(
                        new JsonObject { ["id"]="true_new", ["direction"]="out", ["text"]=message })
                }
            };
            return Task.FromResult<JsonNode?>(result);
        }
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new Exception("WHATSAPP_EFFECT " + reason);
    }

    private static JsonNode View(params (string Id, string Text, string Direction)[] rows)
    {
        var items = new JsonArray();
        foreach (var row in rows)
            items.Add(new JsonObject {
                ["id"]=row.Id, ["text"]=row.Text, ["direction"]=row.Direction
            });
        return new JsonObject { ["messages"]=items };
    }

    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "of_wa_effect_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string message = "Exact WhatsApp effect proof";
            var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message)));
            var key = new LocalPlanEffectKey("business-wa-020", "task-020", "run-020",
                "whatsapp", "254712345678", "254798765432", "send", sha, "send-first");
            var database = Path.Combine(root, "operations.db");
            var evidence = Path.Combine(root, "evidence");
            var port = new Port();
            var bridge = new WhatsAppLocalPlanEffectBridge(
                port, new OperationalMemoryStore(database), evidence);
            var first = await bridge.SendAsync(key, message);
            Require(first.Status == "verified" && first.JournalState == "verified",
                "first effect must be UI verified");
            Require(port.Sends == 1 && File.Exists(first.EvidenceRef),
                "evidence must be durably saved");
            var second = await bridge.SendAsync(key, message);
            Require(second.Status == "already_recorded" && port.Sends == 1,
                "same process duplicate blocked");
            var reopened = new WhatsAppLocalPlanEffectBridge(
                port, new OperationalMemoryStore(database), evidence);
            var third = await reopened.SendAsync(key with { TaskId="task-new" }, message);
            Require(third.Status == "already_recorded" && port.Sends == 1,
                "restart duplicate blocked");
            port.SenderVerified = false;
            var noSender = await reopened.SendAsync(
                key with { EffectId="send-second" }, message);
            Require(noSender.Status == "blocked_preflight" && port.Sends == 1,
                "unverified sender must not send");
            port.SenderVerified = true;
            port.UiConfirmed = false;
            var uncertainKey = key with { EffectId="send-third" };
            var uncertain = await reopened.SendAsync(uncertainKey, message);
            Require(uncertain.Status == "uncertain_effect" &&
                uncertain.JournalState == "uncertain" && port.Sends == 2,
                "missing UI proof is uncertain");
            var noReplay = await reopened.SendAsync(uncertainKey, message);
            Require(noReplay.Status == "already_recorded" && port.Sends == 2,
                "uncertain must never replay");

            var before = View(("true_old","Exact WhatsApp effect proof","out"));
            Require(!WhatsAppBrowser.HasNewOutgoingMessage(before,
                View(("true_old",message,"out")), message), "same ID is not new");
            Require(!WhatsAppBrowser.HasNewOutgoingMessage(View(),
                View(("true_new",message+" extra","out")), message),
                "substring is not exact proof");
            Require(!WhatsAppBrowser.HasNewOutgoingMessage(View(),
                View(("",message,"out")), message),
                "unidentified bubble cannot prove sending");
            Require(WhatsAppBrowser.HasNewOutgoingMessage(before,
                View(("true_old",message,"out"),("true_new",message,"out")), message),
                "new matching message ID is valid UI evidence");
            Console.WriteLine("ARCHFLOW_WHATSAPP_EFFECT_10_CHECKS_OK");
        }
        finally { try { Directory.Delete(root, recursive:true); } catch (IOException) { } }
    }
}
