using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dip.Infrastructure.Persistence.Migrations;

/// <summary>
/// Formal baseline for the schema that existed before DIP adopted EF migrations.
/// Existing compatible installations adopt this migration through LegacyMigrationAdopter;
/// empty databases execute it normally.
/// </summary>
[DbContext(typeof(DipDbContext))]
[Migration("202608120001_InitialBaseline")]
public sealed class InitialBaseline : Migration
{
    public const string MigrationId = "202608120001_InitialBaseline";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var sql in InitialBaselineSql.CreateStatements)
            migrationBuilder.Sql(sql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in InitialBaselineSql.DropOrder)
            migrationBuilder.Sql($"DROP TABLE IF EXISTS {table} CASCADE;");
    }
}

internal static class InitialBaselineSql
{
    public static readonly string[] CreateStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS "Projects" (
            "Id" uuid PRIMARY KEY,
            "Name" varchar(120) NOT NULL,
            "RepositoryUrl" varchar(2000) NOT NULL,
            "DefaultBranch" varchar(200) NOT NULL,
            "ReleaseDateUtc" timestamptz NULL,
            "HealthScore" numeric(5,2) NOT NULL,
            "CreatedAtUtc" timestamptz NOT NULL,
            "UpdatedAtUtc" timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_Projects_RepositoryUrl" ON "Projects" ("RepositoryUrl");
        """,
        """
        CREATE TABLE IF NOT EXISTS "PipelineRuns" (
            "Id" uuid PRIMARY KEY,
            "ProjectId" uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            "CommitSha" varchar(64) NOT NULL,
            "ExternalRunId" varchar(200) NOT NULL,
            "AttemptNumber" integer NOT NULL,
            "Branch" varchar(200) NOT NULL,
            "CollectorType" varchar(100) NOT NULL,
            "CollectorVersion" varchar(50) NOT NULL,
            "Status" integer NOT NULL,
            "QueuedAtUtc" timestamptz NOT NULL,
            "StartedAtUtc" timestamptz NULL,
            "FinishedAtUtc" timestamptz NULL,
            "FailureReason" varchar(2000) NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PipelineRuns_Project_External_Collector_Attempt"
            ON "PipelineRuns" ("ProjectId", "ExternalRunId", "CollectorType", "AttemptNumber");
        CREATE INDEX IF NOT EXISTS "IX_PipelineRuns_Project_Queued" ON "PipelineRuns" ("ProjectId", "QueuedAtUtc");
        """,
        """
        CREATE TABLE IF NOT EXISTS "Metrics" (
            "Id" uuid PRIMARY KEY,
            "PipelineRunId" uuid NOT NULL REFERENCES "PipelineRuns" ("Id") ON DELETE CASCADE,
            "BaselinePipelineRunId" uuid NULL,
            "Name" varchar(120) NOT NULL,
            "Value" numeric(18,6) NOT NULL,
            "Unit" varchar(30) NOT NULL,
            "BaselineValue" numeric(18,6) NULL,
            "Difference" numeric(18,6) NULL,
            "DifferencePercent" numeric(12,4) NULL,
            "Severity" integer NOT NULL,
            "ComparisonStatus" integer NOT NULL,
            "EnvironmentId" varchar(120) NULL,
            "ScenarioId" varchar(120) NULL,
            "Platform" varchar(80) NULL,
            "BuildConfiguration" varchar(80) NULL,
            "BenchmarkVersion" varchar(80) NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_Metrics_PipelineRun_Name" ON "Metrics" ("PipelineRunId", "Name");
        """,
        """
        CREATE TABLE IF NOT EXISTS "QualityIssues" (
            "Id" uuid PRIMARY KEY,
            "ProjectId" uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            "PipelineRunId" uuid NOT NULL REFERENCES "PipelineRuns" ("Id") ON DELETE CASCADE,
            "Fingerprint" varchar(64) NOT NULL,
            "Category" varchar(80) NOT NULL,
            "Severity" integer NOT NULL,
            "Title" varchar(200) NOT NULL,
            "Description" varchar(4000) NOT NULL,
            "Source" varchar(1000) NULL,
            "RuleId" varchar(200) NULL,
            "EvidenceJson" varchar(16000) NULL,
            "Status" integer NOT NULL,
            "OccurrenceCount" integer NOT NULL,
            "FirstSeenPipelineRunId" uuid NOT NULL,
            "LastSeenPipelineRunId" uuid NOT NULL,
            "CreatedAtUtc" timestamptz NOT NULL,
            "LastSeenAtUtc" timestamptz NOT NULL,
            "ResolvedAtUtc" timestamptz NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_QualityIssues_Project_Fingerprint" ON "QualityIssues" ("ProjectId", "Fingerprint");
        CREATE INDEX IF NOT EXISTS "IX_QualityIssues_Project_LastSeen" ON "QualityIssues" ("ProjectId", "LastSeenAtUtc");
        """,
        """
        CREATE TABLE IF NOT EXISTS "QualityGateResults" (
            "Id" uuid PRIMARY KEY,
            "ProjectId" uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            "PipelineRunId" uuid NOT NULL REFERENCES "PipelineRuns" ("Id") ON DELETE CASCADE,
            "Status" varchar(40) NOT NULL,
            "CriticalIssueCount" integer NOT NULL,
            "WarningIssueCount" integer NOT NULL,
            "Score" numeric(5,2) NOT NULL,
            "ReasonsJson" varchar(16000) NOT NULL,
            "EvaluatedAtUtc" timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_QualityGateResults_Project_Evaluated" ON "QualityGateResults" ("ProjectId", "EvaluatedAtUtc");
        """,
        """
        CREATE TABLE IF NOT EXISTS "OutboxMessages" (
            "Id" uuid PRIMARY KEY,
            "MessageType" varchar(500) NOT NULL,
            "PayloadJson" text NOT NULL,
            "CorrelationId" varchar(200) NOT NULL,
            "CausationId" varchar(200) NULL,
            "CreatedAtUtc" timestamptz NOT NULL,
            "LockedUntilUtc" timestamptz NULL,
            "NextAttemptAtUtc" timestamptz NULL,
            "ProcessedAtUtc" timestamptz NULL,
            "AttemptCount" integer NOT NULL,
            "LastError" varchar(4000) NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_Pending" ON "OutboxMessages" ("ProcessedAtUtc", "NextAttemptAtUtc", "LockedUntilUtc");
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProcessedWebhooks" (
            "DeliveryId" varchar(200) PRIMARY KEY,
            "EventName" varchar(100) NOT NULL,
            "PayloadHash" varchar(64) NOT NULL,
            "ReceivedAtUtc" timestamptz NOT NULL,
            "ProcessedAtUtc" timestamptz NULL,
            "AttemptCount" integer NOT NULL,
            "LastError" varchar(4000) NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_ProcessedWebhooks_ReceivedAtUtc" ON "ProcessedWebhooks" ("ReceivedAtUtc");
        """,
        """
        CREATE TABLE IF NOT EXISTS evidence_records (
            id uuid PRIMARY KEY,
            project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            pipeline_run_id uuid NULL REFERENCES "PipelineRuns" ("Id") ON DELETE SET NULL,
            subject_key varchar(200) NOT NULL, source_type varchar(40) NOT NULL,
            evidence_kind varchar(120) NOT NULL, outcome varchar(40) NOT NULL,
            title varchar(200) NOT NULL, summary varchar(4000) NOT NULL,
            confidence varchar(40) NOT NULL, source_reference varchar(1000) NULL,
            payload_json varchar(16000) NULL, observed_at_utc timestamptz NOT NULL,
            created_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_evidence_project_observed ON evidence_records (project_id, observed_at_utc);
        CREATE INDEX IF NOT EXISTS ix_evidence_project_subject_kind ON evidence_records (project_id, subject_key, evidence_kind);
        """,
        """
        CREATE TABLE IF NOT EXISTS project_claims (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            subject_key varchar(200) NOT NULL, claimed_status varchar(120) NOT NULL,
            claimed_progress numeric(5,2) NULL, claimed_by varchar(200) NOT NULL,
            description varchar(4000) NULL, claimed_at_utc timestamptz NOT NULL,
            verification_status varchar(40) NOT NULL, last_verified_at_utc timestamptz NULL
        );
        CREATE INDEX IF NOT EXISTS ix_claim_project_subject_time ON project_claims (project_id, subject_key, claimed_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS project_truth_snapshots (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            truth_status varchar(40) NOT NULL, risk_level varchar(40) NOT NULL,
            health_score numeric(5,2) NOT NULL, verified_progress numeric(5,2) NOT NULL,
            reported_progress numeric(5,2) NULL, evidence_confidence varchar(40) NOT NULL,
            blocker_count integer NOT NULL, contradiction_count integer NOT NULL,
            unverified_claim_count integer NOT NULL, summary varchar(4000) NOT NULL,
            blockers_json varchar(32000) NOT NULL, top_actions_json varchar(16000) NOT NULL,
            evaluated_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_truth_project_time ON project_truth_snapshots (project_id, evaluated_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS audit_logs (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            category varchar(80) NOT NULL, action varchar(120) NOT NULL, actor varchar(200) NOT NULL,
            subject_key varchar(200) NULL, detail_json varchar(16000) NULL, created_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_audit_project_time ON audit_logs (project_id, created_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS claim_evidence_links (
            id uuid PRIMARY KEY, claim_id uuid NOT NULL REFERENCES project_claims (id) ON DELETE CASCADE,
            evidence_record_id uuid NOT NULL REFERENCES evidence_records (id) ON DELETE CASCADE,
            relation varchar(40) NOT NULL, reason varchar(1000) NULL, created_at_utc timestamptz NOT NULL,
            CONSTRAINT uq_claim_evidence UNIQUE (claim_id, evidence_record_id)
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS qa_runs (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            subject_key varchar(200) NOT NULL, language varchar(40) NOT NULL,
            workspace_relative_path varchar(500) NOT NULL, commit_sha varchar(64) NULL,
            artifact_root varchar(1000) NOT NULL, status varchar(40) NOT NULL,
            finding_count integer NOT NULL, critical_finding_count integer NOT NULL,
            warning_finding_count integer NOT NULL, summary_json varchar(32000) NULL,
            created_at_utc timestamptz NOT NULL, started_at_utc timestamptz NULL, finished_at_utc timestamptz NULL
        );
        CREATE INDEX IF NOT EXISTS ix_qa_runs_project_created ON qa_runs (project_id, created_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS qa_findings (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            qa_run_id uuid NOT NULL REFERENCES qa_runs (id) ON DELETE CASCADE,
            subject_key varchar(200) NOT NULL, language varchar(40) NOT NULL,
            category varchar(120) NOT NULL, severity varchar(40) NOT NULL, tool varchar(120) NOT NULL,
            title varchar(240) NOT NULL, message varchar(8000) NOT NULL, fingerprint varchar(64) NOT NULL,
            file_path varchar(1000) NULL, line integer NULL, "column" integer NULL, function varchar(500) NULL,
            exception_type varchar(300) NULL, stack_trace varchar(32000) NULL, rule_id varchar(300) NULL,
            reproduction_status varchar(40) NOT NULL, reproduced_attempts integer NOT NULL,
            total_attempts integer NOT NULL, observed_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_qa_findings_project_fingerprint ON qa_findings (project_id, fingerprint, observed_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS reproduction_attempts (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            qa_finding_id uuid NOT NULL REFERENCES qa_findings (id) ON DELETE CASCADE,
            attempt_number integer NOT NULL, status varchar(40) NOT NULL, reproduced boolean NOT NULL,
            crashed boolean NOT NULL, timed_out boolean NOT NULL, exit_code integer NULL,
            duration_ms bigint NOT NULL, artifact_directory varchar(1000) NOT NULL,
            video_path varchar(1000) NULL, audio_path varchar(1000) NULL, dump_path varchar(1000) NULL,
            result_json varchar(32000) NULL, started_at_utc timestamptz NOT NULL, finished_at_utc timestamptz NULL,
            CONSTRAINT uq_reproduction_attempt UNIQUE (qa_finding_id, attempt_number)
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS hardware_profiles (
            id uuid PRIMARY KEY, name varchar(160) NOT NULL, platform varchar(80) NOT NULL,
            source varchar(40) NOT NULL, cpu_single_score numeric(12,3) NOT NULL,
            cpu_multi_score numeric(12,3) NOT NULL, gpu_score numeric(12,3) NOT NULL,
            memory_score numeric(12,3) NOT NULL, storage_score numeric(12,3) NOT NULL,
            thermal_score numeric(12,3) NOT NULL, memory_budget_mb bigint NOT NULL,
            target_fps integer NOT NULL, metadata_json varchar(32000) NOT NULL,
            created_at_utc timestamptz NOT NULL, CONSTRAINT uq_hardware_profile UNIQUE (name, platform)
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS hardware_calibrations (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            machine_fingerprint varchar(64) NOT NULL, cpu_single_score numeric(12,3) NOT NULL,
            cpu_multi_score numeric(12,3) NOT NULL, gpu_score numeric(12,3) NOT NULL,
            memory_score numeric(12,3) NOT NULL, storage_score numeric(12,3) NOT NULL,
            available_memory_mb bigint NOT NULL, environment_json varchar(32000) NOT NULL,
            measured_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_hardware_calibrations_project_time ON hardware_calibrations (project_id, measured_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS performance_simulation_runs (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            hardware_profile_id uuid NOT NULL REFERENCES hardware_profiles (id) ON DELETE RESTRICT,
            subject_key varchar(200) NOT NULL, artifact_directory varchar(1000) NOT NULL,
            status varchar(40) NOT NULL, confidence_percent numeric(5,2) NOT NULL,
            result_json varchar(32000) NULL, created_at_utc timestamptz NOT NULL,
            started_at_utc timestamptz NULL, finished_at_utc timestamptz NULL
        );
        CREATE INDEX IF NOT EXISTS ix_performance_simulation_project_time ON performance_simulation_runs (project_id, created_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS p2_intelligence_records (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            subject_key varchar(200) NOT NULL, kind varchar(80) NOT NULL, status varchar(40) NOT NULL,
            score numeric(5,2) NULL, confidence varchar(40) NOT NULL, summary varchar(4000) NOT NULL,
            payload_json varchar(32000) NOT NULL, source_reference varchar(1000) NULL,
            created_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_p2_intelligence_project_kind_time ON p2_intelligence_records (project_id, kind, created_at_utc);
        CREATE INDEX IF NOT EXISTS ix_p2_intelligence_project_subject_time ON p2_intelligence_records (project_id, subject_key, created_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS hardware_community_samples (
            id uuid PRIMARY KEY, profile_key varchar(160) NOT NULL, platform varchar(80) NOT NULL,
            hardware_key_hash varchar(64) NOT NULL, os_family varchar(120) NOT NULL,
            driver_family varchar(160) NOT NULL, cpu_single_score numeric(12,3) NOT NULL,
            cpu_multi_score numeric(12,3) NOT NULL, gpu_score numeric(12,3) NOT NULL,
            memory_score numeric(12,3) NOT NULL, storage_score numeric(12,3) NOT NULL,
            thermal_retention_percent numeric(5,2) NOT NULL, available_memory_mb bigint NOT NULL,
            real_device boolean NOT NULL, consent_granted boolean NOT NULL,
            measured_at_utc timestamptz NOT NULL, received_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_hardware_community_profile_time ON hardware_community_samples (profile_key, platform, received_at_utc);
        CREATE INDEX IF NOT EXISTS ix_hardware_community_hash_time ON hardware_community_samples (hardware_key_hash, measured_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS decision_traces (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            decision_type varchar(120) NOT NULL, subject_key varchar(200) NOT NULL,
            previous_value varchar(500) NOT NULL, new_value varchar(500) NOT NULL,
            reason varchar(4000) NOT NULL, evidence_ids_json varchar(16000) NOT NULL,
            detail_json varchar(32000) NOT NULL, created_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_decision_traces_project_time ON decision_traces (project_id, created_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS adaptive_qa_plan_snapshots (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            change_set_hash varchar(64) NOT NULL, risk_score numeric(5,2) NOT NULL,
            estimated_minutes numeric(8,2) NOT NULL, plan_json varchar(32000) NOT NULL,
            created_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_adaptive_qa_plan_project_time ON adaptive_qa_plan_snapshots (project_id, created_at_utc);
        """
    ];

    public static readonly string[] DropOrder =
    [
        "adaptive_qa_plan_snapshots", "decision_traces", "hardware_community_samples", "p2_intelligence_records",
        "performance_simulation_runs", "hardware_calibrations", "hardware_profiles", "reproduction_attempts",
        "qa_findings", "qa_runs", "claim_evidence_links", "audit_logs", "project_truth_snapshots",
        "project_claims", "evidence_records", "\"ProcessedWebhooks\"", "\"OutboxMessages\"",
        "\"QualityGateResults\"", "\"QualityIssues\"", "\"Metrics\"", "\"PipelineRuns\"", "\"Projects\""
    ];
}
