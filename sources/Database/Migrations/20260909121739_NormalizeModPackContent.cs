using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeModPackContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ObservedContentVersions",
                table: "WorkerCommands",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "CanonicalSourceIdentifier",
                table: "Mods",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderIdentifier",
                table: "Mods",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UsageRole",
                table: "Mods",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SupportedContentProviderIdentifiers",
                table: "LifecycleWorkers",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.Sql(
                """
                UPDATE "Mods" AS m
                SET "ProviderIdentifier" = CASE latest."SourceKind"
                        WHEN 1 THEN 'steam-workshop' WHEN 2 THEN 'uploaded-artifact' ELSE 'built-in' END,
                    "CanonicalSourceIdentifier" = latest."SourceIdentifier", "UsageRole" = latest."UsageRole"
                FROM (
                    SELECT DISTINCT ON (mv."ModFk") mv."ModFk", mv."SourceKind", mv."SourceIdentifier",
                        mv."UsageRole"
                    FROM "ModVersions" AS mv
                    ORDER BY mv."ModFk", mv."VersionNumber" DESC, mv."PrimaryKey" DESC) AS latest
                WHERE latest."ModFk" = m."PrimaryKey";
                UPDATE "Events" AS e SET "ModPackRevisionFk" = NULL WHERE e."ModPackRevisionFk" IN (
                    SELECT r."PrimaryKey" FROM "ModPackRevisions" r JOIN "ModPackDefinitions" d
                    ON d."PrimaryKey" = r."DefinitionFk" WHERE d."GameIdentifier" = 'arma3');
                UPDATE "GameServers" SET "PinnedModPackRevisionFk" = NULL WHERE "PinnedModPackRevisionFk" IN (
                    SELECT r."PrimaryKey" FROM "ModPackRevisions" r JOIN "ModPackDefinitions" d
                    ON d."PrimaryKey" = r."DefinitionFk" WHERE d."GameIdentifier" = 'arma3');
                UPDATE "GameServers" SET "SelectedModPackDefinitionFk" = NULL WHERE "SelectedModPackDefinitionFk" IN (
                    SELECT "PrimaryKey" FROM "ModPackDefinitions" WHERE "GameIdentifier" = 'arma3');
                DELETE FROM "ScenarioVersionRequiredModPacks" WHERE "ModPackDefinitionFk" IN (
                    SELECT "PrimaryKey" FROM "ModPackDefinitions" WHERE "GameIdentifier" = 'arma3');
                DELETE FROM "UserModPackMetas" WHERE "ModPackDefinitionFk" IN (
                    SELECT "PrimaryKey" FROM "ModPackDefinitions" WHERE "GameIdentifier" = 'arma3');
                DELETE FROM "ModPackRevisionDependencies" WHERE "ChildDefinitionFk" IN (
                    SELECT "PrimaryKey" FROM "ModPackDefinitions" WHERE "GameIdentifier" = 'arma3');
                DELETE FROM "ClaimRole" WHERE "ClaimsPrimaryKey" IN (SELECT c."PrimaryKey" FROM "Claims" c
                    WHERE c."Identifier" LIKE 'mp%' AND c."Value" IN (SELECT "PrimaryKey"::text FROM "ModPackDefinitions" WHERE "GameIdentifier" = 'arma3'));
                DELETE FROM "ClaimUser" WHERE "ClaimsPrimaryKey" IN (SELECT c."PrimaryKey" FROM "Claims" c
                    WHERE c."Identifier" LIKE 'mp%' AND c."Value" IN (SELECT "PrimaryKey"::text FROM "ModPackDefinitions" WHERE "GameIdentifier" = 'arma3'));
                DELETE FROM "Claims" c WHERE c."Identifier" LIKE 'mp%' AND c."Value" IN (
                    SELECT "PrimaryKey"::text FROM "ModPackDefinitions" WHERE "GameIdentifier" = 'arma3');
                DELETE FROM "ModPackDefinitions" WHERE "GameIdentifier" = 'arma3';
                DELETE FROM "Mods" WHERE "GameIdentifier" = 'arma3';

                CREATE TEMP TABLE "__ModIdentityMerge" ON COMMIT DROP AS
                SELECT ranked."PrimaryKey" AS "DuplicateModFk", ranked."KeepModFk"
                FROM (
                    SELECT m."PrimaryKey",
                        first_value(m."PrimaryKey") OVER (
                            PARTITION BY m."GameIdentifier", m."ProviderIdentifier", m."CanonicalSourceIdentifier"
                            ORDER BY m."PrimaryKey") AS "KeepModFk",
                        row_number() OVER (
                            PARTITION BY m."GameIdentifier", m."ProviderIdentifier", m."CanonicalSourceIdentifier"
                            ORDER BY m."PrimaryKey") AS "IdentityOrder"
                    FROM "Mods" m
                ) ranked
                WHERE ranked."IdentityOrder" > 1;

                DELETE FROM "ModPackRevisionMods" membership
                USING (
                    SELECT candidate."ModPackRevisionFk", candidate."ModFk"
                    FROM (
                        SELECT membership."ModPackRevisionFk", membership."ModFk",
                            row_number() OVER (
                                PARTITION BY membership."ModPackRevisionFk", mod."GameIdentifier",
                                    mod."ProviderIdentifier", mod."CanonicalSourceIdentifier"
                                ORDER BY membership."Order", membership."ModFk") AS "MembershipOrder"
                        FROM "ModPackRevisionMods" membership
                        JOIN "Mods" mod ON mod."PrimaryKey" = membership."ModFk"
                    ) candidate
                    WHERE candidate."MembershipOrder" > 1
                ) duplicate
                WHERE membership."ModPackRevisionFk" = duplicate."ModPackRevisionFk"
                    AND membership."ModFk" = duplicate."ModFk";

                WITH renumbered AS (
                    SELECT version."PrimaryKey", merge."KeepModFk",
                        COALESCE((SELECT max(kept."VersionNumber") FROM "ModVersions" kept
                            WHERE kept."ModFk" = merge."KeepModFk"), 0)
                        + row_number() OVER (
                            PARTITION BY merge."KeepModFk" ORDER BY version."VersionNumber", version."PrimaryKey")
                            AS "NewVersionNumber"
                    FROM "ModVersions" version
                    JOIN "__ModIdentityMerge" merge ON merge."DuplicateModFk" = version."ModFk"
                )
                UPDATE "ModVersions" version
                SET "ModFk" = renumbered."KeepModFk", "VersionNumber" = renumbered."NewVersionNumber"
                FROM renumbered WHERE version."PrimaryKey" = renumbered."PrimaryKey";

                UPDATE "ModPackRevisionMods" membership
                SET "ModFk" = merge."KeepModFk"
                FROM "__ModIdentityMerge" merge
                WHERE membership."ModFk" = merge."DuplicateModFk";

                DELETE FROM "Mods" duplicate USING "__ModIdentityMerge" merge
                WHERE duplicate."PrimaryKey" = merge."DuplicateModFk";

                UPDATE "Mods" SET "ProviderIdentifier" = 'built-in',
                    "CanonicalSourceIdentifier" = "PrimaryKey"::text
                WHERE "ProviderIdentifier" = '' OR "CanonicalSourceIdentifier" = '';
                """);

            migrationBuilder.DropTable(name: "ModPackDefinitionModPackRevision");
            migrationBuilder.DropIndex(name: "IX_ModVersions_ModFk_SourceKind_SourceIdentifier", table: "ModVersions");
            migrationBuilder.DropColumn(name: "SourceKind", table: "ModVersions");
            migrationBuilder.DropColumn(name: "UsageRole", table: "ModVersions");
            migrationBuilder.DropColumn(name: "Html", table: "ModPackRevisions");
            migrationBuilder.DropColumn(name: "IsComposition", table: "ModPackDefinitions");
            migrationBuilder.RenameColumn(name: "SourceIdentifier", table: "ModVersions", newName: "VersionSelector");
            migrationBuilder.Sql("UPDATE \"ModVersions\" SET \"VersionSelector\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_Mods_GameIdentifier_ProviderIdentifier_CanonicalSourceIdent~",
                table: "Mods",
                columns: new[] { "GameIdentifier", "ProviderIdentifier", "CanonicalSourceIdentifier" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Mods_GameIdentifier_ProviderIdentifier_CanonicalSourceIdent~",
                table: "Mods");

            migrationBuilder.DropColumn(
                name: "ObservedContentVersions",
                table: "WorkerCommands");

            migrationBuilder.DropColumn(
                name: "CanonicalSourceIdentifier",
                table: "Mods");

            migrationBuilder.DropColumn(
                name: "ProviderIdentifier",
                table: "Mods");

            migrationBuilder.DropColumn(
                name: "UsageRole",
                table: "Mods");

            migrationBuilder.DropColumn(
                name: "SupportedContentProviderIdentifiers",
                table: "LifecycleWorkers");

            migrationBuilder.RenameColumn(
                name: "VersionSelector",
                table: "ModVersions",
                newName: "SourceIdentifier");

            migrationBuilder.AddColumn<int>(
                name: "SourceKind",
                table: "ModVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UsageRole",
                table: "ModVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Html",
                table: "ModPackRevisions",
                type: "text",
                nullable: false,
                defaultValue: "");

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
                name: "IX_ModVersions_ModFk_SourceKind_SourceIdentifier",
                table: "ModVersions",
                columns: new[] { "ModFk", "SourceKind", "SourceIdentifier" });

            migrationBuilder.CreateIndex(
                name: "IX_ModPackDefinitionModPackRevision_ModPackRevisionsPrimaryKey",
                table: "ModPackDefinitionModPackRevision",
                column: "ModPackRevisionsPrimaryKey");
        }
    }
}
