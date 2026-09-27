using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dip.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DipDbContext))]
[Migration("20260814000600_AddQaGovernance")]
public sealed class AddQaGovernance : Migration
{
    public const string MigrationId = "20260814000600_AddQaGovernance";
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
    """
    ALTER TABLE qa_findings ADD COLUMN IF NOT EXISTS diagnostic_schema_version varchar(80) NOT NULL DEFAULT 'dip.finding.v2';
    ALTER TABLE qa_findings ADD COLUMN IF NOT EXISTS source_format varchar(80) NULL;
    ALTER TABLE qa_findings ADD COLUMN IF NOT EXISTS source_format_version varchar(80) NULL;

    CREATE TABLE IF NOT EXISTS qa_finding_states(
      id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,
      fingerprint varchar(64) NOT NULL,last_finding_id uuid NOT NULL,subject_key varchar(200) NOT NULL,last_commit_sha varchar(64) NULL,
      status varchar(40) NOT NULL,occurrences integer NOT NULL,team varchar(200) NULL,workstream varchar(200) NULL,ownership_source varchar(40) NOT NULL,
      disposition_reason varchar(2000) NULL,updated_by varchar(200) NULL,first_seen_at_utc timestamptz NOT NULL,last_seen_at_utc timestamptz NOT NULL,updated_at_utc timestamptz NOT NULL);
    CREATE UNIQUE INDEX IF NOT EXISTS ux_qa_finding_states_identity ON qa_finding_states(project_id,fingerprint);
    CREATE INDEX IF NOT EXISTS ix_qa_finding_states_status ON qa_finding_states(project_id,status,last_seen_at_utc);

    CREATE TABLE IF NOT EXISTS qa_waivers(
      id uuid PRIMARY KEY,project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,name varchar(200) NOT NULL,reason varchar(2000) NOT NULL,
      approved_by varchar(200) NOT NULL,fingerprint varchar(64) NULL,rule_id varchar(300) NULL,path_pattern varchar(1000) NULL,status varchar(40) NOT NULL,
      created_at_utc timestamptz NOT NULL,expires_at_utc timestamptz NOT NULL,revoked_at_utc timestamptz NULL);
    CREATE INDEX IF NOT EXISTS ix_qa_waivers_active ON qa_waivers(project_id,status,expires_at_utc);

    CREATE TABLE IF NOT EXISTS qa_test_reliability(
      id uuid PRIMARY KEY,project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,test_key varchar(500) NOT NULL,pass_count integer NOT NULL,
      fail_count integer NOT NULL,consecutive_passes integer NOT NULL,consecutive_failures integer NOT NULL,classification varchar(40) NOT NULL,last_commit_sha varchar(64) NULL,
      first_observed_at_utc timestamptz NOT NULL,last_observed_at_utc timestamptz NOT NULL);
    CREATE UNIQUE INDEX IF NOT EXISTS ux_qa_test_reliability_identity ON qa_test_reliability(project_id,test_key);

    CREATE TABLE IF NOT EXISTS qa_regression_baselines(
      id uuid PRIMARY KEY,project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,subject_key varchar(200) NOT NULL,metric_key varchar(200) NOT NULL,
      baseline_commit varchar(64) NOT NULL,last_known_good_commit varchar(64) NOT NULL,first_bad_commit varchar(64) NULL,baseline_value numeric(18,4) NOT NULL,
      current_value numeric(18,4) NOT NULL,regression_percent numeric(12,4) NOT NULL,status varchar(40) NOT NULL,updated_at_utc timestamptz NOT NULL);
    CREATE UNIQUE INDEX IF NOT EXISTS ux_qa_regression_baselines_identity ON qa_regression_baselines(project_id,subject_key,metric_key);

    CREATE TABLE IF NOT EXISTS qa_ownership_rules(
      id uuid PRIMARY KEY,project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,name varchar(200) NOT NULL,path_pattern varchar(1000) NOT NULL,
      team varchar(200) NOT NULL,workstream varchar(200) NULL,priority integer NOT NULL,enabled boolean NOT NULL,created_at_utc timestamptz NOT NULL,updated_at_utc timestamptz NOT NULL);
    CREATE INDEX IF NOT EXISTS ix_qa_ownership_rules_active ON qa_ownership_rules(project_id,enabled,priority DESC);

    CREATE TABLE IF NOT EXISTS qa_policies(
      id uuid PRIMARY KEY,project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,name varchar(200) NOT NULL,version varchar(80) NOT NULL,
      rules_json varchar(32000) NOT NULL,enabled boolean NOT NULL,created_at_utc timestamptz NOT NULL);
    CREATE INDEX IF NOT EXISTS ix_qa_policies_active ON qa_policies(project_id,enabled,created_at_utc DESC);

    CREATE TABLE IF NOT EXISTS qa_policy_evaluations(
      id uuid PRIMARY KEY,project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,qa_run_id uuid NOT NULL REFERENCES qa_runs(id) ON DELETE CASCADE,
      policy_id uuid NULL,policy_version varchar(80) NOT NULL,verdict varchar(40) NOT NULL,blockers_json varchar(16000) NOT NULL,warnings_json varchar(16000) NOT NULL,
      summary varchar(4000) NOT NULL,evaluated_at_utc timestamptz NOT NULL);
    CREATE INDEX IF NOT EXISTS ix_qa_policy_evaluations_recent ON qa_policy_evaluations(project_id,evaluated_at_utc DESC);

    CREATE TABLE IF NOT EXISTS qa_evidence_provenance(
      id uuid PRIMARY KEY,project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,qa_run_id uuid NOT NULL REFERENCES qa_runs(id) ON DELETE CASCADE,
      commit_sha varchar(64) NULL,environment_fingerprint varchar(128) NOT NULL,toolchain_json varchar(16000) NOT NULL,normalizer_version varchar(80) NOT NULL,
      policy_version varchar(80) NOT NULL,input_hash varchar(64) NOT NULL,evidence_hash varchar(64) NOT NULL,captured_at_utc timestamptz NOT NULL);
    CREATE INDEX IF NOT EXISTS ix_qa_evidence_provenance_recent ON qa_evidence_provenance(project_id,captured_at_utc DESC);

    CREATE TABLE IF NOT EXISTS qa_system_health_snapshots(
      id uuid PRIMARY KEY,project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,qa_run_id uuid NULL,component varchar(120) NOT NULL,
      status varchar(40) NOT NULL,detail varchar(4000) NOT NULL,observed_at_utc timestamptz NOT NULL);
    CREATE INDEX IF NOT EXISTS ix_qa_system_health_recent ON qa_system_health_snapshots(project_id,observed_at_utc DESC);

    CREATE TABLE IF NOT EXISTS qa_quality_trend_snapshots(
      id uuid PRIMARY KEY,project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,qa_run_id uuid NOT NULL,open_finding_count integer NOT NULL,
      critical_count integer NOT NULL,warning_count integer NOT NULL,flaky_count integer NOT NULL,active_waiver_count integer NOT NULL,technical_debt_score numeric(12,2) NOT NULL,
      captured_at_utc timestamptz NOT NULL);
    CREATE INDEX IF NOT EXISTS ix_qa_quality_trends_recent ON qa_quality_trend_snapshots(project_id,captured_at_utc DESC);

    CREATE TABLE IF NOT EXISTS qa_test_impact_rules(
      id uuid PRIMARY KEY,project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,path_pattern varchar(1000) NOT NULL,test_key varchar(500) NOT NULL,
      command_json varchar(8000) NOT NULL,priority integer NOT NULL,enabled boolean NOT NULL,created_at_utc timestamptz NOT NULL);
    CREATE INDEX IF NOT EXISTS ix_qa_test_impact_rules_active ON qa_test_impact_rules(project_id,enabled,priority DESC);

    CREATE TABLE IF NOT EXISTS qa_artifact_retention_rules(
      id uuid PRIMARY KEY,project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,kind_pattern varchar(200) NOT NULL,retention_class varchar(40) NOT NULL,
      retention_days integer NOT NULL,preserve_on_critical boolean NOT NULL,enabled boolean NOT NULL,created_at_utc timestamptz NOT NULL);
    CREATE INDEX IF NOT EXISTS ix_qa_artifact_retention_rules_active ON qa_artifact_retention_rules(project_id,enabled);

    CREATE TABLE IF NOT EXISTS qa_run_costs(
      id uuid PRIMARY KEY,project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,qa_run_id uuid NOT NULL,wall_duration_ms bigint NOT NULL,
      tool_duration_ms bigint NOT NULL,artifact_bytes bigint NOT NULL,estimated_cpu_seconds numeric(18,3) NOT NULL,budget_verdict varchar(40) NOT NULL,captured_at_utc timestamptz NOT NULL);
    CREATE INDEX IF NOT EXISTS ix_qa_run_costs_recent ON qa_run_costs(project_id,captured_at_utc DESC);

    CREATE TABLE IF NOT EXISTS qa_bug_minimizations(
      id uuid PRIMARY KEY,project_id uuid NOT NULL REFERENCES "Projects"("Id") ON DELETE CASCADE,finding_id uuid NOT NULL,original_input varchar(16000) NOT NULL,
      minimized_input varchar(16000) NOT NULL,original_units integer NOT NULL,minimized_units integer NOT NULL,status varchar(40) NOT NULL,detail varchar(4000) NOT NULL,created_at_utc timestamptz NOT NULL);
    CREATE INDEX IF NOT EXISTS ix_qa_bug_minimizations_recent ON qa_bug_minimizations(project_id,created_at_utc DESC);
    """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
    """
    DROP TABLE IF EXISTS qa_bug_minimizations CASCADE;DROP TABLE IF EXISTS qa_run_costs CASCADE;DROP TABLE IF EXISTS qa_artifact_retention_rules CASCADE;
    DROP TABLE IF EXISTS qa_test_impact_rules CASCADE;DROP TABLE IF EXISTS qa_quality_trend_snapshots CASCADE;DROP TABLE IF EXISTS qa_system_health_snapshots CASCADE;
    DROP TABLE IF EXISTS qa_evidence_provenance CASCADE;DROP TABLE IF EXISTS qa_policy_evaluations CASCADE;DROP TABLE IF EXISTS qa_policies CASCADE;
    DROP TABLE IF EXISTS qa_ownership_rules CASCADE;DROP TABLE IF EXISTS qa_regression_baselines CASCADE;DROP TABLE IF EXISTS qa_test_reliability CASCADE;
    DROP TABLE IF EXISTS qa_waivers CASCADE;DROP TABLE IF EXISTS qa_finding_states CASCADE;
    ALTER TABLE qa_findings DROP COLUMN IF EXISTS source_format_version;
    ALTER TABLE qa_findings DROP COLUMN IF EXISTS source_format;
    ALTER TABLE qa_findings DROP COLUMN IF EXISTS diagnostic_schema_version;
    """);
}
