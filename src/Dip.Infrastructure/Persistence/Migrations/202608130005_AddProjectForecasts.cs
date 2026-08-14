using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dip.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DipDbContext))]
[Migration("202608130005_AddProjectForecasts")]
public sealed class AddProjectForecasts : Migration
{
    public const string MigrationId = "202608130005_AddProjectForecasts";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS project_forecasts (
                id uuid PRIMARY KEY,
                project_id uuid NOT NULL REFERENCES "Projects" ("Id") ON DELETE CASCADE,
                forecast_type varchar(80) NOT NULL,
                risk_percent numeric NOT NULL,
                earliest_utc timestamptz NULL,
                latest_utc timestamptz NULL,
                confidence text NOT NULL,
                rationale_json varchar(32000) NOT NULL,
                created_at_utc timestamptz NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_project_forecasts_project_created_at_utc
                ON project_forecasts(project_id, created_at_utc);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS project_forecasts CASCADE;");
    }
}
