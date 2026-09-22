using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dip.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DipDbContext))]
[Migration("202608120004_AddProjectControlGraph")]
public sealed class AddProjectControlGraph : Migration
{
    public const string MigrationId = "202608120004_AddProjectControlGraph";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS development_graph_nodes (
                id uuid PRIMARY KEY,
                project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
                node_type varchar(60) NOT NULL,
                node_key varchar(300) NOT NULL,
                name varchar(300) NOT NULL,
                status varchar(80) NOT NULL,
                provider varchar(80) NULL,
                external_reference varchar(2000) NULL,
                metadata_json varchar(64000) NOT NULL,
                first_observed_at_utc timestamptz NOT NULL,
                last_observed_at_utc timestamptz NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ux_development_graph_nodes_identity
                ON development_graph_nodes(project_id,node_type,node_key);
            CREATE INDEX IF NOT EXISTS ix_development_graph_nodes_status
                ON development_graph_nodes(project_id,node_type,status);
            CREATE INDEX IF NOT EXISTS ix_development_graph_nodes_observed
                ON development_graph_nodes(project_id,last_observed_at_utc);

            CREATE TABLE IF NOT EXISTS development_graph_edges (
                id uuid PRIMARY KEY,
                project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
                source_node_id uuid NOT NULL REFERENCES development_graph_nodes(id) ON DELETE CASCADE,
                target_node_id uuid NOT NULL REFERENCES development_graph_nodes(id) ON DELETE CASCADE,
                edge_type varchar(60) NOT NULL,
                weight numeric(6,2) NOT NULL,
                critical boolean NOT NULL,
                metadata_json varchar(32000) NOT NULL,
                first_observed_at_utc timestamptz NOT NULL,
                last_observed_at_utc timestamptz NOT NULL,
                CONSTRAINT ck_development_graph_edge_distinct CHECK (source_node_id <> target_node_id),
                CONSTRAINT ck_development_graph_edge_weight CHECK (weight >= 0 AND weight <= 100)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ux_development_graph_edges_identity
                ON development_graph_edges(project_id,source_node_id,target_node_id,edge_type);
            CREATE INDEX IF NOT EXISTS ix_development_graph_edges_source
                ON development_graph_edges(project_id,source_node_id);
            CREATE INDEX IF NOT EXISTS ix_development_graph_edges_target
                ON development_graph_edges(project_id,target_node_id);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS development_graph_edges CASCADE;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS development_graph_nodes CASCADE;");
    }
}
