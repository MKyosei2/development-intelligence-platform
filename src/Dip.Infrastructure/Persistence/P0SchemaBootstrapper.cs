using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence;

/// <summary>
/// The repository historically used EnsureCreated and has no EF migration
/// baseline. EnsureCreated cannot add tables to an already-created database.
/// This idempotent bootstrap therefore adds only the new Project Truth P0 tables
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
            "CREATE INDEX IF NOT EXISTS ix_audit_project_time ON audit_logs (project_id, created_at_utc);"
        };

        foreach (var statement in statements)
        {
            await db.Database.ExecuteSqlRawAsync(statement, cancellationToken);
        }
    }
}
