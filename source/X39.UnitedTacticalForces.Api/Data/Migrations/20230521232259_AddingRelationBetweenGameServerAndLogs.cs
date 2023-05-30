using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddingRelationBetweenGameServerAndLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "GameServerFk",
                table: "GameServerLogs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_GameServerLogs_GameServerFk",
                table: "GameServerLogs",
                column: "GameServerFk");

            migrationBuilder.Sql(@"DELETE FROM ""GameServerLogs"";");

            migrationBuilder.AddForeignKey(
                name: "FK_GameServerLogs_GameServers_GameServerFk",
                table: "GameServerLogs",
                column: "GameServerFk",
                principalTable: "GameServers",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameServerLogs_GameServers_GameServerFk",
                table: "GameServerLogs");

            migrationBuilder.DropIndex(
                name: "IX_GameServerLogs_GameServerFk",
                table: "GameServerLogs");

            migrationBuilder.DropColumn(
                name: "GameServerFk",
                table: "GameServerLogs");
        }
    }
}
