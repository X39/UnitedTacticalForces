using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableLocalGamePort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AllocatedLocalPort",
                table: "GameServers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_AllocatedLocalPort_Unique",
                table: "GameServers",
                column: "AllocatedLocalPort",
                unique: true,
                filter: "\"AllocatedLocalPort\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GameServers_AllocatedLocalPort",
                table: "GameServers",
                sql: "\"AllocatedLocalPort\" IS NULL OR \"AllocatedLocalPort\" BETWEEN 1 AND 65535");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameServers_AllocatedLocalPort_Unique",
                table: "GameServers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GameServers_AllocatedLocalPort",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "AllocatedLocalPort",
                table: "GameServers");
        }
    }
}
