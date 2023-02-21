using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddingUserModPackMeta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserModPackMetas",
                columns: table => new
                {
                    UserFk = table.Column<Guid>(type: "uuid", nullable: false),
                    ModPackFk = table.Column<long>(type: "bigint", nullable: false),
                    TimeStampDownloaded = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModPackMetas", x => new { x.UserFk, x.ModPackFk });
                    table.ForeignKey(
                        name: "FK_UserModPackMetas_ModPacks_ModPackFk",
                        column: x => x.ModPackFk,
                        principalTable: "ModPacks",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserModPackMetas_Users_UserFk",
                        column: x => x.UserFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserModPackMetas_ModPackFk",
                table: "UserModPackMetas",
                column: "ModPackFk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserModPackMetas");
        }
    }
}
