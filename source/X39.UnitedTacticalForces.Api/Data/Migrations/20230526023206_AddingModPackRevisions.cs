using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddingModPackRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_ModPacks_ModPackFk",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_GameServers_ModPacks_ActiveModPackFk",
                table: "GameServers");

            migrationBuilder.DropForeignKey(
                name: "FK_GameServers_ModPacks_SelectedModPackFk",
                table: "GameServers");

            migrationBuilder.DropForeignKey(
                name: "FK_UserModPackMetas_ModPacks_ModPackFk",
                table: "UserModPackMetas");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserModPackMetas",
                table: "UserModPackMetas");

            migrationBuilder.RenameColumn(
                name: "ModPackFk",
                table: "UserModPackMetas",
                newName: "ModPackRevisionFk");

            migrationBuilder.RenameIndex(
                name: "IX_UserModPackMetas_ModPackFk",
                table: "UserModPackMetas",
                newName: "IX_UserModPackMetas_ModPackRevisionFk");

            migrationBuilder.RenameColumn(
                name: "ModPackFk",
                table: "Events",
                newName: "ModPackRevisionFk");

            migrationBuilder.RenameIndex(
                name: "IX_Events_ModPackFk",
                table: "Events",
                newName: "IX_Events_ModPackRevisionFk");

            migrationBuilder.AddColumn<long>(
                name: "ModPackDefinitionFk",
                table: "UserModPackMetas",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserModPackMetas",
                table: "UserModPackMetas",
                columns: new[] { "UserFk", "ModPackDefinitionFk", "ModPackRevisionFk" });

            migrationBuilder.CreateTable(
                name: "ModPackDefinitions",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimeStampCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    OwnerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModPackDefinitions", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ModPackDefinitions_Users_OwnerFk",
                        column: x => x.OwnerFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModPackRevisions",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimeStampCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Html = table.Column<string>(type: "text", nullable: false),
                    UpdatedByFk = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DefinitionFk = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModPackRevisions", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ModPackRevisions_ModPackDefinitions_DefinitionFk",
                        column: x => x.DefinitionFk,
                        principalTable: "ModPackDefinitions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModPackRevisions_Users_UpdatedByFk",
                        column: x => x.UpdatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "ModPackDefinitions" ("TimeStampCreated", "Title", "OwnerFk", "IsActive")
                SELECT "TimeStampCreated", "Title", "OwnerFk", "IsActive"
                FROM "ModPacks" ORDER BY "PrimaryKey";

                INSERT INTO "ModPackRevisions" ("TimeStampCreated", "Html", "UpdatedByFk", "DefinitionFk", "IsActive")
                SELECT "TimeStampCreated", "Html", "OwnerFk", "PrimaryKey", "IsActive"
                FROM "ModPacks" ORDER BY "PrimaryKey";
""");

            migrationBuilder.CreateIndex(
                name: "IX_UserModPackMetas_ModPackDefinitionFk",
                table: "UserModPackMetas",
                column: "ModPackDefinitionFk");

            migrationBuilder.CreateIndex(
                name: "IX_ModPackDefinitions_OwnerFk",
                table: "ModPackDefinitions",
                column: "OwnerFk");

            migrationBuilder.CreateIndex(
                name: "IX_ModPackRevisions_DefinitionFk",
                table: "ModPackRevisions",
                column: "DefinitionFk");

            migrationBuilder.CreateIndex(
                name: "IX_ModPackRevisions_UpdatedByFk",
                table: "ModPackRevisions",
                column: "UpdatedByFk");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_ModPackRevisions_ModPackRevisionFk",
                table: "Events",
                column: "ModPackRevisionFk",
                principalTable: "ModPackRevisions",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameServers_ModPackDefinitions_SelectedModPackFk",
                table: "GameServers",
                column: "SelectedModPackFk",
                principalTable: "ModPackDefinitions",
                principalColumn: "PrimaryKey");

            migrationBuilder.AddForeignKey(
                name: "FK_GameServers_ModPackRevisions_ActiveModPackFk",
                table: "GameServers",
                column: "ActiveModPackFk",
                principalTable: "ModPackRevisions",
                principalColumn: "PrimaryKey");

            migrationBuilder.AddForeignKey(
                name: "FK_UserModPackMetas_ModPackDefinitions_ModPackDefinitionFk",
                table: "UserModPackMetas",
                column: "ModPackDefinitionFk",
                principalTable: "ModPackDefinitions",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserModPackMetas_ModPackRevisions_ModPackRevisionFk",
                table: "UserModPackMetas",
                column: "ModPackRevisionFk",
                principalTable: "ModPackRevisions",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropTable(
                name: "ModPacks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_ModPackRevisions_ModPackRevisionFk",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_GameServers_ModPackDefinitions_SelectedModPackFk",
                table: "GameServers");

            migrationBuilder.DropForeignKey(
                name: "FK_GameServers_ModPackRevisions_ActiveModPackFk",
                table: "GameServers");

            migrationBuilder.DropForeignKey(
                name: "FK_UserModPackMetas_ModPackDefinitions_ModPackDefinitionFk",
                table: "UserModPackMetas");

            migrationBuilder.DropForeignKey(
                name: "FK_UserModPackMetas_ModPackRevisions_ModPackRevisionFk",
                table: "UserModPackMetas");

            migrationBuilder.DropTable(
                name: "ModPackRevisions");

            migrationBuilder.DropTable(
                name: "ModPackDefinitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserModPackMetas",
                table: "UserModPackMetas");

            migrationBuilder.DropIndex(
                name: "IX_UserModPackMetas_ModPackDefinitionFk",
                table: "UserModPackMetas");

            migrationBuilder.DropColumn(
                name: "ModPackDefinitionFk",
                table: "UserModPackMetas");

            migrationBuilder.RenameColumn(
                name: "ModPackRevisionFk",
                table: "UserModPackMetas",
                newName: "ModPackFk");

            migrationBuilder.RenameIndex(
                name: "IX_UserModPackMetas_ModPackRevisionFk",
                table: "UserModPackMetas",
                newName: "IX_UserModPackMetas_ModPackFk");

            migrationBuilder.RenameColumn(
                name: "ModPackRevisionFk",
                table: "Events",
                newName: "ModPackFk");

            migrationBuilder.RenameIndex(
                name: "IX_Events_ModPackRevisionFk",
                table: "Events",
                newName: "IX_Events_ModPackFk");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserModPackMetas",
                table: "UserModPackMetas",
                columns: new[] { "UserFk", "ModPackFk" });

            migrationBuilder.CreateTable(
                name: "ModPacks",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Html = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStampCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModPacks", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ModPacks_Users_OwnerFk",
                        column: x => x.OwnerFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModPacks_OwnerFk",
                table: "ModPacks",
                column: "OwnerFk");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_ModPacks_ModPackFk",
                table: "Events",
                column: "ModPackFk",
                principalTable: "ModPacks",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameServers_ModPacks_ActiveModPackFk",
                table: "GameServers",
                column: "ActiveModPackFk",
                principalTable: "ModPacks",
                principalColumn: "PrimaryKey");

            migrationBuilder.AddForeignKey(
                name: "FK_GameServers_ModPacks_SelectedModPackFk",
                table: "GameServers",
                column: "SelectedModPackFk",
                principalTable: "ModPacks",
                principalColumn: "PrimaryKey");

            migrationBuilder.AddForeignKey(
                name: "FK_UserModPackMetas_ModPacks_ModPackFk",
                table: "UserModPackMetas",
                column: "ModPackFk",
                principalTable: "ModPacks",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
