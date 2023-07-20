using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddingSupportForCompositeModPacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsComposition",
                table: "ModPackDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ModPackDefinitionModPackRevision",
                columns: table => new
                {
                    ModPackDefinitionsPrimaryKey = table.Column<long>(type: "bigint", nullable: false),
                    ModPackRevisionsPrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModPackDefinitionModPackRevision", x => new { x.ModPackDefinitionsPrimaryKey, x.ModPackRevisionsPrimaryKey });
                    table.ForeignKey(
                        name: "FK_ModPackDefinitionModPackRevision_ModPackDefinitions_ModPack~",
                        column: x => x.ModPackDefinitionsPrimaryKey,
                        principalTable: "ModPackDefinitions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModPackDefinitionModPackRevision_ModPackRevisions_ModPackRe~",
                        column: x => x.ModPackRevisionsPrimaryKey,
                        principalTable: "ModPackRevisions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModPackDefinitionModPackRevision_ModPackRevisionsPrimaryKey",
                table: "ModPackDefinitionModPackRevision",
                column: "ModPackRevisionsPrimaryKey");
            migrationBuilder.Sql(
                """
INSERT INTO "ModPackDefinitionModPackRevision" ("ModPackDefinitionsPrimaryKey", "ModPackRevisionsPrimaryKey")
SELECT "DefinitionFk", "PrimaryKey" FROM "ModPackRevisions" WHERE "IsActive";
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModPackDefinitionModPackRevision");

            migrationBuilder.DropColumn(
                name: "IsComposition",
                table: "ModPackDefinitions");
        }
    }
}
