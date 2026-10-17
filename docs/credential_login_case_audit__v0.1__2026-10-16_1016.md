# OpenFill credential login case audit v0.1
Date: 2026-10-16 10:16 CEST. SOURCE READ ONLY, no source edits, no production changes.
ImportedCredentialVault.Key uses SHA-256 of origin plus exact login string, so identifiers with different letter case can be distinct stored credentials.
ImportedPasswordFiller.FillAsync filters requested login with a.Login.Equals(login, StringComparison.OrdinalIgnoreCase). A caller asking for a differently cased username can therefore select the only available account, even when the account identifier is case-sensitive on the target service. With multiple candidates the CHOOSE_CREDENTIAL guard may prevent automatic selection, but that does not cover the single-candidate mismatch.
StaticSite test server _loginAccounts also uses StringComparer.OrdinalIgnoreCase, so current synthetic login tests cannot establish behavior on a case-sensitive account service.
Recommended independent synthetic test: two usernames differing only by case, plus a one-account wrong-case query, with no real credentials. Do not conflate with the separately blocked change to HTTP Basic realm matching.
No build, tests or production deployment performed for this finding.