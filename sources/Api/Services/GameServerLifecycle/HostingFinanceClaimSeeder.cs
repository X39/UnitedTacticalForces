using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Makes the product-defined global finance capabilities assignable through the existing role/claim administration.
/// It deliberately creates no role and grants no capability to any user.
/// </summary>
internal static class HostingFinanceClaimSeeder
{
    private static readonly ClaimDefinition[] Definitions =
    [
        new(Claims.Finance.All, "All hosting finance capabilities", "Explicit umbrella for global hosting finance actions."),
        new(Claims.Finance.ViewAll, "View all hosting finance", "Inspect every cost owner and hosting ledger position."),
        new(Claims.Finance.RecordPayment, "Record hosting payments", "Record externally received cost-owner payments."),
        new(Claims.Finance.RecordRefund, "Record hosting refunds", "Record externally completed cost-owner refunds."),
        new(Claims.Finance.CorrectLedger, "Correct hosting ledger", "Append reversals and compensating hosting entries."),
        new(Claims.Finance.ManageAllowance, "Manage hosting allowances", "Grant, change, or revoke finite allowances."),
        new(Claims.Finance.FreezeCostOwner, "Freeze hosting cost owners", "Freeze or unfreeze cost owners for new spending."),
        new(Claims.Finance.ManagePolicy, "Manage hosting finance policy", "Change installation-wide hosting finance policy."),
    ];

    public static async Task SeedAsync(DatabaseContext dbContext, CancellationToken cancellationToken = default)
    {
        var identifiers = Definitions.Select(q => q.Identifier).ToArray();
        var existing = await dbContext.Claims
            .Where(q => identifiers.Contains(q.Identifier) && q.Value == null && q.ValueType == null)
            .Select(q => q.Identifier)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);
        foreach (var definition in Definitions.Where(q => !existingSet.Contains(q.Identifier)))
        {
            dbContext.Claims.Add(new Claim
            {
                Category = "Hosting finance",
                Title = definition.Title,
                Identifier = definition.Identifier,
                Description = definition.Description,
                IsPrefix = false,
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record ClaimDefinition(string Identifier, string Title, string Description);
}
