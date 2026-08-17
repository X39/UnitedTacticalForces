using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class MakeEventAssetsOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Terrains_TerrainFk",
                table: "Events");

            migrationBuilder.AlterColumn<long>(
                name: "TerrainFk",
                table: "Events",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<Guid>(
                name: "ImageAssetFk",
                table: "Events",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Terrains_TerrainFk",
                table: "Events",
                column: "TerrainFk",
                principalTable: "Terrains",
                principalColumn: "PrimaryKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Terrains_TerrainFk",
                table: "Events");

            migrationBuilder.AlterColumn<long>(
                name: "TerrainFk",
                table: "Events",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ImageAssetFk",
                table: "Events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Terrains_TerrainFk",
                table: "Events",
                column: "TerrainFk",
                principalTable: "Terrains",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
