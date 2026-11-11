// CAPTURE_TASK_OUTCOME_20261030
// OpenFill SOURCE only - reusable successful capture profiles, 2026-10-30
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenFill.Core.Agent;

public sealed class CaptureRecipe
{
    public string Host { get; set; } = "";
    public string Path { get; set; } = "/";
    public string Mode { get; set; } = "all";
    public string Selector { get; set; } = "body";
    public string Pattern { get; set; } = "";
    public string Operation { get; set; } = "capture";
    public string ViewSignature { get; set; } = "";
    public string AccountScope { get; set; } = "";
    public string FilterKind { get; set; } = "regex";
    public string FilterPlan { get; set; } = "";
    public string Confidence { get; set; } = "candidate";
    public string SuspendReason { get; set; } = "";
    public int Successes { get; set; } // successful technical DOM captures
    public int TaskSuccesses { get; set; }
    public int TaskFailures { get; set; }
    public int TaskPartials { get; set; }
    public string LastTaskOutcomeUtc { get; set; } = "";
    public long LastElapsedMs { get; set; }
    public string LastSuccessfulUtc { get; set; } = "";
}

public sealed class CaptureRecipeStore(string directory)
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private string FileFor(string host)
    {
        var safe = Regex.Replace(host.Trim().ToLowerInvariant(), "[^a-z0-9.-]", "_");
        return Path.Combine(directory, safe + ".json");
    }

    public IReadOnlyList<CaptureRecipe> Get(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return Array.Empty<CaptureRecipe>();
        lock (Gate)
        {
            try
            {
                var file = FileFor(host);
                return File.Exists(file) ? JsonSerializer.Deserialize<List<CaptureRecipe>>(File.ReadAllText(file), Json) ?? [] : [];
            }
            catch { return []; }
        }
    }

    private static int Observations(CaptureRecipe x) => x.TaskSuccesses + x.TaskFailures + x.TaskPartials;

    private static double Rank(CaptureRecipe x)
    {
        var n = Observations(x);
        // Verified end-to-end outcomes dominate raw capture counts. Smoothed rate avoids overrating 1/1.
        return n == 0 ? Math.Min(x.Successes, 1000) / 100000.0 :
            (x.TaskSuccesses + 1.0) / (n + 2.0) + Math.Min(n, 20) / 100.0;
    }

    public CaptureRecipe? BestScoped(CaptureRecipeScope request)
        => CaptureRecipeScopeGate.Select(Get(request.Host), request);

    // Legacy use_best has no trusted account or live view signature. On account-bearing
    // services it must not select a cross-account or stale route-only recipe.
    public CaptureRecipe? Best(string host, string path)
        => BestScoped(new CaptureRecipeScope(host, path));

    public void Record(string host, string path, string mode, string selector, string pattern, long elapsedMs)
    {
        if (string.IsNullOrWhiteSpace(host) || pattern.Length > 400 || selector.Length > 400) return;
        lock (Gate)
        {
            Directory.CreateDirectory(directory);
            var file = FileFor(host);
            List<CaptureRecipe> rows;
            try { rows = File.Exists(file) ? JsonSerializer.Deserialize<List<CaptureRecipe>>(File.ReadAllText(file), Json) ?? [] : []; }
            catch { rows = []; }
            var rec = rows.FirstOrDefault(x => x.Path == path && x.Mode == mode &&
                x.Selector == selector && x.Pattern == pattern &&
                string.IsNullOrWhiteSpace(x.ViewSignature) &&
                string.IsNullOrWhiteSpace(x.AccountScope));
            if (rec is null) { rec = new CaptureRecipe { Host = host, Path = path, Mode = mode, Selector = selector, Pattern = pattern }; rows.Add(rec); }
            rec.Successes++;
            rec.LastElapsedMs = elapsedMs;
            rec.LastSuccessfulUtc = DateTime.UtcNow.ToString("O");
            var temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(rows, Json));
            File.Move(temp, file, true);
        }
    }

    // A05: a capture is a candidate until its whole task succeeds. Persist the
    // exact observed view signature, never silently promote an account-less recipe.
    public void RecordScoped(string host, string path, string mode, string selector,
        string pattern, long elapsedMs, string? viewSignature, string? accountScope)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(path) ||
            pattern.Length > 400 || selector.Length > 400 ||
            string.IsNullOrWhiteSpace(viewSignature) || viewSignature.Length != 64)
            return;
        lock (Gate)
        {
            Directory.CreateDirectory(directory);
            var file = FileFor(host);
            List<CaptureRecipe> rows;
            try { rows = File.Exists(file) ? JsonSerializer.Deserialize<List<CaptureRecipe>>(File.ReadAllText(file), Json) ?? [] : []; }
            catch { rows = []; }
            var account = accountScope ?? "";
            var rec = rows.FirstOrDefault(x => x.Path == path && x.Mode == mode &&
                x.Selector == selector && x.Pattern == pattern &&
                x.ViewSignature.Equals(viewSignature, StringComparison.OrdinalIgnoreCase) &&
                x.AccountScope == account);
            if (rec is null)
            {
                rec = new CaptureRecipe { Host = host, Path = path, Mode = mode,
                    Selector = selector, Pattern = pattern, ViewSignature = viewSignature,
                    AccountScope = account };
                rows.Add(rec);
            }
            rec.Successes++;
            rec.LastElapsedMs = elapsedMs;
            rec.LastSuccessfulUtc = DateTime.UtcNow.ToString("O");
            var temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(rows, Json));
            File.Move(temp, file, true);
        }
    }

    public void RecordTaskOutcomeScoped(string host, string path, string mode, string selector,
        string pattern, string viewSignature, string? accountScope, string outcome)
    {
        if (outcome is not ("success" or "partial" or "failed") ||
            string.IsNullOrWhiteSpace(viewSignature)) return;
        lock (Gate)
        {
            var file = FileFor(host);
            if (!File.Exists(file)) return;
            List<CaptureRecipe> rows;
            try { rows = JsonSerializer.Deserialize<List<CaptureRecipe>>(File.ReadAllText(file), Json) ?? []; }
            catch { return; }
            var rec = rows.FirstOrDefault(x => x.Path == path && x.Mode == mode &&
                x.Selector == selector && x.Pattern == pattern &&
                x.ViewSignature.Equals(viewSignature, StringComparison.OrdinalIgnoreCase) &&
                x.AccountScope == (accountScope ?? ""));
            if (rec is null) return;
            if (outcome == "success")
            {
                rec.TaskSuccesses++;
                // Success on a login page or unscoped account-bearing service does not
                // establish a reusable authenticated recipe.
                if (!CaptureRecipeScopeGate.IsSensitiveHost(host) ||
                    !string.IsNullOrWhiteSpace(accountScope))
                {
                    rec.Confidence = "verified";
                    rec.SuspendReason = "";
                }
            }
            else if (outcome == "partial") rec.TaskPartials++;
            else
            {
                rec.TaskFailures++;
                if (rec.TaskFailures >= 3 && rec.TaskSuccesses == 0)
                {
                    rec.Confidence = "suspended";
                    rec.SuspendReason = "three_failed_tasks";
                }
            }
            rec.LastTaskOutcomeUtc = DateTime.UtcNow.ToString("O");
            var temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(rows, Json));
            File.Move(temp, file, true);
        }
    }

    public void RecordTaskOutcome(string host, string path, string mode, string selector, string pattern, string outcome)
    {
        if (outcome is not ("success" or "partial" or "failed")) return;
        lock (Gate)
        {
            var file = FileFor(host);
            if (!File.Exists(file)) return;
            List<CaptureRecipe> rows;
            try { rows = JsonSerializer.Deserialize<List<CaptureRecipe>>(File.ReadAllText(file), Json) ?? []; }
            catch { return; }
            var rec = rows.FirstOrDefault(x => x.Path == path && x.Mode == mode &&
                x.Selector == selector && x.Pattern == pattern &&
                string.IsNullOrWhiteSpace(x.ViewSignature));
            if (rec is null) return;
            if (outcome == "success") { rec.TaskSuccesses++; rec.Confidence = "verified"; rec.SuspendReason = ""; }
            else if (outcome == "partial") rec.TaskPartials++;
            else { rec.TaskFailures++; if (rec.TaskFailures >= 3 && rec.TaskSuccesses == 0) { rec.Confidence = "suspended"; rec.SuspendReason = "three_failed_tasks"; } }
            rec.LastTaskOutcomeUtc = DateTime.UtcNow.ToString("O");
            var temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(rows, Json));
            File.Move(temp, file, true);
        }
    }

    public string Context(string host, string path, int max = 3)
    {
        var found = Get(host).Where(x => x.Path == path).OrderByDescending(Rank).Take(Math.Clamp(max, 0, 10)).ToArray();
        if (found.Length == 0) return "";
        return "KNOWN CAPTURE PROFILES (technical data, verify live):\n" +
            string.Join("\n", found.Select(x => $"mode={x.Mode}; selector={x.Selector}; regex={x.Pattern}; technical_ok={x.Successes}; task_ok={x.TaskSuccesses}; task_partial={x.TaskPartials}; task_failed={x.TaskFailures}; last_ms={x.LastElapsedMs}"));
    }
}
