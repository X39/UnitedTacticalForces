using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddingGameServerEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Verified",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GameServerLogs",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LogLevel = table.Column<int>(type: "integer", nullable: false),
                    TimeStamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServerLogs", x => x.PrimaryKey);
                });

            migrationBuilder.CreateTable(
                name: "GameServers",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    TimeStampCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpgraded = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActiveModPackFk = table.Column<long>(type: "bigint", nullable: true),
                    SelectedModPackFk = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    VersionString = table.Column<string>(type: "text", nullable: false),
                    ControllerIdentifier = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServers", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_GameServers_ModPacks_ActiveModPackFk",
                        column: x => x.ActiveModPackFk,
                        principalTable: "ModPacks",
                        principalColumn: "PrimaryKey");
                    table.ForeignKey(
                        name: "FK_GameServers_ModPacks_SelectedModPackFk",
                        column: x => x.SelectedModPackFk,
                        principalTable: "ModPacks",
                        principalColumn: "PrimaryKey");
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationEntries",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Realm = table.Column<string>(type: "text", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    GameServerFk = table.Column<long>(type: "bigint", nullable: false),
                    ChangedByFk = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeStamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsSensitive = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationEntries", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ConfigurationEntries_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConfigurationEntries_Users_ChangedByFk",
                        column: x => x.ChangedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LifetimeEvents",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameServerFk = table.Column<long>(type: "bigint", nullable: false),
                    ExecutedByFk = table.Column<Guid>(type: "uuid", nullable: true),
                    TimeStamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LifetimeEvents", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_LifetimeEvents_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LifetimeEvents_Users_ExecutedByFk",
                        column: x => x.ExecutedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey");
                });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 15L,
                column: "Identifier",
                value: "user-roles");

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "PrimaryKey", "Category", "Identifier", "Title" },
                values: new object[,]
                {
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
                    { 28L, "Server", "server-change-modpack", "Server mod-pack" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationEntries_ChangedByFk",
                table: "ConfigurationEntries",
                column: "ChangedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationEntries_GameServerFk",
                table: "ConfigurationEntries",
                column: "GameServerFk");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationEntries_IsActive",
                table: "ConfigurationEntries",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationEntries_Realm_Path",
                table: "ConfigurationEntries",
                columns: new[] { "Realm", "Path" });

            migrationBuilder.CreateIndex(
                name: "IX_GameServerLogs_TimeStamp",
                table: "GameServerLogs",
                column: "TimeStamp");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_ActiveModPackFk",
                table: "GameServers",
                column: "ActiveModPackFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_SelectedModPackFk",
                table: "GameServers",
                column: "SelectedModPackFk");

            migrationBuilder.CreateIndex(
                name: "IX_LifetimeEvents_ExecutedByFk",
                table: "LifetimeEvents",
                column: "ExecutedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_LifetimeEvents_GameServerFk",
                table: "LifetimeEvents",
                column: "GameServerFk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigurationEntries");

            migrationBuilder.DropTable(
                name: "GameServerLogs");

            migrationBuilder.DropTable(
                name: "LifetimeEvents");

            migrationBuilder.DropTable(
                name: "GameServers");

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

            migrationBuilder.DropColumn(
                name: "Verified",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "PrimaryKey",
                keyValue: 15L,
                column: "Identifier",
                value: "user-roles-all");
        }
    }
}
