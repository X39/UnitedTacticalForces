using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class SeedClaimCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Claims",
                columns: new[] { "PrimaryKey", "Category", "Description", "Identifier", "IsPrefix", "Title", "Value", "ValueType" },
                values: new object[,]
                {
                    { -57L, "Resource", "Resource claim for terrain deletion.", "trn:delete", true, "Delete terrain", null, null },
                    { -56L, "Resource", "Resource claim for terrain modification.", "trn:modify", true, "Modify terrain", null, null },
                    { -55L, "Resource", "Resource claim for all actions on terrain.", "trn", true, "Terrain access", null, null },
                    { -54L, "Resource", "Resource claim for mod-pack deletion.", "mp:delete", true, "Delete mod pack", null, null },
                    { -53L, "Resource", "Resource claim for mod-pack modification.", "mp:modify", true, "Modify mod pack", null, null },
                    { -52L, "Resource", "Resource claim for all actions on a mod pack.", "mp", true, "Mod-pack access", null, null },
                    { -51L, "Resource", "Resource claim for viewing a cost owner.", "co:view", true, "View cost owner", null, null },
                    { -50L, "Resource", "Resource claim for charging a cost owner.", "co:use", true, "Use cost owner", null, null },
                    { -49L, "Resource", "Resource claim for deleting persisted server data.", "sv:delete-data", true, "Delete server data", null, null },
                    { -48L, "Resource", "Resource claim for managing server access.", "sv:manage-access", true, "Manage server access", null, null },
                    { -47L, "Resource", "Resource claim for changing a server.", "sv:modify", true, "Modify server", null, null },
                    { -46L, "Resource", "Resource claim for server lifecycle operations.", "sv:operate", true, "Operate server", null, null },
                    { -45L, "Resource", "Resource claim for viewing a server.", "sv:view", true, "View server", null, null },
                    { -44L, "Resource", "Resource claim for all actions on a server.", "sv", true, "Server access", null, null },
                    { -35L, "Verein", "Allows managing Kassenbuch entries.", "verein:kasse", false, "Manage Kassenbuch", null, null },
                    { -34L, "Verein", "Allows managing Verein documents.", "verein:documents", false, "Manage Verein documents", null, null },
                    { -33L, "Verein", "Allows managing formal memberships.", "verein:members", false, "Manage Verein members", null, null },
                    { -32L, "Verein", "Allows all Verein administration actions.", "verein", false, "Manage Verein", null, null },
                    { -31L, "Content", "Allows managing public website pages.", "content:pages", false, "Manage public pages", null, null },
                    { -30L, "Wiki", "Allows renaming wiki pages.", "wiki:rename", false, "Rename wiki", null, null },
                    { -29L, "Wiki", "Allows deleting wiki pages.", "wiki:delete", false, "Delete wiki", null, null },
                    { -28L, "Wiki", "Allows editing wiki pages.", "wiki:modify", false, "Modify wiki", null, null },
                    { -27L, "Wiki", "Allows all wiki actions.", "wiki", false, "Manage wiki", null, null },
                    { -26L, "Users", "Allows assigning user roles.", "usr:rls", false, "Assign roles", null, null },
                    { -25L, "Users", "Allows banning users.", "usr:ban", false, "Ban users", null, null },
                    { -24L, "Users", "Allows verifying users.", "usr:verify", false, "Verify users", null, null },
                    { -23L, "Users", "Allows viewing Discord identifiers.", "usr:discord", false, "View Discord IDs", null, null },
                    { -22L, "Users", "Allows user profile changes.", "usr:modify", false, "Modify users", null, null },
                    { -21L, "Users", "Allows changing email addresses.", "usr:mail", false, "Modify email", null, null },
                    { -20L, "Users", "Allows changing nicknames.", "usr:nick", false, "Modify nicknames", null, null },
                    { -19L, "Users", "Allows viewing Steam identifiers.", "usr:steamid64", false, "View Steam IDs", null, null },
                    { -18L, "Users", "Allows listing users.", "usr:list", false, "List users", null, null },
                    { -17L, "Users", "Allows all user-management actions.", "usr", false, "Manage users", null, null },
                    { -16L, "Creation", "Allows creating game servers.", "crt:sv", false, "Create servers", null, null },
                    { -15L, "Creation", "Allows creating roles.", "crt:rls", false, "Create roles", null, null },
                    { -14L, "Creation", "Allows creating wiki pages.", "crt:wiki", false, "Create wiki pages", null, null },
                    { -13L, "Creation", "Allows creating mod packs.", "crt:mp", false, "Create mod packs", null, null },
                    { -12L, "Creation", "Allows creating terrain.", "crt:trn", false, "Create terrain", null, null },
                    { -11L, "Creation", "Allows creating events.", "crt:ev", false, "Create events", null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -57L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -56L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -55L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -54L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -53L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -52L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -51L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -50L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -49L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -48L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -47L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -46L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -45L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -44L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -35L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -34L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -33L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -32L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -31L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -30L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -29L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -28L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -27L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -26L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -25L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -24L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -23L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -22L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -21L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -20L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -19L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -18L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -17L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -16L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -15L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -14L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -13L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -12L);

            migrationBuilder.DeleteData(
                table: "Claims",
                keyColumn: "PrimaryKey",
                keyValue: -11L);
        }
    }
}
