using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageDemandEstimation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LifecycleWorkers_DiskTelemetry",
                table: "LifecycleWorkers");

            migrationBuilder.AddColumn<long>(
                name: "ControlledDeploymentBytes",
                table: "LifecycleWorkers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentSnapshot",
                table: "GameServerDeployments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PeakStorageRequiredBytes",
                table: "GameServerDeployments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "StorageEstimateCalculatedAt",
                table: "GameServerDeployments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageEstimateStatus",
                table: "GameServerDeployments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentStorageObservations",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ComponentKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceIdentifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApplicationIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ItemIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReportedBytes = table.Column<long>(type: "bigint", nullable: true),
                    MeasuredBytes = table.Column<long>(type: "bigint", nullable: true),
                    ExternalVersion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MeasuredVersion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ObservationStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ObservationSource = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalUpdatedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    ReportedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    MeasuredAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    LastMeasuredWorkerCommandFk = table.Column<Guid>(type: "uuid", nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentStorageObservations", x => x.PrimaryKey);
                    table.CheckConstraint("CK_ContentStorageObservations_Bytes", "(\"ReportedBytes\" IS NULL OR \"ReportedBytes\" >= 0) AND (\"MeasuredBytes\" IS NULL OR \"MeasuredBytes\" >= 0)");
                    table.ForeignKey(
                        name: "FK_ContentStorageObservations_WorkerCommands_LastMeasuredWorke~",
                        column: x => x.LastMeasuredWorkerCommandFk,
                        principalTable: "WorkerCommands",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LifecycleWorkers_DiskTelemetry",
                table: "LifecycleWorkers",
                sql: "(\"DiskTotalBytes\" IS NULL AND \"DiskAvailableBytes\" IS NULL AND \"ControlledDeploymentBytes\" IS NULL AND \"DiskTimeStampObserved\" IS NULL) OR (\"DiskTotalBytes\" IS NOT NULL AND \"DiskAvailableBytes\" IS NOT NULL AND \"ControlledDeploymentBytes\" IS NOT NULL AND \"DiskTimeStampObserved\" IS NOT NULL AND \"DiskTotalBytes\" >= 0 AND \"DiskAvailableBytes\" >= 0 AND \"ControlledDeploymentBytes\" >= 0 AND \"DiskAvailableBytes\" <= \"DiskTotalBytes\")");

            migrationBuilder.CreateIndex(
                name: "IX_ContentStorageObservations_GameIdentifier_SourceIdentifier_~",
                table: "ContentStorageObservations",
                columns: new[] { "GameIdentifier", "SourceIdentifier", "ApplicationIdentifier", "ItemIdentifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentStorageObservations_LastMeasuredWorkerCommandFk",
                table: "ContentStorageObservations",
                column: "LastMeasuredWorkerCommandFk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentStorageObservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LifecycleWorkers_DiskTelemetry",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "ControlledDeploymentBytes",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "ContentSnapshot",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "PeakStorageRequiredBytes",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "StorageEstimateCalculatedAt",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "StorageEstimateStatus",
                table: "GameServerDeployments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LifecycleWorkers_DiskTelemetry",
                table: "LifecycleWorkers",
                sql: "(\"DiskTotalBytes\" IS NULL AND \"DiskAvailableBytes\" IS NULL AND \"DiskTimeStampObserved\" IS NULL) OR (\"DiskTotalBytes\" IS NOT NULL AND \"DiskAvailableBytes\" IS NOT NULL AND \"DiskTimeStampObserved\" IS NOT NULL AND \"DiskTotalBytes\" >= 0 AND \"DiskAvailableBytes\" >= 0 AND \"DiskAvailableBytes\" <= \"DiskTotalBytes\")");
        }
    }
}
