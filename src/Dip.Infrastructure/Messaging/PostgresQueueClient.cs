// Purpose: Provides the P os tg re sQ ue ue Cl ie nt infrastructure implementation.
using System.Data;
using System.Data.Common;
using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using Dip.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Messaging;

public sealed class PostgresQueueClient(IServiceScopeFactory scopeFactory) : IQueueClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim BootstrapGate = new(1, 1);
    private static volatile bool _bootstrapped;
    private static readonly TimeSpan DeliveryLease = TimeSpan.FromMinutes(2);

    public async Task PublishAsync<T>(string stream, T message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        ArgumentNullException.ThrowIfNull(message);
        var payload = JsonSerializer.Serialize(message, JsonOptions);
        var type = ReadScalar(payload, "messageType") ?? typeof(T).FullName ?? typeof(T).Name;
        var correlation = ReadScalar(payload, "correlationId") ?? string.Empty;
        _ = int.TryParse(ReadScalar(payload, "attempt"), out var attempt);
        await AddEnvelopeAsync(stream, type, payload, correlation, Math.Max(0, attempt), cancellationToken);
    }

    public async Task<QueueEnvelope?> ReadAsync(string stream, string consumerGroup, string consumerName, TimeSpan block, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroup);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        if (block <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(block));
        await EnsureSchemaAsync(cancellationToken);
        var deadline = DateTimeOffset.UtcNow + block;

        while (!cancellationToken.IsCancellationRequested)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
            await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            const string claimSql = """
                WITH candidate AS (
                    SELECT m.id
                    FROM dip_queue_messages m
                    LEFT JOIN dip_queue_deliveries d
                      ON d.stream=m.stream AND d.consumer_group=@group AND d.message_id=m.id
                    WHERE m.stream=@stream
                      AND (d.message_id IS NULL OR (d.acknowledged_at_utc IS NULL AND d.lease_until_utc <= now()))
                    ORDER BY m.id
                    LIMIT 1
                    FOR UPDATE OF m SKIP LOCKED
                )
                INSERT INTO dip_queue_deliveries(stream,consumer_group,message_id,consumer_name,lease_until_utc,acknowledged_at_utc)
                SELECT @stream,@group,c.id,@consumer,now()+@lease,NULL FROM candidate c
                ON CONFLICT(stream,consumer_group,message_id) DO UPDATE SET
                    consumer_name=EXCLUDED.consumer_name,
                    lease_until_utc=EXCLUDED.lease_until_utc,
                    acknowledged_at_utc=NULL
                WHERE dip_queue_deliveries.acknowledged_at_utc IS NULL
                  AND dip_queue_deliveries.lease_until_utc <= now()
                RETURNING message_id;
                """;
            var claimed = await ScalarAsync(connection, tx, claimSql, cancellationToken,
                ("@stream", stream), ("@group", consumerGroup), ("@consumer", consumerName), ("@lease", DeliveryLease));

            if (claimed is not null && claimed != DBNull.Value)
            {
                var id = Convert.ToInt64(claimed);
                const string readSql = "SELECT message_type,payload_json,attempt,correlation_id FROM dip_queue_messages WHERE id=@id;";
                await using var cmd = CreateCommand(connection, tx, readSql, ("@id", id));
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var envelope = new QueueEnvelope(
                        id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3));
                    await reader.DisposeAsync();
                    await tx.CommitAsync(cancellationToken);
                    return envelope;
                }
            }
            await tx.CommitAsync(cancellationToken);
            if (DateTimeOffset.UtcNow >= deadline) return null;
            var remaining = deadline - DateTimeOffset.UtcNow;
            await Task.Delay(remaining < TimeSpan.FromMilliseconds(100) ? remaining : TimeSpan.FromMilliseconds(100), cancellationToken);
        }
        return null;
    }

    public async Task AcknowledgeAsync(string stream, string consumerGroup, string messageId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(messageId, out var id)) throw new ArgumentException("PostgreSQL queue message id must be an integer.", nameof(messageId));
        await EnsureSchemaAsync(cancellationToken);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dip_queue_deliveries SET acknowledged_at_utc=now() WHERE stream={stream} AND consumer_group={consumerGroup} AND message_id={id} AND acknowledged_at_utc IS NULL", cancellationToken);
    }

    public async Task RetryAsync(string stream, string consumerGroup, QueueEnvelope message, CancellationToken cancellationToken)
    {
        await AddEnvelopeAsync(stream, message.MessageType, message.PayloadJson, message.CorrelationId, checked(message.Attempt + 1), cancellationToken);
        await AcknowledgeAsync(stream, consumerGroup, message.MessageId, cancellationToken);
    }

    public async Task MoveToDeadLetterAsync(string sourceStream, string consumerGroup, string deadLetterStream, QueueEnvelope message, string reason, CancellationToken cancellationToken)
    {
        var safe = string.IsNullOrWhiteSpace(reason) ? "Unknown queue processing failure." : reason.Trim();
        if (safe.Length > 4000) safe = safe[..4000];
        var payload = JsonSerializer.Serialize(new
        {
            sourceMessageId = message.MessageId,
            message.MessageType,
            payload = message.PayloadJson,
            message.Attempt,
            message.CorrelationId,
            reason = safe,
            failedAtUtc = DateTimeOffset.UtcNow
        }, JsonOptions);
        await AddEnvelopeAsync(deadLetterStream, message.MessageType, payload, message.CorrelationId, message.Attempt, cancellationToken);
        await AcknowledgeAsync(sourceStream, consumerGroup, message.MessageId, cancellationToken);
    }

    private async Task AddEnvelopeAsync(string stream, string type, string payload, string correlation, int attempt, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dip_queue_messages(stream,message_type,payload_json,attempt,correlation_id) VALUES ({stream},{type},{payload},{attempt},{correlation})", ct);
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_bootstrapped) return;
        await BootstrapGate.WaitAsync(ct);
        try
        {
            if (_bootstrapped) return;
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS dip_queue_messages(
                    id BIGSERIAL PRIMARY KEY,
                    stream TEXT NOT NULL,
                    message_type TEXT NOT NULL,
                    payload_json TEXT NOT NULL,
                    attempt INTEGER NOT NULL CHECK(attempt>=0),
                    correlation_id TEXT NOT NULL DEFAULT '',
                    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now());
                CREATE INDEX IF NOT EXISTS ix_dip_queue_messages_stream_id ON dip_queue_messages(stream,id);
                CREATE TABLE IF NOT EXISTS dip_queue_deliveries(
                    stream TEXT NOT NULL,
                    consumer_group TEXT NOT NULL,
                    message_id BIGINT NOT NULL REFERENCES dip_queue_messages(id) ON DELETE CASCADE,
                    consumer_name TEXT NOT NULL,
                    lease_until_utc TIMESTAMPTZ NOT NULL,
                    acknowledged_at_utc TIMESTAMPTZ NULL,
                    PRIMARY KEY(stream,consumer_group,message_id));
                CREATE INDEX IF NOT EXISTS ix_dip_queue_deliveries_lease ON dip_queue_deliveries(stream,consumer_group,acknowledged_at_utc,lease_until_utc);
                """, ct);
            _bootstrapped = true;
        }
        finally { BootstrapGate.Release(); }
    }

    private static DbCommand CreateCommand(DbConnection connection, DbTransaction transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var p = command.CreateParameter(); p.ParameterName = name; p.Value = value ?? DBNull.Value; command.Parameters.Add(p);
        }
        return command;
    }

    private static async Task<object?> ScalarAsync(DbConnection connection, DbTransaction tx, string sql, CancellationToken ct, params (string Name, object? Value)[] parameters)
    {
        await using var command = CreateCommand(connection, tx, sql, parameters);
        return await command.ExecuteScalarAsync(ct);
    }

    private static string? ReadScalar(string json, string name)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            foreach (var p in d.RootElement.EnumerateObject())
            {
                if (!p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                return p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(), JsonValueKind.Number => p.Value.GetRawText(),
                    JsonValueKind.True => "true", JsonValueKind.False => "false", _ => null
                };
            }
        }
        catch (JsonException) { }
        return null;
    }
}
