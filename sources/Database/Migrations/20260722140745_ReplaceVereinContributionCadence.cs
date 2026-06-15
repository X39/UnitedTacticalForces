using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceVereinContributionCadence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MembershipContributionAmount",
                table: "VereinSettings",
                newName: "MembershipContributionMonthlyAmount");

            migrationBuilder.AddColumn<decimal>(
                name: "MembershipContributionAnnualAmount",
                table: "VereinSettings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "VereinSettings"
                SET "MembershipContributionAnnualAmount" = CASE
                        WHEN lower(trim("MembershipContributionCadence")) IN
                             ('monthly', 'month', 'per month', 'monatlich', 'pro monat')
                            THEN "MembershipContributionMonthlyAmount" * 12
                        ELSE "MembershipContributionMonthlyAmount"
                    END,
                    "MembershipContributionMonthlyAmount" = CASE
                        WHEN lower(trim("MembershipContributionCadence")) IN
                             ('monthly', 'month', 'per month', 'monatlich', 'pro monat')
                            THEN "MembershipContributionMonthlyAmount"
                        ELSE round("MembershipContributionMonthlyAmount" / 12, 2)
                    END
                WHERE "MembershipContributionMonthlyAmount" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "MembershipContributionCadence",
                table: "VereinSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MembershipContributionCadence",
                table: "VereinSettings",
                type: "character varying(96)",
                maxLength: 96,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "VereinSettings"
                SET "MembershipContributionMonthlyAmount" = "MembershipContributionAnnualAmount",
                    "MembershipContributionCadence" = CASE
                        WHEN "MembershipContributionAnnualAmount" IS NULL THEN ''
                        ELSE 'annual'
                    END;
                """);

            migrationBuilder.DropColumn(
                name: "MembershipContributionAnnualAmount",
                table: "VereinSettings");

            migrationBuilder.RenameColumn(
                name: "MembershipContributionMonthlyAmount",
                table: "VereinSettings",
                newName: "MembershipContributionAmount");
        }
    }
}
