// OpenFill - Metadata: wersja 0.1, data 2026-10-14 22:35
// OpenFill - Metadata: version 0.3, updated 2026-10-15 21:19
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenFill.Core.Agent;

/// <summary>Selected Firefox credentials in Windows Credential Manager, scoped to an approved website origin.</summary>
public static class ImportedCredentialVault
{
    private const uint Generic = 1;
    private const uint PersistLocalMachine = 2;
    private const string Prefix = "OpenFill/imported/";
    // Site-family metadata uses the embedded Mozilla PSL, including private hosting suffixes.
    // Password access itself remains exact-origin; family matching never reads a secret.

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags, Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredEnumerate(string filter, uint flags, out uint count, out IntPtr credentials);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr p);

    public sealed record Account(string Origin, string Login, string Target, string? Realm = null, long CreatedMillis = 0, long LastUsedMillis = 0, long ChangedMillis = 0, string? SourceId = null);

    public static string? NormalizeOrigin(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http") || string.IsNullOrWhiteSpace(uri.Host))
            return null;
        return uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
    }

    private static string Key(string origin, string login)
    {
        var hashed = SHA256.HashData(Encoding.UTF8.GetBytes(origin + "\n" + login));
        return Prefix + Convert.ToHexString(hashed);
    }

    public static (bool Saved, string Message) Save(string sourceUrl, string login, string password, string? realm = null, string? sourceId = null, long createdMillis = 0, long lastUsedMillis = 0, long changedMillis = 0)
    {
        if (!OperatingSystem.IsWindows()) return (false, "Windows Credential Manager unavailable");
        var origin = NormalizeOrigin(sourceUrl);
        if (origin is null || (string.IsNullOrWhiteSpace(login) && string.IsNullOrWhiteSpace(sourceId)) || string.IsNullOrEmpty(password))
            return (false, "A valid website, login and password are required");
        var bytes = Encoding.Unicode.GetBytes(password);
        if (bytes.Length > 2560) return (false, "Credential exceeds Windows vault limit");
        var pointer = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            var name = string.IsNullOrWhiteSpace(sourceId) ? Key(origin, login) : Key(origin, login + "\nfirefox:" + sourceId);
            var existed = CredRead(name, Generic, 0, out var previous);
            if (existed) CredFree(previous);
            var entry = new Credential
            {
                Type = Generic, TargetName = name, Comment = string.IsNullOrWhiteSpace(sourceId) ? origin : EncodeMetadata(origin, realm, sourceId, createdMillis, lastUsedMillis, changedMillis),
                UserName = login, Persist = PersistLocalMachine,
                CredentialBlobSize = (uint)bytes.Length, CredentialBlob = pointer
            };
            if (!CredWrite(ref entry, 0))
                return (false, "Windows vault write failed (code " + Marshal.GetLastWin32Error() + ")");
            if (!CredRead(name, Generic, 0, out var verify))
                return (false, "Credential written but verification failed");
            try
            {
                var verifiedCredential = Marshal.PtrToStructure<Credential>(verify);
                if (verifiedCredential.CredentialBlobSize != bytes.Length ||
                    verifiedCredential.CredentialBlob == IntPtr.Zero)
                    return (false, "Credential write verification failed: stored size differs");
                var stored = new byte[bytes.Length];
                try
                {
                    Marshal.Copy(verifiedCredential.CredentialBlob, stored, 0, stored.Length);
                    if (!CryptographicOperations.FixedTimeEquals(bytes, stored))
                        return (false, "Credential write verification failed: stored value differs");
                }
                finally { CryptographicOperations.ZeroMemory(stored); }
            }
            finally { CredFree(verify); }
            return (true, (existed ? "Updated" : "Created") + " imported account for " + origin + " | " + (string.IsNullOrEmpty(login) ? "(no login)" : login) + (string.IsNullOrWhiteSpace(realm) ? "" : " | " + realm));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            for (int i = 0; i < bytes.Length; i++) Marshal.WriteByte(pointer, i, 0);
            Marshal.FreeHGlobal(pointer);
        }
    }

    // The vault's Comment field stores only safe Firefox metadata (never the password).
    // Keep legacy origin-only comments readable.
    private static string EncodeMetadata(string origin, string? realm, string sourceId,
        long createdMillis, long lastUsedMillis, long changedMillis)
        => "OF2:" + JsonSerializer.Serialize(new { o = origin, r = (realm ?? "").Length > 90 ? realm![..90] : realm ?? "",
            i = sourceId.Length > 50 ? sourceId[..50] : sourceId,
            c = createdMillis, u = lastUsedMillis, p = changedMillis });

    private static Account? AccountFromCredential(Credential c)
    {
        string originText = c.Comment ?? "";
        string? realm = null, id = null;
        long created = 0, used = 0, changed = 0;
        if (originText.StartsWith("OF2:", StringComparison.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(originText[4..]);
                var r = doc.RootElement;
                originText = r.GetProperty("o").GetString() ?? "";
                realm = r.TryGetProperty("r", out var label) ? label.GetString() : null;
                id = r.TryGetProperty("i", out var key) ? key.GetString() : null;
                created = r.TryGetProperty("c", out var cdate) ? cdate.GetInt64() : 0;
                used = r.TryGetProperty("u", out var udate) ? udate.GetInt64() : 0;
                changed = r.TryGetProperty("p", out var pdate) ? pdate.GetInt64() : 0;
            }
            catch { return null; }
        }
        var origin = NormalizeOrigin(originText);
        return origin is not null && c.TargetName.StartsWith(Prefix, StringComparison.Ordinal)
            ? new Account(origin, c.UserName ?? "", c.TargetName, realm, created, used, changed, id) : null;
    }
    /// <summary>Exact-origin lookup. This remains the default boundary for using a credential.</summary>
    public static IReadOnlyList<Account> ListForSite(string url)
    {
        var origin = NormalizeOrigin(url);
        if (origin is null) return Array.Empty<Account>();
        return ListAll()
            .Where(a => a.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Login, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Safe metadata lookup for a website family. It returns account names and origins only, never secrets.
    /// Useful when a service uses sibling subdomains such as panel.example.pl and login.example.pl.
    /// </summary>
    public static IReadOnlyList<Account> ListForDomain(string siteOrDomain)
    {
        var host = HostFromSiteOrDomain(siteOrDomain);
        if (host is null) return Array.Empty<Account>();
        var family = SiteFamily(host);
        return ListAll()
            .Where(a =>
            {
                var h = HostFromSiteOrDomain(a.Origin);
                return h is not null && SiteFamily(h).Equals(family, StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(a => a.Origin, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Login, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Safe metadata for the user-facing credential list. Returns origins and login names only, never passwords.</summary>
    public static IReadOnlyList<Account> ListAllMetadata() => ListAll()
        .OrderBy(a => a.Origin, StringComparer.OrdinalIgnoreCase)
        .ThenBy(a => a.Login, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static bool SameSiteFamily(string firstSite, string secondSite)
    {
        var a = HostFromSiteOrDomain(firstSite);
        var b = HostFromSiteOrDomain(secondSite);
        return a is not null && b is not null &&
               SiteFamily(a).Equals(SiteFamily(b), StringComparison.OrdinalIgnoreCase);
    }

    private static string? HostFromSiteOrDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) && !string.IsNullOrWhiteSpace(absolute.Host))
            return absolute.Host.TrimEnd('.').ToLowerInvariant();
        if (!value.Contains("://", StringComparison.Ordinal) &&
            Uri.TryCreate("https://" + value.Trim('/'), UriKind.Absolute, out var domain) &&
            !string.IsNullOrWhiteSpace(domain.Host))
            return domain.Host.TrimEnd('.').ToLowerInvariant();
        return null;
    }

    private static string SiteFamily(string host) => PublicSuffixResolver.RegistrableDomain(host);

    private static IReadOnlyList<Account> ListAll()
    {
        if (!OperatingSystem.IsWindows() || !CredEnumerate(Prefix + "*", 0, out var count, out var array))
            return Array.Empty<Account>();
        try
        {
            var found = new List<Account>();
            for (int i = 0; i < count; i++)
            {
                var p = Marshal.ReadIntPtr(array, i * IntPtr.Size);
                var c = Marshal.PtrToStructure<Credential>(p);
                var account = AccountFromCredential(c);
                if (account is not null) found.Add(account);
            }
            return found;
        }
        finally { CredFree(array); }
    }

    public static string? Read(Account account)
    {
        if (!account.Target.StartsWith(Prefix, StringComparison.Ordinal) ||
            !CredRead(account.Target, Generic, 0, out var ptr)) return null;
        try
        {
            var c = Marshal.PtrToStructure<Credential>(ptr);
            if (c.CredentialBlobSize == 0 || c.CredentialBlobSize > 2560 ||
                c.CredentialBlobSize % 2 != 0 || c.CredentialBlob == IntPtr.Zero) return null;
            var bytes = new byte[c.CredentialBlobSize];
            Marshal.Copy(c.CredentialBlob, bytes, 0, bytes.Length);
            try { return Encoding.Unicode.GetString(bytes); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        finally { CredFree(ptr); }
    }

    public static bool Delete(Account account) => account.Target.StartsWith(Prefix, StringComparison.Ordinal) && CredDelete(account.Target, Generic, 0);

    public static bool Delete(string origin, string login)
    {
        var normalized = NormalizeOrigin(origin);
        return normalized is not null && CredDelete(Key(normalized, login), Generic, 0);
    }
}
