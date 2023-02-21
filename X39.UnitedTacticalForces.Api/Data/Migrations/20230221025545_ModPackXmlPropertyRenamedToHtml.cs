using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModPackXmlPropertyRenamedToHtml : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Xml",
                table: "ModPacks",
                newName: "Html");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Html",
                table: "ModPacks",
                newName: "Xml");
        }
    }
}
