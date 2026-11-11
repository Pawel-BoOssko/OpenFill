// OF-IMP-20261031 B01/B02: durable operational state, distinct from task HistoryStore.
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace OpenFill.Core.Agent;

public sealed class OperationalMemoryStore
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly object _gate = new();
    public string DatabasePath => _databasePath;

    public OperationalMemoryStore(string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true
        }.ToString();
        EnsureCreated();
    }

    private SqliteConnection Connect()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var config = conn.CreateCommand();
        config.CommandText = "PRAGMA foreign_keys=ON;PRAGMA busy_timeout=5000;";
        config.ExecuteNonQuery();
        return conn;
    }

    private void EnsureCreated()
    {
        lock (_gate)
        {
            using var db = Connect();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS accounts (
              account_id TEXT PRIMARY KEY, provider TEXT NOT NULL, external_id TEXT NOT NULL,
              display_name TEXT, observed_utc TEXT NOT NULL,
              UNIQUE(provider, external_id)
            );
            CREATE TABLE IF NOT EXISTS identities (
              identity_id TEXT PRIMARY KEY, provider TEXT NOT NULL, account_id TEXT NOT NULL,
              external_id TEXT NOT NULL, canonical_url TEXT, display_name TEXT,
              source_ref TEXT, observed_utc TEXT NOT NULL,
              UNIQUE(provider, account_id, external_id)
            );
            CREATE INDEX IF NOT EXISTS identities_url ON identities(provider,account_id,canonical_url);
            CREATE TABLE IF NOT EXISTS conversations (
              conversation_id TEXT PRIMARY KEY, provider TEXT NOT NULL, account_id TEXT NOT NULL,
              external_id TEXT NOT NULL, canonical_url TEXT, participant_identity_id TEXT,
              source_ref TEXT, observed_utc TEXT NOT NULL,
              UNIQUE(provider, account_id, external_id)
            );
            CREATE TABLE IF NOT EXISTS messages (
              message_id TEXT PRIMARY KEY, conversation_id TEXT NOT NULL,
              provider_message_id TEXT, direction TEXT NOT NULL, body TEXT,
              body_hash TEXT, observed_utc TEXT NOT NULL, source_ref TEXT, identity_confidence TEXT,
              FOREIGN KEY(conversation_id) REFERENCES conversations(conversation_id)
            );
            CREATE VIRTUAL TABLE IF NOT EXISTS messages_fts USING fts5(message_id UNINDEXED, body);
            CREATE TRIGGER IF NOT EXISTS messages_ai AFTER INSERT ON messages BEGIN
              INSERT INTO messages_fts(message_id,body) VALUES (new.message_id,COALESCE(new.body,''));
            END;
            CREATE TABLE IF NOT EXISTS batches (
              batch_id TEXT PRIMARY KEY, provider TEXT NOT NULL, account_id TEXT NOT NULL,
              request_id TEXT NOT NULL, task_id TEXT, requester TEXT, chat TEXT,
              procedure_id TEXT, procedure_version TEXT, created_utc TEXT NOT NULL,
              UNIQUE(provider,account_id,request_id)
            );
            CREATE TABLE IF NOT EXISTS batch_request_fingerprints (
              batch_id TEXT PRIMARY KEY, fingerprint TEXT NOT NULL,
              FOREIGN KEY(batch_id) REFERENCES batches(batch_id)
            );
            CREATE TABLE IF NOT EXISTS operations (
              operation_id TEXT PRIMARY KEY, batch_id TEXT NOT NULL, target_id TEXT NOT NULL,
              action TEXT NOT NULL, payload_hash TEXT NOT NULL,
              state TEXT NOT NULL DEFAULT 'planned', attempt_count INTEGER NOT NULL DEFAULT 0,
              evidence_ref TEXT, external_ref TEXT, last_reason TEXT, updated_utc TEXT NOT NULL,
              UNIQUE(batch_id,target_id,action,payload_hash),
              FOREIGN KEY(batch_id) REFERENCES batches(batch_id)
            );
            CREATE TABLE IF NOT EXISTS operation_payloads (
              operation_id TEXT PRIMARY KEY, payload TEXT NOT NULL,
              FOREIGN KEY(operation_id) REFERENCES operations(operation_id)
            );
            CREATE TABLE IF NOT EXISTS operation_events (
              event_id INTEGER PRIMARY KEY AUTOINCREMENT,
              operation_id TEXT NOT NULL, state TEXT NOT NULL,
              event_utc TEXT NOT NULL, evidence_ref TEXT, reason TEXT,
              FOREIGN KEY(operation_id) REFERENCES operations(operation_id)
            );
            CREATE INDEX IF NOT EXISTS operation_events_by_operation ON operation_events(operation_id,event_id);
            CREATE INDEX IF NOT EXISTS operations_batch_state ON operations(batch_id,state);
            CREATE TABLE IF NOT EXISTS observations (
              capture_id TEXT PRIMARY KEY, account_id TEXT, provider TEXT,
              url TEXT, scope TEXT NOT NULL, complete INTEGER NOT NULL,
              source_path TEXT, source_sha256 TEXT, observed_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS procedure_runs (
              batch_id TEXT PRIMARY KEY, procedure_id TEXT NOT NULL,
              procedure_version TEXT NOT NULL, current_stage TEXT NOT NULL,
              cursor TEXT, updated_utc TEXT NOT NULL,
              FOREIGN KEY(batch_id) REFERENCES batches(batch_id)
            );
            """;
            cmd.ExecuteNonQuery();
        }
    }

    private static string Now() => DateTime.UtcNow.ToString("O");
    private static void Parameters(SqliteCommand cmd, params (string Name, object? Value)[] values)
    {
        foreach (var (name, value) in values)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    public JsonArray RecentObservations(string provider, string? accountId, string search, int limit = 20)
    {
        using var db = Connect();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT capture_id,url,scope,complete,source_path,source_sha256,observed_utc
            FROM observations
            WHERE provider=$provider AND (capture_id=$search OR url LIKE $pattern)
              AND ((account_id IS NULL AND $account='') OR account_id=$account)
            ORDER BY observed_utc DESC LIMIT $limit;
            """;
        Parameters(cmd, ("$provider", provider), ("$account", accountId ?? ""),
            ("$search", search), ("$pattern", "%" + search + "%"),
            ("$limit", Math.Clamp(limit, 1, 100)));
        using var reader = cmd.ExecuteReader();
        var rows = new JsonArray();
        while (reader.Read()) rows.Add(new JsonObject {
            ["capture_id"] = reader.GetString(0),
            ["url"] = reader.IsDBNull(1) ? null : reader.GetString(1),
            ["scope"] = reader.GetString(2), ["complete"] = reader.GetInt32(3) == 1,
            ["source_path"] = reader.IsDBNull(4) ? null : reader.GetString(4),
            ["source_sha256"] = reader.IsDBNull(5) ? null : reader.GetString(5),
            ["observed_utc"] = reader.GetString(6)
        });
        return rows;
    }

    public void RecordObservation(string captureId, string? accountId, string? provider,
        string? url, string scope, bool complete, string? sourcePath, string? sha256)
    {
        if (string.IsNullOrWhiteSpace(captureId)) throw new ArgumentException("capture_id is required");
        lock (_gate)
        {
            using var db = Connect();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO observations(capture_id,account_id,provider,url,scope,complete,source_path,source_sha256,observed_utc)
                VALUES($capture,$account,$provider,$url,$scope,$complete,$path,$hash,$now)
                ON CONFLICT(capture_id) DO NOTHING;
                """;
            Parameters(cmd, ("$capture", captureId), ("$account", accountId), ("$provider", provider),
                ("$url", url), ("$scope", scope), ("$complete", complete ? 1 : 0),
                ("$path", sourcePath), ("$hash", sha256), ("$now", Now()));
            cmd.ExecuteNonQuery();
        }
    }

    public void UpsertAccount(string accountId, string provider, string externalId, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("Account identity requires account_id, provider and external_id.");
        lock (_gate)
        {
            using var db = Connect();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO accounts(account_id,provider,external_id,display_name,observed_utc)
                VALUES($id,$provider,$external,$name,$now)
                ON CONFLICT(account_id) DO UPDATE SET
                display_name=COALESCE(excluded.display_name,accounts.display_name),observed_utc=excluded.observed_utc;
                """;
            Parameters(cmd, ("$id", accountId), ("$provider", provider), ("$external", externalId),
                ("$name", displayName), ("$now", Now()));
            cmd.ExecuteNonQuery();
        }
    }

    public void UpsertIdentity(string provider, string accountId, string externalId,
        string? profileUrl, string? displayName, string? sourceRef)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("Identity requires provider, account and exact provider ID.");
        lock (_gate)
        {
            using var db = Connect();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO identities(identity_id,provider,account_id,external_id,canonical_url,display_name,source_ref,observed_utc)
                VALUES($id,$provider,$account,$external,$url,$name,$source,$now)
                ON CONFLICT(provider,account_id,external_id) DO UPDATE SET
                canonical_url=COALESCE(excluded.canonical_url,identities.canonical_url),
                display_name=COALESCE(excluded.display_name,identities.display_name),
                source_ref=COALESCE(excluded.source_ref,identities.source_ref),
                observed_utc=excluded.observed_utc;
                """;
            Parameters(cmd, ("$id", Guid.NewGuid().ToString("N")), ("$provider", provider), ("$account", accountId),
                ("$external", externalId), ("$url", profileUrl), ("$name", displayName),
                ("$source", sourceRef), ("$now", Now()));
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Resolve a complete LinkedIn target batch with ONE SQLite query. Never infer identities
    /// from display names and never visit the website for identifiers already stored here.
    /// Known thread links take precedence over profile URLs.
    /// </summary>
    public JsonArray ResolveLinkedInBatch(string accountId, IReadOnlyList<string> targets)
    {
        if (string.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("Sender account scope required.");
        if (targets.Count is < 1 or > 200 || targets.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Provide 1 to 200 explicit LinkedIn IDs or URLs.");
        using var db = Connect();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT r.value AS requested,
                   i.external_id, i.canonical_url, i.source_ref, i.observed_utc,
                   (SELECT c.canonical_url
                      FROM conversations c
                     WHERE c.provider='linkedin' AND c.account_id=$account
                       AND (c.participant_identity_id=i.identity_id
                         OR c.external_id=r.value OR c.external_id=i.external_id
                         OR c.canonical_url=r.value)
                       AND c.canonical_url LIKE 'https://www.linkedin.com/messaging/%'
                     ORDER BY c.observed_utc DESC LIMIT 1) AS thread_url
              FROM json_each($targets) AS r
              LEFT JOIN identities i ON i.provider='linkedin' AND i.account_id=$account
                AND (i.external_id=r.value OR i.canonical_url=r.value)
             ORDER BY CAST(r.key AS INTEGER);
            """;
        var ids = new JsonArray(targets.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
        Parameters(cmd, ("$targets", ids.ToJsonString()), ("$account", accountId));
        var result = new JsonArray();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.IsDBNull(1) ? null : reader.GetString(1);
            var profile = reader.IsDBNull(2) ? null : reader.GetString(2);
            var thread = reader.IsDBNull(5) ? null : reader.GetString(5);
            result.Add(new JsonObject
            {
                ["requested"] = reader.GetString(0),
                ["external_id"] = id, ["profile_url"] = profile, ["thread_url"] = thread,
                ["source_ref"] = reader.IsDBNull(3) ? null : reader.GetString(3),
                ["observed_utc"] = reader.IsDBNull(4) ? null : reader.GetString(4),
                ["status"] = thread is not null ? "resolved_thread" :
                             id is not null ? "resolved_profile" : "unresolved"
            });
        }
        return result;
    }

    public JsonArray FindIdentities(string provider, string accountId, string search, int limit = 20)
    {
        using var db = Connect();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT external_id,canonical_url,display_name,source_ref,observed_utc
            FROM identities WHERE provider=$provider AND account_id=$account
            AND (external_id=$search OR canonical_url=$search OR display_name LIKE $pattern)
            ORDER BY CASE WHEN external_id=$search THEN 0 WHEN canonical_url=$search THEN 1 ELSE 2 END
            LIMIT $limit;
            """;
        Parameters(cmd, ("$provider", provider), ("$account", accountId), ("$search", search),
            ("$pattern", "%" + search.Replace("%", "[%]").Replace("_", "[_]") + "%"), ("$limit", Math.Clamp(limit, 1, 100)));
        using var reader = cmd.ExecuteReader();
        var array = new JsonArray();
        while (reader.Read())
        {
            array.Add(new JsonObject {
                ["external_id"] = reader.GetString(0), ["canonical_url"] = reader.IsDBNull(1) ? null : reader.GetString(1),
                ["display_name"] = reader.IsDBNull(2) ? null : reader.GetString(2),
                ["source_ref"] = reader.IsDBNull(3) ? null : reader.GetString(3), ["observed_utc"] = reader.GetString(4)
            });
        }
        return array;
    }

    public string UpsertConversation(string provider, string accountId, string externalId,
        string? canonicalUrl, string? participantIdentityId, string? sourceRef)
    {
        lock (_gate)
        {
            using var db = Connect();
            using var tx = db.BeginTransaction();
            using (var cmd = db.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO conversations(conversation_id,provider,account_id,external_id,canonical_url,participant_identity_id,source_ref,observed_utc)
                    VALUES($id,$provider,$account,$external,$url,$participant,$source,$now)
                    ON CONFLICT(provider,account_id,external_id) DO UPDATE SET
                    canonical_url=COALESCE(excluded.canonical_url,conversations.canonical_url),
                    participant_identity_id=COALESCE(excluded.participant_identity_id,conversations.participant_identity_id),
                    source_ref=COALESCE(excluded.source_ref,conversations.source_ref),observed_utc=excluded.observed_utc;
                    """;
                Parameters(cmd,("$id",Guid.NewGuid().ToString("N")),("$provider",provider),("$account",accountId),
                    ("$external",externalId),("$url",canonicalUrl),("$participant",participantIdentityId),("$source",sourceRef),("$now",Now()));
                cmd.ExecuteNonQuery();
            }
            using var find = db.CreateCommand();
            find.Transaction = tx;
            find.CommandText = "SELECT conversation_id FROM conversations WHERE provider=$provider AND account_id=$account AND external_id=$external;";
            Parameters(find,("$provider",provider),("$account",accountId),("$external",externalId));
            var id = find.ExecuteScalar() as string ?? throw new InvalidOperationException("Cannot retrieve conversation");
            tx.Commit();
            return id;
        }
    }

    public void SaveMessage(string messageId, string conversationId, string direction,
        string? body, string? bodyHash, string? sourceRef, string identityConfidence = "observed")
    {
        lock (_gate)
        {
            using var db = Connect();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT OR IGNORE INTO messages(message_id,conversation_id,direction,body,body_hash,observed_utc,source_ref,identity_confidence)
                VALUES($id,$conversation,$direction,$body,$hash,$now,$source,$confidence);
                """;
            Parameters(cmd,("$id",messageId),("$conversation",conversationId),("$direction",direction),
                ("$body",body),("$hash",bodyHash),("$now",Now()),("$source",sourceRef),("$confidence",identityConfidence));
            cmd.ExecuteNonQuery();
        }
    }

    public JsonArray SearchMessages(string provider, string accountId, string ftsQuery, int limit = 20)
    {
        using var db = Connect();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT m.message_id,m.body,m.direction,c.external_id,m.observed_utc
            FROM messages_fts JOIN messages m ON m.message_id=messages_fts.message_id
            JOIN conversations c ON c.conversation_id=m.conversation_id
            WHERE c.provider=$provider AND c.account_id=$account AND messages_fts MATCH $query
            ORDER BY m.observed_utc DESC LIMIT $limit;
            """;
        Parameters(cmd,("$provider",provider),("$account",accountId),("$query",ftsQuery),("$limit",Math.Clamp(limit,1,100)));
        using var reader = cmd.ExecuteReader();
        var results = new JsonArray();
        while (reader.Read()) results.Add(new JsonObject {
            ["message_id"] = reader.GetString(0), ["body"] = reader.IsDBNull(1) ? null : reader.GetString(1),
            ["direction"] = reader.GetString(2), ["thread"] = reader.GetString(3), ["observed_utc"] = reader.GetString(4)
        });
        return results;
    }

    public string BeginBatch(string provider, string accountId, string requestId,
        string procedureId, string procedureVersion, string? taskId, string? requester, string? chat)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException("Batch requires provider, account and stable request_id.");
        lock (_gate)
        {
            using var db = Connect();
            using var tx = db.BeginTransaction();
            var proposed = Guid.NewGuid().ToString("N");
            using (var insert = db.CreateCommand())
            {
                insert.Transaction = tx;
                insert.CommandText = """
                    INSERT INTO batches(batch_id,provider,account_id,request_id,task_id,requester,chat,procedure_id,procedure_version,created_utc)
                    VALUES($id,$provider,$account,$request,$task,$requester,$chat,$procedure,$version,$now)
                    ON CONFLICT(provider,account_id,request_id) DO NOTHING;
                    """;
                Parameters(insert, ("$id", proposed), ("$provider", provider), ("$account", accountId),
                    ("$request", requestId), ("$task", taskId), ("$requester", requester),
                    ("$chat", chat), ("$procedure", procedureId), ("$version", procedureVersion), ("$now", Now()));
                insert.ExecuteNonQuery();
            }
            using var find = db.CreateCommand();
            find.Transaction = tx;
            find.CommandText = "SELECT batch_id FROM batches WHERE provider=$provider AND account_id=$account AND request_id=$request;";
            Parameters(find, ("$provider", provider), ("$account", accountId), ("$request", requestId));
            var id = (string?)find.ExecuteScalar() ?? throw new InvalidOperationException("Unable to retrieve batch.");
            tx.Commit();
            return id;
        }
    }

    public bool BindRequestFingerprint(string batchId, string fingerprint)
    {
        lock (_gate)
        {
            using var db = Connect();
            using var tx = db.BeginTransaction();
            using (var insert = db.CreateCommand())
            {
                insert.Transaction = tx;
                insert.CommandText = "INSERT OR IGNORE INTO batch_request_fingerprints(batch_id,fingerprint) VALUES($id,$fingerprint);";
                Parameters(insert, ("$id", batchId), ("$fingerprint", fingerprint));
                insert.ExecuteNonQuery();
            }
            using var find = db.CreateCommand();
            find.Transaction = tx;
            find.CommandText = "SELECT fingerprint FROM batch_request_fingerprints WHERE batch_id=$id;";
            Parameters(find, ("$id", batchId));
            var actual = find.ExecuteScalar() as string;
            tx.Commit();
            return string.Equals(actual, fingerprint, StringComparison.Ordinal);
        }
    }

    public string PlanOperation(string batchId, string targetId, string action, string payloadHash, string? payload = null)
    {
        lock (_gate)
        {
            using var db = Connect();
            using var tx = db.BeginTransaction();
            var proposed = Guid.NewGuid().ToString("N");
            bool created = false;
            using (var insert = db.CreateCommand())
            {
                insert.Transaction = tx;
                insert.CommandText = """
                    INSERT INTO operations(operation_id,batch_id,target_id,action,payload_hash,updated_utc)
                    VALUES($id,$batch,$target,$action,$hash,$now)
                    ON CONFLICT(batch_id,target_id,action,payload_hash) DO NOTHING;
                    """;
                Parameters(insert, ("$id", proposed), ("$batch", batchId), ("$target", targetId),
                    ("$action", action), ("$hash", payloadHash), ("$now", Now()));
                created = insert.ExecuteNonQuery() == 1;
            }
            using var find = db.CreateCommand();
            find.Transaction = tx;
            find.CommandText = "SELECT operation_id FROM operations WHERE batch_id=$batch AND target_id=$target AND action=$action AND payload_hash=$hash;";
            Parameters(find, ("$batch", batchId), ("$target", targetId), ("$action", action), ("$hash", payloadHash));
            var id = (string?)find.ExecuteScalar() ?? throw new InvalidOperationException("Unable to retrieve operation.");
            if (payload is not null)
            {
                using var savePayload = db.CreateCommand();
                savePayload.Transaction = tx;
                savePayload.CommandText = "INSERT OR IGNORE INTO operation_payloads(operation_id,payload) VALUES($id,$payload);";
                Parameters(savePayload, ("$id", id), ("$payload", payload));
                savePayload.ExecuteNonQuery();
            }
            if (created) AppendOperationEvent(db, tx, id, "planned", null, null, Now());
            tx.Commit();
            return id;
        }
    }

    public string? OperationPayload(string operationId)
    {
        using var db = Connect();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT payload FROM operation_payloads WHERE operation_id=$id;";
        Parameters(cmd, ("$id", operationId));
        return cmd.ExecuteScalar() as string;
    }

    public string? OperationState(string operationId)
    {
        using var db = Connect();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT state FROM operations WHERE operation_id=$id;";
        Parameters(cmd, ("$id", operationId));
        return cmd.ExecuteScalar() as string;
    }

    private static void AppendOperationEvent(SqliteConnection db, SqliteTransaction tx,
        string operationId, string state, string? evidenceRef, string? reason, string when)
    {
        using var evt = db.CreateCommand();
        evt.Transaction = tx;
        evt.CommandText = """
            INSERT INTO operation_events(operation_id,state,event_utc,evidence_ref,reason)
            VALUES($id,$state,$when,$evidence,$reason);
            """;
        Parameters(evt, ("$id", operationId), ("$state", state), ("$when", when),
            ("$evidence", evidenceRef), ("$reason", reason));
        evt.ExecuteNonQuery();
    }

    public bool MarkAttempted(string operationId)
    {
        lock (_gate)
        {
            using var db = Connect();
            using var tx = db.BeginTransaction();
            using var cmd = db.CreateCommand();
            cmd.Transaction = tx;
            var now = Now();
            cmd.CommandText = """
                UPDATE operations SET state='attempted',attempt_count=attempt_count+1,updated_utc=$now
                WHERE operation_id=$id AND state IN ('planned','prepared','failed');
                """;
            Parameters(cmd, ("$id", operationId), ("$now", now));
            var changed = cmd.ExecuteNonQuery() == 1;
            if (changed) AppendOperationEvent(db, tx, operationId, "attempted", null, null, now);
            tx.Commit();
            return changed;
        }
    }

    public bool MarkOutcome(string operationId, string state, string? evidenceRef, string? externalRef, string? reason)
    {
        if (state is not ("verified" or "uncertain" or "failed" or "skipped" or "blocked"))
            throw new ArgumentException("Unsupported operation outcome.");
        lock (_gate)
        {
            using var db = Connect();
            using var tx = db.BeginTransaction();
            using var cmd = db.CreateCommand();
            cmd.Transaction = tx;
            var now = Now();
            cmd.CommandText = """
                UPDATE operations SET state=$state,evidence_ref=$proof,external_ref=$external,
                last_reason=$reason,updated_utc=$now
                WHERE operation_id=$id AND state NOT IN ('verified','skipped');
                """;
            Parameters(cmd, ("$state", state), ("$proof", evidenceRef), ("$external", externalRef),
                ("$reason", reason), ("$now", now), ("$id", operationId));
            var changed = cmd.ExecuteNonQuery() == 1;
            if (changed) AppendOperationEvent(db, tx, operationId, state, evidenceRef, reason, now);
            tx.Commit();
            return changed;
        }
    }

    public JsonArray OperationEvents(string operationId, int limit = 100)
    {
        using var db = Connect();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT state,event_utc,evidence_ref,reason FROM operation_events
            WHERE operation_id=$id ORDER BY event_id ASC LIMIT $limit;
            """;
        Parameters(cmd, ("$id", operationId), ("$limit", Math.Clamp(limit, 1, 1000)));
        using var reader = cmd.ExecuteReader();
        var results = new JsonArray();
        while (reader.Read()) results.Add(new JsonObject {
            ["state"] = reader.GetString(0), ["at"] = reader.GetString(1),
            ["evidence_ref"] = reader.IsDBNull(2) ? null : reader.GetString(2),
            ["reason"] = reader.IsDBNull(3) ? null : reader.GetString(3)
        });
        return results;
    }

    /// <summary>Compact operational metrics from the existing SQLite ledger; no new reporting subsystem.</summary>
    public JsonObject Metrics(string provider = "", string accountId = "")
    {
        using var db = Connect();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT b.provider,b.procedure_id,b.procedure_version,
                   COUNT(DISTINCT b.batch_id) AS batches,
                   COUNT(o.operation_id) AS items,
                   COALESCE(SUM(o.attempt_count),0) AS attempts,
                   COALESCE(SUM(CASE WHEN o.state='verified' THEN 1 ELSE 0 END),0) AS verified,
                   COALESCE(SUM(CASE WHEN o.state='uncertain' THEN 1 ELSE 0 END),0) AS uncertain,
                   COALESCE(SUM(CASE WHEN o.state='failed' THEN 1 ELSE 0 END),0) AS failed,
                   COALESCE(SUM(CASE WHEN o.state='skipped' THEN 1 ELSE 0 END),0) AS skipped,
                   COALESCE(SUM(CASE WHEN o.state='planned' THEN 1 ELSE 0 END),0) AS planned,
                   COALESCE(SUM(CASE WHEN o.state='attempted' THEN 1 ELSE 0 END),0) AS attempted,
                   COALESCE(SUM(CASE WHEN o.evidence_ref IS NOT NULL THEN 1 ELSE 0 END),0) AS with_evidence
            FROM batches b LEFT JOIN operations o ON o.batch_id=b.batch_id
            WHERE ($provider='' OR b.provider=$provider)
              AND ($account='' OR b.account_id=$account)
            GROUP BY b.provider,b.procedure_id,b.procedure_version
            ORDER BY items DESC,batches DESC LIMIT 100;
            """;
        Parameters(cmd, ("$provider", provider), ("$account", accountId));
        using var reader = cmd.ExecuteReader();
        var rows = new JsonArray();
        long batchCount = 0, operationCount = 0;
        while (reader.Read())
        {
            batchCount += reader.GetInt64(3);
            operationCount += reader.GetInt64(4);
            rows.Add(new JsonObject
            {
                ["provider"] = reader.GetString(0),
                ["procedure_id"] = reader.IsDBNull(1) ? null : reader.GetString(1),
                ["procedure_version"] = reader.IsDBNull(2) ? null : reader.GetString(2),
                ["batches"] = reader.GetInt64(3), ["items"] = reader.GetInt64(4),
                ["attempts"] = reader.GetInt64(5), ["verified"] = reader.GetInt64(6),
                ["uncertain"] = reader.GetInt64(7), ["failed"] = reader.GetInt64(8),
                ["skipped"] = reader.GetInt64(9), ["planned"] = reader.GetInt64(10),
                ["attempted"] = reader.GetInt64(11), ["with_evidence"] = reader.GetInt64(12)
            });
        }
        return new JsonObject
        {
            ["status"] = "ok", ["provider"] = provider, ["account_id"] = accountId,
            ["batch_count"] = batchCount, ["operation_count"] = operationCount,
            ["groups"] = rows,
            ["note"] = "Operational ledger only: model tokens and cost remain in existing run usage logs."
        };
    }

    public JsonObject BatchStatus(string batchId)
    {
        using var db = Connect();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT state,COUNT(*) FROM operations WHERE batch_id=$batch GROUP BY state;";
        Parameters(cmd, ("$batch", batchId));
        using var reader = cmd.ExecuteReader();
        var result = new JsonObject
        {
            ["batch_id"] = batchId, ["planned"] = 0, ["prepared"] = 0,
            ["attempted"] = 0, ["verified"] = 0, ["uncertain"] = 0,
            ["failed"] = 0, ["skipped"] = 0, ["blocked"] = 0
        };
        var total = 0;
        while (reader.Read())
        {
            var state = reader.GetString(0);
            var count = reader.GetInt32(1);
            result[state] = count;
            total += count;
        }
        result["total"] = total;
        return result;
    }
}
