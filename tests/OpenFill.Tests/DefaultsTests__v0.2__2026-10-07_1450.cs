// OpenFill - Metadata: wersja 0.2, data 2026-10-07 14:50
using OpenFill.Core.Config;

namespace OpenFill.Tests;

/// <summary>Built-in defaults: the price list of a fresh install and the (always empty) key in the repository.</summary>
public static class DefaultsTests
{
    public static async Task RunAsync()
    {
        await T.Section("Defaults: built-in prices and no key in the repository", () =>
        {
            var cfg = new AppConfig();
            var p = cfg.FindPrice("gpt-6-luna");
            T.Check("fresh config has the default model price", p != null && p.Input == 0.1 && p.CachedInput == 0.01 && p.Output == 0.5);
            T.Check("dated snapshot of the default model is priced", cfg.FindPrice("gpt-6-luna-2026-09-01") != null);
            T.Check("unknown model has no price", cfg.FindPrice("some-other-model") == null);

            var own = AppConfig.FileJson;
            var loaded = System.Text.Json.JsonSerializer.Deserialize<AppConfig>("{\"prices\":{\"x\":{\"input\":2,\"output\":3}}}", own)!;
            T.Check("a config with its own prices keeps them and still has the defaults", loaded.FindPrice("x") != null && loaded.FindPrice("gpt-6-luna") != null);
            var empty = System.Text.Json.JsonSerializer.Deserialize<AppConfig>("{\"prices\":{}}", own)!;
            T.Check("empty prices in config.json: defaults still apply", empty.FindPrice("gpt-6-luna") != null);
            var nul = System.Text.Json.JsonSerializer.Deserialize<AppConfig>("{\"prices\":null}", own)!;
            T.Check("null prices in config.json: defaults still apply", nul.FindPrice("gpt-6-luna") != null);
            var over = System.Text.Json.JsonSerializer.Deserialize<AppConfig>("{\"prices\":{\"gpt-6-luna\":{\"input\":9,\"output\":9}}}", own)!;
            T.Check("a price set in config.json overrides the default", over.FindPrice("gpt-6-luna")?.Input == 9);

            T.Check("repository build has NO embedded key", EmbeddedDefaults.ApiKey.Length == 0);

            // Fallback order: environment variable, saved file, built-in key. Here neither of the first two exists.
            var savedEnv = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            try
            {
                Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
                var missing = Path.Combine(Path.GetTempPath(), "openfill-test-nokey-" + Guid.NewGuid().ToString("N"), "openai.key");
                var store = new SecretStore(missing);
                var got = store.GetApiKey();
                var expected = EmbeddedDefaults.ApiKey.Trim().Length > 0 ? EmbeddedDefaults.ApiKey.Trim() : null;
                T.Check("no variable, no file: the built-in key is used (none in the repository)", got == expected);
                T.Check("key source names the built-in key only when there is one", (store.KeySource() == "built into this build") == (expected != null));

                Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-test-from-env");
                T.Check("the environment variable beats the built-in key", store.GetApiKey() == "sk-test-from-env");
            }
            finally { Environment.SetEnvironmentVariable("OPENAI_API_KEY", savedEnv); }
            return Task.CompletedTask;
        });
    }
}
