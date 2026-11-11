using OpenFill.Core.Agent;

namespace OpenFill.Tests;

public static class ToolArgumentParserTests
{
    public static async Task RunAsync()
    {
        await T.Section("A22: duplicate model tool arguments do not crash task", () =>
        {
            const string identical = """
                {"steps":[{"selector":"#send","action":"click",
                  "expected_host":"127.0.0.1",
                  "verify":{"selector":"#status","expected_host":"127.0.0.1",
                    "check":"text_contains","expected":"sent",
                    "expected_host":"127.0.0.1"}}]}
                """;
            var parsed = ToolArgumentParser.Parse(identical);
            T.Check("identical duplicate accepted", parsed.Error is null);
            T.Eq("kept expected host", "127.0.0.1",
                parsed.Args["steps"]![0]!["verify"]!["expected_host"]!.ToString());
            T.Eq("one expected_host key", 1, parsed.Args["steps"]![0]!["verify"]!
                .AsObject().Count(x => x.Key == "expected_host"));
            const string conflicting = """
                {"steps":[{"selector":"#send","action":"click","expected_host":"127.0.0.1",
                  "expected_host":"another.host"}]}
                """;
            var conflict = ToolArgumentParser.Parse(conflicting);
            T.Check("different hosts stop without actions",
                conflict.Error?.StartsWith("duplicate_conflicting_property:") == true &&
                conflict.Args.Count == 0);
            var malformed = ToolArgumentParser.Parse("{broken");
            T.Check("bad JSON becomes tool error", malformed.Error is not null);
            var normal = ToolArgumentParser.Parse("""{"steps":[{"action":"navigate","url":"http://127.0.0.1/"}]}""");
            T.Check("ordinary arguments unchanged", normal.Error is null &&
                normal.Args["steps"]![0]!["action"]!.ToString() == "navigate");
            return Task.CompletedTask;
        });
    }
}
