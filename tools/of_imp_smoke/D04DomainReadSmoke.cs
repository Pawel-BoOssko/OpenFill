using System.Text.Json.Nodes;
using OpenFill.Core.Agent;
using OpenFill.Core.Browser;

internal static class D04DomainReadSmoke
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool condition, string reason)
        {
            if (!condition) throw new Exception("D04_" + reason);
            checks++;
        }
        PageModel Page(string url, bool truncated = false) => PageModel.From(
            new JsonObject {
                ["url"] = url,
                ["title"] = "Fixture",
                ["headings"] = new JsonArray("Overview", "Videos"),
                ["buttons"] = new JsonArray(new JsonObject {
                    ["id"] = "b1", ["text"] = "Edit" }),
                ["looseFields"] = new JsonArray(new JsonObject {
                    ["id"] = "f1", ["label"] = "Search", ["value"] = "SECRET_NOT_EXPOSED" }),
                ["truncated"] = truncated
            });
        const string y = "https://studio.youtube.com/channel/UC00000000/videos/short";
        const string m = "https://business.facebook.com/latest/home?business_id=1234&asset_id=5678";
        JsonObject View(string provider, string intent, string id, string key, string url) =>
            DomainReadAuditService.Inspect(provider, intent, id, key, Page(url));
        var yt = View("youtube_studio", "audit_channel_readonly",
            "UC00000000", "channel_id", y);
        Check(yt["status"]?.ToString() == "observed_readonly", "youtube_scoped_read");
        Check(yt["authenticated"]?.GetValue<bool>() == false, "url_not_auth");
        Check(!yt.ToJsonString().Contains("SECRET_NOT_EXPOSED"), "omit_control_values");
        Check(yt["action_attempted"]?.GetValue<bool>() == false, "no_actions");
        Check(View("youtube_studio", "upload_schedule_short",
            "UC00000000", "channel_id", y)["status"]?.ToString() ==
            "observed_readonly", "youtube_upload_preflight");
        Check(View("youtube_studio", "audit_channel_readonly",
            "wrong", "channel_id", y)["reason_code"]?.ToString() ==
            "account_scope_mismatch", "youtube_mismatch");
        Check(View("youtube_studio", "audit_channel_readonly",
            "UC00000000", "channel_id", "https://evil.example/channel/UC00000000")
            ["status"]?.ToString() == "blocked", "youtube_host_rejected");
        Check(View("youtube_studio", "audit_channel_readonly",
            "UC00000000", "channel_id", "http://studio.youtube.com/channel/UC00000000")
            ["status"]?.ToString() == "blocked", "http_rejected");
        var meta = View("meta_business", "audit_page_readonly", "1234",
            "business_id", m);
        Check(meta["status"]?.ToString() == "observed_readonly", "meta_scoped_read");
        Check(View("meta_business", "reel_editor_workflow", "5678",
            "asset_id", m)["status"]?.ToString() == "observed_readonly", "meta_reel_preflight");
        Check(View("meta_business", "audit_page_readonly", "incorrect",
            "business_id", m)["reason_code"]?.ToString() ==
            "account_scope_mismatch", "meta_mismatch");
        Check(View("meta_business", "audit_page_readonly", "1234",
            "business_id", m + "&business_id=999")["status"]?.ToString() ==
            "blocked", "meta_duplicate_id_rejected");
        Check(View("meta_business", "schedule_page_posts", "1234",
            "business_id", m)["reason_code"]?.ToString() ==
            "unsupported_provider_intent", "unreviewed_intent_rejected");
        var incomplete = DomainReadAuditService.Inspect("meta_business",
            "audit_page_readonly", "1234", "business_id", Page(m, true));
        Check(incomplete["completeness"]?.ToString() ==
            "partial_visible", "partial_does_not_claim_all");
        var catalog = ProcedureCatalog.FromEmbedded();
        Check(catalog.Get("youtube_studio.audit_channel_readonly")?.Status ==
            "candidate", "new_procedure_candidate_only");
        Check(catalog.Get("meta_business.reel_editor_workflow")?.Status ==
            "candidate", "publication_candidate_only");
        Console.WriteLine("D04_DOMAIN_READ_" + checks + "_CHECKS_OK");
    }
}
