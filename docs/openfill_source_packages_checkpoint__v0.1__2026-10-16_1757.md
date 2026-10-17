# OpenFill: source packages checkpoint v0.1 (2026-10-16 17:57 CEST)

## Context
Newest confirmed master plan on private Drive: v1.4, 224 points. Live installed OpenFill executable remains 1.52.0.0. NO deployment, build, CI or .NET test executed in this work. The historical safety denial of Core/dependent builds, staging and deployment wrapper has NOT been retried.

## Package A: previously applied diskMatch source fix, reconfirmed
- `src/OpenFill.Core/Mcp/McpTaskManager__v0.17__2026-10-14_2235.cs`: undeclared `diskMatch` now has `McpTaskRecord? diskMatch = null;` before the disk scan.
- Final SHA256 BA675F4EB25C5BB4CA1F71DBC507711143AA02D4E30E8AFD5532881777DFF027.
- Original backed up separately in Documents/OpenFill-Backups/Source-Patches.

## Package B: complete PSL metadata site family, SOURCE ONLY
- Linked existing Mozilla PSL snapshot as `OpenFill.Assets.public_suffix_list.dat` in `src/OpenFill.Core/OpenFill.Core__v0.2__2026-10-05_1515.csproj` (SHA256 8344A355B5183A545EAA3E3545DDC65F1BE8594870CD51B507D92148CA7241E7).
- Switched `ImportedCredentialVault.SiteFamily` from incomplete hard-coded hosting exceptions to `PublicSuffixResolver.RegistrableDomain` (SHA256 B39296AADD826818B1541C23C318F19929795A981DB6A8709D613F46BA355F25). Exact-origin secret reads remain unchanged.
- Extended `tests/OpenFill.Tests/Program__v0.21__2026-10-14_2259.cs` with six synthetic PSL cases (ReadTheDocs/Notion tenants; wildcard/exception; ordinary domain). SHA256 31A81187CA40A3FC108D3B5A56856F7758FA76657B5DC7202F988C89F5354C95.
- Static check PASS: CSProj parses as XML, exactly one resource declaration and exactly one matching file. PSL inventory: 10,018 exact, 310 wildcard, 8 exception rules. Actual C# execution not tested.

## Package C: reject malformed HTTP Basic challenge, SOURCE ONLY
- `BasicAuthRealmMatcher__v0.1__2026-10-16_0237.cs`: rejects a trailing comma not followed by any parameter, SHA256 FCF13137299CD2BC0EFD4E47E74A507D8DCBA9AEC5B690028F9A20440160F5BB.
- `BasicAuthRealmTests__v0.1__2026-10-16_0238.cs`: three synthetic cases (malformed comma, whitespace, valid additional field), SHA256 45D8674C98B754AF7F957D566708EF754F807D55B311002785286E774DFD618A.

## Package D: privacy in domain lock refusals, SOURCE ONLY
- `DomainLocks__v0.1__2026-10-07_1732.cs`: refusal to another task no longer includes the prompt of the task holding the domain, SHA256 B8787E471A02CBB681EB276EC705583B0F1F58D27B4AB4948DCB39BE2DFDC074.
- `ParallelTests__v0.4__2026-10-12_1924.cs`: updated synthetic assertions for prompt nondisclosure, SHA256 9DD64012F37DF46A5B4B9AAD766848B906B7E94512D84740930278584C7DA217.

## Package E: audit and release gates
- `CostGuard.DomainOf` continues to use an incomplete heuristic: unrelated `alice.github.io`/`bob.github.io` map to `github.io`. This can overly serialize domain tasks and combine budgets. Changing its grouping needs a separate cost-state migration/compatibility design and automated tests; not silently switched.
- `PrepareShutdown` remains NOT FIXED: prior safety refusal on maintenance rollback write must not be rerouted. It can leave persisted `interrupted` records inconsistent and gate active on failure.
- Other existing release blockers: no authorized Core build/regression, no validated full real encrypted state restore on SATANAMD, no proven release staging, real GUI/MCP SessionId1 smoke or rollback.
- Production 1.52 untouched. Previous safe preflight NO-GO is a release approval statement, not proof that source editing is impossible.

## Change handling
All 8 changed source/test/project files have SHA256-verified previous versions in `C:\Users\pboru\Documents\OpenFill-Backups\Source-Patches`. No key, credential, token or production browser profile copied. Do not confuse test source with executed regression. Keep GitHub Draft PR separate; no publish/merge.