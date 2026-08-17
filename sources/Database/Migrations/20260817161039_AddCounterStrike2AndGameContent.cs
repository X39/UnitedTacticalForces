using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCounterStrike2AndGameContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_ModPackRevisions_ModPackRevisionFk",
                table: "Events");

            migrationBuilder.AddColumn<string>(
                name: "ProtectedSecrets",
                table: "WorkerCommands",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ImageAssetFk",
                table: "Terrains",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "ProtocolVersion",
                table: "LifecycleWorkers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SupportedGameIdentifiers",
                table: "LifecycleWorkers",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "PinnedModPackRevisionFk",
                table: "GameServers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PinnedScenarioVersionFk",
                table: "GameServers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PinnedTerrainVersionFk",
                table: "GameServers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SelectedScenarioFk",
                table: "GameServers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SelectedTerrainFk",
                table: "GameServers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScenarioSnapshot",
                table: "GameServerDeployments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerrainSnapshot",
                table: "GameServerDeployments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ModPackRevisionFk",
                table: "Events",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<Guid>(
                name: "PinnedScenarioVersionFk",
                table: "Events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PinnedTerrainVersionFk",
                table: "Events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScenarioFk",
                table: "Events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentBackfillRuns",
                columns: table => new
                {
                    Identifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TimeStampCompleted = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    ImplementationVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentBackfillRuns", x => x.Identifier);
                });

            migrationBuilder.CreateTable(
                name: "GameServerSecrets",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    ProtectedValue = table.Column<string>(type: "text", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: true),
                    TimeStampRetired = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServerSecrets", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_GameServerSecrets_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameServerSecrets_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModPackRevisionDependencies",
                columns: table => new
                {
                    ParentRevisionFk = table.Column<long>(type: "bigint", nullable: false),
                    ChildDefinitionFk = table.Column<long>(type: "bigint", nullable: false),
                    PinnedChildRevisionFk = table.Column<long>(type: "bigint", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModPackRevisionDependencies", x => new { x.ParentRevisionFk, x.ChildDefinitionFk });
                    table.ForeignKey(
                        name: "FK_ModPackRevisionDependencies_ModPackDefinitions_ChildDefinit~",
                        column: x => x.ChildDefinitionFk,
                        principalTable: "ModPackDefinitions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModPackRevisionDependencies_ModPackRevisions_ParentRevision~",
                        column: x => x.ParentRevisionFk,
                        principalTable: "ModPackRevisions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModPackRevisionDependencies_ModPackRevisions_PinnedChildRev~",
                        column: x => x.PinnedChildRevisionFk,
                        principalTable: "ModPackRevisions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Mods",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameIdentifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mods", x => x.PrimaryKey);
                });

            migrationBuilder.CreateTable(
                name: "Scenarios",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameIdentifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scenarios", x => x.PrimaryKey);
                });

            migrationBuilder.CreateTable(
                name: "TerrainVersions",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    TerrainFk = table.Column<long>(type: "bigint", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    SourceIdentifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ArtifactIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ChecksumSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ArtifactSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerrainVersions", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_TerrainVersions_Terrains_TerrainFk",
                        column: x => x.TerrainFk,
                        principalTable: "Terrains",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TerrainVersions_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModVersions",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    ModFk = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    SourceIdentifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UsageRole = table.Column<int>(type: "integer", nullable: false),
                    InstallerIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    InstallerConfiguration = table.Column<string>(type: "jsonb", nullable: false),
                    ArtifactIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ChecksumSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ArtifactSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModVersions", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ModVersions_Mods_ModFk",
                        column: x => x.ModFk,
                        principalTable: "Mods",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioVersions",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioFk = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    SourceIdentifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ArtifactIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ChecksumSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ArtifactSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false),
                    Changelog = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioVersions", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ScenarioVersions_Scenarios_ScenarioFk",
                        column: x => x.ScenarioFk,
                        principalTable: "Scenarios",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScenarioVersions_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModPackRevisionMods",
                columns: table => new
                {
                    ModPackRevisionFk = table.Column<long>(type: "bigint", nullable: false),
                    ModFk = table.Column<Guid>(type: "uuid", nullable: false),
                    PinnedVersionFk = table.Column<Guid>(type: "uuid", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModPackRevisionMods", x => new { x.ModPackRevisionFk, x.ModFk });
                    table.ForeignKey(
                        name: "FK_ModPackRevisionMods_ModPackRevisions_ModPackRevisionFk",
                        column: x => x.ModPackRevisionFk,
                        principalTable: "ModPackRevisions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModPackRevisionMods_ModVersions_PinnedVersionFk",
                        column: x => x.PinnedVersionFk,
                        principalTable: "ModVersions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ModPackRevisionMods_Mods_ModFk",
                        column: x => x.ModFk,
                        principalTable: "Mods",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioVersionRequiredModPacks",
                columns: table => new
                {
                    ScenarioVersionFk = table.Column<Guid>(type: "uuid", nullable: false),
                    ModPackDefinitionFk = table.Column<long>(type: "bigint", nullable: false),
                    PinnedRevisionFk = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioVersionRequiredModPacks", x => new { x.ScenarioVersionFk, x.ModPackDefinitionFk });
                    table.ForeignKey(
                        name: "FK_ScenarioVersionRequiredModPacks_ModPackDefinitions_ModPackD~",
                        column: x => x.ModPackDefinitionFk,
                        principalTable: "ModPackDefinitions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScenarioVersionRequiredModPacks_ModPackRevisions_PinnedRevi~",
                        column: x => x.PinnedRevisionFk,
                        principalTable: "ModPackRevisions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScenarioVersionRequiredModPacks_ScenarioVersions_ScenarioVe~",
                        column: x => x.ScenarioVersionFk,
                        principalTable: "ScenarioVersions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_PinnedModPackRevisionFk",
                table: "GameServers",
                column: "PinnedModPackRevisionFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_PinnedScenarioVersionFk",
                table: "GameServers",
                column: "PinnedScenarioVersionFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_PinnedTerrainVersionFk",
                table: "GameServers",
                column: "PinnedTerrainVersionFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_SelectedScenarioFk",
                table: "GameServers",
                column: "SelectedScenarioFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_SelectedTerrainFk",
                table: "GameServers",
                column: "SelectedTerrainFk");

            migrationBuilder.CreateIndex(
                name: "IX_Events_PinnedScenarioVersionFk",
                table: "Events",
                column: "PinnedScenarioVersionFk");

            migrationBuilder.CreateIndex(
                name: "IX_Events_PinnedTerrainVersionFk",
                table: "Events",
                column: "PinnedTerrainVersionFk");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ScenarioFk",
                table: "Events",
                column: "ScenarioFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerSecrets_CreatedByFk",
                table: "GameServerSecrets",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerSecrets_GameServerFk_Key",
                table: "GameServerSecrets",
                columns: new[] { "GameServerFk", "Key" },
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerSecrets_GameServerFk_Key_Version",
                table: "GameServerSecrets",
                columns: new[] { "GameServerFk", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModPackRevisionDependencies_ChildDefinitionFk",
                table: "ModPackRevisionDependencies",
                column: "ChildDefinitionFk");

            migrationBuilder.CreateIndex(
                name: "IX_ModPackRevisionDependencies_PinnedChildRevisionFk",
                table: "ModPackRevisionDependencies",
                column: "PinnedChildRevisionFk");

            migrationBuilder.CreateIndex(
                name: "IX_ModPackRevisionMods_ModFk",
                table: "ModPackRevisionMods",
                column: "ModFk");

            migrationBuilder.CreateIndex(
                name: "IX_ModPackRevisionMods_PinnedVersionFk",
                table: "ModPackRevisionMods",
                column: "PinnedVersionFk");

            migrationBuilder.CreateIndex(
                name: "IX_Mods_GameIdentifier_IsActive",
                table: "Mods",
                columns: new[] { "GameIdentifier", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ModVersions_ModFk_SourceKind_SourceIdentifier",
                table: "ModVersions",
                columns: new[] { "ModFk", "SourceKind", "SourceIdentifier" });

            migrationBuilder.CreateIndex(
                name: "IX_ModVersions_ModFk_VersionNumber",
                table: "ModVersions",
                columns: new[] { "ModFk", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_GameIdentifier_IsActive",
                table: "Scenarios",
                columns: new[] { "GameIdentifier", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioVersionRequiredModPacks_ModPackDefinitionFk",
                table: "ScenarioVersionRequiredModPacks",
                column: "ModPackDefinitionFk");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioVersionRequiredModPacks_PinnedRevisionFk",
                table: "ScenarioVersionRequiredModPacks",
                column: "PinnedRevisionFk");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioVersions_CreatedByFk",
                table: "ScenarioVersions",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioVersions_ScenarioFk_VersionNumber",
                table: "ScenarioVersions",
                columns: new[] { "ScenarioFk", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TerrainVersions_CreatedByFk",
                table: "TerrainVersions",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_TerrainVersions_TerrainFk_VersionNumber",
                table: "TerrainVersions",
                columns: new[] { "TerrainFk", "VersionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_ModPackRevisions_ModPackRevisionFk",
                table: "Events",
                column: "ModPackRevisionFk",
                principalTable: "ModPackRevisions",
                principalColumn: "PrimaryKey");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_ScenarioVersions_PinnedScenarioVersionFk",
                table: "Events",
                column: "PinnedScenarioVersionFk",
                principalTable: "ScenarioVersions",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Scenarios_ScenarioFk",
                table: "Events",
                column: "ScenarioFk",
                principalTable: "Scenarios",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_TerrainVersions_PinnedTerrainVersionFk",
                table: "Events",
                column: "PinnedTerrainVersionFk",
                principalTable: "TerrainVersions",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameServers_ModPackRevisions_PinnedModPackRevisionFk",
                table: "GameServers",
                column: "PinnedModPackRevisionFk",
                principalTable: "ModPackRevisions",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameServers_ScenarioVersions_PinnedScenarioVersionFk",
                table: "GameServers",
                column: "PinnedScenarioVersionFk",
                principalTable: "ScenarioVersions",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameServers_Scenarios_SelectedScenarioFk",
                table: "GameServers",
                column: "SelectedScenarioFk",
                principalTable: "Scenarios",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameServers_TerrainVersions_PinnedTerrainVersionFk",
                table: "GameServers",
                column: "PinnedTerrainVersionFk",
                principalTable: "TerrainVersions",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameServers_Terrains_SelectedTerrainFk",
                table: "GameServers",
                column: "SelectedTerrainFk",
                principalTable: "Terrains",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_ModPackRevisions_ModPackRevisionFk",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_ScenarioVersions_PinnedScenarioVersionFk",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Scenarios_ScenarioFk",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_TerrainVersions_PinnedTerrainVersionFk",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_GameServers_ModPackRevisions_PinnedModPackRevisionFk",
                table: "GameServers");

            migrationBuilder.DropForeignKey(
                name: "FK_GameServers_ScenarioVersions_PinnedScenarioVersionFk",
                table: "GameServers");

            migrationBuilder.DropForeignKey(
                name: "FK_GameServers_Scenarios_SelectedScenarioFk",
                table: "GameServers");

            migrationBuilder.DropForeignKey(
                name: "FK_GameServers_TerrainVersions_PinnedTerrainVersionFk",
                table: "GameServers");

            migrationBuilder.DropForeignKey(
                name: "FK_GameServers_Terrains_SelectedTerrainFk",
                table: "GameServers");

            migrationBuilder.DropTable(
                name: "ContentBackfillRuns");

            migrationBuilder.DropTable(
                name: "GameServerSecrets");

            migrationBuilder.DropTable(
                name: "ModPackRevisionDependencies");

            migrationBuilder.DropTable(
                name: "ModPackRevisionMods");

            migrationBuilder.DropTable(
                name: "ScenarioVersionRequiredModPacks");

            migrationBuilder.DropTable(
                name: "TerrainVersions");

            migrationBuilder.DropTable(
                name: "ModVersions");

            migrationBuilder.DropTable(
                name: "ScenarioVersions");

            migrationBuilder.DropTable(
                name: "Mods");

            migrationBuilder.DropTable(
                name: "Scenarios");

            migrationBuilder.DropIndex(
                name: "IX_GameServers_PinnedModPackRevisionFk",
                table: "GameServers");

            migrationBuilder.DropIndex(
                name: "IX_GameServers_PinnedScenarioVersionFk",
                table: "GameServers");

            migrationBuilder.DropIndex(
                name: "IX_GameServers_PinnedTerrainVersionFk",
                table: "GameServers");

            migrationBuilder.DropIndex(
                name: "IX_GameServers_SelectedScenarioFk",
                table: "GameServers");

            migrationBuilder.DropIndex(
                name: "IX_GameServers_SelectedTerrainFk",
                table: "GameServers");

            migrationBuilder.DropIndex(
                name: "IX_Events_PinnedScenarioVersionFk",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_PinnedTerrainVersionFk",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_ScenarioFk",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ProtectedSecrets",
                table: "WorkerCommands");

            migrationBuilder.DropColumn(
                name: "ProtocolVersion",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "SupportedGameIdentifiers",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "PinnedModPackRevisionFk",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "PinnedScenarioVersionFk",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "PinnedTerrainVersionFk",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "SelectedScenarioFk",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "SelectedTerrainFk",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "ScenarioSnapshot",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "TerrainSnapshot",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "PinnedScenarioVersionFk",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "PinnedTerrainVersionFk",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ScenarioFk",
                table: "Events");

            migrationBuilder.AlterColumn<Guid>(
                name: "ImageAssetFk",
                table: "Terrains",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ModPackRevisionFk",
                table: "Events",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_ModPackRevisions_ModPackRevisionFk",
                table: "Events",
                column: "ModPackRevisionFk",
                principalTable: "ModPackRevisions",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
