using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddHostingFinanceAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HostingFinancePolicies",
                columns: table => new
                {
                    PrimaryKey = table.Column<int>(type: "integer", nullable: false),
                    MinimumSafetyRangeEur = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReservationSliceMinutes = table.Column<int>(type: "integer", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    UpdatedByFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostingFinancePolicies", x => x.PrimaryKey);
                    table.CheckConstraint("CK_HostingFinancePolicies_Singleton", "\"PrimaryKey\" = 1");
                    table.CheckConstraint("CK_HostingFinancePolicies_Values", "\"MinimumSafetyRangeEur\" >= 0 AND \"ReservationSliceMinutes\" BETWEEN 1 AND 1440");
                    table.ForeignKey(
                        name: "FK_HostingFinancePolicies_Users_UpdatedByFk",
                        column: x => x.UpdatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HostingFinancePolicies_UpdatedByFk",
                table: "HostingFinancePolicies",
                column: "UpdatedByFk");

            migrationBuilder.Sql(
                """
                INSERT INTO "CostOwners"
                    ("PrimaryKey", "Kind", "UserFk", "DisplayName", "IsFrozen", "Balance", "Allowance",
                     "Currency", "TimeStampCreated", "TimeStampUpdated")
                SELECT md5(u."PrimaryKey"::text || ':hosting-cost-owner')::uuid, 0, u."PrimaryKey", u."Nickname",
                       FALSE, 0, NULL, 'EUR', NOW(), NOW()
                FROM "Users" u
                WHERE u."IsDeleted" = FALSE
                  AND NOT EXISTS (SELECT 1 FROM "CostOwners" co WHERE co."UserFk" = u."PrimaryKey");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HostingFinancePolicies");
        }
    }
}
