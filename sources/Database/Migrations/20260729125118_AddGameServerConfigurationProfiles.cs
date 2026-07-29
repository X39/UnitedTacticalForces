using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddGameServerConfigurationProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PinnedConfigurationProfileRevisionFk",
                table: "GameServers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SelectedConfigurationProfileFk",
                table: "GameServers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConfigurationProfiles",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    GameIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampArchived = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationProfiles", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ConfigurationProfiles_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameServerConfigurationValues",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ValueJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedByFk = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServerConfigurationValues", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_GameServerConfigurationValues_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameServerConfigurationValues_Users_UpdatedByFk",
                        column: x => x.UpdatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationProfileRevisions",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationProfileFk = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationProfileRevisions", x => x.PrimaryKey);
                    table.CheckConstraint("CK_ConfigurationProfileRevisions_RevisionNumber", "\"RevisionNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_ConfigurationProfileRevisions_ConfigurationProfiles_Configu~",
                        column: x => x.ConfigurationProfileFk,
                        principalTable: "ConfigurationProfiles",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfigurationProfileRevisions_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationProfileValues",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationProfileRevisionFk = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ValueJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationProfileValues", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ConfigurationProfileValues_ConfigurationProfileRevisions_Co~",
                        column: x => x.ConfigurationProfileRevisionFk,
                        principalTable: "ConfigurationProfileRevisions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Claims",
                columns: new[] { "PrimaryKey", "Category", "Description", "Identifier", "IsPrefix", "Title", "Value", "ValueType" },
                values: new object[,]
                {
                    { -63L, "Resource", "Resource claim for archiving configuration profiles.", "cfg:archive", true, "Archive configuration profile", null, null },
                    { -62L, "Resource", "Resource claim for managing configuration-profile access.", "cfg:manage-access", true, "Manage configuration-profile access", null, null },
                    { -61L, "Resource", "Resource claim for modifying a configuration profile.", "cfg:modify", true, "Modify configuration profile", null, null },
                    { -60L, "Resource", "Resource claim for viewing a configuration profile.", "cfg:view", true, "View configuration profile", null, null },
                    { -59L, "Resource", "Resource claim for all actions on a configuration profile.", "cfg", true, "Configuration-profile access", null, null },
                    { -58L, "Creation", "Allows creating configuration profiles.", "crt:cfg", false, "Create configuration profiles", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_PinnedConfigurationProfileRevisionFk",
                table: "GameServers",
                column: "PinnedConfigurationProfileRevisionFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_SelectedConfigurationProfileFk",
                table: "GameServers",
                column: "SelectedConfigurationProfileFk");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationProfileRevisions_ConfigurationProfileFk_Revisi~",
                table: "ConfigurationProfileRevisions",
                columns: new[] { "ConfigurationProfileFk", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationProfileRevisions_CreatedByFk",
                table: "ConfigurationProfileRevisions",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationProfiles_CreatedByFk",
                table: "ConfigurationProfiles",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationProfiles_GameIdentifier_IsArchived",
                table: "ConfigurationProfiles",
                columns: new[] { "GameIdentifier", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationProfileValues_ConfigurationProfileRevisionFk_D~",
                table: "ConfigurationProfileValues",
                columns: new[] { "ConfigurationProfileRevisionFk", "DefinitionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameServerConfigurationValues_GameServerFk_DefinitionKey",
                table: "GameServerConfigurationValues",
                columns: new[] { "GameServerFk", "DefinitionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameServerConfigurationValues_UpdatedByFk",
                table: "GameServerConfigurationValues",
                column: "UpdatedByFk");

            migrationBuilder.AddForeignKey(
                name: "FK_GameServers_ConfigurationProfileRevisions_PinnedConfigurati~",
                table: "GameServers",
                column: "PinnedConfigurationProfileRevisionFk",
                principalTable: "ConfigurationProfileRevisions",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameServers_ConfigurationProfiles_SelectedConfigurationProf~",
                table: "GameServers",
                column: "SelectedConfigurationProfileFk",
                principalTable: "ConfigurationProfiles",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameServers_ConfigurationProfileRevisions_PinnedConfigurati~",
                table: "GameServers");

            migrationBuilder.DropForeignKey(
                name: "FK_GameServers_ConfigurationProfiles_SelectedConfigurationProf~",
                table: "GameServers");

            migrationBuilder.DropTable(
                name: "ConfigurationProfileValues");

            migrationBuilder.DropTable(
                name: "GameServerConfigurationValues");

            migrationBuilder.DropTable(
                name: "ConfigurationProfileRevisions");

            migrationBuilder.DropTable(
                name: "ConfigurationProfiles");

            migrationBuilder.DropIndex(
                name: "IX_GameServers_PinnedConfigurationProfileRevisionFk",
                table: "GameServers");

            migrationBuilder.DropIndex(
                name: "IX_GameServers_SelectedConfigurationProfileFk",
                table: "GameServers");

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -63L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -62L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -61L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -60L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -59L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -58L);

            migrationBuilder.DropColumn(
                name: "PinnedConfigurationProfileRevisionFk",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "SelectedConfigurationProfileFk",
                table: "GameServers");
        }
    }
}
