// OpenFill - Metadata: wersja 0.1, data 2026-10-14 22:35
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;

namespace OpenFill.Core.Agent;

/// <summary>Persists temporary passwords in the current Windows user's encrypted Credential Manager.</summary>
public static class TemporaryCredentialVault
{
    private const uint Generic = 1;
    private const uint PersistLocalMachine = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite([In] ref Credential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr pointer);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    /// <summary>Delete a temporary password after it has been changed or when a test is finished.</summary>
    public static bool Delete(string service, string login)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(service) || string.IsNullOrWhiteSpace(login)) return false;
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(service.Trim() + "\n" + login.Trim())));
        return CredDelete("OpenFill/temporary/" + id, Generic, 0);
    }

    public static (bool Saved, string Message) Save(string service, string login, string password)
    {
        if (!OperatingSystem.IsWindows()) return (false, "Windows Credential Manager is unavailable");
        if (string.IsNullOrWhiteSpace(service) || string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
            return (false, "Service, login and password are required");
        if (password.Length > 1000) return (false, "Password exceeds the vault limit");
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(service.Trim() + "\n" + login.Trim())));
        var target = "OpenFill/temporary/" + id;
        var secretBytes = Encoding.Unicode.GetBytes(password);
        var pointer = Marshal.AllocHGlobal(secretBytes.Length);
        try
        {
            Marshal.Copy(secretBytes, 0, pointer, secretBytes.Length);
            var c = new Credential
            {
                Type = Generic,
                TargetName = target,
                Comment = "Temporary account password for " + service.Trim() + "; change immediately",
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = pointer,
                Persist = PersistLocalMachine,
                UserName = login.Trim()
            };
            if (!CredWrite(ref c, 0))
                return (false, "Windows Credential Manager could not save the temporary password (code " + Marshal.GetLastWin32Error() + ")");
            if (!CredRead(target, Generic, 0, out var saved))
                return (false, "Temporary password was written but persistence could not be verified");
            CredFree(saved);
            return (true, "Saved and verified in Windows Credential Manager as " + target);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            for (var i = 0; i < secretBytes.Length; i++) Marshal.WriteByte(pointer, i, 0);
            Marshal.FreeHGlobal(pointer);
        }
    }
}
