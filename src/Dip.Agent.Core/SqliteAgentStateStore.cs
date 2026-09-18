// Purpose: Implements endpoint-agent behavior for S ql it eA ge nt St at eS to re.
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Dip.Agent.Core;

/// <summary>
/// Durable local state. File snapshots and file-event outbox entries are committed
/// in one transaction. Tool and media queues are independent durable outboxes.
/// </summary>
public sealed class SqliteAgentStateStore
{
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SqliteAgentStateStore(string databasePath)
    {
        var full = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = full,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync(ct);
        var sql = """
        PRAGMA journal_mode=WAL;
        CREATE TABLE IF NOT EXISTS file_snapshot(
            relative_path TEXT PRIMARY KEY COLLATE NOCASE,
            size_bytes INTEGER NOT NULL,
            last_write_utc TEXT NOT NULL,
            sha256 TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS observation_outbox(
            id TEXT PRIMARY KEY,
            payload_json TEXT NOT NULL,
            created_utc TEXT NOT NULL,
            attempts INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS ix_observation_outbox_created
            ON observation_outbox(created_utc);
        CREATE TABLE IF NOT EXISTS tool_event_outbox(
            id TEXT PRIMARY KEY,
            payload_json TEXT NOT NULL,
            created_utc TEXT NOT NULL,
            attempts INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS ix_tool_event_outbox_created
            ON tool_event_outbox(created_utc);
        CREATE TABLE IF NOT EXISTS media_outbox(
            id TEXT PRIMARY KEY,
            event_id TEXT NOT NULL,
            source INTEGER NOT NULL,
            kind INTEGER NOT NULL,
            relative_path TEXT NOT NULL,
            spool_path TEXT NOT NULL,
            content_type TEXT NOT NULL,
            sha256 TEXT NOT NULL,
            size_bytes INTEGER NOT NULL,
            created_utc TEXT NOT NULL,
            attempts INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS ix_media_outbox_created
            ON media_outbox(created_utc);
        """;
        await using var command = db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, FileSnapshot>> LoadSnapshotAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, FileSnapshot>(StringComparer.OrdinalIgnoreCase);
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync(ct);
        await using var command = db.CreateCommand();
        command.CommandText = "SELECT relative_path,size_bytes,last_write_utc,sha256 FROM file_snapshot";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var path = reader.GetString(0);
            result[path] = new FileSnapshot(
                path,
                reader.GetInt64(1),
                DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture),
                reader.GetString(3));
        }
        return result;
    }

    public async Task ApplyScanAsync(ScanResult scan, CancellationToken ct)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync(ct);
        await using var tx = (SqliteTransaction)await db.BeginTransactionAsync(ct);

        foreach (var item in scan.Observations)
        {
            await using var insert = db.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = "INSERT OR IGNORE INTO observation_outbox(id,payload_json,created_utc,attempts) VALUES($id,$json,$created,0)";
            insert.Parameters.AddWithValue("$id", item.EventId);
            insert.Parameters.AddWithValue("$json", JsonSerializer.Serialize(item, JsonOptions));
            insert.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            await insert.ExecuteNonQueryAsync(ct);
        }

        await using (var clear = db.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM file_snapshot";
            await clear.ExecuteNonQueryAsync(ct);
        }

        foreach (var snapshot in scan.Snapshot.Values)
        {
            await using var insert = db.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = "INSERT INTO file_snapshot(relative_path,size_bytes,last_write_utc,sha256) VALUES($path,$size,$write,$sha)";
            insert.Parameters.AddWithValue("$path", snapshot.RelativePath);
            insert.Parameters.AddWithValue("$size", snapshot.SizeBytes);
            insert.Parameters.AddWithValue("$write", snapshot.LastWriteUtc.ToString("O"));
            insert.Parameters.AddWithValue("$sha", snapshot.Sha256);
            await insert.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<bool> HasToolEventAsync(string eventId, CancellationToken ct)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync(ct);
        await using var command = db.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM tool_event_outbox WHERE id=$id)";
        command.Parameters.AddWithValue("$id", eventId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) != 0;
    }

    public Task EnqueueToolEventAsync(ToolEventObservation item, QueuedMediaUpload? media, CancellationToken ct)
        => ExecuteToolEnqueueAsync(item, media, ct);

    private async Task ExecuteToolEnqueueAsync(ToolEventObservation item, QueuedMediaUpload? media, CancellationToken ct)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync(ct);
        await using var tx = (SqliteTransaction)await db.BeginTransactionAsync(ct);

        await using (var command = db.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = "INSERT OR IGNORE INTO tool_event_outbox(id,payload_json,created_utc,attempts) VALUES($id,$json,$created,0)";
            command.Parameters.AddWithValue("$id", item.EventId);
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(item, JsonOptions));
            command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(ct);
        }

        if (media is not null)
        {
            await using var command = db.CreateCommand();
            command.Transaction = tx;
            command.CommandText = "INSERT OR IGNORE INTO media_outbox(id,event_id,source,kind,relative_path,spool_path,content_type,sha256,size_bytes,created_utc,attempts) VALUES($id,$event,$source,$kind,$relative,$spool,$content,$sha,$size,$created,0)";
            command.Parameters.AddWithValue("$id", media.Id);
            command.Parameters.AddWithValue("$event", media.EventId);
            command.Parameters.AddWithValue("$source", (int)media.Source);
            command.Parameters.AddWithValue("$kind", (int)media.Kind);
            command.Parameters.AddWithValue("$relative", media.RelativePath);
            command.Parameters.AddWithValue("$spool", media.SpoolPath);
            command.Parameters.AddWithValue("$content", media.ContentType);
            command.Parameters.AddWithValue("$sha", media.Sha256);
            command.Parameters.AddWithValue("$size", media.SizeBytes);
            command.Parameters.AddWithValue("$created", media.CreatedAtUtc.ToString("O"));
            await command.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<QueuedObservation>> PeekAsync(int take, CancellationToken ct)
    {
        var output = new List<QueuedObservation>();
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync(ct);
        await using var command = db.CreateCommand();
        command.CommandText = "SELECT id,payload_json,created_utc,attempts FROM observation_outbox ORDER BY created_utc LIMIT $take";
        command.Parameters.AddWithValue("$take", Math.Clamp(take, 1, 500));
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var observation = JsonSerializer.Deserialize<LocalFileObservation>(reader.GetString(1), JsonOptions)
                ?? throw new InvalidDataException("Outbox payload could not be decoded.");
            output.Add(new QueuedObservation(reader.GetString(0), observation,
                DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture), reader.GetInt32(3)));
        }
        return output;
    }

    public async Task<IReadOnlyList<QueuedToolEvent>> PeekToolEventsAsync(int take, CancellationToken ct)
    {
        var output = new List<QueuedToolEvent>();
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync(ct);
        await using var command = db.CreateCommand();
        command.CommandText = "SELECT id,payload_json,created_utc,attempts FROM tool_event_outbox ORDER BY created_utc LIMIT $take";
        command.Parameters.AddWithValue("$take", Math.Clamp(take, 1, 250));
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<ToolEventObservation>(reader.GetString(1), JsonOptions)
                ?? throw new InvalidDataException("Tool-event outbox payload could not be decoded.");
            output.Add(new QueuedToolEvent(reader.GetString(0), item,
                DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture), reader.GetInt32(3)));
        }
        return output;
    }

    public async Task<IReadOnlyList<QueuedMediaUpload>> PeekMediaAsync(int take, CancellationToken ct)
    {
        var output = new List<QueuedMediaUpload>();
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync(ct);
        await using var command = db.CreateCommand();
        command.CommandText = "SELECT id,event_id,source,kind,relative_path,spool_path,content_type,sha256,size_bytes,created_utc,attempts FROM media_outbox ORDER BY created_utc LIMIT $take";
        command.Parameters.AddWithValue("$take", Math.Clamp(take, 1, 50));
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            output.Add(new QueuedMediaUpload(
                reader.GetString(0), reader.GetString(1), (ProductionToolSource)reader.GetInt32(2), (ProductionMediaKind)reader.GetInt32(3), reader.GetString(4),
                reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetInt64(8),
                DateTimeOffset.Parse(reader.GetString(9), System.Globalization.CultureInfo.InvariantCulture), reader.GetInt32(10)));
        }
        return output;
    }

    public Task CompleteAsync(IEnumerable<string> ids, CancellationToken ct)
        => DeleteAsync("observation_outbox", ids, ct);
    public Task CompleteToolEventsAsync(IEnumerable<string> ids, CancellationToken ct)
        => DeleteAsync("tool_event_outbox", ids, ct);
    public Task CompleteMediaAsync(IEnumerable<string> ids, CancellationToken ct)
        => DeleteAsync("media_outbox", ids, ct);

    public Task RecordFailureAsync(IEnumerable<string> ids, CancellationToken ct)
        => IncrementAsync("observation_outbox", ids, ct);
    public Task RecordToolFailureAsync(IEnumerable<string> ids, CancellationToken ct)
        => IncrementAsync("tool_event_outbox", ids, ct);
    public Task RecordMediaFailureAsync(IEnumerable<string> ids, CancellationToken ct)
        => IncrementAsync("media_outbox", ids, ct);

    private async Task DeleteAsync(string table, IEnumerable<string> ids, CancellationToken ct)
    {
        var values = ids.Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length == 0) return;
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync(ct);
        await using var tx = (SqliteTransaction)await db.BeginTransactionAsync(ct);
        foreach (var id in values)
        {
            await using var command = db.CreateCommand();
            command.Transaction = tx;
            command.CommandText = $"DELETE FROM {table} WHERE id=$id";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private async Task IncrementAsync(string table, IEnumerable<string> ids, CancellationToken ct)
    {
        var values = ids.Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length == 0) return;
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync(ct);
        foreach (var id in values)
        {
            await using var command = db.CreateCommand();
            command.CommandText = $"UPDATE {table} SET attempts=attempts+1 WHERE id=$id";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync(ct);
        }
    }
}
