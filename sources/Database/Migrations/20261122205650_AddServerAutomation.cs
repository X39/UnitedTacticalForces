using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddServerAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CostAlerts_DeploymentFk_ThresholdAmount",
                table: "CostAlerts");

            migrationBuilder.AddColumn<int>(
                name: "Trigger",
                table: "LifecycleJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TriggerLimit",
                table: "LifecycleJobs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "CostAlerts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingStopFk",
                table: "CostAlerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServerAutomationSettings",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    IdleShutdownEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IdleShutdownMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxRuntimeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaxRuntimeHours = table.Column<int>(type: "integer", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    StartConsentCostOwnerFk = table.Column<Guid>(type: "uuid", nullable: true),
                    StartConsentByFk = table.Column<Guid>(type: "uuid", nullable: true),
                    StartConsentAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    StartConsentHourlyEstimateEur = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    UpdatedByFk = table.Column<Guid>(type: "uuid", nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerAutomationSettings", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ServerAutomationSettings_CostOwners_StartConsentCostOwnerFk",
                        column: x => x.StartConsentCostOwnerFk,
                        principalTable: "CostOwners",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServerAutomationSettings_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServerAutomationSettings_Users_StartConsentByFk",
                        column: x => x.StartConsentByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServerAutomationSettings_Users_UpdatedByFk",
                        column: x => x.UpdatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServerAutomationStates",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    IdleDeploymentFk = table.Column<Guid>(type: "uuid", nullable: true),
                    IdleSince = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    IdleHeldSince = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    SchedulesEvaluatedThrough = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerAutomationStates", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ServerAutomationStates_GameServerDeployments_IdleDeployment~",
                        column: x => x.IdleDeploymentFk,
                        principalTable: "GameServerDeployments",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServerAutomationStates_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServerSchedules",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingsFk = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    DaysMask = table.Column<int>(type: "integer", nullable: false),
                    MinuteOfDay = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerSchedules", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ServerSchedules_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServerSchedules_ServerAutomationSettings_SettingsFk",
                        column: x => x.SettingsFk,
                        principalTable: "ServerAutomationSettings",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AutomationPendingStops",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Limit = table.Column<int>(type: "integer", nullable: true),
                    ScheduleFk = table.Column<Guid>(type: "uuid", nullable: true),
                    ScheduleOccurrenceAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    WarnedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    StopsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LifecycleJobFk = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationPendingStops", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_AutomationPendingStops_GameServerDeployments_DeploymentFk",
                        column: x => x.DeploymentFk,
                        principalTable: "GameServerDeployments",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationPendingStops_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationPendingStops_LifecycleJobs_LifecycleJobFk",
                        column: x => x.LifecycleJobFk,
                        principalTable: "LifecycleJobs",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationPendingStops_ServerSchedules_ScheduleFk",
                        column: x => x.ScheduleFk,
                        principalTable: "ServerSchedules",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ServerScheduleRuns",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleFk = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurrenceAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    LifecycleJobFk = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RecordedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerScheduleRuns", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ServerScheduleRuns_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServerScheduleRuns_LifecycleJobs_LifecycleJobFk",
                        column: x => x.LifecycleJobFk,
                        principalTable: "LifecycleJobs",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServerScheduleRuns_ServerSchedules_ScheduleFk",
                        column: x => x.ScheduleFk,
                        principalTable: "ServerSchedules",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostAlerts_DeploymentFk_ThresholdAmount",
                table: "CostAlerts",
                columns: new[] { "DeploymentFk", "ThresholdAmount" },
                unique: true,
                filter: "\"Kind\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CostAlerts_PendingStopFk",
                table: "CostAlerts",
                column: "PendingStopFk",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationPendingStops_DeploymentFk_Pending",
                table: "AutomationPendingStops",
                column: "DeploymentFk",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationPendingStops_GameServerFk",
                table: "AutomationPendingStops",
                column: "GameServerFk");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationPendingStops_LifecycleJobFk",
                table: "AutomationPendingStops",
                column: "LifecycleJobFk");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationPendingStops_ScheduleFk",
                table: "AutomationPendingStops",
                column: "ScheduleFk");

            migrationBuilder.CreateIndex(
                name: "IX_ServerAutomationSettings_GameServerFk",
                table: "ServerAutomationSettings",
                column: "GameServerFk",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerAutomationSettings_StartConsentByFk",
                table: "ServerAutomationSettings",
                column: "StartConsentByFk");

            migrationBuilder.CreateIndex(
                name: "IX_ServerAutomationSettings_StartConsentCostOwnerFk",
                table: "ServerAutomationSettings",
                column: "StartConsentCostOwnerFk");

            migrationBuilder.CreateIndex(
                name: "IX_ServerAutomationSettings_UpdatedByFk",
                table: "ServerAutomationSettings",
                column: "UpdatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_ServerAutomationStates_GameServerFk",
                table: "ServerAutomationStates",
                column: "GameServerFk",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerAutomationStates_IdleDeploymentFk",
                table: "ServerAutomationStates",
                column: "IdleDeploymentFk");

            migrationBuilder.CreateIndex(
                name: "IX_ServerScheduleRuns_GameServerFk_OccurrenceAt",
                table: "ServerScheduleRuns",
                columns: new[] { "GameServerFk", "OccurrenceAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServerScheduleRuns_LifecycleJobFk",
                table: "ServerScheduleRuns",
                column: "LifecycleJobFk");

            migrationBuilder.CreateIndex(
                name: "IX_ServerScheduleRuns_ScheduleFk_OccurrenceAt",
                table: "ServerScheduleRuns",
                columns: new[] { "ScheduleFk", "OccurrenceAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerSchedules_GameServerFk",
                table: "ServerSchedules",
                column: "GameServerFk");

            migrationBuilder.CreateIndex(
                name: "IX_ServerSchedules_SettingsFk_Position",
                table: "ServerSchedules",
                columns: new[] { "SettingsFk", "Position" });

            migrationBuilder.AddForeignKey(
                name: "FK_CostAlerts_AutomationPendingStops_PendingStopFk",
                table: "CostAlerts",
                column: "PendingStopFk",
                principalTable: "AutomationPendingStops",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CostAlerts_AutomationPendingStops_PendingStopFk",
                table: "CostAlerts");

            migrationBuilder.DropTable(
                name: "AutomationPendingStops");

            migrationBuilder.DropTable(
                name: "ServerAutomationStates");

            migrationBuilder.DropTable(
                name: "ServerScheduleRuns");

            migrationBuilder.DropTable(
                name: "ServerSchedules");

            migrationBuilder.DropTable(
                name: "ServerAutomationSettings");

            migrationBuilder.DropIndex(
                name: "IX_CostAlerts_DeploymentFk_ThresholdAmount",
                table: "CostAlerts");

            migrationBuilder.DropIndex(
                name: "IX_CostAlerts_PendingStopFk",
                table: "CostAlerts");

            migrationBuilder.DropColumn(
                name: "Trigger",
                table: "LifecycleJobs");

            migrationBuilder.DropColumn(
                name: "TriggerLimit",
                table: "LifecycleJobs");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "CostAlerts");

            migrationBuilder.DropColumn(
                name: "PendingStopFk",
                table: "CostAlerts");

            migrationBuilder.CreateIndex(
                name: "IX_CostAlerts_DeploymentFk_ThresholdAmount",
                table: "CostAlerts",
                columns: new[] { "DeploymentFk", "ThresholdAmount" },
                unique: true);
        }
    }
}
