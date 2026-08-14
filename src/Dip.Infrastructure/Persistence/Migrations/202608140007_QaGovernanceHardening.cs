using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dip.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DipDbContext))]
[Migration("202608140007_QaGovernanceHardening")]
public sealed class QaGovernanceHardening:Migration
{
    public const string MigrationId="202608140007_QaGovernanceHardening";
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql(
    """
    -- Project-scoped authorization.
    CREATE TABLE IF NOT EXISTS project_memberships(
      id uuid PRIMARY KEY,
      project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,
      subject_id varchar(200) NOT NULL,
      login varchar(200) NOT NULL,
      role varchar(40) NOT NULL,
      granted_by varchar(200) NOT NULL,
      created_at_utc timestamptz NOT NULL,
      updated_at_utc timestamptz NOT NULL);
    CREATE UNIQUE INDEX IF NOT EXISTS ux_project_memberships_subject ON project_memberships(project_id,subject_id);
    CREATE INDEX IF NOT EXISTS ix_project_memberships_subject ON project_memberships(subject_id);

    -- Authenticated audit provenance.  Actor itself remains server supplied.
    ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS request_id varchar(200) NULL;
    ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS correlation_id varchar(200) NULL;
    ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS reason varchar(2000) NULL;

    -- Optimistic concurrency and formal waiver workflow. Existing waivers remain valid/approved.
    ALTER TABLE qa_finding_states ADD COLUMN IF NOT EXISTS version bigint NOT NULL DEFAULT 1;
    ALTER TABLE qa_waivers ADD COLUMN IF NOT EXISTS requested_by varchar(200) NULL;
    ALTER TABLE qa_waivers ADD COLUMN IF NOT EXISTS decision_reason varchar(2000) NULL;
    ALTER TABLE qa_waivers ADD COLUMN IF NOT EXISTS updated_at_utc timestamptz NULL;
    ALTER TABLE qa_waivers ADD COLUMN IF NOT EXISTS approved_at_utc timestamptz NULL;
    ALTER TABLE qa_waivers ADD COLUMN IF NOT EXISTS version bigint NOT NULL DEFAULT 1;
    UPDATE qa_waivers SET requested_by=COALESCE(requested_by,approved_by),updated_at_utc=COALESCE(updated_at_utc,created_at_utc),approved_at_utc=COALESCE(approved_at_utc,created_at_utc) WHERE requested_by IS NULL OR updated_at_utc IS NULL;
    ALTER TABLE qa_waivers ALTER COLUMN requested_by SET NOT NULL;
    ALTER TABLE qa_waivers ALTER COLUMN updated_at_utc SET NOT NULL;
    ALTER TABLE qa_waivers ALTER COLUMN approved_by DROP NOT NULL;

    -- Policy lifecycle/versioning. Existing enabled policies are treated as Active, disabled as Draft.
    ALTER TABLE qa_policies ADD COLUMN IF NOT EXISTS lifecycle_status varchar(40) NULL;
    ALTER TABLE qa_policies ADD COLUMN IF NOT EXISTS revision integer NOT NULL DEFAULT 1;
    ALTER TABLE qa_policies ADD COLUMN IF NOT EXISTS supersedes_policy_id uuid NULL;
    ALTER TABLE qa_policies ADD COLUMN IF NOT EXISTS approved_by varchar(200) NULL;
    ALTER TABLE qa_policies ADD COLUMN IF NOT EXISTS activated_by varchar(200) NULL;
    ALTER TABLE qa_policies ADD COLUMN IF NOT EXISTS updated_at_utc timestamptz NULL;
    ALTER TABLE qa_policies ADD COLUMN IF NOT EXISTS approved_at_utc timestamptz NULL;
    ALTER TABLE qa_policies ADD COLUMN IF NOT EXISTS activated_at_utc timestamptz NULL;
    ALTER TABLE qa_policies ADD COLUMN IF NOT EXISTS concurrency_version bigint NOT NULL DEFAULT 1;
    UPDATE qa_policies SET lifecycle_status=CASE WHEN enabled THEN 'Active' ELSE 'Draft' END,updated_at_utc=COALESCE(updated_at_utc,created_at_utc),approved_at_utc=CASE WHEN enabled THEN COALESCE(approved_at_utc,created_at_utc) ELSE approved_at_utc END,activated_at_utc=CASE WHEN enabled THEN COALESCE(activated_at_utc,created_at_utc) ELSE activated_at_utc END WHERE lifecycle_status IS NULL OR updated_at_utc IS NULL;
    -- Older builds did not enforce a unique version or a single active policy. Preserve data while making it deterministic.
    WITH ranked AS (SELECT id,row_number() OVER(PARTITION BY project_id,version ORDER BY created_at_utc DESC,id) rn FROM qa_policies)
      UPDATE qa_policies p SET version=left(p.version,55)||'-legacy-'||left(p.id::text,8) FROM ranked r WHERE p.id=r.id AND r.rn>1;
    WITH ranked AS (SELECT id,row_number() OVER(PARTITION BY project_id ORDER BY created_at_utc DESC,id) rn FROM qa_policies WHERE lifecycle_status='Active')
      UPDATE qa_policies p SET lifecycle_status='Superseded',enabled=false,updated_at_utc=now() FROM ranked r WHERE p.id=r.id AND r.rn>1;
    ALTER TABLE qa_policies ALTER COLUMN lifecycle_status SET NOT NULL;
    ALTER TABLE qa_policies ALTER COLUMN updated_at_utc SET NOT NULL;
    CREATE UNIQUE INDEX IF NOT EXISTS ux_qa_policies_project_version ON qa_policies(project_id,version);
    CREATE INDEX IF NOT EXISTS ix_qa_policies_lifecycle ON qa_policies(project_id,lifecycle_status,revision DESC);
    ALTER TABLE qa_policies ADD COLUMN IF NOT EXISTS active_slot smallint GENERATED ALWAYS AS (CASE WHEN lifecycle_status='Active' THEN 1 ELSE NULL END) STORED;
    DO $$ BEGIN IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='uq_qa_policies_single_active') THEN ALTER TABLE qa_policies ADD CONSTRAINT uq_qa_policies_single_active UNIQUE(project_id,active_slot) DEFERRABLE INITIALLY DEFERRED; END IF; END $$;

    -- Exactly-once governance ledger. qa_run_id uniqueness is the final duplicate-processing guard.
    CREATE TABLE IF NOT EXISTS qa_governance_processed_runs(
      id uuid PRIMARY KEY,
      project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,
      qa_run_id uuid NOT NULL,
      governance_version varchar(80) NOT NULL,
      input_hash varchar(64) NOT NULL,
      result_hash varchar(64) NULL,
      status varchar(40) NOT NULL,
      failure_detail varchar(4000) NULL,
      started_at_utc timestamptz NOT NULL,
      updated_at_utc timestamptz NOT NULL,
      completed_at_utc timestamptz NULL);
    CREATE UNIQUE INDEX IF NOT EXISTS ux_qa_governance_processed_runs_run ON qa_governance_processed_runs(qa_run_id);
    CREATE INDEX IF NOT EXISTS ix_qa_governance_processed_runs_status ON qa_governance_processed_runs(project_id,status);

    -- Two-person, commit-bound, expiring release-gate override workflow.
    CREATE TABLE IF NOT EXISTS qa_release_gate_overrides(
      id uuid PRIMARY KEY,
      project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,
      policy_evaluation_id uuid NULL,
      qa_run_id uuid NULL,
      commit_sha varchar(64) NOT NULL,
      reason varchar(2000) NOT NULL,
      requested_by varchar(200) NOT NULL,
      status varchar(40) NOT NULL,
      approved_by varchar(200) NULL,
      decision_reason varchar(2000) NULL,
      created_at_utc timestamptz NOT NULL,
      updated_at_utc timestamptz NOT NULL,
      expires_at_utc timestamptz NOT NULL,
      decided_at_utc timestamptz NULL,
      version bigint NOT NULL DEFAULT 1);
    CREATE INDEX IF NOT EXISTS ix_qa_release_gate_overrides_active ON qa_release_gate_overrides(project_id,status,expires_at_utc);

    CREATE TABLE IF NOT EXISTS qa_backup_verifications(
      id uuid PRIMARY KEY,
      project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,
      backup_kind varchar(80) NOT NULL,
      backup_reference varchar(1000) NOT NULL,
      source_commit varchar(64) NOT NULL,
      environment_fingerprint varchar(128) NOT NULL,
      status varchar(40) NOT NULL,
      detail varchar(4000) NOT NULL,
      verified_at_utc timestamptz NOT NULL);
    CREATE INDEX IF NOT EXISTS ix_qa_backup_verifications_recent ON qa_backup_verifications(project_id,verified_at_utc DESC);
    """);

    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql(
    """
    DROP TABLE IF EXISTS qa_backup_verifications CASCADE;
    DROP TABLE IF EXISTS qa_release_gate_overrides CASCADE;
    DROP TABLE IF EXISTS qa_governance_processed_runs CASCADE;
    DROP TABLE IF EXISTS project_memberships CASCADE;
    ALTER TABLE qa_policies DROP CONSTRAINT IF EXISTS uq_qa_policies_single_active;
    ALTER TABLE qa_policies DROP COLUMN IF EXISTS active_slot;
    DROP INDEX IF EXISTS ux_qa_policies_project_version;
    DROP INDEX IF EXISTS ix_qa_policies_lifecycle;
    ALTER TABLE qa_policies DROP COLUMN IF EXISTS concurrency_version;
    ALTER TABLE qa_policies DROP COLUMN IF EXISTS activated_at_utc;
    ALTER TABLE qa_policies DROP COLUMN IF EXISTS approved_at_utc;
    ALTER TABLE qa_policies DROP COLUMN IF EXISTS updated_at_utc;
    ALTER TABLE qa_policies DROP COLUMN IF EXISTS activated_by;
    ALTER TABLE qa_policies DROP COLUMN IF EXISTS approved_by;
    ALTER TABLE qa_policies DROP COLUMN IF EXISTS supersedes_policy_id;
    ALTER TABLE qa_policies DROP COLUMN IF EXISTS revision;
    ALTER TABLE qa_policies DROP COLUMN IF EXISTS lifecycle_status;
    UPDATE qa_waivers SET approved_by=COALESCE(approved_by,requested_by,'migration-rollback') WHERE approved_by IS NULL;
    ALTER TABLE qa_waivers ALTER COLUMN approved_by SET NOT NULL;
    ALTER TABLE qa_waivers DROP COLUMN IF EXISTS version;
    ALTER TABLE qa_waivers DROP COLUMN IF EXISTS approved_at_utc;
    ALTER TABLE qa_waivers DROP COLUMN IF EXISTS updated_at_utc;
    ALTER TABLE qa_waivers DROP COLUMN IF EXISTS decision_reason;
    ALTER TABLE qa_waivers DROP COLUMN IF EXISTS requested_by;
    ALTER TABLE qa_finding_states DROP COLUMN IF EXISTS version;
    ALTER TABLE audit_logs DROP COLUMN IF EXISTS reason;
    ALTER TABLE audit_logs DROP COLUMN IF EXISTS correlation_id;
    ALTER TABLE audit_logs DROP COLUMN IF EXISTS request_id;
    """);
}
