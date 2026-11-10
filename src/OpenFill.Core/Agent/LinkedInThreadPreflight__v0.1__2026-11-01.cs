// OF-IMP-20261031 D02: read-only LinkedIn sender/thread/recipient preflight.
// No navigation, typing or sending. A failed or ambiguous check never authorizes a send.
using System.Text.Json.Nodes;
using OpenFill.Core.Browser;

namespace OpenFill.Core.Agent;

public sealed class LinkedInThreadPreflight(BrowserController browser)
{
    private const string ObserveScript = """
        (() => {
          const visible = e => !!(e && e.getClientRects().length && getComputedStyle(e).visibility !== 'hidden');
          const urls = selector => [...document.querySelectorAll(selector)]
            .filter(visible).map(e => e.href || e.closest('a')?.href || '')
            .filter(Boolean);
          const unique = xs => [...new Set(xs)];
          const sender = unique(urls('.global-nav__me a[href*="/in/"], .global-nav__primary-item--profile a[href*="/in/"], [data-test-global-nav-link="profile"][href*="/in/"]'));
          const target = unique(urls('.msg-thread__link-to-profile[href*="/in/"], .msg-thread__participant-link[href*="/in/"], .msg-thread__participant-names a[href*="/in/"], .msg-overlay-conversation-bubble__header a[href*="/in/"]'));
          const editors = [...document.querySelectorAll('.msg-form__contenteditable[contenteditable="true"], .msg-form [contenteditable="true"][role="textbox"]')]
            .filter(visible);
          return {url: location.href, sender_profiles: sender, target_profiles: target,
                  composer_count: unique(editors).length, observed_at: new Date().toISOString(),
                  scope: 'currently_loaded_dom'};
        })()
        """;

    public async Task<JsonObject> ObserveAndCheckAsync(string expectedSenderProfile,
        string expectedThreadUrl, string expectedTargetProfile, CancellationToken ct = default)
    {
        var raw = await browser.RunJsAsync(ObserveScript, ct);
        if (raw?["ok"]?.GetValue<bool>() != true || raw["value"] is not JsonObject observation)
            return Fail("observation_failed", raw?["error"]?.ToString() ?? "no_dom_observation");
        var checkedView = Check(observation, expectedSenderProfile, expectedThreadUrl, expectedTargetProfile);
        // LinkedIn's current navigation hides the own-profile link until "Me"
        // is expanded. Reuse the existing bounded account-menu probe only when
        // NO sender link was observed; never override an observed mismatch.
        if (checkedView["reason_code"]?.ToString() == "account_unverified_or_mismatch" &&
            observation["sender_profiles"] is JsonArray visibleSenders && visibleSenders.Count == 0)
        {
            var menuResult = await browser.RunJsAsync(LinkedInAccountMenuProbe.Javascript, ct);
            if (menuResult?["ok"]?.GetValue<bool>() == true &&
                menuResult["value"] is JsonArray menuProfiles && menuProfiles.Count > 0)
            {
                observation["sender_profiles"] = menuProfiles.DeepClone();
                return Check(observation, expectedSenderProfile, expectedThreadUrl, expectedTargetProfile);
            }
        }
        return checkedView;
    }

    public static JsonObject Check(JsonObject observation, string expectedSenderProfile,
        string expectedThreadUrl, string expectedTargetProfile)
    {
        var sender = Canonical(expectedSenderProfile, "/in/");
        var target = Canonical(expectedTargetProfile, "/in/");
        var thread = Canonical(expectedThreadUrl, "/messaging/");
        if (sender is null || target is null || thread is null)
            return Fail("invalid_expected_identity", "Exact HTTPS LinkedIn sender, recipient and thread URLs required.");
        var current = Canonical(observation["url"]?.ToString(), "/messaging/");
        if (current is null)
            return Fail("auth_required_or_wrong_page", "Current view is not a LinkedIn messaging thread.");
        if (!string.Equals(current, thread, StringComparison.OrdinalIgnoreCase))
            return Fail("thread_mismatch", "Observed messaging thread differs from expected thread.");
        var observedSenders = Profiles(observation["sender_profiles"]);
        if (observedSenders.Count != 1 || !observedSenders.Contains(sender))
            return Fail("account_unverified_or_mismatch", "A unique exact sender profile is not visible in the account navigation.");
        var observedTargets = Profiles(observation["target_profiles"]);
        if (observedTargets.Count != 1 || !observedTargets.Contains(target))
            return Fail("target_unverified_or_mismatch", "A unique exact recipient profile is not visible in the thread header.");
        if (observation["composer_count"]?.GetValue<int>() != 1)
            return Fail("composer_missing_or_ambiguous", "Exactly one visible message composer is required.");
        return new JsonObject
        {
            ["status"] = "ready",
            ["reason_code"] = "exact_sender_thread_target_and_composer",
            ["sender_profile_url"] = sender,
            ["target_profile_url"] = target,
            ["thread_url"] = thread,
            ["scope"] = "currently_loaded_dom",
            ["complete"] = false,
            ["observed_at"] = observation["observed_at"]?.ToString()
        };
    }

    private static HashSet<string> Profiles(JsonNode? values)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (values is JsonArray array)
            foreach (var item in array)
            {
                var canonical = Canonical(item?.ToString(), "/in/");
                if (canonical is not null) found.Add(canonical);
            }
        return found;
    }

    private static string? Canonical(string? value, string prefix)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            (uri.Host != "www.linkedin.com" && uri.Host != "linkedin.com") ||
            !uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var suffix = uri.AbsolutePath[prefix.Length..].Trim('/');
        if (prefix == "/messaging/")
        {
            if (!suffix.StartsWith("thread/", StringComparison.OrdinalIgnoreCase)) return null;
            suffix = suffix["thread/".Length..];
            if (suffix.Length == 0 || suffix.Contains('/') || suffix.Contains('%')) return null;
            return "https://www.linkedin.com/messaging/thread/" + suffix.ToLowerInvariant();
        }
        if (suffix.Length == 0 || suffix.Contains('/') || suffix.Contains('%'))
            return null;
        return "https://www.linkedin.com" + prefix + suffix.ToLowerInvariant();
    }

    private static JsonObject Fail(string code, string reason) => new()
    {
        ["status"] = "blocked", ["reason_code"] = code, ["reason"] = reason
    };
}
