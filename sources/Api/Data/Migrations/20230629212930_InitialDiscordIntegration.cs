using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialDiscordIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SteamId64",
                table: "Users",
                newName: "Steam_Id64");

            migrationBuilder.RenameIndex(
                name: "IX_Users_SteamId64",
                table: "Users",
                newName: "IX_Users_Steam_Id64");

            migrationBuilder.AddColumn<decimal>(
                name: "Discord_Id",
                table: "Users",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Discord_Username",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "PrimaryKey", "Category", "Identifier", "Title" },
                values: new object[] { 33L, "User", "user-view-discord", "View Discord data of user" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 33L);

            migrationBuilder.DropColumn(
                name: "Discord_Id",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Discord_Username",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "Steam_Id64",
                table: "Users",
                newName: "SteamId64");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Steam_Id64",
                table: "Users",
                newName: "IX_Users_SteamId64");
        }
    }
}
