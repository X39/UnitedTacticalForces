using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddingUserInteractionDataToEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Users_CreatedByFk",
                table: "Events");

            migrationBuilder.RenameColumn(
                name: "CreatedByFk",
                table: "Events",
                newName: "OwnerFk");

            migrationBuilder.RenameIndex(
                name: "IX_Events_CreatedByFk",
                table: "Events",
                newName: "IX_Events_OwnerFk");

            migrationBuilder.AddColumn<int>(
                name: "AcceptedCount",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaybeCount",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumAccepted",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RejectedCount",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "UserEventMetas",
                columns: table => new
                {
                    UserFk = table.Column<Guid>(type: "uuid", nullable: false),
                    EventFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Acceptance = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEventMetas", x => new { x.UserFk, x.EventFk });
                    table.ForeignKey(
                        name: "FK_UserEventMetas_Events_EventFk",
                        column: x => x.EventFk,
                        principalTable: "Events",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserEventMetas_Users_UserFk",
                        column: x => x.UserFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_ScheduledFor",
                table: "Events",
                column: "ScheduledFor");

            migrationBuilder.CreateIndex(
                name: "IX_UserEventMetas_EventFk",
                table: "UserEventMetas",
                column: "EventFk");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Users_OwnerFk",
                table: "Events",
                column: "OwnerFk",
                principalTable: "Users",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Users_OwnerFk",
                table: "Events");

            migrationBuilder.DropTable(
                name: "UserEventMetas");

            migrationBuilder.DropIndex(
                name: "IX_Events_ScheduledFor",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "AcceptedCount",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "MaybeCount",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "MinimumAccepted",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RejectedCount",
                table: "Events");

            migrationBuilder.RenameColumn(
                name: "OwnerFk",
                table: "Events",
                newName: "CreatedByFk");

            migrationBuilder.RenameIndex(
                name: "IX_Events_OwnerFk",
                table: "Events",
                newName: "IX_Events_CreatedByFk");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Users_CreatedByFk",
                table: "Events",
                column: "CreatedByFk",
                principalTable: "Users",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
