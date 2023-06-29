using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddingNewRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "PrimaryKey", "Category", "Identifier", "Title" },
                values: new object[,]
                {
                    { 31L, "Server", "server-logs", "Server access logs" },
                    { 32L, "Server", "server-logs-clear", "Server clear logs" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 31L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 32L);
        }
    }
}
