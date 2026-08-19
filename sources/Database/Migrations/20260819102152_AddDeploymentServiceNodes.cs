using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDeploymentServiceNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkerCommands_DeploymentFk_Active",
                table: "WorkerCommands");

            migrationBuilder.DropIndex(
                name: "IX_WorkerCommands_WorkerFk",
                table: "WorkerCommands");

            migrationBuilder.DropIndex(
                name: "IX_LifecycleWorkers_DeploymentFk_Active",
                table: "LifecycleWorkers");

            migrationBuilder.DropIndex(
                name: "IX_LifecycleWorkers_DeploymentFk_Status",
                table: "LifecycleWorkers");

            migrationBuilder.AddColumn<string>(
                name: "ServiceIdentifier",
                table: "LifecycleWorkers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "game-server");

            migrationBuilder.AddColumn<int>(
                name: "ServiceOrdinal",
                table: "LifecycleWorkers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkerFk",
                table: "DeploymentResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "DeploymentResources" AS resource
                SET "WorkerFk" = (
                    SELECT candidate."PrimaryKey"
                    FROM "LifecycleWorkers" AS candidate
                    WHERE candidate."DeploymentFk" = resource."DeploymentFk"
                    ORDER BY candidate."TimeStampCreated"
                    LIMIT 1)
                WHERE resource."WorkerFk" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCommands_WorkerFk_Active",
                table: "WorkerCommands",
                column: "WorkerFk",
                unique: true,
                filter: "\"Status\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleWorkers_DeploymentFk_Active",
                table: "LifecycleWorkers",
                columns: new[] { "DeploymentFk", "ServiceIdentifier", "ServiceOrdinal" },
                unique: true,
                filter: "\"TimeStampStopped\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleWorkers_DeploymentFk_ServiceIdentifier_ServiceOrdi~",
                table: "LifecycleWorkers",
                columns: new[] { "DeploymentFk", "ServiceIdentifier", "ServiceOrdinal", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentResources_WorkerFk",
                table: "DeploymentResources",
                column: "WorkerFk");

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentResources_LifecycleWorkers_WorkerFk",
                table: "DeploymentResources",
                column: "WorkerFk",
                principalTable: "LifecycleWorkers",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentResources_LifecycleWorkers_WorkerFk",
                table: "DeploymentResources");

            migrationBuilder.DropIndex(
                name: "IX_WorkerCommands_WorkerFk_Active",
                table: "WorkerCommands");

            migrationBuilder.DropIndex(
                name: "IX_LifecycleWorkers_DeploymentFk_Active",
                table: "LifecycleWorkers");

            migrationBuilder.DropIndex(
                name: "IX_LifecycleWorkers_DeploymentFk_ServiceIdentifier_ServiceOrdi~",
                table: "LifecycleWorkers");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentResources_WorkerFk",
                table: "DeploymentResources");

            migrationBuilder.DropColumn(
                name: "ServiceIdentifier",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "ServiceOrdinal",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "WorkerFk",
                table: "DeploymentResources");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCommands_DeploymentFk_Active",
                table: "WorkerCommands",
                column: "DeploymentFk",
                unique: true,
                filter: "\"Status\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCommands_WorkerFk",
                table: "WorkerCommands",
                column: "WorkerFk");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleWorkers_DeploymentFk_Active",
                table: "LifecycleWorkers",
                column: "DeploymentFk",
                unique: true,
                filter: "\"TimeStampStopped\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleWorkers_DeploymentFk_Status",
                table: "LifecycleWorkers",
                columns: new[] { "DeploymentFk", "Status" });
        }
    }
}
