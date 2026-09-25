// Purpose: Implements Legacy Migration Adopter infrastructure responsibilities for persistence.
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dip.Infrastructure.Persistence;

public sealed class LegacySchemaMismatchException : InvalidOperationException
{
    public LegacySchemaMismatchException(string message) : base(message) { }
}

/// <summary>
/// Safely adopts databases created by the historical EnsureCreated path into the
/// formal EF migration history. It never marks a baseline as applied unless the
/// legacy schema has passed compatibility validation.
/// </summary>
public sealed class LegacyMigrationAdopter
{
    private const string BaselineMigration = "202608120001_InitialBaseline";
    private const string EfProductVersion = "8.0.4";
    private readonly ILogger<LegacyMigrationAdopter> _logger;

    public LegacyMigrationAdopter(ILogger<LegacyMigrationAdopter> logger) => _logger = logger;

    public async Task<bool> AdoptIfNecessaryAsync(DipDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var connection = db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        if (await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken)) return false;

        var hasAnyLegacySentinel =
            await TableExistsAsync(connection, "Projects", cancellationToken) ||
            await TableExistsAsync(connection, "PipelineRuns", cancellationToken) ||
            await TableExistsAsync(connection, "evidence_records", cancellationToken) ||
            await TableExistsAsync(connection, "qa_runs", cancellationToken);

        if (!hasAnyLegacySentinel) return false; // Empty/new database: migrations create everything.

        var validation = await LegacySchemaCompatibilityValidator.ValidateAsync(connection, cancellationToken);
        if (!validation.Compatible)
            throw new LegacySchemaMismatchException(
                "Legacy DIP database was detected but cannot be adopted safely. " +
                string.Join(" | ", validation.Errors));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );
                """,
                cancellationToken);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ({BaselineMigration}, {EfProductVersion})
                ON CONFLICT ("MigrationId") DO NOTHING;
                """,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "Adopted compatible legacy DIP schema into EF Core migration baseline {MigrationId}.",
                BaselineMigration);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task EnsureOpenAsync(DbConnection connection, CancellationToken ct)
    {
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct);
    }

    internal static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @name);";
        AddParameter(command, "@name", table);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(ct));
    }

    internal static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

public sealed record LegacySchemaValidationResult(bool Compatible, IReadOnlyList<string> Errors);

/// <summary>
/// Validates the immutable baseline surface before LegacyMigrationAdopter writes
/// __EFMigrationsHistory. The checks cover required tables, critical columns,
/// PostgreSQL type families, nullability, primary keys and critical FK/index
/// contracts. A mismatch fails closed instead of attempting destructive repair.
/// </summary>
public static class LegacySchemaCompatibilityValidator
{
    private sealed record ColumnContract(string Table, string Column, string Type, bool Nullable);

    private static readonly string[] RequiredTables =
    [
        "Projects", "PipelineRuns", "Metrics", "QualityIssues", "QualityGateResults", "OutboxMessages", "ProcessedWebhooks",
        "evidence_records", "project_claims", "project_truth_snapshots", "audit_logs", "claim_evidence_links",
        "qa_runs", "qa_findings", "reproduction_attempts", "hardware_profiles", "hardware_calibrations",
        "performance_simulation_runs", "p2_intelligence_records", "hardware_community_samples", "decision_traces",
        "adaptive_qa_plan_snapshots"
    ];

    private static readonly ColumnContract[] CriticalColumns =
    [
        new("Projects", "Id", "uuid", false),
        new("Projects", "HealthScore", "numeric", false),
        new("PipelineRuns", "Id", "uuid", false),
        new("PipelineRuns", "ProjectId", "uuid", false),
        new("PipelineRuns", "CommitSha", "character varying", false),
        new("PipelineRuns", "StartedAtUtc", "timestamp with time zone", true),
        new("PipelineRuns", "FinishedAtUtc", "timestamp with time zone", true),
        new("PipelineRuns", "Status", "integer", false),
        new("Projects", "CreatedAtUtc", "timestamp with time zone", false),
        new("Projects", "ReleaseDateUtc", "timestamp with time zone", true),
        new("Projects", "UpdatedAtUtc", "timestamp with time zone", false),
        new("Metrics", "BaselinePipelineRunId", "uuid", true),
        new("Metrics", "ComparisonStatus", "integer", false),
        new("Metrics", "Severity", "integer", false),
        new("QualityIssues", "CreatedAtUtc", "timestamp with time zone", false),
        new("QualityIssues", "FirstSeenPipelineRunId", "uuid", false),
        new("QualityIssues", "LastSeenPipelineRunId", "uuid", false),
        new("QualityIssues", "OccurrenceCount", "integer", false),
        new("QualityIssues", "ResolvedAtUtc", "timestamp with time zone", true),
        new("QualityIssues", "Severity", "integer", false),
        new("QualityIssues", "Status", "integer", false),
        new("QualityGateResults", "CriticalIssueCount", "integer", false),
        new("QualityGateResults", "WarningIssueCount", "integer", false),
        new("ProcessedWebhooks", "AttemptCount", "integer", false),
        new("ProcessedWebhooks", "ProcessedAtUtc", "timestamp with time zone", true),
        new("OutboxMessages", "AttemptCount", "integer", false),
        new("OutboxMessages", "CreatedAtUtc", "timestamp with time zone", false),
        new("OutboxMessages", "Id", "uuid", false),
        new("OutboxMessages", "PayloadJson", "text", false),
        new("ProcessedWebhooks", "DeliveryId", "character varying", false),
        new("evidence_records", "id", "uuid", false),
        new("evidence_records", "project_id", "uuid", false),
        new("qa_runs", "id", "uuid", false),
        new("qa_findings", "fingerprint", "character varying", false),
        new("p2_intelligence_records", "id", "uuid", false)
    ];

    public static async Task<LegacySchemaValidationResult> ValidateAsync(DbConnection connection, CancellationToken ct)
    {
        var errors = new List<string>();
        foreach (var table in RequiredTables)
            if (!await LegacyMigrationAdopter.TableExistsAsync(connection, table, ct)) errors.Add($"missing table '{table}'");

        if (errors.Count > 0) return new(false, errors);

        foreach (var expected in CriticalColumns)
        {
            var actual = await ReadColumnAsync(connection, expected.Table, expected.Column, ct);
            if (actual is null) { errors.Add($"missing column '{expected.Table}.{expected.Column}'"); continue; }
            if (!TypeCompatible(expected.Type, actual.Value.Type)) errors.Add($"type mismatch '{expected.Table}.{expected.Column}': expected {expected.Type}, found {actual.Value.Type}");
            if (expected.Nullable != actual.Value.Nullable) errors.Add($"nullability mismatch '{expected.Table}.{expected.Column}'");
        }

        foreach (var (table, column) in new[] { ("Projects", "Id"), ("PipelineRuns", "Id"), ("OutboxMessages", "Id"), ("ProcessedWebhooks", "DeliveryId"), ("evidence_records", "id"), ("qa_runs", "id") })
            if (!await IsPrimaryKeyAsync(connection, table, column, ct)) errors.Add($"primary key mismatch '{table}.{column}'");

        if (!await HasForeignKeyAsync(connection, "PipelineRuns", "ProjectId", "Projects", "Id", ct))
            errors.Add("missing FK PipelineRuns.ProjectId -> Projects.Id");
        if (!await HasForeignKeyAsync(connection, "evidence_records", "project_id", "Projects", "Id", ct))
            errors.Add("missing FK evidence_records.project_id -> Projects.Id");
        if (!await HasIndexStartingWithAsync(connection, "qa_findings", "project_id", ct))
            errors.Add("missing qa_findings project index");

        return new(errors.Count == 0, errors);
    }

    private static bool TypeCompatible(string expected, string actual)
        => string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase) ||
           expected == "character varying" && actual == "text";

    private static async Task<(string Type, bool Nullable)?> ReadColumnAsync(DbConnection connection, string table, string column, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT data_type, is_nullable FROM information_schema.columns WHERE table_schema='public' AND table_name=@table AND column_name=@column;";
        LegacyMigrationAdopter.AddParameter(command, "@table", table);
        LegacyMigrationAdopter.AddParameter(command, "@column", column);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return (reader.GetString(0), string.Equals(reader.GetString(1), "YES", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<bool> IsPrimaryKeyAsync(DbConnection connection, string table, string column, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
              SELECT 1 FROM information_schema.table_constraints tc
              JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name=kcu.constraint_name AND tc.table_schema=kcu.table_schema
              WHERE tc.table_schema='public' AND tc.table_name=@table
                AND tc.constraint_type='PRIMARY KEY' AND kcu.column_name=@column);
            """;
        LegacyMigrationAdopter.AddParameter(command, "@table", table);
        LegacyMigrationAdopter.AddParameter(command, "@column", column);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(ct));
    }

    private static async Task<bool> HasForeignKeyAsync(DbConnection connection, string table, string column, string targetTable, string targetColumn, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
              SELECT 1
              FROM information_schema.table_constraints tc
              JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name=kcu.constraint_name AND tc.table_schema=kcu.table_schema
              JOIN information_schema.constraint_column_usage ccu
                ON ccu.constraint_name=tc.constraint_name AND ccu.table_schema=tc.table_schema
              WHERE tc.constraint_type='FOREIGN KEY' AND tc.table_schema='public'
                AND tc.table_name=@table AND kcu.column_name=@column
                AND ccu.table_name=@target_table AND ccu.column_name=@target_column);
            """;
        LegacyMigrationAdopter.AddParameter(command, "@table", table);
        LegacyMigrationAdopter.AddParameter(command, "@column", column);
        LegacyMigrationAdopter.AddParameter(command, "@target_table", targetTable);
        LegacyMigrationAdopter.AddParameter(command, "@target_column", targetColumn);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(ct));
    }

    private static async Task<bool> HasIndexStartingWithAsync(DbConnection connection, string table, string column, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND tablename=@table AND indexdef ILIKE '%' || @column || '%');";
        LegacyMigrationAdopter.AddParameter(command, "@table", table);
        LegacyMigrationAdopter.AddParameter(command, "@column", column);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(ct));
    }
}
