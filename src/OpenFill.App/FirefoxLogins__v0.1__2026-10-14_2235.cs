// OpenFill - Metadata: wersja 0.1, data 2026-10-14 22:35
// OpenFill - Metadata: version 0.1, 2026-10-13 22:29
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenFill.App;

/// <summary>Firefox profile reader. Listing exposes URLs only; credentials are decrypted in memory for the user-selected item.</summary>
internal static class FirefoxLogins
{
    internal sealed record Item(string Host, string EncryptedUsername, string EncryptedPassword, string Id, string? Realm = null, long CreatedMillis = 0, long LastUsedMillis = 0, long ChangedMillis = 0, int TimesUsed = 0);
    internal static string? FindProfile()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox");
        var ini = Path.Combine(root, "profiles.ini");
        if (File.Exists(ini))
        {
            string? path = null;
            bool defaultProfile = false, relative = true;
            foreach (var line in File.ReadLines(ini).Append("[NEXT]"))
            {
                var s = line.Trim();
                if (s.StartsWith("[", StringComparison.Ordinal))
                {
                    if (path is not null && defaultProfile)
                    {
                        var p = relative ? Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)) : path;
                        if (File.Exists(Path.Combine(p, "logins.json")) && File.Exists(Path.Combine(p, "key4.db"))) return p;
                    }
                    path = null; defaultProfile = false; relative = true;
                }
                else if (s.StartsWith("Path=", StringComparison.OrdinalIgnoreCase)) path = s[5..].Trim();
                else if (s.Equals("Default=1", StringComparison.OrdinalIgnoreCase)) defaultProfile = true;
                else if (s.Equals("IsRelative=0", StringComparison.OrdinalIgnoreCase)) relative = false;
            }
        }
        var profiles = Path.Combine(root, "Profiles");
        if (!Directory.Exists(profiles)) return null;
        return Directory.GetDirectories(profiles)
            .Where(p => File.Exists(Path.Combine(p, "logins.json")) && File.Exists(Path.Combine(p, "key4.db")))
            .OrderByDescending(p => Directory.GetLastWriteTimeUtc(Path.Combine(p, "logins.json")))
            .FirstOrDefault();
    }

    internal static IReadOnlyList<Item> List(string profile)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(profile, "logins.json")));
        var list = new List<Item>();
        foreach (var element in doc.RootElement.GetProperty("logins").EnumerateArray())
        {
            if (element.TryGetProperty("deleted", out var del) && del.ValueKind == JsonValueKind.True) continue;
            var host = element.TryGetProperty("hostname", out var h) ? h.GetString() : null;
            var eu = element.TryGetProperty("encryptedUsername", out var u) ? u.GetString() : null;
            var ep = element.TryGetProperty("encryptedPassword", out var p) ? p.GetString() : null;
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(eu) || string.IsNullOrWhiteSpace(ep)) continue;
                        static long Time(JsonElement item, string field) => item.TryGetProperty(field, out var value) && value.TryGetInt64(out var time) ? time : 0;
            var realm = element.TryGetProperty("httpRealm", out var realmValue) && realmValue.ValueKind == JsonValueKind.String ? realmValue.GetString() : null;
            var times = element.TryGetProperty("timesUsed", out var used) && used.TryGetInt32(out var count) ? count : 0;
            list.Add(new Item(host!, eu!, ep!,
                element.TryGetProperty("guid", out var g) ? g.GetString() ?? "" : "",
                realm, Time(element, "timeCreated"), Time(element, "timeLastUsed"),
                Time(element, "timePasswordChanged"), times));
        }
        return list.OrderBy(x => x.Host, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

/// <summary>NSS native Firefox decryption; runs locally after the operator opens the chooser. No credential appears in logs.</summary>
internal sealed class FirefoxNssSession : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SecItem { public int Type; public IntPtr Data; public uint Length; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NssInit([MarshalAs(UnmanagedType.LPUTF8Str)] string profileDir);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NssShutdown();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SdrDecrypt(ref SecItem source, ref SecItem dest, IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeItem(ref SecItem item, int freeItem);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetKeySlot();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SlotNeedsLogin(IntPtr slot);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CheckUserPassword(IntPtr slot, [MarshalAs(UnmanagedType.LPUTF8Str)] string password);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void FreeSlot(IntPtr slot);

    private readonly IntPtr _lib;
    private readonly string _copyDir;
    private readonly NssShutdown _shutdown;
    private readonly SdrDecrypt _decrypt;
    private readonly FreeItem _free;
    private readonly GetKeySlot _getSlot;
    private readonly SlotNeedsLogin _needsLogin;
    private readonly CheckUserPassword _checkPassword;
    private readonly FreeSlot _freeSlot;
    private bool _initialized;
    private bool _disposed;

    private static T Fn<T>(IntPtr h, string n) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(h, n));

    public FirefoxNssSession(string firefoxProfile)
    {
        // Work from a copy to avoid locking the live Firefox sqlite databases.
        _copyDir = Path.Combine(Path.GetTempPath(), "openfill-ff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_copyDir);
        try
        {
            foreach (var name in new[] { "key4.db", "cert9.db", "pkcs11.txt" })
                if (File.Exists(Path.Combine(firefoxProfile, name)))
                    File.Copy(Path.Combine(firefoxProfile, name), Path.Combine(_copyDir, name));
            var libPath = new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mozilla Firefox", "nss3.dll"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mozilla Firefox", "nss3.dll")
            }.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("Firefox nss3.dll is missing");
            _lib = LoadLibraryEx(libPath, IntPtr.Zero, 0x00000100 | 0x00001000);
            if (_lib == IntPtr.Zero) throw new InvalidOperationException("Cannot load Firefox NSS native library (" + Marshal.GetLastWin32Error() + ")");
            _shutdown = Fn<NssShutdown>(_lib, "NSS_Shutdown");
            _decrypt = Fn<SdrDecrypt>(_lib, "PK11SDR_Decrypt");
            _free = Fn<FreeItem>(_lib, "SECITEM_FreeItem");
            _getSlot = Fn<GetKeySlot>(_lib, "PK11_GetInternalKeySlot");
            _needsLogin = Fn<SlotNeedsLogin>(_lib, "PK11_NeedLogin");
            _checkPassword = Fn<CheckUserPassword>(_lib, "PK11_CheckUserPassword");
            _freeSlot = Fn<FreeSlot>(_lib, "PK11_FreeSlot");
            var init = Fn<NssInit>(_lib, "NSS_Init");
            if (init("sql:" + _copyDir) != 0)
                throw new InvalidOperationException("Firefox NSS could not unlock the copied profile database");
            _initialized = true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string fileName, IntPtr file, uint flags);

    public bool RequiresPrimaryPassword
    {
        get
        {
            var slot = _getSlot();
            if (slot == IntPtr.Zero) return false;
            try { return _needsLogin(slot) != 0; }
            finally { _freeSlot(slot); }
        }
    }

    public bool Unlock(string password)
    {
        var slot = _getSlot();
        if (slot == IntPtr.Zero) return false;
        try { return _checkPassword(slot, password) == 0; }
        finally { _freeSlot(slot); }
    }

    public string Decrypt(string encrypted)
    {
        if (_disposed || !_initialized) throw new ObjectDisposedException(nameof(FirefoxNssSession));
        var bytes = Convert.FromBase64String(encrypted);
        IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            var src = new SecItem { Type = 0, Data = ptr, Length = (uint)bytes.Length };
            var dest = new SecItem();
            if (_decrypt(ref src, ref dest, IntPtr.Zero) != 0)
                throw new InvalidOperationException("Firefox could not decrypt this credential. Check the primary password.");
            try
            {
                var plaintext = new byte[dest.Length];
                Marshal.Copy(dest.Data, plaintext, 0, plaintext.Length);
                try { return Encoding.UTF8.GetString(plaintext); }
                finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext); }
            }
            finally { _free(ref dest, 0); }
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
            for (int i = 0; i < bytes.Length; i++) Marshal.WriteByte(ptr, i, 0);
            Marshal.FreeHGlobal(ptr);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { if (_initialized) _shutdown(); } catch { }
        try { if (Directory.Exists(_copyDir)) Directory.Delete(_copyDir, true); } catch { }
        if (_lib != IntPtr.Zero) try { NativeLibrary.Free(_lib); } catch { }
    }
}
