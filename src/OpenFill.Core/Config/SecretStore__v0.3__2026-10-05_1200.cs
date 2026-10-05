// OpenFill - Metadata: wersja 0.3, data 2026-10-05 12:00
using System.Text;

namespace OpenFill.Core.Config;

/// <summary>Platform-dependent secret encryption (on Windows: DPAPI in the windowed app).</summary>
public interface ISecretProtector
{
    string Name { get; }
    byte[] Protect(byte[] data);
    byte[] Unprotect(byte[] data);
}

/// <summary>No encryption (Linux / tests). The file gets owner-only permissions.</summary>
public sealed class PlainSecretProtector : ISecretProtector
{
    public string Name => "plain";
    public byte[] Protect(byte[] data) => data;
    public byte[] Unprotect(byte[] data) => data;
}

/// <summary>
/// OpenAI key: first the OPENAI_API_KEY environment variable, then a file in the instance directory.
/// The key never reaches the logs or the panel (the panel sees only the tail end).
/// </summary>
public sealed class SecretStore
{
    private readonly string _file;
    private readonly ISecretProtector _protector;

    public SecretStore(string file, ISecretProtector? protector = null)
    {
        _file = file;
        _protector = protector ?? new PlainSecretProtector();
    }

    public string? GetApiKey()
    {
        var env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
        try
        {
            if (!File.Exists(_file)) return null;
            var raw = File.ReadAllBytes(_file);
            var key = Encoding.UTF8.GetString(_protector.Unprotect(raw)).Trim();
            return key.Length > 0 ? key : null;
        }
        catch
        {
            return null;
        }
    }

    public string KeySource()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))) return "environment variable OPENAI_API_KEY";
        return File.Exists(_file) ? $"file ({_protector.Name})" : "none";
    }

    public void SetApiKey(string key)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllBytes(_file, _protector.Protect(Encoding.UTF8.GetBytes(key.Trim())));
        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(_file, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { /* best effort */ }
        }
    }

    public static string Mask(string? key) =>
        string.IsNullOrEmpty(key) ? "" : key.Length <= 8 ? "****" : $"…{key[^4..]}";
}
