using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddGameServerRuntimeProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedHourlyCostEur",
                table: "GameServers",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedMonthlyCostEur",
                table: "GameServers",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuntimeProfileDisplayName",
                table: "GameServers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RuntimeProfileIdentifier",
                table: "GameServers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RuntimeProfileDisplayName",
                table: "GameServerDeployments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RuntimeProfileIdentifier",
                table: "GameServerDeployments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            // Existing records previously used the installation-wide Hetzner default. Preserve that effective
            // selection explicitly so operators can see and change it before the next Start.
            migrationBuilder.Sql(
                """
                UPDATE "GameServers"
                SET "RuntimeProfileIdentifier" = 'ccx13',
                    "RuntimeProfileDisplayName" = 'CCX13',
                    "EstimatedHourlyCostEur" = 0.10
                WHERE "RuntimeProviderIdentifier" = 'hetzner';

                UPDATE "GameServerDeployments" AS deployment
                SET "RuntimeProfileIdentifier" = server."RuntimeProfileIdentifier",
                    "RuntimeProfileDisplayName" = server."RuntimeProfileDisplayName"
                FROM "GameServers" AS server
                WHERE deployment."GameServerFk" = server."PrimaryKey";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedHourlyCostEur",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "EstimatedMonthlyCostEur",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "RuntimeProfileDisplayName",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "RuntimeProfileIdentifier",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "RuntimeProfileDisplayName",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "RuntimeProfileIdentifier",
                table: "GameServerDeployments");
        }
    }
}
