using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddModPackFormatDeliveryEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModPackFormatDeliveries",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    ModPackRevisionFk = table.Column<long>(type: "bigint", nullable: false),
                    FormatIdentifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EventFk = table.Column<Guid>(type: "uuid", nullable: true),
                    UserFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModPackFormatDeliveries", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ModPackFormatDeliveries_Events_EventFk",
                        column: x => x.EventFk,
                        principalTable: "Events",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ModPackFormatDeliveries_ModPackRevisions_ModPackRevisionFk",
                        column: x => x.ModPackRevisionFk,
                        principalTable: "ModPackRevisions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModPackFormatDeliveries_Users_UserFk",
                        column: x => x.UserFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModPackFormatDeliveries_EventFk_TimeStampCreated",
                table: "ModPackFormatDeliveries",
                columns: new[] { "EventFk", "TimeStampCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_ModPackFormatDeliveries_ModPackRevisionFk_TimeStampCreated",
                table: "ModPackFormatDeliveries",
                columns: new[] { "ModPackRevisionFk", "TimeStampCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_ModPackFormatDeliveries_UserFk",
                table: "ModPackFormatDeliveries",
                column: "UserFk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModPackFormatDeliveries");
        }
    }
}
