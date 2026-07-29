using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalRuntimeStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ExternalStorageSizeGiB",
                table: "GameServers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RuntimeStorageStrategy",
                table: "GameServers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DeploymentRoot",
                table: "GameServerDeployments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedComputeHourlyCostEur",
                table: "GameServerDeployments",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedStorageHourlyCostEur",
                table: "GameServerDeployments",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "IncludedStorageBytes",
                table: "GameServerDeployments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RuntimeStorageStrategy",
                table: "GameServerDeployments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "SelectedExternalStorageBytes",
                table: "GameServerDeployments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedHourlyCostEur",
                table: "DeploymentResources",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProvisionedCapacityBytes",
                table: "DeploymentResources",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalStorageSizeGiB",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "RuntimeStorageStrategy",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "DeploymentRoot",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "EstimatedComputeHourlyCostEur",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "EstimatedStorageHourlyCostEur",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "IncludedStorageBytes",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "RuntimeStorageStrategy",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "SelectedExternalStorageBytes",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "EstimatedHourlyCostEur",
                table: "DeploymentResources");

            migrationBuilder.DropColumn(
                name: "ProvisionedCapacityBytes",
                table: "DeploymentResources");
        }
    }
}
