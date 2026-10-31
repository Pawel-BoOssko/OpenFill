// OpenFill Basic Auth challenge parser, v0.1, 2026-10-16 02:37 Europe/Warsaw.
// No secrets or real credentials are handled here.
using System.Text;

namespace OpenFill.Core.Agent;

public static class BasicAuthRealmMatcher
{
    public static bool Matches(string? challenge, string? expectedRealm)
    {
        if (string.IsNullOrEmpty(challenge) || string.IsNullOrEmpty(expectedRealm)) return false;
        var s = challenge.AsSpan().Trim();
        if (s.Length < 6 || !s[..5].Equals("Basic".AsSpan(), StringComparison.OrdinalIgnoreCase)
            || (s[5] != ' ' && s[5] != '\t')) return false;

        int i = 5;
        string? realm = null;
        while (i < s.Length)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) break;
            int keyStart = i;
            while (i < s.Length && IsToken(s[i])) i++;
            if (i == keyStart) return false;
            var key = s[keyStart..i];
            SkipSpace(s, ref i);
            if (i >= s.Length || s[i] != '=') return false;
            i++;
            SkipSpace(s, ref i);
            if (i >= s.Length) return false;
            string value;
            if (s[i] == '"')
            {
                i++;
                var b = new StringBuilder();
                bool closed = false;
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') { closed = true; break; }
                    if (c == '\\')
                    {
                        if (i >= s.Length) return false;
                        c = s[i++];
                    }
                    b.Append(c);
                }
                if (!closed) return false;
                value = b.ToString();
            }
            else
            {
                int valueStart = i;
                while (i < s.Length && IsToken(s[i])) i++;
                if (i == valueStart) return false;
                value = s[valueStart..i].ToString();
            }

            if (key.Equals("realm".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                if (realm is not null) return false; // duplicate realm is ambiguous
                realm = value;
            }
            SkipSpace(s, ref i);
            if (i < s.Length)
            {
                if (s[i] != ',') return false;
                i++;
                SkipSpace(s, ref i);
                if (i >= s.Length) return false; // trailing comma without another parameter
            }
        }
        return realm is not null && string.Equals(realm, expectedRealm, StringComparison.Ordinal);
    }

    private static void SkipSpace(ReadOnlySpan<char> s, ref int i)
    {
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
    }

    private static bool IsToken(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'
        or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-'
        or '.' or '^' or '_' or '|' or '~';
}