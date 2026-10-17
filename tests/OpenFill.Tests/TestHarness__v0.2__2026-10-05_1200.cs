// OpenFill - Metadata: wersja 0.2, data 2026-10-05 12:00
namespace OpenFill.Tests;

/// <summary>Minimal test harness (no NuGet; the registry is blocked in this environment).</summary>
public static class T
{
    public static int Passed, Failed;
    public static readonly List<string> Failures = new();

    public static void Check(string name, bool ok, string? detail = null)
    {
        if (ok) { Passed++; Console.WriteLine($"  OK  {name}"); }
        else { Failed++; Failures.Add(name + (detail != null ? " — " + detail : "")); Console.WriteLine($" FAIL {name}{(detail != null ? " — " + detail : "")}"); }
    }

    public static void Eq(string name, object? expected, object? actual)
        => Check(name, Equals(expected, actual), $"expected <{expected}>, was <{actual}>");

    public static void Contains(string name, string haystack, string needle)
        => Check(name, haystack.Contains(needle), $"missing \"{needle}\" in: {Trim(haystack)}");

    public static void NotContains(string name, string haystack, string needle)
        => Check(name, !haystack.Contains(needle), $"found \"{needle}\" in: {Trim(haystack)}");

    private static string Trim(string s) => s.Length > 160 ? s[..160] + "…" : s;

    public static async Task Section(string title, Func<Task> body)
    {
        Console.WriteLine("\n== " + title + " ==");
        try { await body(); }
        catch (Exception ex) { Failed++; Failures.Add(title + " THREW: " + ex.Message); Console.WriteLine($" FAIL {title} — exception: {ex}"); }
    }

    public static int Report()
    {
        Console.WriteLine($"\n==================== TESTS ====================");
        Console.WriteLine($"Passed: {Passed}, Failed: {Failed}");
        foreach (var f in Failures) Console.WriteLine("  - " + f);
        Console.WriteLine("==============================================");
        return Failed == 0 ? 0 : 1;
    }
}
