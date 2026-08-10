using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence;

/// <summary>
/// The repository historically used EnsureCreated and has no EF migration
/// baseline. EnsureCreated cannot add tables to an already-created database.
/// This idempotent bootstrap therefore adds the Project Truth P0 and Technical Evidence P1 tables
/// when the installation is still in the no-migrations mode.
/// </summary>
public static class P0SchemaBootstrapper
{
    public static async Task EnsureProjectTruthSchemaAsync(
        DipDbContext db,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var statements = new[]
        {
            """
            CREATE TABLE IF NOT EXISTS evidence_records (
                id uuid PRIMARY KEY,
                project_id uuid NOT NULL REFERENCES projects ("Id") ON DELETE CASCADE,
                pipeline_run_id uuid NULL REFERENCES pipeline_runs ("Id") ON DELETE SET NULL,
                subject_key varchar(200) NOT NULL,
                source_type varchar(40) NOT NULL,
                evidence_kind varchar(120) NOT NULL,
                outcome varchar(40) NOT NULL,
                title varchar(200) NOT NULL,
                summary varchar(4000) NOT NULL,
                confidence varchar(40) NOT NULL,
                source_reference varchar(1000) NULL,
                payload_json varchar(16000) NULL,
                observed_at_utc timestamptz NOT NULL,
                created_at_utc timestamptz NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS project_claims (
                id uuid PRIMARY KEY,
                project_id uuid NOT NULL REFERENCES projects ("Id") ON DELETE CASCADE,
                subject_key varchar(200) NOT NULL,
                claimed_status varchar(120) NOT NULL,
                claimed_progress numeric(5,2) NULL,
                claimed_by varchar(200) NOT NULL,
                description varchar(4000) NULL,
                claimed_at_utc timestamptz NOT NULL,
                verification_status varchar(40) NOT NULL,
                last_verified_at_utc timestamptz NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS project_truth_snapshots (
                id uuid PRIMARY KEY,
                project_id uuid NOT NULL REFERENCES projects ("Id") ON DELETE CASCADE,
                truth_status varchar(40) NOT NULL,
                risk_level varchar(40) NOT NULL,
                health_score numeric(5,2) NOT NULL,
                verified_progress numeric(5,2) NOT NULL,
                reported_progress numeric(5,2) NULL,
                evidence_confidence varchar(40) NOT NULL,
                blocker_count integer NOT NULL,
                contradiction_count integer NOT NULL,
                unverified_claim_count integer NOT NULL,
                summary varchar(4000) NOT NULL,
                blockers_json varchar(32000) NOT NULL,
                top_actions_json varchar(16000) NOT NULL,
                evaluated_at_utc timestamptz NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS audit_logs (
                id uuid PRIMARY KEY,
                project_id uuid NOT NULL REFERENCES projects ("Id") ON DELETE CASCADE,
                category varchar(80) NOT NULL,
                action varchar(120) NOT NULL,
                actor varchar(200) NOT NULL,
                subject_key varchar(200) NULL,
                detail_json varchar(16000) NULL,
                created_at_utc timestamptz NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS claim_evidence_links (
                id uuid PRIMARY KEY,
                claim_id uuid NOT NULL REFERENCES project_claims (id) ON DELETE CASCADE,
                evidence_record_id uuid NOT NULL REFERENCES evidence_records (id) ON DELETE CASCADE,
                relation varchar(40) NOT NULL,
                reason varchar(1000) NULL,
                created_at_utc timestamptz NOT NULL,
                CONSTRAINT uq_claim_evidence UNIQUE (claim_id, evidence_record_id)
            );
            """,
            "CREATE INDEX IF NOT EXISTS ix_evidence_project_observed ON evidence_records (project_id, observed_at_utc);",
            "CREATE INDEX IF NOT EXISTS ix_evidence_project_subject_kind ON evidence_records (project_id, subject_key, evidence_kind);",
            "CREATE INDEX IF NOT EXISTS ix_claim_project_subject_time ON project_claims (project_id, subject_key, claimed_at_utc);",
            "CREATE INDEX IF NOT EXISTS ix_truth_project_time ON project_truth_snapshots (project_id, evaluated_at_utc);",
            "CREATE INDEX IF NOT EXISTS ix_audit_project_time ON audit_logs (project_id, created_at_utc);",
            """
            CREATE TABLE IF NOT EXISTS qa_runs (
                id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES projects ("Id") ON DELETE CASCADE,
                subject_key varchar(200) NOT NULL, language varchar(40) NOT NULL, workspace_relative_path varchar(500) NOT NULL,
                commit_sha varchar(64) NULL, artifact_root varchar(1000) NOT NULL, status varchar(40) NOT NULL,
                finding_count integer NOT NULL, critical_finding_count integer NOT NULL, warning_finding_count integer NOT NULL,
                summary_json varchar(32000) NULL, created_at_utc timestamptz NOT NULL, started_at_utc timestamptz NULL, finished_at_utc timestamptz NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS qa_findings (
                id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES projects ("Id") ON DELETE CASCADE,
                qa_run_id uuid NOT NULL REFERENCES qa_runs (id) ON DELETE CASCADE, subject_key varchar(200) NOT NULL,
                language varchar(40) NOT NULL, category varchar(120) NOT NULL, severity varchar(40) NOT NULL, tool varchar(120) NOT NULL,
                title varchar(240) NOT NULL, message varchar(8000) NOT NULL, fingerprint varchar(64) NOT NULL,
                file_path varchar(1000) NULL, line integer NULL, column integer NULL, function varchar(500) NULL,
                exception_type varchar(300) NULL, stack_trace varchar(32000) NULL, rule_id varchar(300) NULL,
                reproduction_status varchar(40) NOT NULL, reproduced_attempts integer NOT NULL, total_attempts integer NOT NULL,
                observed_at_utc timestamptz NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS reproduction_attempts (
                id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES projects ("Id") ON DELETE CASCADE,
                qa_finding_id uuid NOT NULL REFERENCES qa_findings (id) ON DELETE CASCADE, attempt_number integer NOT NULL,
                status varchar(40) NOT NULL, reproduced boolean NOT NULL, crashed boolean NOT NULL, timed_out boolean NOT NULL,
                exit_code integer NULL, duration_ms bigint NOT NULL, artifact_directory varchar(1000) NOT NULL,
                video_path varchar(1000) NULL, audio_path varchar(1000) NULL, dump_path varchar(1000) NULL,
                result_json varchar(32000) NULL, started_at_utc timestamptz NOT NULL, finished_at_utc timestamptz NULL,
                CONSTRAINT uq_reproduction_attempt UNIQUE (qa_finding_id, attempt_number)
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS hardware_profiles (
                id uuid PRIMARY KEY, name varchar(160) NOT NULL, platform varchar(80) NOT NULL, source varchar(40) NOT NULL,
                cpu_single_score numeric(12,3) NOT NULL, cpu_multi_score numeric(12,3) NOT NULL, gpu_score numeric(12,3) NOT NULL,
                memory_score numeric(12,3) NOT NULL, storage_score numeric(12,3) NOT NULL, thermal_score numeric(12,3) NOT NULL,
                memory_budget_mb bigint NOT NULL, target_fps integer NOT NULL, metadata_json varchar(32000) NOT NULL,
                created_at_utc timestamptz NOT NULL, CONSTRAINT uq_hardware_profile UNIQUE (name, platform)
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS hardware_calibrations (
                id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES projects ("Id") ON DELETE CASCADE,
                machine_fingerprint varchar(64) NOT NULL, cpu_single_score numeric(12,3) NOT NULL,
                cpu_multi_score numeric(12,3) NOT NULL, gpu_score numeric(12,3) NOT NULL,
                memory_score numeric(12,3) NOT NULL, storage_score numeric(12,3) NOT NULL,
                available_memory_mb bigint NOT NULL, environment_json varchar(32000) NOT NULL, measured_at_utc timestamptz NOT NULL
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS performance_simulation_runs (
                id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES projects ("Id") ON DELETE CASCADE,
                hardware_profile_id uuid NOT NULL REFERENCES hardware_profiles (id) ON DELETE RESTRICT,
                subject_key varchar(200) NOT NULL, artifact_directory varchar(1000) NOT NULL, status varchar(40) NOT NULL,
                confidence_percent numeric(5,2) NOT NULL, result_json varchar(32000) NULL,
                created_at_utc timestamptz NOT NULL, started_at_utc timestamptz NULL, finished_at_utc timestamptz NULL
            );
            """,
            "CREATE INDEX IF NOT EXISTS ix_qa_runs_project_created ON qa_runs (project_id, created_at_utc);",
            "CREATE INDEX IF NOT EXISTS ix_qa_findings_project_fingerprint ON qa_findings (project_id, fingerprint, observed_at_utc);",
            "CREATE INDEX IF NOT EXISTS ix_hardware_calibrations_project_time ON hardware_calibrations (project_id, measured_at_utc);",
            "CREATE INDEX IF NOT EXISTS ix_performance_simulation_project_time ON performance_simulation_runs (project_id, created_at_utc);"
        };

        foreach (var statement in statements)
        {
            await db.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }
    }
}
