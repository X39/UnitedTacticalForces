using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenamingIdToPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoleUser_Privileges_RolesId",
                table: "RoleUser");

            migrationBuilder.DropForeignKey(
                name: "FK_RoleUser_Users_UsersId",
                table: "RoleUser");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Users",
                newName: "PrimaryKey");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Terrains",
                newName: "PrimaryKey");

            migrationBuilder.RenameColumn(
                name: "UsersId",
                table: "RoleUser",
                newName: "UsersPrimaryKey");

            migrationBuilder.RenameColumn(
                name: "RolesId",
                table: "RoleUser",
                newName: "RolesPrimaryKey");

            migrationBuilder.RenameIndex(
                name: "IX_RoleUser_UsersId",
                table: "RoleUser",
                newName: "IX_RoleUser_UsersPrimaryKey");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Privileges",
                newName: "PrimaryKey");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ModPacks",
                newName: "PrimaryKey");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Events",
                newName: "PrimaryKey");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ModPacks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_RoleUser_Privileges_RolesPrimaryKey",
                table: "RoleUser",
                column: "RolesPrimaryKey",
                principalTable: "Privileges",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoleUser_Users_UsersPrimaryKey",
                table: "RoleUser",
                column: "UsersPrimaryKey",
                principalTable: "Users",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoleUser_Privileges_RolesPrimaryKey",
                table: "RoleUser");

            migrationBuilder.DropForeignKey(
                name: "FK_RoleUser_Users_UsersPrimaryKey",
                table: "RoleUser");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ModPacks");

            migrationBuilder.RenameColumn(
                name: "PrimaryKey",
                table: "Users",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "PrimaryKey",
                table: "Terrains",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "UsersPrimaryKey",
                table: "RoleUser",
                newName: "UsersId");

            migrationBuilder.RenameColumn(
                name: "RolesPrimaryKey",
                table: "RoleUser",
                newName: "RolesId");

            migrationBuilder.RenameIndex(
                name: "IX_RoleUser_UsersPrimaryKey",
                table: "RoleUser",
                newName: "IX_RoleUser_UsersId");

            migrationBuilder.RenameColumn(
                name: "PrimaryKey",
                table: "Privileges",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "PrimaryKey",
                table: "ModPacks",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "PrimaryKey",
                table: "Events",
                newName: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RoleUser_Privileges_RolesId",
                table: "RoleUser",
                column: "RolesId",
                principalTable: "Privileges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoleUser_Users_UsersId",
                table: "RoleUser",
                column: "UsersId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
