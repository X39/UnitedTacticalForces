using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddingEventSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventSlots",
                columns: table => new
                {
                    SlotNumber = table.Column<short>(type: "smallint", nullable: false),
                    EventFk = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToFk = table.Column<Guid>(type: "uuid", nullable: true),
                    IsSelfAssignable = table.Column<bool>(type: "boolean", nullable: false),
                    Group = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSlots", x => new { x.EventFk, x.SlotNumber });
                    table.ForeignKey(
                        name: "FK_EventSlots_Events_EventFk",
                        column: x => x.EventFk,
                        principalTable: "Events",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventSlots_Users_AssignedToFk",
                        column: x => x.AssignedToFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Nickname",
                table: "Users",
                column: "Nickname");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SteamId64",
                table: "Users",
                column: "SteamId64",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventSlots_AssignedToFk",
                table: "EventSlots",
                column: "AssignedToFk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventSlots");

            migrationBuilder.DropIndex(
                name: "IX_Users_Nickname",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_SteamId64",
                table: "Users");
        }
    }
}
