using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddServerContentAndWorkerDiskTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DiskAvailableBytes",
                table: "LifecycleWorkers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "DiskTimeStampObserved",
                table: "LifecycleWorkers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DiskTotalBytes",
                table: "LifecycleWorkers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SelectedModPackDefinitionFk",
                table: "GameServers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LifecycleWorkers_DiskTelemetry",
                table: "LifecycleWorkers",
                sql: "(\"DiskTotalBytes\" IS NULL AND \"DiskAvailableBytes\" IS NULL AND \"DiskTimeStampObserved\" IS NULL) OR (\"DiskTotalBytes\" IS NOT NULL AND \"DiskAvailableBytes\" IS NOT NULL AND \"DiskTimeStampObserved\" IS NOT NULL AND \"DiskTotalBytes\" >= 0 AND \"DiskAvailableBytes\" >= 0 AND \"DiskAvailableBytes\" <= \"DiskTotalBytes\")");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_SelectedModPackDefinitionFk",
                table: "GameServers",
                column: "SelectedModPackDefinitionFk");

            migrationBuilder.AddForeignKey(
                name: "FK_GameServers_ModPackDefinitions_SelectedModPackDefinitionFk",
                table: "GameServers",
                column: "SelectedModPackDefinitionFk",
                principalTable: "ModPackDefinitions",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameServers_ModPackDefinitions_SelectedModPackDefinitionFk",
                table: "GameServers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LifecycleWorkers_DiskTelemetry",
                table: "LifecycleWorkers");

            migrationBuilder.DropIndex(
                name: "IX_GameServers_SelectedModPackDefinitionFk",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "DiskAvailableBytes",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "DiskTimeStampObserved",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "DiskTotalBytes",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "SelectedModPackDefinitionFk",
                table: "GameServers");
        }
    }
}
