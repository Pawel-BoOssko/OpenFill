// OpenFill - Metadata: wersja 0.1, data 2026-10-14 22:35
// OpenFill - Metadata: version 0.2, 2026-10-14 22:23
using System.Text.Json.Nodes;
using OpenFill.Core.Browser;

namespace OpenFill.Core.Agent;

/// <summary>Lists and uses only credentials explicitly imported by the person. Password values never reach tool output.</summary>
internal static class ImportedPasswordFiller
{
    public static async Task<ToolResult> ListAsync(BrowserController browser, string? site, CancellationToken ct)
    {
        var query = string.IsNullOrWhiteSpace(site) ? await browser.CurrentUrlAsync(ct) : site.Trim();
        var accounts = ImportedCredentialVault.ListForDomain(query);
        var array = new JsonArray();
        foreach (var account in accounts)
            array.Add(new JsonObject { ["origin"] = account.Origin, ["login"] = account.Login, ["realm"] = account.Realm, ["source_id"] = account.SourceId, ["created_ms"] = account.CreatedMillis, ["last_used_ms"] = account.LastUsedMillis, ["changed_ms"] = account.ChangedMillis });

        return ToolResult.Text(new JsonObject
        {
            ["site"] = query,
            ["count"] = accounts.Count,
            ["accounts"] = array
        }.ToJsonString());
    }

    public static async Task<ToolResult> FillAsync(BrowserController browser, string? login, string? origin, CancellationToken ct, string? realm = null, string? sourceId = null)
    {
        var site = await browser.CurrentUrlAsync(ct);
        IReadOnlyList<ImportedCredentialVault.Account> accounts;

        if (!string.IsNullOrWhiteSpace(origin))
        {
            var normalized = ImportedCredentialVault.NormalizeOrigin(origin);
            if (normalized is null)
                return ToolResult.Error("INVALID_ORIGIN: pass a full http/https origin returned by list_imported_logins.");
            if (!string.Equals(ImportedCredentialVault.NormalizeOrigin(site), normalized, StringComparison.OrdinalIgnoreCase))
                return ToolResult.Error("ORIGIN_MISMATCH: navigate to the exact saved credential origin before filling.");
            accounts = ImportedCredentialVault.ListForSite(normalized);
        }
        else
        {
            accounts = ImportedCredentialVault.ListForSite(site);
        }

        if (!string.IsNullOrWhiteSpace(login))
            accounts = accounts.Where(a => a.Login.Equals(login, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(realm))
            accounts = accounts.Where(a => string.Equals(a.Realm, realm, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(sourceId))
            accounts = accounts.Where(a => string.Equals(a.SourceId, sourceId, StringComparison.Ordinal)).ToList();

        if (accounts.Count == 0)
        {
            var related = ImportedCredentialVault.ListForDomain(site);
            if (!string.IsNullOrWhiteSpace(login))
                related = related.Where(a => a.Login.Equals(login, StringComparison.OrdinalIgnoreCase)).ToList();

            if (related.Count > 0)
            {
                var choices = string.Join(", ", related.Select(a => a.Login + " @ " + a.Origin));
                return ToolResult.Error("NO_EXACT_IMPORTED_ACCOUNT: related imported account(s) exist: " + choices +
                    ". Call list_imported_logins if needed, then call use_imported_password with both login and origin.");
            }
            return ToolResult.Error("NO_IMPORTED_ACCOUNT: use the OpenFill Import Firefox button to import an account for this website.");
        }

        if (accounts.Count > 1)
        {
            var choices = string.Join(", ", accounts.Select(a => (string.IsNullOrEmpty(a.Login) ? "(no login)" : a.Login) + " @ " + a.Origin + " | realm=" + (a.Realm ?? "(none)") + " | source_id=" + (a.SourceId ?? "(legacy)")));
            return ToolResult.Error("CHOOSE_CREDENTIAL: specify realm or source_id from list_imported_logins, rather than guessing among devices: " + choices);
        }

        var saved = accounts[0];
        var value = ImportedCredentialVault.Read(saved);
        if (value is null) return ToolResult.Error("ACCOUNT_UNAVAILABLE");

        var result = await browser.FillImportedCredentialAsync(saved.Login, value, ct);
        var filled = result?["ok"]?.GetValue<bool>() == true;
        var loginFilled = result?["loginFilled"]?.GetValue<bool>() == true;
        var loginFieldFound = result?["loginFieldFound"]?.GetValue<bool>() == true;
        return new ToolResult(
            "Imported credential filled: " + filled +
            "; login=" + saved.Login +
            "; origin=" + saved.Origin +
            "; realm=" + (saved.Realm ?? "(none)") +
            "; loginFieldFound=" + loginFieldFound +
            "; loginFilled=" + loginFilled,
            Ok: filled);
    }
}
