using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dip.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DipDbContext))]
[Migration("202608120002_AddDurablePlatformIntelligence")]
public sealed class AddDurablePlatformIntelligence : Migration
{
    public const string MigrationId = "202608120002_AddDurablePlatformIntelligence";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var sql in CreateStatements) migrationBuilder.Sql(sql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "benchmark_comparisons", "benchmark_metrics", "artifact_records", "incident_events", "incident_occurrences", "benchmark_runs", "benchmark_definitions", "incidents", "durable_jobs", "artifact_blobs" })
            migrationBuilder.Sql($"DROP TABLE IF EXISTS {table} CASCADE;");
    }

    private static readonly string[] CreateStatements =
    [
        """
        -- Normalize legacy EnsureCreated core metadata before adding the new platform tables.
        ALTER TABLE "Projects" ALTER COLUMN "Name" TYPE varchar(120);
        ALTER TABLE "Projects" ALTER COLUMN "RepositoryUrl" TYPE varchar(2000);
        ALTER TABLE "Projects" ALTER COLUMN "DefaultBranch" TYPE varchar(200);
        ALTER TABLE "Projects" ALTER COLUMN "HealthScore" TYPE numeric(5,2) USING "HealthScore"::numeric;
        CREATE INDEX IF NOT EXISTS "IX_Projects_RepositoryUrl" ON "Projects" ("RepositoryUrl");

        ALTER TABLE "PipelineRuns" ALTER COLUMN "CommitSha" TYPE varchar(64);
        ALTER TABLE "PipelineRuns" ALTER COLUMN "ExternalRunId" TYPE varchar(200);
        ALTER TABLE "PipelineRuns" ALTER COLUMN "Branch" TYPE varchar(200);
        ALTER TABLE "PipelineRuns" ALTER COLUMN "CollectorType" TYPE varchar(100);
        ALTER TABLE "PipelineRuns" ALTER COLUMN "CollectorVersion" TYPE varchar(50);
        ALTER TABLE "PipelineRuns" ALTER COLUMN "FailureReason" TYPE varchar(2000);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PipelineRuns_Project_External_Collector_Attempt"
            ON "PipelineRuns" ("ProjectId", "ExternalRunId", "CollectorType", "AttemptNumber");
        CREATE INDEX IF NOT EXISTS "IX_PipelineRuns_Project_Queued" ON "PipelineRuns" ("ProjectId", "QueuedAtUtc");

        ALTER TABLE "Metrics" ALTER COLUMN "Name" TYPE varchar(120);
        ALTER TABLE "Metrics" ALTER COLUMN "Unit" TYPE varchar(30);
        ALTER TABLE "Metrics" ALTER COLUMN "Value" TYPE numeric(18,6) USING "Value"::numeric;
        ALTER TABLE "Metrics" ALTER COLUMN "BaselineValue" TYPE numeric(18,6) USING "BaselineValue"::numeric;
        ALTER TABLE "Metrics" ALTER COLUMN "Difference" TYPE numeric(18,6) USING "Difference"::numeric;
        ALTER TABLE "Metrics" ALTER COLUMN "DifferencePercent" TYPE numeric(12,4) USING "DifferencePercent"::numeric;
        ALTER TABLE "Metrics" ALTER COLUMN "EnvironmentId" TYPE varchar(120);
        ALTER TABLE "Metrics" ALTER COLUMN "ScenarioId" TYPE varchar(120);
        ALTER TABLE "Metrics" ALTER COLUMN "Platform" TYPE varchar(80);
        ALTER TABLE "Metrics" ALTER COLUMN "BuildConfiguration" TYPE varchar(80);
        ALTER TABLE "Metrics" ALTER COLUMN "BenchmarkVersion" TYPE varchar(80);
        CREATE INDEX IF NOT EXISTS "IX_Metrics_PipelineRun_Name" ON "Metrics" ("PipelineRunId", "Name");

        ALTER TABLE "QualityIssues" ALTER COLUMN "Fingerprint" TYPE varchar(64);
        ALTER TABLE "QualityIssues" ALTER COLUMN "Category" TYPE varchar(80);
        ALTER TABLE "QualityIssues" ALTER COLUMN "Title" TYPE varchar(200);
        ALTER TABLE "QualityIssues" ALTER COLUMN "Description" TYPE varchar(4000);
        ALTER TABLE "QualityIssues" ALTER COLUMN "Source" TYPE varchar(1000);
        ALTER TABLE "QualityIssues" ALTER COLUMN "RuleId" TYPE varchar(200);
        ALTER TABLE "QualityIssues" ALTER COLUMN "EvidenceJson" TYPE varchar(16000);
        CREATE INDEX IF NOT EXISTS "IX_QualityIssues_Project_Fingerprint" ON "QualityIssues" ("ProjectId", "Fingerprint");
        CREATE INDEX IF NOT EXISTS "IX_QualityIssues_Project_LastSeen" ON "QualityIssues" ("ProjectId", "LastSeenAtUtc");

        ALTER TABLE "QualityGateResults" ALTER COLUMN "Status" TYPE varchar(40);
        ALTER TABLE "QualityGateResults" ALTER COLUMN "Score" TYPE numeric(5,2) USING "Score"::numeric;
        ALTER TABLE "QualityGateResults" ALTER COLUMN "ReasonsJson" TYPE varchar(16000);
        CREATE INDEX IF NOT EXISTS "IX_QualityGateResults_Project_Evaluated" ON "QualityGateResults" ("ProjectId", "EvaluatedAtUtc");

        ALTER TABLE "OutboxMessages" ALTER COLUMN "MessageType" TYPE varchar(500);
        ALTER TABLE "OutboxMessages" ALTER COLUMN "CorrelationId" TYPE varchar(200);
        ALTER TABLE "OutboxMessages" ALTER COLUMN "CausationId" TYPE varchar(200);
        ALTER TABLE "OutboxMessages" ALTER COLUMN "LastError" TYPE varchar(4000);
        CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_Pending" ON "OutboxMessages" ("ProcessedAtUtc", "NextAttemptAtUtc", "LockedUntilUtc");

        ALTER TABLE "ProcessedWebhooks" ALTER COLUMN "DeliveryId" TYPE varchar(200);
        ALTER TABLE "ProcessedWebhooks" ALTER COLUMN "EventName" TYPE varchar(100);
        ALTER TABLE "ProcessedWebhooks" ALTER COLUMN "PayloadHash" TYPE varchar(64);
        ALTER TABLE "ProcessedWebhooks" ALTER COLUMN "LastError" TYPE varchar(4000);
        CREATE INDEX IF NOT EXISTS "IX_ProcessedWebhooks_ReceivedAtUtc" ON "ProcessedWebhooks" ("ReceivedAtUtc");
        """,
        """
        CREATE TABLE IF NOT EXISTS artifact_blobs (
            id uuid PRIMARY KEY, sha256 varchar(64) NOT NULL, size_bytes bigint NOT NULL,
            content_type varchar(200) NOT NULL, storage_provider varchar(40) NOT NULL,
            storage_key varchar(1000) NOT NULL, created_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_artifact_blob_provider_sha ON artifact_blobs (storage_provider, sha256);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_artifact_blob_provider_key ON artifact_blobs (storage_provider, storage_key);
        """,
        """
        CREATE TABLE IF NOT EXISTS durable_jobs (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            type varchar(80) NOT NULL, status varchar(40) NOT NULL, subject_key varchar(200) NOT NULL,
            payload_json text NOT NULL, result_json text NULL, attempt integer NOT NULL, max_attempts integer NOT NULL,
            progress_percent numeric(5,2) NOT NULL, current_stage varchar(200) NULL,
            created_at_utc timestamptz NOT NULL, queued_at_utc timestamptz NOT NULL,
            started_at_utc timestamptz NULL, finished_at_utc timestamptz NULL,
            heartbeat_at_utc timestamptz NULL, lease_owner varchar(200) NULL, lease_expires_at_utc timestamptz NULL,
            cancellation_requested_at_utc timestamptz NULL, failure_code varchar(120) NULL,
            failure_message varchar(8000) NULL
        );
        CREATE INDEX IF NOT EXISTS ix_durable_jobs_status_queued ON durable_jobs (status, queued_at_utc);
        CREATE INDEX IF NOT EXISTS ix_durable_jobs_lease ON durable_jobs (lease_expires_at_utc);
        CREATE INDEX IF NOT EXISTS ix_durable_jobs_project_created ON durable_jobs (project_id, created_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS incidents (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            fingerprint varchar(128) NOT NULL, subject_key varchar(200) NOT NULL, title varchar(240) NOT NULL,
            severity varchar(40) NOT NULL, status varchar(40) NOT NULL,
            first_seen_at_utc timestamptz NOT NULL, last_seen_at_utc timestamptz NOT NULL,
            occurrence_count integer NOT NULL, reproduced_count integer NOT NULL,
            suspected_regression_commit varchar(64) NULL, fixed_commit varchar(64) NULL,
            root_cause_summary varchar(4000) NULL, resolved_at_utc timestamptz NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_incidents_project_fingerprint ON incidents (project_id, fingerprint);
        CREATE INDEX IF NOT EXISTS ix_incidents_project_last_seen ON incidents (project_id, last_seen_at_utc);
        CREATE INDEX IF NOT EXISTS ix_incidents_project_subject_status ON incidents (project_id, subject_key, status);
        """,
        """
        CREATE TABLE IF NOT EXISTS benchmark_definitions (
            id uuid PRIMARY KEY, key varchar(160) NOT NULL, name varchar(200) NOT NULL,
            kind varchar(80) NOT NULL, configuration_json varchar(32000) NOT NULL,
            enabled boolean NOT NULL, created_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_benchmark_definitions_key ON benchmark_definitions (key);
        """,
        """
        CREATE TABLE IF NOT EXISTS benchmark_runs (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            benchmark_definition_id uuid NOT NULL REFERENCES benchmark_definitions (id) ON DELETE RESTRICT,
            job_id uuid NOT NULL REFERENCES durable_jobs (id) ON DELETE RESTRICT,
            subject_key varchar(200) NOT NULL, commit_sha varchar(64) NOT NULL,
            environment_fingerprint varchar(128) NOT NULL, configuration_hash varchar(64) NOT NULL,
            status varchar(40) NOT NULL, created_at_utc timestamptz NOT NULL,
            started_at_utc timestamptz NULL, finished_at_utc timestamptz NULL, summary varchar(4000) NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_benchmark_runs_job ON benchmark_runs (job_id);
        CREATE INDEX IF NOT EXISTS ix_benchmark_runs_baseline ON benchmark_runs
            (project_id, benchmark_definition_id, environment_fingerprint, configuration_hash, created_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS artifact_records (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            artifact_blob_id uuid NOT NULL REFERENCES artifact_blobs (id) ON DELETE RESTRICT,
            kind varchar(120) NOT NULL, file_name varchar(500) NOT NULL, retention_class varchar(40) NOT NULL,
            job_id uuid NULL REFERENCES durable_jobs (id) ON DELETE SET NULL,
            qa_run_id uuid NULL REFERENCES qa_runs (id) ON DELETE SET NULL,
            incident_id uuid NULL REFERENCES incidents (id) ON DELETE SET NULL,
            benchmark_run_id uuid NULL REFERENCES benchmark_runs (id) ON DELETE SET NULL,
            created_at_utc timestamptz NOT NULL, expires_at_utc timestamptz NULL
        );
        CREATE INDEX IF NOT EXISTS ix_artifact_records_project_created ON artifact_records (project_id, created_at_utc);
        CREATE INDEX IF NOT EXISTS ix_artifact_records_job ON artifact_records (job_id);
        CREATE INDEX IF NOT EXISTS ix_artifact_records_incident ON artifact_records (incident_id);
        CREATE INDEX IF NOT EXISTS ix_artifact_records_benchmark ON artifact_records (benchmark_run_id);
        """,
        """
        CREATE TABLE IF NOT EXISTS incident_occurrences (
            id uuid PRIMARY KEY, incident_id uuid NOT NULL REFERENCES incidents (id) ON DELETE CASCADE,
            project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            subject_key varchar(200) NOT NULL,
            qa_finding_id uuid NULL REFERENCES qa_findings (id) ON DELETE SET NULL,
            qa_run_id uuid NULL REFERENCES qa_runs (id) ON DELETE SET NULL,
            job_id uuid NULL REFERENCES durable_jobs (id) ON DELETE SET NULL,
            commit_sha varchar(64) NULL, detail_json varchar(16000) NULL, observed_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_incident_occurrences_timeline ON incident_occurrences (incident_id, observed_at_utc);
        CREATE INDEX IF NOT EXISTS ix_incident_occurrences_finding ON incident_occurrences (qa_finding_id);
        """,
        """
        CREATE TABLE IF NOT EXISTS incident_events (
            id uuid PRIMARY KEY, incident_id uuid NOT NULL REFERENCES incidents (id) ON DELETE CASCADE,
            type varchar(80) NOT NULL, summary varchar(2000) NOT NULL,
            payload_json varchar(32000) NULL, occurred_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_incident_events_timeline ON incident_events (incident_id, occurred_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS benchmark_metrics (
            id uuid PRIMARY KEY, benchmark_run_id uuid NOT NULL REFERENCES benchmark_runs (id) ON DELETE CASCADE,
            name varchar(160) NOT NULL, value numeric(20,6) NOT NULL, unit varchar(40) NOT NULL,
            aggregation varchar(40) NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_benchmark_metrics_run_name_agg ON benchmark_metrics (benchmark_run_id, name, aggregation);
        """,
        """
        CREATE TABLE IF NOT EXISTS benchmark_comparisons (
            id uuid PRIMARY KEY, current_run_id uuid NOT NULL REFERENCES benchmark_runs (id) ON DELETE CASCADE,
            baseline_run_id uuid NOT NULL REFERENCES benchmark_runs (id) ON DELETE RESTRICT,
            metric_name varchar(160) NOT NULL, baseline_value numeric(20,6) NOT NULL,
            current_value numeric(20,6) NOT NULL, difference_percent numeric(12,3) NOT NULL,
            verdict varchar(40) NOT NULL, explanation varchar(2000) NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_benchmark_comparisons_current_metric ON benchmark_comparisons (current_run_id, metric_name);
        """
    ];
}
