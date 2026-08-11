using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dip.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DipDbContext))]
[Migration("202608120003_AddDevelopmentIntelligenceExpansion")]
public sealed class AddDevelopmentIntelligenceExpansion:Migration
{
    public const string MigrationId="202608120003_AddDevelopmentIntelligenceExpansion";
    protected override void Up(MigrationBuilder migrationBuilder){foreach(var sql in Statements)migrationBuilder.Sql(sql);}
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE artifact_records DROP COLUMN IF EXISTS game_replay_run_id;");
        foreach(var table in new[]{"cross_repository_impacts","repository_dependencies","repository_nodes","release_evidence_bundles","external_work_item_links","release_readiness_snapshots","regression_triages","change_risk_assessments","hardware_backtests","performance_budget_evaluations","performance_budget_profiles","game_replay_metrics","game_replay_runs","game_replay_sessions"})migrationBuilder.Sql($"DROP TABLE IF EXISTS {table} CASCADE;");
    }
    private static readonly string[] Statements=
    [
        """
        CREATE TABLE IF NOT EXISTS game_replay_sessions (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            key varchar(120) NOT NULL, name varchar(200) NOT NULL, scene varchar(300) NOT NULL,
            input_artifact_id varchar(1000) NOT NULL, random_seed integer NOT NULL, duration_seconds integer NOT NULL,
            launch_command varchar(1000) NOT NULL, arguments varchar(4000) NOT NULL, working_directory varchar(1000) NULL,
            baseline_commit varchar(64) NULL, enabled boolean NOT NULL, created_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_game_replay_session_key ON game_replay_sessions(project_id,key);
        """,
        """
        CREATE TABLE IF NOT EXISTS game_replay_runs (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            session_id uuid NOT NULL REFERENCES game_replay_sessions(id) ON DELETE CASCADE,
            job_id uuid NOT NULL REFERENCES durable_jobs(id) ON DELETE RESTRICT,
            commit_sha varchar(64) NOT NULL, build_id varchar(200) NOT NULL, hardware_fingerprint varchar(256) NOT NULL,
            status varchar(40) NOT NULL, summary_json varchar(32000) NULL, created_at_utc timestamptz NOT NULL,
            started_at_utc timestamptz NULL, finished_at_utc timestamptz NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_game_replay_runs_job ON game_replay_runs(job_id);
        CREATE INDEX IF NOT EXISTS ix_game_replay_baseline ON game_replay_runs(project_id,session_id,hardware_fingerprint,finished_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS game_replay_metrics (
            id uuid PRIMARY KEY, replay_run_id uuid NOT NULL REFERENCES game_replay_runs(id) ON DELETE CASCADE,
            name varchar(160) NOT NULL, value numeric(20,6) NOT NULL, unit varchar(40) NOT NULL, aggregation varchar(40) NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_game_replay_metrics ON game_replay_metrics(replay_run_id,name,aggregation);
        ALTER TABLE artifact_records ADD COLUMN IF NOT EXISTS game_replay_run_id uuid NULL;
        DO $$ BEGIN
          IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_artifact_records_game_replay_run') THEN
            ALTER TABLE artifact_records ADD CONSTRAINT fk_artifact_records_game_replay_run FOREIGN KEY(game_replay_run_id) REFERENCES game_replay_runs(id) ON DELETE SET NULL;
          END IF;
        END $$;
        CREATE INDEX IF NOT EXISTS ix_artifact_records_replay ON artifact_records(game_replay_run_id);
        """,
        """
        CREATE TABLE IF NOT EXISTS performance_budget_profiles (
            id uuid PRIMARY KEY, project_id uuid NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            key varchar(120) NOT NULL, name varchar(200) NOT NULL, platform varchar(80) NOT NULL,
            target_profile varchar(120) NOT NULL, rules_json varchar(32000) NOT NULL, enabled boolean NOT NULL,
            created_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_performance_budget_scope_key ON performance_budget_profiles(project_id,key);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_performance_budget_global_key ON performance_budget_profiles(key) WHERE project_id IS NULL;

        INSERT INTO performance_budget_profiles(id,project_id,key,name,platform,target_profile,rules_json,enabled,created_at_utc)
        SELECT '11111111-1111-4111-8111-111111111101',NULL,'pc-low','PC Low','PC','Low',
        '[{"Metric":"fps","Aggregation":"p50","Minimum":30,"Maximum":null,"Unit":"fps","WarningPercent":5},{"Metric":"frame_time_ms","Aggregation":"p95","Minimum":null,"Maximum":33.333,"Unit":"ms","WarningPercent":5},{"Metric":"cpu_ms","Aggregation":"p95","Minimum":null,"Maximum":25,"Unit":"ms","WarningPercent":5},{"Metric":"gpu_ms","Aggregation":"p95","Minimum":null,"Maximum":30,"Unit":"ms","WarningPercent":5},{"Metric":"memory_mb","Aggregation":"p95","Minimum":null,"Maximum":8192,"Unit":"MB","WarningPercent":5},{"Metric":"vram_mb","Aggregation":"p95","Minimum":null,"Maximum":4096,"Unit":"MB","WarningPercent":5}]',TRUE,NOW()
        WHERE NOT EXISTS (SELECT 1 FROM performance_budget_profiles WHERE project_id IS NULL AND key='pc-low');

        INSERT INTO performance_budget_profiles(id,project_id,key,name,platform,target_profile,rules_json,enabled,created_at_utc)
        SELECT '11111111-1111-4111-8111-111111111102',NULL,'pc-recommended','PC Recommended','PC','Recommended',
        '[{"Metric":"fps","Aggregation":"p50","Minimum":60,"Maximum":null,"Unit":"fps","WarningPercent":5},{"Metric":"frame_time_ms","Aggregation":"p95","Minimum":null,"Maximum":16.667,"Unit":"ms","WarningPercent":5},{"Metric":"cpu_ms","Aggregation":"p95","Minimum":null,"Maximum":12,"Unit":"ms","WarningPercent":5},{"Metric":"gpu_ms","Aggregation":"p95","Minimum":null,"Maximum":14,"Unit":"ms","WarningPercent":5},{"Metric":"memory_mb","Aggregation":"p95","Minimum":null,"Maximum":8192,"Unit":"MB","WarningPercent":5},{"Metric":"vram_mb","Aggregation":"p95","Minimum":null,"Maximum":6144,"Unit":"MB","WarningPercent":5}]',TRUE,NOW()
        WHERE NOT EXISTS (SELECT 1 FROM performance_budget_profiles WHERE project_id IS NULL AND key='pc-recommended');

        INSERT INTO performance_budget_profiles(id,project_id,key,name,platform,target_profile,rules_json,enabled,created_at_utc)
        SELECT '11111111-1111-4111-8111-111111111103',NULL,'console-60','Console 60 FPS','Console','60FPS',
        '[{"Metric":"fps","Aggregation":"p50","Minimum":60,"Maximum":null,"Unit":"fps","WarningPercent":5},{"Metric":"frame_time_ms","Aggregation":"p95","Minimum":null,"Maximum":16.667,"Unit":"ms","WarningPercent":5},{"Metric":"cpu_ms","Aggregation":"p95","Minimum":null,"Maximum":13,"Unit":"ms","WarningPercent":5},{"Metric":"gpu_ms","Aggregation":"p95","Minimum":null,"Maximum":15,"Unit":"ms","WarningPercent":5},{"Metric":"memory_mb","Aggregation":"p95","Minimum":null,"Maximum":12288,"Unit":"MB","WarningPercent":5}]',TRUE,NOW()
        WHERE NOT EXISTS (SELECT 1 FROM performance_budget_profiles WHERE project_id IS NULL AND key='console-60');

        INSERT INTO performance_budget_profiles(id,project_id,key,name,platform,target_profile,rules_json,enabled,created_at_utc)
        SELECT '11111111-1111-4111-8111-111111111104',NULL,'console-30','Console 30 FPS','Console','30FPS',
        '[{"Metric":"fps","Aggregation":"p50","Minimum":30,"Maximum":null,"Unit":"fps","WarningPercent":5},{"Metric":"frame_time_ms","Aggregation":"p95","Minimum":null,"Maximum":33.333,"Unit":"ms","WarningPercent":5},{"Metric":"cpu_ms","Aggregation":"p95","Minimum":null,"Maximum":25,"Unit":"ms","WarningPercent":5},{"Metric":"gpu_ms","Aggregation":"p95","Minimum":null,"Maximum":30,"Unit":"ms","WarningPercent":5},{"Metric":"memory_mb","Aggregation":"p95","Minimum":null,"Maximum":12288,"Unit":"MB","WarningPercent":5}]',TRUE,NOW()
        WHERE NOT EXISTS (SELECT 1 FROM performance_budget_profiles WHERE project_id IS NULL AND key='console-30');

        INSERT INTO performance_budget_profiles(id,project_id,key,name,platform,target_profile,rules_json,enabled,created_at_utc)
        SELECT '11111111-1111-4111-8111-111111111105',NULL,'mobile-low','Mobile Low','Mobile','Low',
        '[{"Metric":"fps","Aggregation":"p50","Minimum":30,"Maximum":null,"Unit":"fps","WarningPercent":5},{"Metric":"frame_time_ms","Aggregation":"p95","Minimum":null,"Maximum":33.333,"Unit":"ms","WarningPercent":5},{"Metric":"cpu_ms","Aggregation":"p95","Minimum":null,"Maximum":24,"Unit":"ms","WarningPercent":5},{"Metric":"gpu_ms","Aggregation":"p95","Minimum":null,"Maximum":28,"Unit":"ms","WarningPercent":5},{"Metric":"memory_mb","Aggregation":"p95","Minimum":null,"Maximum":3072,"Unit":"MB","WarningPercent":5}]',TRUE,NOW()
        WHERE NOT EXISTS (SELECT 1 FROM performance_budget_profiles WHERE project_id IS NULL AND key='mobile-low');

        INSERT INTO performance_budget_profiles(id,project_id,key,name,platform,target_profile,rules_json,enabled,created_at_utc)
        SELECT '11111111-1111-4111-8111-111111111106',NULL,'mobile-high','Mobile High','Mobile','High',
        '[{"Metric":"fps","Aggregation":"p50","Minimum":60,"Maximum":null,"Unit":"fps","WarningPercent":5},{"Metric":"frame_time_ms","Aggregation":"p95","Minimum":null,"Maximum":16.667,"Unit":"ms","WarningPercent":5},{"Metric":"cpu_ms","Aggregation":"p95","Minimum":null,"Maximum":13,"Unit":"ms","WarningPercent":5},{"Metric":"gpu_ms","Aggregation":"p95","Minimum":null,"Maximum":15,"Unit":"ms","WarningPercent":5},{"Metric":"memory_mb","Aggregation":"p95","Minimum":null,"Maximum":6144,"Unit":"MB","WarningPercent":5}]',TRUE,NOW()
        WHERE NOT EXISTS (SELECT 1 FROM performance_budget_profiles WHERE project_id IS NULL AND key='mobile-high');

        INSERT INTO performance_budget_profiles(id,project_id,key,name,platform,target_profile,rules_json,enabled,created_at_utc)
        SELECT '11111111-1111-4111-8111-111111111107',NULL,'vr-90','VR 90 Hz','VR','90Hz',
        '[{"Metric":"fps","Aggregation":"p50","Minimum":90,"Maximum":null,"Unit":"fps","WarningPercent":5},{"Metric":"frame_time_ms","Aggregation":"p95","Minimum":null,"Maximum":11.111,"Unit":"ms","WarningPercent":5},{"Metric":"cpu_ms","Aggregation":"p95","Minimum":null,"Maximum":8.5,"Unit":"ms","WarningPercent":5},{"Metric":"gpu_ms","Aggregation":"p95","Minimum":null,"Maximum":9.5,"Unit":"ms","WarningPercent":5},{"Metric":"memory_mb","Aggregation":"p95","Minimum":null,"Maximum":8192,"Unit":"MB","WarningPercent":5},{"Metric":"vram_mb","Aggregation":"p95","Minimum":null,"Maximum":8192,"Unit":"MB","WarningPercent":5}]',TRUE,NOW()
        WHERE NOT EXISTS (SELECT 1 FROM performance_budget_profiles WHERE project_id IS NULL AND key='vr-90');

        CREATE TABLE IF NOT EXISTS performance_budget_evaluations (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            profile_id uuid NOT NULL REFERENCES performance_budget_profiles(id) ON DELETE RESTRICT,
            replay_run_id uuid NULL REFERENCES game_replay_runs(id) ON DELETE SET NULL,
            benchmark_run_id uuid NULL REFERENCES benchmark_runs(id) ON DELETE SET NULL,
            verdict varchar(40) NOT NULL, summary varchar(2000) NOT NULL, detail_json varchar(32000) NOT NULL,
            evaluated_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_performance_budget_evaluations_project ON performance_budget_evaluations(project_id,evaluated_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS hardware_backtests (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            hardware_profile_id uuid NOT NULL REFERENCES hardware_profiles(id) ON DELETE RESTRICT,
            scenario_key varchar(200) NOT NULL, commit_sha varchar(64) NOT NULL,
            predicted_fps numeric(12,3) NOT NULL, actual_fps numeric(12,3) NOT NULL,
            prediction_error_percent numeric(12,3) NOT NULL, environment_fingerprint varchar(256) NOT NULL,
            measured_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_hardware_backtests_profile ON hardware_backtests(project_id,hardware_profile_id,measured_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS change_risk_assessments (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            commit_sha varchar(64) NOT NULL, risk_score numeric(5,2) NOT NULL, risk_level varchar(40) NOT NULL,
            reasons_json varchar(16000) NOT NULL, affected_repositories_json varchar(16000) NOT NULL,
            assessed_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_change_risk_project_commit ON change_risk_assessments(project_id,commit_sha,assessed_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS regression_triages (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            incident_id uuid NOT NULL REFERENCES incidents(id) ON DELETE CASCADE, suspected_commit varchar(64) NULL,
            suspected_subsystem varchar(200) NULL, suspected_files_json varchar(16000) NOT NULL,
            suggested_owner varchar(200) NULL, confidence numeric(5,2) NOT NULL, evidence_ids_json varchar(16000) NOT NULL,
            explanation varchar(4000) NOT NULL, created_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_regression_triages_incident ON regression_triages(incident_id,created_at_utc);
        CREATE INDEX IF NOT EXISTS ix_regression_triages_project ON regression_triages(project_id,created_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS release_readiness_snapshots (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            commit_sha varchar(64) NOT NULL, decision varchar(40) NOT NULL, confidence numeric(5,2) NOT NULL,
            blockers_json varchar(16000) NOT NULL, warnings_json varchar(16000) NOT NULL,
            required_actions_json varchar(16000) NOT NULL, summary varchar(4000) NOT NULL, evaluated_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_release_readiness_project ON release_readiness_snapshots(project_id,evaluated_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS external_work_item_links (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            incident_id uuid NOT NULL REFERENCES incidents(id) ON DELETE CASCADE, provider varchar(40) NOT NULL,
            external_id varchar(200) NOT NULL, url varchar(2000) NOT NULL, title varchar(300) NOT NULL,
            status varchar(40) NOT NULL, created_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_external_work_item_incident_provider ON external_work_item_links(incident_id,provider);
        """,
        """
        CREATE TABLE IF NOT EXISTS release_evidence_bundles (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            commit_sha varchar(64) NOT NULL, build_id varchar(200) NOT NULL, status varchar(40) NOT NULL,
            manifest_artifact_id varchar(64) NOT NULL, sbom_artifact_id varchar(64) NULL,
            bundle_artifact_id varchar(64) NULL, artifact_sha256 varchar(64) NOT NULL, created_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_release_evidence_project ON release_evidence_bundles(project_id,created_at_utc);
        """,
        """
        CREATE TABLE IF NOT EXISTS repository_nodes (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            key varchar(160) NOT NULL, repository_url varchar(1000) NOT NULL, default_branch varchar(200) NOT NULL,
            role varchar(120) NOT NULL, created_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_repository_nodes_project_key ON repository_nodes(project_id,key);
        CREATE TABLE IF NOT EXISTS repository_dependencies (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            source_repository_id uuid NOT NULL REFERENCES repository_nodes(id) ON DELETE CASCADE,
            target_repository_id uuid NOT NULL REFERENCES repository_nodes(id) ON DELETE CASCADE,
            kind varchar(40) NOT NULL, contract_key varchar(200) NOT NULL, critical boolean NOT NULL,
            created_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_repository_dependencies ON repository_dependencies(project_id,source_repository_id,target_repository_id,kind,contract_key);
        CREATE TABLE IF NOT EXISTS cross_repository_impacts (
            id uuid PRIMARY KEY, project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
            commit_sha varchar(64) NOT NULL, source_repository_id uuid NOT NULL REFERENCES repository_nodes(id) ON DELETE CASCADE,
            level varchar(40) NOT NULL, affected_repository_ids_json varchar(16000) NOT NULL,
            reasons_json varchar(16000) NOT NULL, analyzed_at_utc timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_cross_repository_impacts ON cross_repository_impacts(project_id,commit_sha,analyzed_at_utc);
        """
    ];
}
