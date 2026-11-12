// 2026-11-12 14:23 CET | GPT-6 | OpenFill | lightweight ChatGPT visit registry without AI.
using Microsoft.Data.Sqlite;

namespace OpenFill.Core.Hosting;

/// <summary>One durable row per ChatGPT conversation. No model calls or browser polling.</summary>
public sealed class ChatGptVisitStore
{
    private const string FileSuffix = "_GPT-6_OpenFill_ChatGPT_CzatyURLTytulOstatniDostep_RejestrAutomatyczny.sqlite";
    private readonly object _sync = new();
    public string DatabasePath { get; }

    public ChatGptVisitStore(string directory)
    {
        Directory.CreateDirectory(directory);
        var existing = Directory.GetFiles(directory, "*" + FileSuffix).OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault();
        var local = DateTimeOffset.Now;
        DatabasePath = existing ?? Path.Combine(directory,
            local.ToString("yyyy-MM-dd_HH-mm-ss") + (local.Offset.Hours == 2 ? "_CEST" : "_CET") + FileSuffix);
        lock (_sync)
        {
            using var db = Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS chat_visits (
                  chat_id TEXT PRIMARY KEY NOT NULL,
                  title TEXT NOT NULL DEFAULT '',
                  url TEXT NOT NULL,
                  first_access_utc TEXT NOT NULL,
                  last_access_utc TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_chat_visits_last_access
                  ON chat_visits(last_access_utc);
                """;
            cmd.ExecuteNonQuery();
        }
    }

    public static bool TryParse(string? url, out string chatId, out string canonicalUrl)
    {
        chatId = "";
        canonicalUrl = "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return false;
        if (!string.Equals(uri.Host, "chatgpt.com", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Host, "chat.openai.com", StringComparison.OrdinalIgnoreCase))
            return false;
        var pieces = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i + 1 < pieces.Length; i++)
        {
            if (!string.Equals(pieces[i], "c", StringComparison.OrdinalIgnoreCase))
                continue;
            var id = pieces[i + 1];
            if (id.Length < 12 || id.Length > 128 ||
                id.Any(ch => !char.IsLetterOrDigit(ch) && ch is not '-' and not '_'))
                return false;
            chatId = id;
            canonicalUrl = uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath;
            return true;
        }
        return false;
    }

    public bool Record(string? url, string? title, DateTimeOffset? accessedAt = null)
    {
        if (!TryParse(url, out var id, out var canonicalUrl))
            return false;
        var stamp = (accessedAt ?? DateTimeOffset.UtcNow).ToUniversalTime().ToString("O");
        var normalizedTitle = (title ?? "").Trim();
        if (normalizedTitle is "ChatGPT" or "New chat" or "Nowy czat" ||
            normalizedTitle.StartsWith("ChatGPT:", StringComparison.OrdinalIgnoreCase))
            normalizedTitle = "";
        else if (normalizedTitle.EndsWith(" - ChatGPT", StringComparison.OrdinalIgnoreCase))
            normalizedTitle = normalizedTitle[..^10].Trim();
        if (normalizedTitle.Length > 500)
            normalizedTitle = normalizedTitle[..500];

        lock (_sync)
        {
            using var db = Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO chat_visits(chat_id,title,url,first_access_utc,last_access_utc)
                VALUES($id,$title,$url,$at,$at)
                ON CONFLICT(chat_id) DO UPDATE SET
                    title = CASE WHEN excluded.title <> '' THEN excluded.title ELSE chat_visits.title END,
                    url = CASE WHEN excluded.last_access_utc >= chat_visits.last_access_utc
                               THEN excluded.url ELSE chat_visits.url END,
                    last_access_utc = CASE WHEN excluded.last_access_utc > chat_visits.last_access_utc
                                           THEN excluded.last_access_utc ELSE chat_visits.last_access_utc END;
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$title", normalizedTitle);
            cmd.Parameters.AddWithValue("$url", canonicalUrl);
            cmd.Parameters.AddWithValue("$at", stamp);
            cmd.ExecuteNonQuery();
        }
        return true;
    }

    private SqliteConnection Open()
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true
        }.ToString());
        db.Open();
        using var pragma = db.CreateCommand();
        pragma.CommandText = "PRAGMA busy_timeout = 5000;";
        pragma.ExecuteNonQuery();
        return db;
    }
}

