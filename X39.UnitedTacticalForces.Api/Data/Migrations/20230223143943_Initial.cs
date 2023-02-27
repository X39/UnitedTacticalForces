using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Identifier = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.PrimaryKey);
                });

            migrationBuilder.CreateTable(
                name: "Terrains",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: false),
                    ImageMimeType = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terrains", x => x.PrimaryKey);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Nickname = table.Column<string>(type: "text", nullable: false),
                    SteamId64 = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EMail = table.Column<string>(type: "text", nullable: true),
                    IsBanned = table.Column<bool>(type: "boolean", nullable: false),
                    Avatar = table.Column<byte[]>(type: "bytea", nullable: false),
                    AvatarMimeType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.PrimaryKey);
                });

            migrationBuilder.CreateTable(
                name: "ModPacks",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimeStampCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Html = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    OwnerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "RoleUser",
                columns: table => new
                {
                    RolesPrimaryKey = table.Column<long>(type: "bigint", nullable: false),
                    UsersPrimaryKey = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleUser", x => new { x.RolesPrimaryKey, x.UsersPrimaryKey });
                    table.ForeignKey(
                        name: "FK_RoleUser_Roles_RolesPrimaryKey",
                        column: x => x.RolesPrimaryKey,
                        principalTable: "Roles",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoleUser_Users_UsersPrimaryKey",
                        column: x => x.UsersPrimaryKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TerrainFk = table.Column<long>(type: "bigint", nullable: false),
                    ModPackFk = table.Column<long>(type: "bigint", nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: false),
                    ImageMimeType = table.Column<string>(type: "text", nullable: false),
                    ScheduledForOriginal = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeStampCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedCount = table.Column<int>(type: "integer", nullable: false),
                    RejectedCount = table.Column<int>(type: "integer", nullable: false),
                    MaybeCount = table.Column<int>(type: "integer", nullable: false),
                    MinimumAccepted = table.Column<int>(type: "integer", nullable: false),
                    OwnerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    HostedByFk = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_Events_ModPacks_ModPackFk",
                        column: x => x.ModPackFk,
                        principalTable: "ModPacks",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_Terrains_TerrainFk",
                        column: x => x.TerrainFk,
                        principalTable: "Terrains",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_Users_HostedByFk",
                        column: x => x.HostedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_Users_OwnerFk",
                        column: x => x.OwnerFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserModPackMetas",
                columns: table => new
                {
                    UserFk = table.Column<Guid>(type: "uuid", nullable: false),
                    ModPackFk = table.Column<long>(type: "bigint", nullable: false),
                    TimeStampDownloaded = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModPackMetas", x => new { x.UserFk, x.ModPackFk });
                    table.ForeignKey(
                        name: "FK_UserModPackMetas_ModPacks_ModPackFk",
                        column: x => x.ModPackFk,
                        principalTable: "ModPacks",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserModPackMetas_Users_UserFk",
                        column: x => x.UserFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserEventMetas",
                columns: table => new
                {
                    UserFk = table.Column<Guid>(type: "uuid", nullable: false),
                    EventFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Acceptance = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEventMetas", x => new { x.UserFk, x.EventFk });
                    table.ForeignKey(
                        name: "FK_UserEventMetas_Events_EventFk",
                        column: x => x.EventFk,
                        principalTable: "Events",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserEventMetas_Users_UserFk",
                        column: x => x.UserFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "PrimaryKey", "Category", "Identifier", "Title" },
                values: new object[,]
                {
                    { 1L, "General", "admin", "Admin" },
                    { 2L, "Events", "event-create", "Create events" },
                    { 3L, "Events", "event-modify", "Modify events" },
                    { 4L, "Events", "event-delete", "Delete events" },
                    { 5L, "Terrains", "terrain-create", "Create terrain" },
                    { 6L, "Terrains", "terrain-modify", "Modify terrain" },
                    { 7L, "Terrains", "terrain-delete", "Delete terrain" },
                    { 8L, "ModPacks", "modpack-create", "Create mod pack" },
                    { 9L, "ModPacks", "modpack-modify", "Modify mod pack" },
                    { 10L, "ModPacks", "modpack-delete", "Delete mod pack" },
                    { 11L, "User", "user-view-steamid64", "View SteamId64 of user" },
                    { 12L, "User", "user-view-mail", "View E-Mail of user" },
                    { 13L, "User", "user-modify", "Modify user" },
                    { 14L, "User", "user-ban", "(Un-)Ban user" },
                    { 15L, "User", "user-roles-all", "Manage user roles" },
                    { 16L, "User", "user-list", "List users" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_HostedByFk",
                table: "Events",
                column: "HostedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ModPackFk",
                table: "Events",
                column: "ModPackFk");

            migrationBuilder.CreateIndex(
                name: "IX_Events_OwnerFk",
                table: "Events",
                column: "OwnerFk");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ScheduledFor",
                table: "Events",
                column: "ScheduledFor");

            migrationBuilder.CreateIndex(
                name: "IX_Events_TerrainFk",
                table: "Events",
                column: "TerrainFk");

            migrationBuilder.CreateIndex(
                name: "IX_ModPacks_OwnerFk",
                table: "ModPacks",
                column: "OwnerFk");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Identifier",
                table: "Roles",
                column: "Identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleUser_UsersPrimaryKey",
                table: "RoleUser",
                column: "UsersPrimaryKey");

            migrationBuilder.CreateIndex(
                name: "IX_UserEventMetas_EventFk",
                table: "UserEventMetas",
                column: "EventFk");

            migrationBuilder.CreateIndex(
                name: "IX_UserModPackMetas_ModPackFk",
                table: "UserModPackMetas",
                column: "ModPackFk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleUser");

            migrationBuilder.DropTable(
                name: "UserEventMetas");

            migrationBuilder.DropTable(
                name: "UserModPackMetas");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "ModPacks");

            migrationBuilder.DropTable(
                name: "Terrains");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
