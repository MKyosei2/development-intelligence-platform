using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dip.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDifficulty1Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cross_repository_impacts_repository_nodes_source_repository_id",
                table: "cross_repository_impacts");

            migrationBuilder.DropForeignKey(
                name: "FK_development_graph_edges_development_graph_nodes_source_node_id",
                table: "development_graph_edges");

            migrationBuilder.DropForeignKey(
                name: "FK_development_graph_edges_development_graph_nodes_target_node_id",
                table: "development_graph_edges");

            migrationBuilder.DropForeignKey(
                name: "FK_performance_budget_evaluations_benchmark_runs_benchmark_run_id",
                table: "performance_budget_evaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_performance_budget_evaluations_game_replay_runs_replay_run_id",
                table: "performance_budget_evaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_performance_budget_evaluations_performance_budget_profiles_profile_id",
                table: "performance_budget_evaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_performance_simulation_runs_hardware_profiles_hardware_profile_id",
                table: "performance_simulation_runs");

            migrationBuilder.DropForeignKey(
                name: "FK_repository_dependencies_repository_nodes_source_repository_id",
                table: "repository_dependencies");

            migrationBuilder.DropForeignKey(
                name: "FK_repository_dependencies_repository_nodes_target_repository_id",
                table: "repository_dependencies");

            migrationBuilder.RenameIndex(
                name: "IX_repository_dependencies_project_id_source_repository_id_target_repository_id_kind_contract_key",
                table: "repository_dependencies",
                newName: "IX_repository_dependencies_project_id_source_repository_id_tar~");

            migrationBuilder.RenameIndex(
                name: "IX_PipelineRuns_ProjectId_ExternalRunId_CollectorType_AttemptNumber",
                table: "PipelineRuns",
                newName: "IX_PipelineRuns_ProjectId_ExternalRunId_CollectorType_AttemptN~");

            migrationBuilder.RenameIndex(
                name: "IX_p2_intelligence_records_project_id_subject_key_created_at_utc",
                table: "p2_intelligence_records",
                newName: "IX_p2_intelligence_records_project_id_subject_key_created_at_u~");

            migrationBuilder.RenameIndex(
                name: "IX_OutboxMessages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntilUtc",
                table: "OutboxMessages",
                newName: "IX_OutboxMessages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntilU~");

            migrationBuilder.RenameIndex(
                name: "IX_hardware_community_samples_profile_key_platform_received_at_utc",
                table: "hardware_community_samples",
                newName: "IX_hardware_community_samples_profile_key_platform_received_at~");

            migrationBuilder.RenameIndex(
                name: "IX_hardware_backtests_project_id_hardware_profile_id_measured_at_utc",
                table: "hardware_backtests",
                newName: "IX_hardware_backtests_project_id_hardware_profile_id_measured_~");

            migrationBuilder.RenameIndex(
                name: "IX_game_replay_runs_project_id_session_id_hardware_fingerprint_finished_at_utc",
                table: "game_replay_runs",
                newName: "IX_game_replay_runs_project_id_session_id_hardware_fingerprint~");

            migrationBuilder.RenameIndex(
                name: "IX_development_graph_edges_project_id_source_node_id_target_node_id_edge_type",
                table: "development_graph_edges",
                newName: "IX_development_graph_edges_project_id_source_node_id_target_no~");

            migrationBuilder.RenameIndex(
                name: "IX_cross_repository_impacts_project_id_commit_sha_analyzed_at_utc",
                table: "cross_repository_impacts",
                newName: "IX_cross_repository_impacts_project_id_commit_sha_analyzed_at_~");

            migrationBuilder.RenameIndex(
                name: "IX_change_risk_assessments_project_id_commit_sha_assessed_at_utc",
                table: "change_risk_assessments",
                newName: "IX_change_risk_assessments_project_id_commit_sha_assessed_at_u~");

            migrationBuilder.RenameIndex(
                name: "IX_benchmark_runs_project_id_benchmark_definition_id_environment_fingerprint_configuration_hash_created_at_utc",
                table: "benchmark_runs",
                newName: "IX_benchmark_runs_project_id_benchmark_definition_id_environme~");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "repository_nodes",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "repository_url",
                table: "repository_nodes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "key",
                table: "repository_nodes",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "default_branch",
                table: "repository_nodes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "contract_key",
                table: "repository_dependencies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "warnings_json",
                table: "release_readiness_snapshots",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "summary",
                table: "release_readiness_snapshots",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "required_actions_json",
                table: "release_readiness_snapshots",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "release_readiness_snapshots",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "blockers_json",
                table: "release_readiness_snapshots",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "manifest_artifact_id",
                table: "release_evidence_bundles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "release_evidence_bundles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "build_id",
                table: "release_evidence_bundles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "artifact_sha256",
                table: "release_evidence_bundles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "suspected_files_json",
                table: "regression_triages",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "explanation",
                table: "regression_triages",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "evidence_ids_json",
                table: "regression_triages",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "QualityIssues",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "FirstSeenPipelineRunId",
                table: "QualityIssues",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "LastSeenPipelineRunId",
                table: "QualityIssues",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "OccurrenceCount",
                table: "QualityIssues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResolvedAtUtc",
                table: "QualityIssues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Severity",
                table: "QualityIssues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "QualityIssues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CriticalIssueCount",
                table: "QualityGateResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WarningIssueCount",
                table: "QualityGateResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "requested_by",
                table: "qa_waivers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "reason",
                table: "qa_waivers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "qa_waivers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "test_key",
                table: "qa_test_reliability",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "test_key",
                table: "qa_test_impact_rules",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "path_pattern",
                table: "qa_test_impact_rules",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "command_json",
                table: "qa_test_impact_rules",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(8000)",
                oldMaxLength: 8000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "detail",
                table: "qa_system_health_snapshots",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "component",
                table: "qa_system_health_snapshots",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "budget_verdict",
                table: "qa_run_costs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "requested_by",
                table: "qa_release_gate_overrides",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "reason",
                table: "qa_release_gate_overrides",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "qa_release_gate_overrides",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "subject_key",
                table: "qa_regression_baselines",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "metric_key",
                table: "qa_regression_baselines",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "last_known_good_commit",
                table: "qa_regression_baselines",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "baseline_commit",
                table: "qa_regression_baselines",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "warnings_json",
                table: "qa_policy_evaluations",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "summary",
                table: "qa_policy_evaluations",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "policy_version",
                table: "qa_policy_evaluations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "blockers_json",
                table: "qa_policy_evaluations",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "version",
                table: "qa_policies",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "rules_json",
                table: "qa_policies",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32000)",
                oldMaxLength: 32000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "qa_policies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "team",
                table: "qa_ownership_rules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "path_pattern",
                table: "qa_ownership_rules",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "qa_ownership_rules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "input_hash",
                table: "qa_governance_processed_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "governance_version",
                table: "qa_governance_processed_runs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "toolchain_json",
                table: "qa_evidence_provenance",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "policy_version",
                table: "qa_evidence_provenance",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "normalizer_version",
                table: "qa_evidence_provenance",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "input_hash",
                table: "qa_evidence_provenance",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "evidence_hash",
                table: "qa_evidence_provenance",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "environment_fingerprint",
                table: "qa_evidence_provenance",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "original_input",
                table: "qa_bug_minimizations",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "minimized_input",
                table: "qa_bug_minimizations",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "detail",
                table: "qa_bug_minimizations",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "source_commit",
                table: "qa_backup_verifications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "environment_fingerprint",
                table: "qa_backup_verifications",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "detail",
                table: "qa_backup_verifications",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "backup_reference",
                table: "qa_backup_verifications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "backup_kind",
                table: "qa_backup_verifications",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "retention_class",
                table: "qa_artifact_retention_rules",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "kind_pattern",
                table: "qa_artifact_retention_rules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReleaseDateUtc",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "ProcessedWebhooks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessedAtUtc",
                table: "ProcessedWebhooks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinishedAtUtc",
                table: "PipelineRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAtUtc",
                table: "PipelineRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "PipelineRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "target_profile",
                table: "performance_budget_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "rules_json",
                table: "performance_budget_profiles",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32000)",
                oldMaxLength: 32000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "platform",
                table: "performance_budget_profiles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "performance_budget_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "key",
                table: "performance_budget_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "summary",
                table: "performance_budget_evaluations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "detail_json",
                table: "performance_budget_evaluations",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32000)",
                oldMaxLength: 32000,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "OutboxMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "BaselinePipelineRunId",
                table: "Metrics",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComparisonStatus",
                table: "Metrics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Severity",
                table: "Metrics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "incidents",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(240)",
                oldMaxLength: 240,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "subject_key",
                table: "incidents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "fingerprint",
                table: "incidents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "subject_key",
                table: "incident_occurrences",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "summary",
                table: "incident_events",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "scenario_key",
                table: "hardware_backtests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "environment_fingerprint",
                table: "hardware_backtests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "hardware_backtests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "scene",
                table: "game_replay_sessions",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "game_replay_sessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "launch_command",
                table: "game_replay_sessions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "key",
                table: "game_replay_sessions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "input_artifact_id",
                table: "game_replay_sessions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "arguments",
                table: "game_replay_sessions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "hardware_fingerprint",
                table: "game_replay_runs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "game_replay_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "build_id",
                table: "game_replay_runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "unit",
                table: "game_replay_metrics",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "game_replay_metrics",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "aggregation",
                table: "game_replay_metrics",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "url",
                table: "external_work_item_links",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "external_work_item_links",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "external_id",
                table: "external_work_item_links",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "actor_id",
                table: "evidence_records",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "artifact_id",
                table: "evidence_records",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "content_hash",
                table: "evidence_records",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "event_type",
                table: "evidence_records",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "integrity_status",
                table: "evidence_records",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "LegacyUnsigned");

            migrationBuilder.AddColumn<string>(
                name: "machine_id",
                table: "evidence_records",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "metadata_json",
                table: "evidence_records",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_evidence_id",
                table: "evidence_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "privacy_class",
                table: "evidence_records",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Internal");

            migrationBuilder.AddColumn<string>(
                name: "source_event_id",
                table: "evidence_records",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_instance_id",
                table: "evidence_records",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "subject_key",
                table: "durable_jobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "payload_json",
                table: "durable_jobs",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "development_graph_nodes",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "node_key",
                table: "development_graph_nodes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "development_graph_nodes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "metadata_json",
                table: "development_graph_nodes",
                type: "character varying(64000)",
                maxLength: 64000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64000)",
                oldMaxLength: 64000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "metadata_json",
                table: "development_graph_edges",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32000)",
                oldMaxLength: 32000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "reasons_json",
                table: "cross_repository_impacts",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "cross_repository_impacts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "affected_repository_ids_json",
                table: "cross_repository_impacts",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "reasons_json",
                table: "change_risk_assessments",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "change_risk_assessments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "affected_repositories_json",
                table: "change_risk_assessments",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "subject_key",
                table: "benchmark_runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "environment_fingerprint",
                table: "benchmark_runs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "configuration_hash",
                table: "benchmark_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "benchmark_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "unit",
                table: "benchmark_metrics",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "benchmark_metrics",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "aggregation",
                table: "benchmark_metrics",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "benchmark_definitions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "kind",
                table: "benchmark_definitions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "key",
                table: "benchmark_definitions",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "configuration_json",
                table: "benchmark_definitions",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32000)",
                oldMaxLength: 32000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "metric_name",
                table: "benchmark_comparisons",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "explanation",
                table: "benchmark_comparisons",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "community_hardware_samples",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hardware_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    platform = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    cpu_arch = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    gpu_vendor = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    os_family = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    driver_family = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    cpu_single_score = table.Column<decimal>(type: "numeric", nullable: false),
                    cpu_multi_score = table.Column<decimal>(type: "numeric", nullable: false),
                    gpu_score = table.Column<decimal>(type: "numeric", nullable: false),
                    memory_score = table.Column<decimal>(type: "numeric", nullable: false),
                    storage_score = table.Column<decimal>(type: "numeric", nullable: false),
                    thermal_score = table.Column<decimal>(type: "numeric", nullable: false),
                    memory_mb = table.Column<long>(type: "bigint", nullable: false),
                    source_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    measured_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_hardware_samples", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "integration_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    base_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    credential_reference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    external_account = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    health_state = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    last_health_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    last_tested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_connections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "intelligence_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    subject_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    artifact_root = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    input_json = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    result_json = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intelligence_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_intelligence_runs_Projects_project_id",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "policy_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    agent_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    evidence_policy_json = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    agent_policy_json = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    retention_policy_json = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    alert_policy_json = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    privacy_policy_json = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    qa_policy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_policy_profiles_Projects_project_id",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_policy_profiles_qa_policies_qa_policy_id",
                        column: x => x.qa_policy_id,
                        principalTable: "qa_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "project_forecasts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    forecast_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    risk_percent = table.Column<decimal>(type: "numeric", nullable: false),
                    earliest_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    latest_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confidence = table.Column<string>(type: "text", nullable: false),
                    rationale_json = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_forecasts", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_forecasts_Projects_project_id",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_source_bindings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_resource_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    external_resource_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    configuration_json = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_synced_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_source_bindings", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_source_bindings_Projects_project_id",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_source_bindings_integration_connections_connection_~",
                        column: x => x.connection_id,
                        principalTable: "integration_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reproduction_attempts_project_id",
                table: "reproduction_attempts",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_repository_dependencies_source_repository_id",
                table: "repository_dependencies",
                column: "source_repository_id");

            migrationBuilder.CreateIndex(
                name: "IX_repository_dependencies_target_repository_id",
                table: "repository_dependencies",
                column: "target_repository_id");

            migrationBuilder.CreateIndex(
                name: "IX_QualityIssues_PipelineRunId",
                table: "QualityIssues",
                column: "PipelineRunId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityGateResults_PipelineRunId",
                table: "QualityGateResults",
                column: "PipelineRunId");

            migrationBuilder.CreateIndex(
                name: "IX_qa_policy_evaluations_qa_run_id",
                table: "qa_policy_evaluations",
                column: "qa_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_qa_findings_qa_run_id",
                table: "qa_findings",
                column: "qa_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_qa_evidence_provenance_qa_run_id",
                table: "qa_evidence_provenance",
                column: "qa_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_performance_simulation_runs_hardware_profile_id",
                table: "performance_simulation_runs",
                column: "hardware_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_performance_budget_evaluations_benchmark_run_id",
                table: "performance_budget_evaluations",
                column: "benchmark_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_performance_budget_evaluations_profile_id",
                table: "performance_budget_evaluations",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_performance_budget_evaluations_replay_run_id",
                table: "performance_budget_evaluations",
                column: "replay_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_occurrences_job_id",
                table: "incident_occurrences",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_occurrences_project_id",
                table: "incident_occurrences",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_occurrences_qa_run_id",
                table: "incident_occurrences",
                column: "qa_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_hardware_backtests_hardware_profile_id",
                table: "hardware_backtests",
                column: "hardware_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_game_replay_runs_session_id",
                table: "game_replay_runs",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_work_item_links_project_id",
                table: "external_work_item_links",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_records_pipeline_run_id",
                table: "evidence_records",
                column: "pipeline_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_records_project_id_event_type_observed_at_utc",
                table: "evidence_records",
                columns: new[] { "project_id", "event_type", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_records_project_id_source_type_source_instance_id",
                table: "evidence_records",
                columns: new[] { "project_id", "source_type", "source_instance_id" });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_records_project_id_source_type_source_instance_id_~",
                table: "evidence_records",
                columns: new[] { "project_id", "source_type", "source_instance_id", "source_event_id" },
                unique: true,
                filter: "\"source_event_id\" IS NOT NULL AND \"source_instance_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_development_graph_edges_source_node_id",
                table: "development_graph_edges",
                column: "source_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_development_graph_edges_target_node_id",
                table: "development_graph_edges",
                column: "target_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_cross_repository_impacts_source_repository_id",
                table: "cross_repository_impacts",
                column: "source_repository_id");

            migrationBuilder.CreateIndex(
                name: "IX_claim_evidence_links_evidence_record_id",
                table: "claim_evidence_links",
                column: "evidence_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_benchmark_runs_benchmark_definition_id",
                table: "benchmark_runs",
                column: "benchmark_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_benchmark_comparisons_baseline_run_id",
                table: "benchmark_comparisons",
                column: "baseline_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_artifact_records_artifact_blob_id",
                table: "artifact_records",
                column: "artifact_blob_id");

            migrationBuilder.CreateIndex(
                name: "IX_artifact_records_qa_run_id",
                table: "artifact_records",
                column: "qa_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_hardware_samples_hardware_key_measured_at_utc",
                table: "community_hardware_samples",
                columns: new[] { "hardware_key", "measured_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_display_name",
                table: "integration_connections",
                column: "display_name");

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_provider_enabled",
                table: "integration_connections",
                columns: new[] { "provider", "enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_intelligence_runs_project_id_created_at_utc",
                table: "intelligence_runs",
                columns: new[] { "project_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_policy_profiles_agent_id",
                table: "policy_profiles",
                column: "agent_id",
                unique: true,
                filter: "\"agent_id\" IS NOT NULL AND \"scope_type\" = 'Agent'");

            migrationBuilder.CreateIndex(
                name: "IX_policy_profiles_project_id",
                table: "policy_profiles",
                column: "project_id",
                unique: true,
                filter: "\"project_id\" IS NOT NULL AND \"scope_type\" = 'Project'");

            migrationBuilder.CreateIndex(
                name: "IX_policy_profiles_qa_policy_id",
                table: "policy_profiles",
                column: "qa_policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_policy_profiles_scope_type",
                table: "policy_profiles",
                column: "scope_type",
                unique: true,
                filter: "\"scope_type\" = 'Global'");

            migrationBuilder.CreateIndex(
                name: "IX_project_forecasts_project_id_created_at_utc",
                table: "project_forecasts",
                columns: new[] { "project_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_project_source_bindings_connection_id",
                table: "project_source_bindings",
                column: "connection_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_source_bindings_project_id_connection_id_external_r~",
                table: "project_source_bindings",
                columns: new[] { "project_id", "connection_id", "external_resource_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_source_bindings_project_id_enabled",
                table: "project_source_bindings",
                columns: new[] { "project_id", "enabled" });

            migrationBuilder.AddForeignKey(
                name: "FK_cross_repository_impacts_repository_nodes_source_repository~",
                table: "cross_repository_impacts",
                column: "source_repository_id",
                principalTable: "repository_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_development_graph_edges_development_graph_nodes_source_node~",
                table: "development_graph_edges",
                column: "source_node_id",
                principalTable: "development_graph_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_development_graph_edges_development_graph_nodes_target_node~",
                table: "development_graph_edges",
                column: "target_node_id",
                principalTable: "development_graph_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_performance_budget_evaluations_benchmark_runs_benchmark_run~",
                table: "performance_budget_evaluations",
                column: "benchmark_run_id",
                principalTable: "benchmark_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_performance_budget_evaluations_game_replay_runs_replay_run_~",
                table: "performance_budget_evaluations",
                column: "replay_run_id",
                principalTable: "game_replay_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_performance_budget_evaluations_performance_budget_profiles_~",
                table: "performance_budget_evaluations",
                column: "profile_id",
                principalTable: "performance_budget_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_performance_simulation_runs_hardware_profiles_hardware_prof~",
                table: "performance_simulation_runs",
                column: "hardware_profile_id",
                principalTable: "hardware_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_repository_dependencies_repository_nodes_source_repository_~",
                table: "repository_dependencies",
                column: "source_repository_id",
                principalTable: "repository_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_repository_dependencies_repository_nodes_target_repository_~",
                table: "repository_dependencies",
                column: "target_repository_id",
                principalTable: "repository_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cross_repository_impacts_repository_nodes_source_repository~",
                table: "cross_repository_impacts");

            migrationBuilder.DropForeignKey(
                name: "FK_development_graph_edges_development_graph_nodes_source_node~",
                table: "development_graph_edges");

            migrationBuilder.DropForeignKey(
                name: "FK_development_graph_edges_development_graph_nodes_target_node~",
                table: "development_graph_edges");

            migrationBuilder.DropForeignKey(
                name: "FK_performance_budget_evaluations_benchmark_runs_benchmark_run~",
                table: "performance_budget_evaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_performance_budget_evaluations_game_replay_runs_replay_run_~",
                table: "performance_budget_evaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_performance_budget_evaluations_performance_budget_profiles_~",
                table: "performance_budget_evaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_performance_simulation_runs_hardware_profiles_hardware_prof~",
                table: "performance_simulation_runs");

            migrationBuilder.DropForeignKey(
                name: "FK_repository_dependencies_repository_nodes_source_repository_~",
                table: "repository_dependencies");

            migrationBuilder.DropForeignKey(
                name: "FK_repository_dependencies_repository_nodes_target_repository_~",
                table: "repository_dependencies");

            migrationBuilder.DropTable(
                name: "community_hardware_samples");

            migrationBuilder.DropTable(
                name: "intelligence_runs");

            migrationBuilder.DropTable(
                name: "policy_profiles");

            migrationBuilder.DropTable(
                name: "project_forecasts");

            migrationBuilder.DropTable(
                name: "project_source_bindings");

            migrationBuilder.DropTable(
                name: "integration_connections");

            migrationBuilder.DropIndex(
                name: "IX_reproduction_attempts_project_id",
                table: "reproduction_attempts");

            migrationBuilder.DropIndex(
                name: "IX_repository_dependencies_source_repository_id",
                table: "repository_dependencies");

            migrationBuilder.DropIndex(
                name: "IX_repository_dependencies_target_repository_id",
                table: "repository_dependencies");

            migrationBuilder.DropIndex(
                name: "IX_QualityIssues_PipelineRunId",
                table: "QualityIssues");

            migrationBuilder.DropIndex(
                name: "IX_QualityGateResults_PipelineRunId",
                table: "QualityGateResults");

            migrationBuilder.DropIndex(
                name: "IX_qa_policy_evaluations_qa_run_id",
                table: "qa_policy_evaluations");

            migrationBuilder.DropIndex(
                name: "IX_qa_findings_qa_run_id",
                table: "qa_findings");

            migrationBuilder.DropIndex(
                name: "IX_qa_evidence_provenance_qa_run_id",
                table: "qa_evidence_provenance");

            migrationBuilder.DropIndex(
                name: "IX_performance_simulation_runs_hardware_profile_id",
                table: "performance_simulation_runs");

            migrationBuilder.DropIndex(
                name: "IX_performance_budget_evaluations_benchmark_run_id",
                table: "performance_budget_evaluations");

            migrationBuilder.DropIndex(
                name: "IX_performance_budget_evaluations_profile_id",
                table: "performance_budget_evaluations");

            migrationBuilder.DropIndex(
                name: "IX_performance_budget_evaluations_replay_run_id",
                table: "performance_budget_evaluations");

            migrationBuilder.DropIndex(
                name: "IX_incident_occurrences_job_id",
                table: "incident_occurrences");

            migrationBuilder.DropIndex(
                name: "IX_incident_occurrences_project_id",
                table: "incident_occurrences");

            migrationBuilder.DropIndex(
                name: "IX_incident_occurrences_qa_run_id",
                table: "incident_occurrences");

            migrationBuilder.DropIndex(
                name: "IX_hardware_backtests_hardware_profile_id",
                table: "hardware_backtests");

            migrationBuilder.DropIndex(
                name: "IX_game_replay_runs_session_id",
                table: "game_replay_runs");

            migrationBuilder.DropIndex(
                name: "IX_external_work_item_links_project_id",
                table: "external_work_item_links");

            migrationBuilder.DropIndex(
                name: "IX_evidence_records_pipeline_run_id",
                table: "evidence_records");

            migrationBuilder.DropIndex(
                name: "IX_evidence_records_project_id_event_type_observed_at_utc",
                table: "evidence_records");

            migrationBuilder.DropIndex(
                name: "IX_evidence_records_project_id_source_type_source_instance_id",
                table: "evidence_records");

            migrationBuilder.DropIndex(
                name: "IX_evidence_records_project_id_source_type_source_instance_id_~",
                table: "evidence_records");

            migrationBuilder.DropIndex(
                name: "IX_development_graph_edges_source_node_id",
                table: "development_graph_edges");

            migrationBuilder.DropIndex(
                name: "IX_development_graph_edges_target_node_id",
                table: "development_graph_edges");

            migrationBuilder.DropIndex(
                name: "IX_cross_repository_impacts_source_repository_id",
                table: "cross_repository_impacts");

            migrationBuilder.DropIndex(
                name: "IX_claim_evidence_links_evidence_record_id",
                table: "claim_evidence_links");

            migrationBuilder.DropIndex(
                name: "IX_benchmark_runs_benchmark_definition_id",
                table: "benchmark_runs");

            migrationBuilder.DropIndex(
                name: "IX_benchmark_comparisons_baseline_run_id",
                table: "benchmark_comparisons");

            migrationBuilder.DropIndex(
                name: "IX_artifact_records_artifact_blob_id",
                table: "artifact_records");

            migrationBuilder.DropIndex(
                name: "IX_artifact_records_qa_run_id",
                table: "artifact_records");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "QualityIssues");

            migrationBuilder.DropColumn(
                name: "FirstSeenPipelineRunId",
                table: "QualityIssues");

            migrationBuilder.DropColumn(
                name: "LastSeenPipelineRunId",
                table: "QualityIssues");

            migrationBuilder.DropColumn(
                name: "OccurrenceCount",
                table: "QualityIssues");

            migrationBuilder.DropColumn(
                name: "ResolvedAtUtc",
                table: "QualityIssues");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "QualityIssues");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "QualityIssues");

            migrationBuilder.DropColumn(
                name: "CriticalIssueCount",
                table: "QualityGateResults");

            migrationBuilder.DropColumn(
                name: "WarningIssueCount",
                table: "QualityGateResults");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ReleaseDateUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "ProcessedWebhooks");

            migrationBuilder.DropColumn(
                name: "ProcessedAtUtc",
                table: "ProcessedWebhooks");

            migrationBuilder.DropColumn(
                name: "FinishedAtUtc",
                table: "PipelineRuns");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "PipelineRuns");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PipelineRuns");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "BaselinePipelineRunId",
                table: "Metrics");

            migrationBuilder.DropColumn(
                name: "ComparisonStatus",
                table: "Metrics");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "Metrics");

            migrationBuilder.DropColumn(
                name: "actor_id",
                table: "evidence_records");

            migrationBuilder.DropColumn(
                name: "artifact_id",
                table: "evidence_records");

            migrationBuilder.DropColumn(
                name: "content_hash",
                table: "evidence_records");

            migrationBuilder.DropColumn(
                name: "event_type",
                table: "evidence_records");

            migrationBuilder.DropColumn(
                name: "integrity_status",
                table: "evidence_records");

            migrationBuilder.DropColumn(
                name: "machine_id",
                table: "evidence_records");

            migrationBuilder.DropColumn(
                name: "metadata_json",
                table: "evidence_records");

            migrationBuilder.DropColumn(
                name: "parent_evidence_id",
                table: "evidence_records");

            migrationBuilder.DropColumn(
                name: "privacy_class",
                table: "evidence_records");

            migrationBuilder.DropColumn(
                name: "source_event_id",
                table: "evidence_records");

            migrationBuilder.DropColumn(
                name: "source_instance_id",
                table: "evidence_records");

            migrationBuilder.RenameIndex(
                name: "IX_repository_dependencies_project_id_source_repository_id_tar~",
                table: "repository_dependencies",
                newName: "IX_repository_dependencies_project_id_source_repository_id_target_repository_id_kind_contract_key");

            migrationBuilder.RenameIndex(
                name: "IX_PipelineRuns_ProjectId_ExternalRunId_CollectorType_AttemptN~",
                table: "PipelineRuns",
                newName: "IX_PipelineRuns_ProjectId_ExternalRunId_CollectorType_AttemptNumber");

            migrationBuilder.RenameIndex(
                name: "IX_p2_intelligence_records_project_id_subject_key_created_at_u~",
                table: "p2_intelligence_records",
                newName: "IX_p2_intelligence_records_project_id_subject_key_created_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_OutboxMessages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntilU~",
                table: "OutboxMessages",
                newName: "IX_OutboxMessages_ProcessedAtUtc_NextAttemptAtUtc_LockedUntilUtc");

            migrationBuilder.RenameIndex(
                name: "IX_hardware_community_samples_profile_key_platform_received_at~",
                table: "hardware_community_samples",
                newName: "IX_hardware_community_samples_profile_key_platform_received_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_hardware_backtests_project_id_hardware_profile_id_measured_~",
                table: "hardware_backtests",
                newName: "IX_hardware_backtests_project_id_hardware_profile_id_measured_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_game_replay_runs_project_id_session_id_hardware_fingerprint~",
                table: "game_replay_runs",
                newName: "IX_game_replay_runs_project_id_session_id_hardware_fingerprint_finished_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_development_graph_edges_project_id_source_node_id_target_no~",
                table: "development_graph_edges",
                newName: "IX_development_graph_edges_project_id_source_node_id_target_node_id_edge_type");

            migrationBuilder.RenameIndex(
                name: "IX_cross_repository_impacts_project_id_commit_sha_analyzed_at_~",
                table: "cross_repository_impacts",
                newName: "IX_cross_repository_impacts_project_id_commit_sha_analyzed_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_change_risk_assessments_project_id_commit_sha_assessed_at_u~",
                table: "change_risk_assessments",
                newName: "IX_change_risk_assessments_project_id_commit_sha_assessed_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_benchmark_runs_project_id_benchmark_definition_id_environme~",
                table: "benchmark_runs",
                newName: "IX_benchmark_runs_project_id_benchmark_definition_id_environment_fingerprint_configuration_hash_created_at_utc");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "repository_nodes",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "repository_url",
                table: "repository_nodes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "key",
                table: "repository_nodes",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "default_branch",
                table: "repository_nodes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "contract_key",
                table: "repository_dependencies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "warnings_json",
                table: "release_readiness_snapshots",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "summary",
                table: "release_readiness_snapshots",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "required_actions_json",
                table: "release_readiness_snapshots",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "release_readiness_snapshots",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "blockers_json",
                table: "release_readiness_snapshots",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "manifest_artifact_id",
                table: "release_evidence_bundles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "release_evidence_bundles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "build_id",
                table: "release_evidence_bundles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "artifact_sha256",
                table: "release_evidence_bundles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "suspected_files_json",
                table: "regression_triages",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "explanation",
                table: "regression_triages",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "evidence_ids_json",
                table: "regression_triages",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "requested_by",
                table: "qa_waivers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "reason",
                table: "qa_waivers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "qa_waivers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "test_key",
                table: "qa_test_reliability",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "test_key",
                table: "qa_test_impact_rules",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "path_pattern",
                table: "qa_test_impact_rules",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "command_json",
                table: "qa_test_impact_rules",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8000)",
                oldMaxLength: 8000);

            migrationBuilder.AlterColumn<string>(
                name: "detail",
                table: "qa_system_health_snapshots",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "component",
                table: "qa_system_health_snapshots",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "budget_verdict",
                table: "qa_run_costs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "requested_by",
                table: "qa_release_gate_overrides",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "reason",
                table: "qa_release_gate_overrides",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "qa_release_gate_overrides",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "subject_key",
                table: "qa_regression_baselines",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "metric_key",
                table: "qa_regression_baselines",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "last_known_good_commit",
                table: "qa_regression_baselines",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "baseline_commit",
                table: "qa_regression_baselines",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "warnings_json",
                table: "qa_policy_evaluations",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "summary",
                table: "qa_policy_evaluations",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "policy_version",
                table: "qa_policy_evaluations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "blockers_json",
                table: "qa_policy_evaluations",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "version",
                table: "qa_policies",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "rules_json",
                table: "qa_policies",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32000)",
                oldMaxLength: 32000);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "qa_policies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "team",
                table: "qa_ownership_rules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "path_pattern",
                table: "qa_ownership_rules",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "qa_ownership_rules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "input_hash",
                table: "qa_governance_processed_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "governance_version",
                table: "qa_governance_processed_runs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "toolchain_json",
                table: "qa_evidence_provenance",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "policy_version",
                table: "qa_evidence_provenance",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "normalizer_version",
                table: "qa_evidence_provenance",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "input_hash",
                table: "qa_evidence_provenance",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "evidence_hash",
                table: "qa_evidence_provenance",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "environment_fingerprint",
                table: "qa_evidence_provenance",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "original_input",
                table: "qa_bug_minimizations",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "minimized_input",
                table: "qa_bug_minimizations",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "detail",
                table: "qa_bug_minimizations",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "source_commit",
                table: "qa_backup_verifications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "environment_fingerprint",
                table: "qa_backup_verifications",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "detail",
                table: "qa_backup_verifications",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "backup_reference",
                table: "qa_backup_verifications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "backup_kind",
                table: "qa_backup_verifications",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "retention_class",
                table: "qa_artifact_retention_rules",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "kind_pattern",
                table: "qa_artifact_retention_rules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "target_profile",
                table: "performance_budget_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "rules_json",
                table: "performance_budget_profiles",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32000)",
                oldMaxLength: 32000);

            migrationBuilder.AlterColumn<string>(
                name: "platform",
                table: "performance_budget_profiles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "performance_budget_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "key",
                table: "performance_budget_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "summary",
                table: "performance_budget_evaluations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "detail_json",
                table: "performance_budget_evaluations",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32000)",
                oldMaxLength: 32000);

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "incidents",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(240)",
                oldMaxLength: 240);

            migrationBuilder.AlterColumn<string>(
                name: "subject_key",
                table: "incidents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "fingerprint",
                table: "incidents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "subject_key",
                table: "incident_occurrences",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "summary",
                table: "incident_events",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "scenario_key",
                table: "hardware_backtests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "environment_fingerprint",
                table: "hardware_backtests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "hardware_backtests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "scene",
                table: "game_replay_sessions",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "game_replay_sessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "launch_command",
                table: "game_replay_sessions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "key",
                table: "game_replay_sessions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "input_artifact_id",
                table: "game_replay_sessions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "arguments",
                table: "game_replay_sessions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "hardware_fingerprint",
                table: "game_replay_runs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "game_replay_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "build_id",
                table: "game_replay_runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "unit",
                table: "game_replay_metrics",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "game_replay_metrics",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "aggregation",
                table: "game_replay_metrics",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "url",
                table: "external_work_item_links",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "external_work_item_links",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "external_id",
                table: "external_work_item_links",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "subject_key",
                table: "durable_jobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "payload_json",
                table: "durable_jobs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "development_graph_nodes",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "node_key",
                table: "development_graph_nodes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "development_graph_nodes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "metadata_json",
                table: "development_graph_nodes",
                type: "character varying(64000)",
                maxLength: 64000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64000)",
                oldMaxLength: 64000);

            migrationBuilder.AlterColumn<string>(
                name: "metadata_json",
                table: "development_graph_edges",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32000)",
                oldMaxLength: 32000);

            migrationBuilder.AlterColumn<string>(
                name: "reasons_json",
                table: "cross_repository_impacts",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "cross_repository_impacts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "affected_repository_ids_json",
                table: "cross_repository_impacts",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "reasons_json",
                table: "change_risk_assessments",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "change_risk_assessments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "affected_repositories_json",
                table: "change_risk_assessments",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16000)",
                oldMaxLength: 16000);

            migrationBuilder.AlterColumn<string>(
                name: "subject_key",
                table: "benchmark_runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "environment_fingerprint",
                table: "benchmark_runs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "configuration_hash",
                table: "benchmark_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "commit_sha",
                table: "benchmark_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "unit",
                table: "benchmark_metrics",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "benchmark_metrics",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "aggregation",
                table: "benchmark_metrics",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "benchmark_definitions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "kind",
                table: "benchmark_definitions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "key",
                table: "benchmark_definitions",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "configuration_json",
                table: "benchmark_definitions",
                type: "character varying(32000)",
                maxLength: 32000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32000)",
                oldMaxLength: 32000);

            migrationBuilder.AlterColumn<string>(
                name: "metric_name",
                table: "benchmark_comparisons",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "explanation",
                table: "benchmark_comparisons",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddForeignKey(
                name: "FK_cross_repository_impacts_repository_nodes_source_repository_id",
                table: "cross_repository_impacts",
                column: "source_repository_id",
                principalTable: "repository_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_development_graph_edges_development_graph_nodes_source_node_id",
                table: "development_graph_edges",
                column: "source_node_id",
                principalTable: "development_graph_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_development_graph_edges_development_graph_nodes_target_node_id",
                table: "development_graph_edges",
                column: "target_node_id",
                principalTable: "development_graph_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_performance_budget_evaluations_benchmark_runs_benchmark_run_id",
                table: "performance_budget_evaluations",
                column: "benchmark_run_id",
                principalTable: "benchmark_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_performance_budget_evaluations_game_replay_runs_replay_run_id",
                table: "performance_budget_evaluations",
                column: "replay_run_id",
                principalTable: "game_replay_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_performance_budget_evaluations_performance_budget_profiles_profile_id",
                table: "performance_budget_evaluations",
                column: "profile_id",
                principalTable: "performance_budget_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_performance_simulation_runs_hardware_profiles_hardware_profile_id",
                table: "performance_simulation_runs",
                column: "hardware_profile_id",
                principalTable: "hardware_profiles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_repository_dependencies_repository_nodes_source_repository_id",
                table: "repository_dependencies",
                column: "source_repository_id",
                principalTable: "repository_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_repository_dependencies_repository_nodes_target_repository_id",
                table: "repository_dependencies",
                column: "target_repository_id",
                principalTable: "repository_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
