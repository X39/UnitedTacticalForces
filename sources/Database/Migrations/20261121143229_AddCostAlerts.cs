using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCostAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Locale",
                table: "Users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CostAlerts",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentFk = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    CostOwnerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AccruedAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostAlerts", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_CostAlerts_CostOwners_CostOwnerFk",
                        column: x => x.CostOwnerFk,
                        principalTable: "CostOwners",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostAlerts_GameServerDeployments_DeploymentFk",
                        column: x => x.DeploymentFk,
                        principalTable: "GameServerDeployments",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostAlerts_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostAlertSettings",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CostOwnerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscordEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    UpdatedByFk = table.Column<Guid>(type: "uuid", nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostAlertSettings", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_CostAlertSettings_CostOwners_CostOwnerFk",
                        column: x => x.CostOwnerFk,
                        principalTable: "CostOwners",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostAlertSettings_Users_UpdatedByFk",
                        column: x => x.UpdatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostAlertDeliveries",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CostAlertFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    RecipientUserFk = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LeaseExpires = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampNextAttempt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    StatusReason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampSent = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostAlertDeliveries", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_CostAlertDeliveries_CostAlerts_CostAlertFk",
                        column: x => x.CostAlertFk,
                        principalTable: "CostAlerts",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostAlertDeliveries_Users_RecipientUserFk",
                        column: x => x.RecipientUserFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostAlertDismissals",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CostAlertFk = table.Column<Guid>(type: "uuid", nullable: false),
                    UserFk = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostAlertDismissals", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_CostAlertDismissals_CostAlerts_CostAlertFk",
                        column: x => x.CostAlertFk,
                        principalTable: "CostAlerts",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostAlertDismissals_Users_UserFk",
                        column: x => x.UserFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostAlertThresholds",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostAlertThresholds", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_CostAlertThresholds_CostAlertSettings_SettingFk",
                        column: x => x.SettingFk,
                        principalTable: "CostAlertSettings",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostAlertDeliveries_CostAlertFk_Channel_RecipientUserFk",
                table: "CostAlertDeliveries",
                columns: new[] { "CostAlertFk", "Channel", "RecipientUserFk" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostAlertDeliveries_RecipientUserFk",
                table: "CostAlertDeliveries",
                column: "RecipientUserFk");

            migrationBuilder.CreateIndex(
                name: "IX_CostAlertDeliveries_Status_TimeStampNextAttempt",
                table: "CostAlertDeliveries",
                columns: new[] { "Status", "TimeStampNextAttempt" });

            migrationBuilder.CreateIndex(
                name: "IX_CostAlertDismissals_CostAlertFk_UserFk",
                table: "CostAlertDismissals",
                columns: new[] { "CostAlertFk", "UserFk" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostAlertDismissals_UserFk",
                table: "CostAlertDismissals",
                column: "UserFk");

            migrationBuilder.CreateIndex(
                name: "IX_CostAlerts_CostOwnerFk_TimeStampCreated",
                table: "CostAlerts",
                columns: new[] { "CostOwnerFk", "TimeStampCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_CostAlerts_DeploymentFk_ThresholdAmount",
                table: "CostAlerts",
                columns: new[] { "DeploymentFk", "ThresholdAmount" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostAlerts_GameServerFk",
                table: "CostAlerts",
                column: "GameServerFk");

            migrationBuilder.CreateIndex(
                name: "IX_CostAlertSettings_CostOwnerFk",
                table: "CostAlertSettings",
                column: "CostOwnerFk",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostAlertSettings_UpdatedByFk",
                table: "CostAlertSettings",
                column: "UpdatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_CostAlertThresholds_SettingFk_Amount",
                table: "CostAlertThresholds",
                columns: new[] { "SettingFk", "Amount" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostAlertDeliveries");

            migrationBuilder.DropTable(
                name: "CostAlertDismissals");

            migrationBuilder.DropTable(
                name: "CostAlertThresholds");

            migrationBuilder.DropTable(
                name: "CostAlerts");

            migrationBuilder.DropTable(
                name: "CostAlertSettings");

            migrationBuilder.DropColumn(
                name: "Locale",
                table: "Users");
        }
    }
}
