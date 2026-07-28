using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class ScopeTerrainsAndModPacksToGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GameIdentifier",
                table: "Terrains",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameIdentifier",
                table: "ModPackDefinitions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql("UPDATE \"Terrains\" SET \"GameIdentifier\" = 'arma3';");
            migrationBuilder.Sql("UPDATE \"ModPackDefinitions\" SET \"GameIdentifier\" = 'arma3';");

            migrationBuilder.AlterColumn<string>(
                name: "GameIdentifier",
                table: "Terrains",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GameIdentifier",
                table: "ModPackDefinitions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Terrains_GameIdentifier",
                table: "Terrains",
                column: "GameIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_ModPackDefinitions_GameIdentifier",
                table: "ModPackDefinitions",
                column: "GameIdentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Terrains_GameIdentifier",
                table: "Terrains");

            migrationBuilder.DropIndex(
                name: "IX_ModPackDefinitions_GameIdentifier",
                table: "ModPackDefinitions");

            migrationBuilder.DropColumn(
                name: "GameIdentifier",
                table: "Terrains");

            migrationBuilder.DropColumn(
                name: "GameIdentifier",
                table: "ModPackDefinitions");
        }
    }
}
