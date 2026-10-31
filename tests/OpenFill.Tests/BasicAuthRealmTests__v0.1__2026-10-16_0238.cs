// OpenFill synthetic Basic authentication realm matching regression, v0.1, 2026-10-16 02:38.
// No real router credentials or network connections.
using OpenFill.Core.Agent;

namespace OpenFill.Tests;

public static class BasicAuthRealmTests
{
    public static Task RunAsync() => T.Section("HTTP Basic: exact realm matching", () =>
    {
        T.Check("same realm matches", BasicAuthRealmMatcher.Matches("Basic realm=\"router\"", "router"));
        T.Check("realm substring does not match", !BasicAuthRealmMatcher.Matches("Basic realm=\"notrouter\"", "router"));
        T.Check("realm prefix does not match", !BasicAuthRealmMatcher.Matches("Basic realm=\"router-other\"", "router"));
        T.Check("realm is case sensitive", !BasicAuthRealmMatcher.Matches("Basic realm=\"Router\"", "router"));
        T.Check("scheme case insensitive", BasicAuthRealmMatcher.Matches("basic realm=\"router\"", "router"));
        T.Check("charset after realm", BasicAuthRealmMatcher.Matches("Basic realm=\"router\", charset=\"UTF-8\"", "router"));
        T.Check("charset before realm", BasicAuthRealmMatcher.Matches("Basic charset=\"UTF-8\", realm=\"router\"", "router"));
        T.Check("quoted comma in realm", BasicAuthRealmMatcher.Matches("Basic realm=\"router, guest\"", "router, guest"));
        T.Check("escaped quote in realm", BasicAuthRealmMatcher.Matches("Basic realm=\"router\\\" guest\"", "router\" guest"));
        T.Check("other authentication scheme rejected", !BasicAuthRealmMatcher.Matches("Digest realm=\"router\"", "router"));
        T.Check("missing realm rejected", !BasicAuthRealmMatcher.Matches("Basic charset=\"UTF-8\"", "router"));
        T.Check("duplicate realm rejected", !BasicAuthRealmMatcher.Matches("Basic realm=\"router\", realm=\"router\"", "router"));
        T.Check("trailing garbage rejected", !BasicAuthRealmMatcher.Matches("Basic realm=\"router\" garbage", "router"));
        T.Check("empty expected realm rejected", !BasicAuthRealmMatcher.Matches("Basic realm=\"router\"", ""));
        T.Check("token realm supported", BasicAuthRealmMatcher.Matches("Basic realm=router", "router"));
        T.Check("trailing comma is not a valid parameter", !BasicAuthRealmMatcher.Matches("Basic realm=\"router\",", "router"));
        T.Check("trailing comma with whitespace is rejected", !BasicAuthRealmMatcher.Matches("Basic realm=\"router\",  ", "router"));
        T.Check("valid second parameter is accepted", BasicAuthRealmMatcher.Matches("Basic realm=\"router\", charset=\"UTF-8\"", "router"));
        return Task.CompletedTask;
    });
}