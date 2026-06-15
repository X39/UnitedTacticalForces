using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Authorization.Requirements;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Hosting.Contracts;
using SecurityClaim = System.Security.Claims.Claim;

namespace Api.Tests.GameServerHosting;

public sealed class HostingFinanceServiceTests
{
    [Fact]
    public async Task FinanceClaimSeeder_IsIdempotentAndNeverCreatesOrGrantsARole()
    {
        await using var store = await HostingTestStore.CreateAsync();

        await HostingFinanceClaimSeeder.SeedAsync(store.DbContext);
        await HostingFinanceClaimSeeder.SeedAsync(store.DbContext);

        var claims = await store.DbContext.Claims
            .Where(q => q.Identifier.StartsWith("fin"))
            .ToArrayAsync();
        Assert.Equal(8, claims.Length);
        Assert.All(claims, claim =>
        {
            Assert.Null(claim.Value);
            Assert.Null(claim.ValueType);
            Assert.False(claim.IsPrefix);
        });
        Assert.Empty(await store.DbContext.Roles.ToArrayAsync());
        Assert.All(await store.DbContext.Users.Include(q => q.Claims).ToArrayAsync(), user =>
            Assert.Empty(user.Claims ?? []));
    }

    [Fact]
    public async Task RecordPayment_CreditsZeroBalanceExactlyOnceAndEnablesAffordabilityReservation()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var finance = new HostingFinanceService(store.DbContext, new HostingAuditWriter());
        var recordIdentifier = Guid.NewGuid();
        var effectiveAt = Instant.FromUnixTimeSeconds(1_700_000_000);

        var first = await finance.RecordBankTransferPaymentAsync(
            seed.CostOwner.PrimaryKey,
            recordIdentifier,
            10m,
            "eur",
            effectiveAt,
            "BANK-2026-0001",
            "Prepaid hosting credit",
            seed.User.PrimaryKey);
        var retry = await finance.RecordBankTransferPaymentAsync(
            seed.CostOwner.PrimaryKey,
            recordIdentifier,
            10m,
            "EUR",
            effectiveAt,
            "BANK-2026-0001",
            "Prepaid hosting credit",
            seed.User.PrimaryKey);

        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        Assert.Equal(first.PrimaryKey, retry.PrimaryKey);
        Assert.Equal(10m, seed.CostOwner.Balance);
        var payment = Assert.Single(await store.DbContext.HostingCostLedgerEntries.ToArrayAsync());
        Assert.Equal(EHostingLedgerEntryKind.BankTransferPayment, payment.Kind);
        Assert.Equal(10m, payment.Amount);
        Assert.Equal(seed.User.PrimaryKey, payment.RecordedByFk);
        Assert.Equal(recordIdentifier, payment.RecordIdentifier);
        Assert.Equal("BANK-2026-0001", payment.ExternalReference);

        var cost = CreateCostService(store);
        await cost.ReserveInitialSliceAsync(seed.Deployment!.PrimaryKey, TestRuntimeProvider.Identifier);
        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        Assert.Equal(10m, seed.CostOwner.Balance);
        Assert.Equal(0.0250m, seed.Deployment.ReservedProviderCostEur);
    }

    [Fact]
    public async Task RecordPayment_ReusedIdentifierWithDifferentMutationIsRejectedWithoutSecondCredit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var finance = new HostingFinanceService(store.DbContext, new HostingAuditWriter());
        var recordIdentifier = Guid.NewGuid();
        var effectiveAt = Instant.FromUnixTimeSeconds(1_700_000_000);
        await finance.RecordBankTransferPaymentAsync(
            seed.CostOwner.PrimaryKey,
            recordIdentifier,
            10m,
            "EUR",
            effectiveAt,
            "BANK-2026-0002",
            null,
            seed.User.PrimaryKey);

        await Assert.ThrowsAsync<HostingConflictException>(() => finance.RecordBankTransferPaymentAsync(
            seed.CostOwner.PrimaryKey,
            recordIdentifier,
            11m,
            "EUR",
            effectiveAt,
            "BANK-2026-0002",
            null,
            seed.User.PrimaryKey));

        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        Assert.Equal(10m, seed.CostOwner.Balance);
        Assert.Single(await store.DbContext.HostingCostLedgerEntries.ToArrayAsync());
    }

    [Fact]
    public async Task ChangeAllowance_IsAppendOnlyRetrySafeAndCanFundZeroBalanceOwner()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var finance = new HostingFinanceService(store.DbContext, new HostingAuditWriter());
        var recordIdentifier = Guid.NewGuid();

        var first = await finance.ChangeAllowanceAsync(
            seed.CostOwner.PrimaryKey,
            recordIdentifier,
            10m,
            "Approved finite hosting allowance",
            seed.User.PrimaryKey);
        var retry = await finance.ChangeAllowanceAsync(
            seed.CostOwner.PrimaryKey,
            recordIdentifier,
            10m,
            "Approved finite hosting allowance",
            seed.User.PrimaryKey);

        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        Assert.Equal(first.PrimaryKey, retry.PrimaryKey);
        Assert.Equal(0m, seed.CostOwner.Balance);
        Assert.Equal(10m, seed.CostOwner.Allowance);
        var entry = Assert.Single(await store.DbContext.HostingCostLedgerEntries.ToArrayAsync());
        Assert.Equal(EHostingLedgerEntryKind.AllowanceChanged, entry.Kind);
        Assert.Equal(0m, entry.Amount);
        Assert.Null(entry.PreviousAllowance);
        Assert.Equal(10m, entry.NewAllowance);

        await CreateCostService(store)
            .ReserveInitialSliceAsync(seed.Deployment!.PrimaryKey, TestRuntimeProvider.Identifier);
        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        Assert.Equal(0m, seed.CostOwner.Balance);
        Assert.Equal(0.0250m, seed.Deployment.ReservedProviderCostEur);
    }

    [Fact]
    public async Task FinancePolicies_AreIndependentAndFinanceAllSatisfiesEveryPolicy()
    {
        Assert.Equal(
            Claims.Finance.RecordPayment,
            GetAuthorizePolicy(
                nameof(CostOwnersController.RecordPaymentAsync),
                typeof(Guid),
                typeof(CostOwnerPaymentPayload),
                typeof(CancellationToken)));
        Assert.Equal(
            Claims.Finance.ManageAllowance,
            GetAuthorizePolicy(
                nameof(CostOwnersController.ChangeAllowanceAsync),
                typeof(Guid),
                typeof(CostOwnerAllowancePayload),
                typeof(CancellationToken)));
        Assert.Equal(
            Claims.Finance.RecordRefund,
            GetAuthorizePolicy(
                nameof(CostOwnersController.RecordRefundAsync),
                typeof(Guid),
                typeof(CostOwnerRefundPayload),
                typeof(CancellationToken)));
        Assert.Equal(
            Claims.Finance.CorrectLedger,
            GetAuthorizePolicy(
                nameof(CostOwnersController.CorrectDeploymentCostOwnerAsync),
                typeof(Guid),
                typeof(Guid),
                typeof(DeploymentCostOwnerCorrectionPayload),
                typeof(CancellationToken)));
        Assert.Equal(
            Claims.Finance.CorrectLedger,
            GetAuthorizePolicy(
                nameof(CostOwnersController.ReverseLedgerEntryAsync),
                typeof(Guid),
                typeof(Guid),
                typeof(HostingLedgerReversalPayload),
                typeof(CancellationToken)));

        Assert.True(await IsSatisfiedAsync(new FinanceRecordPaymentRequirement(), Claims.Finance.RecordPayment));
        Assert.False(await IsSatisfiedAsync(new FinanceRecordPaymentRequirement(), Claims.Finance.ManageAllowance));
        Assert.True(await IsSatisfiedAsync(new FinanceManageAllowanceRequirement(), Claims.Finance.ManageAllowance));
        Assert.False(await IsSatisfiedAsync(new FinanceManageAllowanceRequirement(), Claims.Finance.RecordPayment));
        Assert.True(await IsSatisfiedAsync(new FinanceRecordRefundRequirement(), Claims.Finance.RecordRefund));
        Assert.False(await IsSatisfiedAsync(new FinanceRecordRefundRequirement(), Claims.Finance.RecordPayment));
        Assert.False(await IsSatisfiedAsync(new FinanceRecordRefundRequirement(), Claims.Finance.CorrectLedger));
        Assert.True(await IsSatisfiedAsync(new FinanceCorrectLedgerRequirement(), Claims.Finance.CorrectLedger));
        Assert.False(await IsSatisfiedAsync(new FinanceCorrectLedgerRequirement(), Claims.Finance.RecordPayment));
        Assert.False(await IsSatisfiedAsync(new FinanceCorrectLedgerRequirement(), Claims.Finance.RecordRefund));
        Assert.True(await IsSatisfiedAsync(new FinanceRecordPaymentRequirement(), Claims.Finance.All));
        Assert.True(await IsSatisfiedAsync(new FinanceManageAllowanceRequirement(), Claims.Finance.All));
        Assert.True(await IsSatisfiedAsync(new FinanceRecordRefundRequirement(), Claims.Finance.All));
        Assert.True(await IsSatisfiedAsync(new FinanceCorrectLedgerRequirement(), Claims.Finance.All));
    }

    private static HostingCostSliceService CreateCostService(HostingTestStore store)
        => new(
            store.DbContext,
            new HostingAuditWriter(),
            Options.Create(new HostingFinanceOptions
            {
                MinimumSafetyRangeEur = 5m,
                ReservationSlice = TimeSpan.FromMinutes(15),
                ReconciliationInterval = TimeSpan.FromSeconds(30),
                EstimatedHourlyCostEur = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                {
                    [TestRuntimeProvider.Identifier] = 0.10m,
                },
            }));

    private static string? GetAuthorizePolicy(string methodName, params Type[] parameterTypes)
    {
        var method = typeof(CostOwnersController).GetMethod(methodName, parameterTypes)
                     ?? throw new InvalidOperationException($"Could not find {methodName}.");
        return Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>())
            .Policy;
    }

    private static async Task<bool> IsSatisfiedAsync(
        X39.UnitedTacticalForces.Api.Authorization.Abstraction.IAppRequirement requirement,
        string claim)
    {
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new SecurityClaim(claim, string.Empty));
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };
        return await requirement.IsSatisfiedAsync(context, context.RequestServices);
    }
}
