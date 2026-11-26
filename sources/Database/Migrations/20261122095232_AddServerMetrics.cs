using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddServerMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerMetricBuckets",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolutionSeconds = table.Column<int>(type: "integer", nullable: false),
                    BucketStart = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    CpuCount = table.Column<int>(type: "integer", nullable: false),
                    CpuSum = table.Column<double>(type: "double precision", nullable: false),
                    CpuMin = table.Column<double>(type: "double precision", nullable: true),
                    CpuMax = table.Column<double>(type: "double precision", nullable: true),
                    MemoryCount = table.Column<int>(type: "integer", nullable: false),
                    MemoryUsedSum = table.Column<long>(type: "bigint", nullable: false),
                    MemoryUsedMin = table.Column<long>(type: "bigint", nullable: true),
                    MemoryUsedMax = table.Column<long>(type: "bigint", nullable: true),
                    MemoryTotalBytes = table.Column<long>(type: "bigint", nullable: true),
                    PlayersCount = table.Column<int>(type: "integer", nullable: false),
                    PlayersSum = table.Column<long>(type: "bigint", nullable: false),
                    PlayersMin = table.Column<int>(type: "integer", nullable: true),
                    PlayersMax = table.Column<int>(type: "integer", nullable: true),
                    PlayerCapacity = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerMetricBuckets", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ServerMetricBuckets_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServerMetricSnapshots",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentFk = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    WorkerSampledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CpuPercent = table.Column<double>(type: "double precision", nullable: true),
                    CpuUnavailableReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MemoryUsedBytes = table.Column<long>(type: "bigint", nullable: true),
                    MemoryTotalBytes = table.Column<long>(type: "bigint", nullable: true),
                    MemoryUnavailableReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PlayerCount = table.Column<int>(type: "integer", nullable: true),
                    PlayerCapacity = table.Column<int>(type: "integer", nullable: true),
                    PlayersUnavailableReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerMetricSnapshots", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ServerMetricSnapshots_GameServerDeployments_DeploymentFk",
                        column: x => x.DeploymentFk,
                        principalTable: "GameServerDeployments",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServerMetricSnapshots_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServerMetricSnapshots_LifecycleWorkers_WorkerFk",
                        column: x => x.WorkerFk,
                        principalTable: "LifecycleWorkers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServerMetricBuckets_GameServerFk_ResolutionSeconds_BucketSt~",
                table: "ServerMetricBuckets",
                columns: new[] { "GameServerFk", "ResolutionSeconds", "BucketStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerMetricBuckets_ResolutionSeconds_BucketStart",
                table: "ServerMetricBuckets",
                columns: new[] { "ResolutionSeconds", "BucketStart" });

            migrationBuilder.CreateIndex(
                name: "IX_ServerMetricSnapshots_DeploymentFk",
                table: "ServerMetricSnapshots",
                column: "DeploymentFk");

            migrationBuilder.CreateIndex(
                name: "IX_ServerMetricSnapshots_GameServerFk",
                table: "ServerMetricSnapshots",
                column: "GameServerFk",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerMetricSnapshots_WorkerFk",
                table: "ServerMetricSnapshots",
                column: "WorkerFk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerMetricBuckets");

            migrationBuilder.DropTable(
                name: "ServerMetricSnapshots");
        }
    }
}
