<!-- OpenFill - how to make a tester package. Metadata: wersja 0.1, data 2026-10-16 12:00 -->
# Tester package: how to make one

A tester package is a zip someone can unpack and run with nothing to set up: an OpenAI key is built in,
MCP is on, and Polish instructions explain how to add OpenFill to Claude or ChatGPT.
First made on 2026-10-16 (OpenFill 1.54-dev) for a friend; this note is for the next chat that does it.

## Files

| File | Role |
|---|---|
| `tools/pack-tester__v*.ps1` | builds the zip (run on Windows) |
| `tools/tester/URUCHOM__v*.cmd` | in the package as `URUCHOM OpenFill.cmd` |
| `tools/tester/start-tester__v*.ps1` | in the package as `app\start-tester.ps1`: starts OpenFill, waits for `runtime\mcp-url.txt`, shows and copies the MCP address |
| `tools/tester/INSTRUKCJA__v*.md` | in the package as `INSTRUKCJA.txt`; `{{VALID_UNTIL}}` and `{{VERSION}}` are filled in |

The script needs `src/OpenFill.Core/Config/EmbeddedDefaults__v*.cs` (`public const string ApiKey = "";`, read by `SecretStore` as the last fallback)
and `public bool McpEnabled { get; set; } = false;` in `AppConfig__v*.cs`. If either line changes, the script stops with a clear message: adjust the two `Patch` calls.

## Steps

1. **Key from the owner.** A key for a separate OpenAI project with a low spending limit, a short lifetime and only the model OpenFill uses.
   The key is never in the repository (a test enforces an empty `EmbeddedDefaults.ApiKey`); do not look for it on the PC, ask the owner.
2. **Check the key** (free, no tokens): `GET https://api.openai.com/v1/models` with `Authorization: Bearer <key>`.
   The list should contain the model set in `AppConfig.Model` (default `gpt-6-luna`).
3. **Build on a Windows PC with .NET SDK 10** (the owner's PC `PBOX1` has it; the build takes about 1-2 minutes):
   ```
   git clone https://github.com/Pawel-BoOssko/OpenFill.git C:\Users\<user>\OpenFill-tester-build
   powershell -ExecutionPolicy Bypass -File C:\Users\<user>\OpenFill-tester-build\tools\pack-tester__v0.1__2026-10-16_0925.ps1 -ApiKey <key> -ValidUntil "17 października 2026" -Name <tester>
   ```
   Build from the branch with the current code (at the time: `sync/...`, not `main`), so the tester gets the newest version.
   Result: `dist\OpenFill-tester__v<ver>__<stamp>__<tester>.zip` (about 46 MB). The throwaway copy with the key is deleted; the script checks that the repository is unchanged.
4. **Do a dry run first** with a fake key (`sk-` + 20 or more letters) when the script or the code changed, then delete that zip.
5. **Hand it over:** copy the zip to the owner's desktop (`[Environment]::GetFolderPath('Desktop')`, on PBOX1 that is OneDrive),
   so the owner can share a OneDrive link. It is too big for a Gmail attachment.
6. **Do not start the packaged app on the owner's PC:** it uses the same data folder (`%USERPROFILE%\OpenFill\data\stable`) as the owner's own OpenFill.

## Known limits

- The Cloudflare quick tunnel gives a new MCP address at every start; the tester updates the connector (the instructions say how).
- The key can be extracted from the exe: that is why the key must be cheap and short-lived.
- The exe is unsigned: SmartScreen shows a warning once ("More info" -> "Run anyway").
