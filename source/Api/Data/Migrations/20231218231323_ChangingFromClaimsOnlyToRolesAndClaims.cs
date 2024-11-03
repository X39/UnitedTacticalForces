using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangingFromClaimsOnlyToRolesAndClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "RoleUser" WHERE TRUE;""");
            migrationBuilder.DropIndex(
                name: "IX_Roles_Identifier",
                table: "Roles");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 5L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 6L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 7L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 8L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 9L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 10L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 11L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 12L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 13L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 14L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 15L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 16L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 17L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 18L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 19L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 20L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 21L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 22L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 23L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 24L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 25L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 26L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 27L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 28L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 29L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 30L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 31L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 32L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 33L);

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Roles");

            migrationBuilder.RenameColumn(
                name: "Identifier",
                table: "Roles",
                newName: "Description");

            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Identifier = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsPrefix = table.Column<bool>(type: "boolean", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true),
                    ValueType = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.PrimaryKey);
                });

            migrationBuilder.CreateTable(
                name: "ClaimRole",
                columns: table => new
                {
                    ClaimsPrimaryKey = table.Column<long>(type: "bigint", nullable: false),
                    RolesPrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimRole", x => new { x.ClaimsPrimaryKey, x.RolesPrimaryKey });
                    table.ForeignKey(
                        name: "FK_ClaimRole_Claims_ClaimsPrimaryKey",
                        column: x => x.ClaimsPrimaryKey,
                        principalTable: "Claims",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClaimRole_Roles_RolesPrimaryKey",
                        column: x => x.RolesPrimaryKey,
                        principalTable: "Roles",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimUser",
                columns: table => new
                {
                    ClaimsPrimaryKey = table.Column<long>(type: "bigint", nullable: false),
                    UsersPrimaryKey = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimUser", x => new { x.ClaimsPrimaryKey, x.UsersPrimaryKey });
                    table.ForeignKey(
                        name: "FK_ClaimUser_Claims_ClaimsPrimaryKey",
                        column: x => x.ClaimsPrimaryKey,
                        principalTable: "Claims",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClaimUser_Users_UsersPrimaryKey",
                        column: x => x.UsersPrimaryKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRole_RolesPrimaryKey",
                table: "ClaimRole",
                column: "RolesPrimaryKey");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Identifier_Value_ValueType",
                table: "Claims",
                columns: new[] { "Identifier", "Value", "ValueType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimUser_UsersPrimaryKey",
                table: "ClaimUser",
                column: "UsersPrimaryKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimRole");

            migrationBuilder.DropTable(
                name: "ClaimUser");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Roles",
                newName: "Identifier");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Roles",
                type: "text",
                nullable: false,
                defaultValue: "");

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
                    { 15L, "User", "user-roles", "Manage user roles" },
                    { 16L, "User", "user-list", "List users" },
                    { 17L, "User", "user-verify", "Verify users" },
                    { 18L, "Event-Slotting", "event-slot-ignore", "Ignore slot rules" },
                    { 19L, "Event-Slotting", "event-slot-assign", "Assign slot" },
                    { 20L, "Event-Slotting", "event-slot-create", "Create slot" },
                    { 21L, "Event-Slotting", "event-slot-update", "Update slot" },
                    { 22L, "Event-Slotting", "event-slot-delete", "Delete slot" },
                    { 23L, "Server", "server-access", "Server Base Role" },
                    { 24L, "Server", "server-start-stop", "Server start/stop" },
                    { 25L, "Server", "server-create-delete", "Server create/delete" },
                    { 26L, "Server", "server-update", "Server config" },
                    { 27L, "Server", "server-upgrade", "Server upgrade" },
                    { 28L, "Server", "server-change-modpack", "Server mod-pack" },
                    { 29L, "Server", "server-modify-files", "Server files" },
                    { 30L, "Wiki", "wiki-editor", "Wiki Editor" },
                    { 31L, "Server", "server-logs", "Server access logs" },
                    { 32L, "Server", "server-logs-clear", "Server clear logs" },
                    { 33L, "User", "user-view-discord", "View Discord data of user" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Identifier",
                table: "Roles",
                column: "Identifier",
                unique: true);
        }
    }
}
