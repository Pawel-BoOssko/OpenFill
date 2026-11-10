// OF-IMP-20261031 C01: versioned data-only procedure registry.
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

public sealed class ProcedureDefinition
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Intent { get; set; } = "";
    public string Status { get; set; } = "candidate";
    public string Adapter { get; set; } = "";
    public string? Evidence { get; set; }
    public string? Resume { get; set; }
    public string[] Stages { get; set; } = [];
}

public sealed class ProcedureCatalog
{
    private static readonly HashSet<string> Stages = new(StringComparer.Ordinal)
    {
        "ensure_auth", "resolve_target", "navigate_known", "observe",
        "query_stored", "select_batch", "execute_existing_action",
        "verify", "persist", "report", "escalate_to_luna"
    };
    private readonly Dictionary<string, ProcedureDefinition> _definitions;

    public ProcedureCatalog(string registryFile) : this(JsonNode.Parse(File.ReadAllText(registryFile))
        ?? throw new InvalidDataException("Procedure registry empty.")) { }

    public static ProcedureCatalog FromEmbedded(string? learningDirectory = null)
    {
        using var stream = typeof(ProcedureCatalog).Assembly.GetManifestResourceStream("OpenFill.Assets.procedures.registry.json")
            ?? throw new FileNotFoundException("Embedded procedures registry missing.");
        var catalog = new ProcedureCatalog(JsonNode.Parse(stream)
            ?? throw new InvalidDataException("Embedded procedure registry is empty."));
        if (!string.IsNullOrWhiteSpace(learningDirectory))
        {
            var learning = new ProcedureLearningStore(learningDirectory);
            foreach (var definition in catalog._definitions.Values)
            {
                var stored = learning.Read(definition.Id, definition.Version);
                if (stored is not null && stored.Status is "verified" or "suspended")
                    definition.Status = stored.Status;
            }
        }
        return catalog;
    }

    private ProcedureCatalog(JsonNode json)
    {
        if (json["schema_version"]?.GetValue<int>() != 1)
            throw new InvalidDataException("Unsupported procedure registry schema.");
        var entries = json["procedures"] as JsonArray
            ?? throw new InvalidDataException("Missing procedure array.");
        _definitions = new(StringComparer.Ordinal);
        foreach (var item in entries)
        {
            var p = item?.Deserialize<ProcedureDefinition>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidDataException("Empty procedure definition.");
            if (string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Provider) ||
                string.IsNullOrWhiteSpace(p.Version) || string.IsNullOrWhiteSpace(p.Intent) ||
                p.Stages is null || p.Stages.Length == 0 || p.Stages.Any(s => !Stages.Contains(s)) ||
                p.Status is not ("candidate" or "verified" or "suspended"))
                throw new InvalidDataException("Invalid procedure definition: " + p.Id);
            if (!_definitions.TryAdd(p.Id, p))
                throw new InvalidDataException("Duplicate procedure_id: " + p.Id);
        }
    }

    public ProcedureDefinition? Get(string id) =>
        _definitions.TryGetValue(id, out var p) ? p : null;

    // Only verified definitions can be automatically selected. A candidate is never silently dispatched.
    public ProcedureDefinition? FindVerified(string provider, string intent) =>
        _definitions.Values.Where(p => p.Status == "verified" &&
            p.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase) &&
            p.Intent.Equals(intent, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Version, StringComparer.Ordinal)
            .FirstOrDefault();

    public JsonArray List() => new(_definitions.Values.OrderBy(x => x.Id).Select(p =>
        (JsonNode?)new JsonObject
        {
            ["id"] = p.Id, ["version"] = p.Version, ["provider"] = p.Provider,
            ["intent"] = p.Intent, ["status"] = p.Status,
            ["stages"] = new JsonArray(p.Stages.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray())
        }).ToArray());
}
