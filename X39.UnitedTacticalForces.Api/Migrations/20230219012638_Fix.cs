using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Migrations
{
    /// <inheritdoc />
    public partial class Fix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_ModPack_ModPackFk",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Terrain_TerrainFk",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_ModPack_Users_OwnerFk",
                table: "ModPack");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Terrain",
                table: "Terrain");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ModPack",
                table: "ModPack");

            migrationBuilder.RenameTable(
                name: "Terrain",
                newName: "Terrains");

            migrationBuilder.RenameTable(
                name: "ModPack",
                newName: "ModPacks");

            migrationBuilder.RenameIndex(
                name: "IX_ModPack_OwnerFk",
                table: "ModPacks",
                newName: "IX_ModPacks_OwnerFk");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Terrains",
                table: "Terrains",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ModPacks",
                table: "ModPacks",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 7L,
                column: "ClaimCode",
                value: "modpack-create");

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 8L,
                column: "ClaimCode",
                value: "modpack-modify");

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 9L,
                column: "ClaimCode",
                value: "modpack-delete");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_ModPacks_ModPackFk",
                table: "Events",
                column: "ModPackFk",
                principalTable: "ModPacks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Terrains_TerrainFk",
                table: "Events",
                column: "TerrainFk",
                principalTable: "Terrains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModPacks_Users_OwnerFk",
                table: "ModPacks",
                column: "OwnerFk",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_ModPacks_ModPackFk",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Terrains_TerrainFk",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_ModPacks_Users_OwnerFk",
                table: "ModPacks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Terrains",
                table: "Terrains");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ModPacks",
                table: "ModPacks");

            migrationBuilder.RenameTable(
                name: "Terrains",
                newName: "Terrain");

            migrationBuilder.RenameTable(
                name: "ModPacks",
                newName: "ModPack");

            migrationBuilder.RenameIndex(
                name: "IX_ModPacks_OwnerFk",
                table: "ModPack",
                newName: "IX_ModPack_OwnerFk");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Terrain",
                table: "Terrain",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ModPack",
                table: "ModPack",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 7L,
                column: "ClaimCode",
                value: "terrain-create");

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 8L,
                column: "ClaimCode",
                value: "terrain-modify");

            migrationBuilder.UpdateData(
                table: "Privileges",
                keyColumn: "Id",
                keyValue: 9L,
                column: "ClaimCode",
                value: "terrain-delete");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_ModPack_ModPackFk",
                table: "Events",
                column: "ModPackFk",
                principalTable: "ModPack",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Terrain_TerrainFk",
                table: "Events",
                column: "TerrainFk",
                principalTable: "Terrain",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModPack_Users_OwnerFk",
                table: "ModPack",
                column: "OwnerFk",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
