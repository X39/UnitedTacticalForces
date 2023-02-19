using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace X39.UnitedTacticalForces.Api.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Terrain",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: false),
                    ImageMimeType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terrain", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nickname = table.Column<string>(type: "text", nullable: false),
                    SteamUuid = table.Column<string>(type: "text", nullable: false),
                    EMail = table.Column<string>(type: "text", nullable: true),
                    Avatar = table.Column<byte[]>(type: "bytea", nullable: false),
                    AvatarMimeType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModPack",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimeStampCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Xml = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    OwnerFk = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModPack", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModPack_Users_OwnerFk",
                        column: x => x.OwnerFk,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Privileges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ClaimCode = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Privileges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Privileges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TerrainFk = table.Column<long>(type: "bigint", nullable: false),
                    ModPackFk = table.Column<long>(type: "bigint", nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: false),
                    ImageMimeType = table.Column<string>(type: "text", nullable: false),
                    ScheduledForOriginal = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeStampCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: false),
                    HostedByFk = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_ModPack_ModPackFk",
                        column: x => x.ModPackFk,
                        principalTable: "ModPack",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_Terrain_TerrainFk",
                        column: x => x.TerrainFk,
                        principalTable: "Terrain",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_Users_HostedByFk",
                        column: x => x.HostedByFk,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Privileges",
                columns: new[] { "Id", "Category", "ClaimCode", "Title", "UserId" },
                values: new object[,]
                {
                    { 1L, "Events", "event-create", "Events erstellen", null },
                    { 2L, "Events", "event-modify", "Alle events bearbeiten", null },
                    { 3L, "Events", "event-delete", "Alle events löschen", null },
                    { 4L, "Terrains", "terrain-create", "Terrain anlegen", null },
                    { 5L, "Terrains", "terrain-modify", "Terrain bearbeiten", null },
                    { 6L, "Terrains", "terrain-delete", "Terrain löschen", null },
                    { 7L, "ModPacks", "modpack-create", "ModPack anlegen", null },
                    { 8L, "ModPacks", "modpack-modify", "ModPack bearbeiten", null },
                    { 9L, "ModPacks", "modpack-delete", "ModPack löschen", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatedByFk",
                table: "Events",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_Events_HostedByFk",
                table: "Events",
                column: "HostedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ModPackFk",
                table: "Events",
                column: "ModPackFk");

            migrationBuilder.CreateIndex(
                name: "IX_Events_TerrainFk",
                table: "Events",
                column: "TerrainFk");

            migrationBuilder.CreateIndex(
                name: "IX_ModPack_OwnerFk",
                table: "ModPack",
                column: "OwnerFk");

            migrationBuilder.CreateIndex(
                name: "IX_Privileges_ClaimCode",
                table: "Privileges",
                column: "ClaimCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Privileges_UserId",
                table: "Privileges",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Privileges");

            migrationBuilder.DropTable(
                name: "ModPack");

            migrationBuilder.DropTable(
                name: "Terrain");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
