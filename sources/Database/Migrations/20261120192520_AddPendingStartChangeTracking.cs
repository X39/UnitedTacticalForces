using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingStartChangeTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PendingStartChangeAreas",
                table: "GameServers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Instant>(
                name: "PendingStartSince",
                table: "GameServers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingStartChangeAreas",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "PendingStartSince",
                table: "GameServers");
        }
    }
}
