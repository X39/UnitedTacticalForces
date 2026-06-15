using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class PersistCloudProviderResourceObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderState",
                table: "DeploymentResources",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicAddress",
                table: "DeploymentResources",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderState",
                table: "DeploymentResources");

            migrationBuilder.DropColumn(
                name: "PublicAddress",
                table: "DeploymentResources");
        }
    }
}
