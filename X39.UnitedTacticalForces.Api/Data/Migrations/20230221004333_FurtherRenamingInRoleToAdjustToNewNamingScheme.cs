using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FurtherRenamingInRoleToAdjustToNewNamingScheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClaimCode",
                table: "Privileges",
                newName: "Identifier");

            migrationBuilder.RenameIndex(
                name: "IX_Privileges_ClaimCode",
                table: "Privileges",
                newName: "IX_Privileges_Identifier");

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "Category", "Identifier", "Title" },
                values: new object[] { "General", "admin", "Admin" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "Identifier", "Title" },
                values: new object[] { "event-create", "Events erstellen" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 3L,
                columns: new[] { "Identifier", "Title" },
                values: new object[] { "event-modify", "Alle events bearbeiten" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 4L,
                columns: new[] { "Category", "Identifier", "Title" },
                values: new object[] { "Events", "event-delete", "Alle events löschen" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 5L,
                columns: new[] { "Identifier", "Title" },
                values: new object[] { "terrain-create", "Terrain anlegen" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 6L,
                columns: new[] { "Identifier", "Title" },
                values: new object[] { "terrain-modify", "Terrain bearbeiten" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 7L,
                columns: new[] { "Category", "Identifier", "Title" },
                values: new object[] { "Terrains", "terrain-delete", "Terrain löschen" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 8L,
                columns: new[] { "Identifier", "Title" },
                values: new object[] { "modpack-create", "ModPack anlegen" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 9L,
                columns: new[] { "Identifier", "Title" },
                values: new object[] { "modpack-modify", "ModPack bearbeiten" });

            migrationBuilder.InsertData(
                table: "Privileges",
                columns: new[] { "Id", "Category", "Identifier", "Title" },
                values: new object[] { 10L, "ModPacks", "modpack-delete", "ModPack löschen" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 10L);

            migrationBuilder.RenameColumn(
                name: "Identifier",
                table: "Privileges",
                newName: "ClaimCode");

            migrationBuilder.RenameIndex(
                name: "IX_Privileges_Identifier",
                table: "Privileges",
                newName: "IX_Privileges_ClaimCode");

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "Category", "ClaimCode", "Title" },
                values: new object[] { "Events", "event-create", "Events erstellen" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "ClaimCode", "Title" },
                values: new object[] { "event-modify", "Alle events bearbeiten" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 3L,
                columns: new[] { "ClaimCode", "Title" },
                values: new object[] { "event-delete", "Alle events löschen" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 4L,
                columns: new[] { "Category", "ClaimCode", "Title" },
                values: new object[] { "Terrains", "terrain-create", "Terrain anlegen" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 5L,
                columns: new[] { "ClaimCode", "Title" },
                values: new object[] { "terrain-modify", "Terrain bearbeiten" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 6L,
                columns: new[] { "ClaimCode", "Title" },
                values: new object[] { "terrain-delete", "Terrain löschen" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 7L,
                columns: new[] { "Category", "ClaimCode", "Title" },
                values: new object[] { "ModPacks", "modpack-create", "ModPack anlegen" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 8L,
                columns: new[] { "ClaimCode", "Title" },
                values: new object[] { "modpack-modify", "ModPack bearbeiten" });

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 9L,
                columns: new[] { "ClaimCode", "Title" },
                values: new object[] { "modpack-delete", "ModPack löschen" });
        }
    }
}
