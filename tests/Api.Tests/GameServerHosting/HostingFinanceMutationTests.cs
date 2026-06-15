using Microsoft.EntityFrameworkCore;
using Xunit;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace Api.Tests.GameServerHosting;

public sealed class HostingFinanceMutationTests
{
    private static readonly Instant EffectiveAt = Instant.FromUnixTimeSeconds(1_700_000_000);

    [Fact]
    public async Task Refund_IsAppendOnlyAndExactRetryDoesNotWithdrawTwice()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var finance = CreateFinanceService(store);
        await RecordPaymentAsync(finance, seed, 10m);
        var recordIdentifier = Guid.NewGuid();

        var first = await finance.RecordExternalRefundAsync(
            seed.CostOwner.PrimaryKey,
            recordIdentifier,
            4m,
            "eur",
            EffectiveAt,
            "BANK-REFUND-0001",
            "Unused prepaid credit returned",
            seed.User.PrimaryKey);
        var retry = await finance.RecordExternalRefundAsync(
            seed.CostOwner.PrimaryKey,
            recordIdentifier,
            4m,
            "EUR",
            EffectiveAt,
            "BANK-REFUND-0001",
            "Unused prepaid credit returned",
            seed.User.PrimaryKey);

        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        Assert.Equal(first.PrimaryKey, retry.PrimaryKey);
        Assert.Equal(6m, seed.CostOwner.Balance);
        var refund = Assert.Single(await store.DbContext.HostingCostLedgerEntries
            .Where(q => q.Kind == EHostingLedgerEntryKind.ExternalRefund)
            .ToArrayAsync());
        Assert.Equal(-4m, refund.Amount);
        Assert.Equal(6m, refund.BalanceAfter);
        Assert.Equal(recordIdentifier, refund.RecordIdentifier);
        Assert.Equal(seed.User.PrimaryKey, refund.RecordedByFk);
        Assert.Equal("BANK-REFUND-0001", refund.ExternalReference);
        Assert.Equal("Unused prepaid credit returned", refund.Reason);
        Assert.Single(await store.DbContext.HostingCostLedgerEntries
            .Where(q => q.Kind == EHostingLedgerEntryKind.BankTransferPayment)
            .ToArrayAsync());
    }

    [Fact]
    public async Task Refund_CannotConsumeCreditReservedForAnActiveDeployment()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var finance = CreateFinanceService(store);
        await RecordPaymentAsync(finance, seed, 10m);
        var reservationStartsAt = EffectiveAt + Duration.FromHours(1);
        seed.Deployment!.CostReservationStartsAt = reservationStartsAt;
        seed.Deployment.CostReservationExpiresAt = reservationStartsAt + Duration.FromMinutes(15);
        seed.Deployment.ReservedProviderCostEur = 4m;
        seed.Deployment.EstimatedHourlyCostEur = 16m;
        await store.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<HostingValidationException>(() => finance.RecordExternalRefundAsync(
            seed.CostOwner.PrimaryKey,
            Guid.NewGuid(),
            6.0001m,
            "EUR",
            EffectiveAt,
            "BANK-REFUND-RESERVED-REJECTED",
            "Must not withdraw reserved credit",
            seed.User.PrimaryKey));

        var accepted = await finance.RecordExternalRefundAsync(
            seed.CostOwner.PrimaryKey,
            Guid.NewGuid(),
            6m,
            "EUR",
            EffectiveAt,
            "BANK-REFUND-RESERVED-BOUNDARY",
            "Withdraw only unreserved credit",
            seed.User.PrimaryKey);

        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        Assert.Equal(-6m, accepted.Amount);
        Assert.Equal(4m, seed.CostOwner.Balance);
        Assert.Single(await store.DbContext.HostingCostLedgerEntries
            .Where(q => q.Kind == EHostingLedgerEntryKind.ExternalRefund)
            .ToArrayAsync());
    }

    [Fact]
    public async Task Refund_CannotUseFiniteAllowanceAsRefundablePrepaidCredit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var finance = CreateFinanceService(store);
        await RecordPaymentAsync(finance, seed, 2m);
        await finance.ChangeAllowanceAsync(
            seed.CostOwner.PrimaryKey,
            Guid.NewGuid(),
            100m,
            "Finite hosting allowance",
            seed.User.PrimaryKey);

        await Assert.ThrowsAsync<HostingValidationException>(() => finance.RecordExternalRefundAsync(
            seed.CostOwner.PrimaryKey,
            Guid.NewGuid(),
            2.0001m,
            "EUR",
            EffectiveAt,
            "BANK-REFUND-ALLOWANCE-REJECTED",
            "Allowance is not refundable credit",
            seed.User.PrimaryKey));

        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        Assert.Equal(2m, seed.CostOwner.Balance);
        Assert.Equal(100m, seed.CostOwner.Allowance);
        Assert.Empty(await store.DbContext.HostingCostLedgerEntries
            .Where(q => q.Kind == EHostingLedgerEntryKind.ExternalRefund)
            .ToArrayAsync());
    }

    [Fact]
    public async Task CompletedDeploymentCorrection_IsBalancedRetrySafeAndKeepsOriginalAttribution()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var context = await SeedDeploymentCostAsync(store, completed: true, providerCost: 6m);
        var finance = CreateFinanceService(store);
        var operationIdentifier = Guid.NewGuid();

        var first = await finance.CorrectCompletedDeploymentCostOwnerAsync(
            context.Seed.CostOwner.PrimaryKey,
            context.Seed.Deployment!.PrimaryKey,
            operationIdentifier,
            context.Destination.PrimaryKey,
            4m,
            "eur",
            EffectiveAt,
            "Correct completed deployment payer",
            context.Seed.User.PrimaryKey);
        var retry = await finance.CorrectCompletedDeploymentCostOwnerAsync(
            context.Seed.CostOwner.PrimaryKey,
            context.Seed.Deployment.PrimaryKey,
            operationIdentifier,
            context.Destination.PrimaryKey,
            4m,
            "EUR",
            EffectiveAt,
            "Correct completed deployment payer",
            context.Seed.User.PrimaryKey);

        Assert.Equal(operationIdentifier, first.PrimaryKey);
        Assert.Equal(first.PrimaryKey, retry.PrimaryKey);
        await ReloadOwnersAsync(store, context.Seed.CostOwner, context.Destination);
        await store.DbContext.Entry(context.Seed.Deployment).ReloadAsync();
        await store.DbContext.Entry(context.ProviderCostEntry).ReloadAsync();
        Assert.Equal(-2m, context.Seed.CostOwner.Balance);
        Assert.Equal(-4m, context.Destination.Balance);
        Assert.Equal(context.Seed.CostOwner.PrimaryKey, context.Seed.Deployment.CostOwnerFk);
        Assert.Equal(EHostingLedgerEntryKind.ProviderCostSlice, context.ProviderCostEntry.Kind);
        Assert.Equal(-6m, context.ProviderCostEntry.Amount);

        var operation = await store.DbContext.HostingFinanceOperations
            .Include(q => q.Entries)
            .Include(q => q.AffectedLedgerEntries)
            .SingleAsync(q => q.PrimaryKey == operationIdentifier);
        Assert.Equal(EHostingFinanceOperationKind.DeploymentCostOwnerCorrection, operation.Kind);
        Assert.Equal(context.Seed.Deployment.PrimaryKey, operation.DeploymentFk);
        Assert.Equal(context.Seed.CostOwner.PrimaryKey, operation.SourceCostOwnerFk);
        Assert.Equal(context.Destination.PrimaryKey, operation.DestinationCostOwnerFk);
        Assert.Equal(4m, operation.Amount);
        Assert.Null(operation.TargetLedgerEntryFk);
        Assert.Null(operation.TargetFinanceOperationFk);
        var allocation = Assert.Single(operation.AffectedLedgerEntries);
        Assert.Equal(context.ProviderCostEntry.PrimaryKey, allocation.AffectedLedgerEntryFk);
        Assert.Equal(4m, allocation.Amount);
        Assert.Equal(2, operation.Entries.Count);
        Assert.Equal(0m, operation.Entries.Sum(q => q.Amount));

        var credit = Assert.Single(operation.Entries, q => q.Kind == EHostingLedgerEntryKind.DeploymentCostCorrectionCredit);
        var debit = Assert.Single(operation.Entries, q => q.Kind == EHostingLedgerEntryKind.DeploymentCostCorrectionDebit);
        Assert.Equal(context.Seed.CostOwner.PrimaryKey, credit.CostOwnerFk);
        Assert.Equal(4m, credit.Amount);
        Assert.Equal(-2m, credit.BalanceAfter);
        Assert.Equal(context.Destination.PrimaryKey, debit.CostOwnerFk);
        Assert.Equal(-4m, debit.Amount);
        Assert.Equal(-4m, debit.BalanceAfter);
        Assert.All(operation.Entries, entry =>
        {
            Assert.Equal(context.Seed.Deployment.PrimaryKey, entry.DeploymentFk);
            Assert.Equal(operationIdentifier, entry.FinanceOperationFk);
            Assert.Equal(context.Seed.User.PrimaryKey, entry.RecordedByFk);
            Assert.Equal("Correct completed deployment payer", entry.Reason);
        });
    }

    [Fact]
    public async Task CompletedDeploymentCorrection_RejectsAnIncompleteDeploymentWithoutMutation()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var context = await SeedDeploymentCostAsync(store, completed: false, providerCost: 6m);
        var finance = CreateFinanceService(store);

        await Assert.ThrowsAsync<HostingValidationException>(() => finance.CorrectCompletedDeploymentCostOwnerAsync(
            context.Seed.CostOwner.PrimaryKey,
            context.Seed.Deployment!.PrimaryKey,
            Guid.NewGuid(),
            context.Destination.PrimaryKey,
            4m,
            "EUR",
            EffectiveAt,
            "Must wait for verified teardown",
            context.Seed.User.PrimaryKey));

        await ReloadOwnersAsync(store, context.Seed.CostOwner, context.Destination);
        Assert.Equal(-6m, context.Seed.CostOwner.Balance);
        Assert.Equal(0m, context.Destination.Balance);
        Assert.Empty(await store.DbContext.HostingFinanceOperations.ToArrayAsync());
        Assert.Single(await store.DbContext.HostingCostLedgerEntries.ToArrayAsync());
    }

    [Fact]
    public async Task CompletedDeploymentCorrection_CannotExceedUncorrectedProviderCost()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var context = await SeedDeploymentCostAsync(store, completed: true, providerCost: 6m);
        var finance = CreateFinanceService(store);
        await finance.CorrectCompletedDeploymentCostOwnerAsync(
            context.Seed.CostOwner.PrimaryKey,
            context.Seed.Deployment!.PrimaryKey,
            Guid.NewGuid(),
            context.Destination.PrimaryKey,
            4m,
            "EUR",
            EffectiveAt,
            "First partial correction",
            context.Seed.User.PrimaryKey);

        await Assert.ThrowsAsync<HostingValidationException>(() => finance.CorrectCompletedDeploymentCostOwnerAsync(
            context.Seed.CostOwner.PrimaryKey,
            context.Seed.Deployment.PrimaryKey,
            Guid.NewGuid(),
            context.Destination.PrimaryKey,
            2.0001m,
            "EUR",
            EffectiveAt,
            "Must not overcorrect remaining cost",
            context.Seed.User.PrimaryKey));

        await ReloadOwnersAsync(store, context.Seed.CostOwner, context.Destination);
        Assert.Equal(-2m, context.Seed.CostOwner.Balance);
        Assert.Equal(-4m, context.Destination.Balance);
        Assert.Single(await store.DbContext.HostingFinanceOperations.ToArrayAsync());
        Assert.Equal(2, await store.DbContext.HostingCostLedgerEntries
            .CountAsync(q => q.Kind == EHostingLedgerEntryKind.DeploymentCostCorrectionCredit
                             || q.Kind == EHostingLedgerEntryKind.DeploymentCostCorrectionDebit));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CompletedDeploymentCorrection_RejectsTerminalDeploymentWithUnresolvedResource(int resourceCase)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var context = await SeedDeploymentCostAsync(store, completed: true, providerCost: 6m);
        var now = EffectiveAt + Duration.FromHours(3);
        var resource = new DeploymentResource
        {
            PrimaryKey = Guid.NewGuid(),
            DeploymentFk = context.Seed.Deployment!.PrimaryKey,
            ProviderIdentifier = TestRuntimeProvider.Identifier,
            ProvisioningIntentIdentifier = Guid.NewGuid().ToString("N"),
            Kind = "server",
            State = EDeploymentResourceState.Released,
            TimeStampCreated = EffectiveAt,
            TimeStampUpdated = now,
            TimeStampReleased = now,
        };
        switch (resourceCase)
        {
            case 0:
                resource.State = EDeploymentResourceState.Provisioning;
                resource.TimeStampProvisioningRequested = EffectiveAt;
                resource.TimeStampProvisioningResolved = null;
                resource.TimeStampReleased = null;
                break;
            case 1:
                resource.State = EDeploymentResourceState.Allocated;
                resource.ProviderResourceIdentifier = $"provider-{Guid.NewGuid():N}";
                resource.TimeStampProvisioningRequested = EffectiveAt;
                resource.TimeStampProvisioningResolved = now;
                resource.TimeStampReleased = null;
                break;
            case 2:
                resource.PossibleCost = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(resourceCase));
        }
        store.DbContext.DeploymentResources.Add(resource);
        await store.DbContext.SaveChangesAsync();
        var finance = CreateFinanceService(store);

        await Assert.ThrowsAsync<HostingValidationException>(() => finance.CorrectCompletedDeploymentCostOwnerAsync(
            context.Seed.CostOwner.PrimaryKey,
            context.Seed.Deployment.PrimaryKey,
            Guid.NewGuid(),
            context.Destination.PrimaryKey,
            4m,
            "EUR",
            EffectiveAt,
            "Resource state is not verified non-billable",
            context.Seed.User.PrimaryKey));

        await ReloadOwnersAsync(store, context.Seed.CostOwner, context.Destination);
        Assert.Equal(-6m, context.Seed.CostOwner.Balance);
        Assert.Equal(0m, context.Destination.Balance);
        Assert.Empty(await store.DbContext.HostingFinanceOperations.ToArrayAsync());
    }

    [Fact]
    public async Task PaymentReversal_CanCreateDebtAfterThePaymentCreditWasConsumed()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var finance = CreateFinanceService(store);
        var payment = await RecordPaymentAsync(finance, seed, 10m);
        var providerCost = AddProviderCost(store, seed, 10m);
        await store.DbContext.SaveChangesAsync();

        await finance.ReverseLedgerEntryAsync(
            seed.CostOwner.PrimaryKey,
            payment.PrimaryKey,
            Guid.NewGuid(),
            EffectiveAt,
            "Correct payment even though its credit was consumed",
            seed.User.PrimaryKey);

        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        await store.DbContext.Entry(payment).ReloadAsync();
        await store.DbContext.Entry(providerCost).ReloadAsync();
        Assert.Equal(-10m, seed.CostOwner.Balance);
        Assert.Equal(10m, payment.Amount);
        Assert.Equal(-10m, providerCost.Amount);
        Assert.Equal(EHostingLedgerEntryKind.ProviderCostSlice, providerCost.Kind);
        var reversal = Assert.Single(await store.DbContext.HostingCostLedgerEntries
            .Where(q => q.Kind == EHostingLedgerEntryKind.LedgerReversal)
            .ToArrayAsync());
        Assert.Equal(-10m, reversal.Amount);
        Assert.Equal(-10m, reversal.BalanceAfter);
    }

    [Fact]
    public async Task PaymentReversal_IsLinkedAppendOnlyAndExactRetryDoesNotApplyTwice()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var finance = CreateFinanceService(store);
        var payment = await RecordPaymentAsync(finance, seed, 10m);
        var operationIdentifier = Guid.NewGuid();

        var first = await finance.ReverseLedgerEntryAsync(
            seed.CostOwner.PrimaryKey,
            payment.PrimaryKey,
            operationIdentifier,
            EffectiveAt,
            "Payment was recorded in error",
            seed.User.PrimaryKey);
        var retry = await finance.ReverseLedgerEntryAsync(
            seed.CostOwner.PrimaryKey,
            payment.PrimaryKey,
            operationIdentifier,
            EffectiveAt,
            "Payment was recorded in error",
            seed.User.PrimaryKey);

        Assert.Equal(first.PrimaryKey, retry.PrimaryKey);
        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        await store.DbContext.Entry(payment).ReloadAsync();
        Assert.Equal(0m, seed.CostOwner.Balance);
        Assert.Equal(EHostingLedgerEntryKind.BankTransferPayment, payment.Kind);
        Assert.Equal(10m, payment.Amount);
        var operation = await store.DbContext.HostingFinanceOperations
            .Include(q => q.Entries)
            .SingleAsync(q => q.PrimaryKey == operationIdentifier);
        Assert.Equal(payment.PrimaryKey, operation.TargetLedgerEntryFk);
        Assert.Null(operation.TargetFinanceOperationFk);
        var reversal = Assert.Single(operation.Entries);
        Assert.Equal(EHostingLedgerEntryKind.LedgerReversal, reversal.Kind);
        Assert.Equal(payment.PrimaryKey, reversal.ReversedEntryFk);
        Assert.Equal(-10m, reversal.Amount);
        Assert.Equal(0m, reversal.BalanceAfter);
        await Assert.ThrowsAsync<HostingConflictException>(() => finance.ReverseLedgerEntryAsync(
            seed.CostOwner.PrimaryKey,
            payment.PrimaryKey,
            Guid.NewGuid(),
            EffectiveAt,
            "A distinct operation cannot reverse the same payment twice",
            seed.User.PrimaryKey));
        Assert.Equal(2, await store.DbContext.HostingCostLedgerEntries.CountAsync());
        Assert.Single(await store.DbContext.HostingFinanceOperations.ToArrayAsync());
    }

    [Fact]
    public async Task RefundReversal_RestoresCreditWithoutChangingTheRefund()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var finance = CreateFinanceService(store);
        await RecordPaymentAsync(finance, seed, 10m);
        var refund = await finance.RecordExternalRefundAsync(
            seed.CostOwner.PrimaryKey,
            Guid.NewGuid(),
            4m,
            "EUR",
            EffectiveAt,
            "BANK-REFUND-REVERSAL",
            "Refund later found to be misrecorded",
            seed.User.PrimaryKey);
        var operationIdentifier = Guid.NewGuid();

        var first = await finance.ReverseLedgerEntryAsync(
            seed.CostOwner.PrimaryKey,
            refund.PrimaryKey,
            operationIdentifier,
            EffectiveAt,
            "Reverse the mistaken refund record",
            seed.User.PrimaryKey);
        var retry = await finance.ReverseLedgerEntryAsync(
            seed.CostOwner.PrimaryKey,
            refund.PrimaryKey,
            operationIdentifier,
            EffectiveAt,
            "Reverse the mistaken refund record",
            seed.User.PrimaryKey);

        Assert.Equal(first.PrimaryKey, retry.PrimaryKey);
        await store.DbContext.Entry(seed.CostOwner).ReloadAsync();
        await store.DbContext.Entry(refund).ReloadAsync();
        Assert.Equal(10m, seed.CostOwner.Balance);
        Assert.Equal(EHostingLedgerEntryKind.ExternalRefund, refund.Kind);
        Assert.Equal(-4m, refund.Amount);
        var operation = await store.DbContext.HostingFinanceOperations
            .Include(q => q.Entries)
            .SingleAsync(q => q.PrimaryKey == operationIdentifier);
        var reversal = Assert.Single(operation.Entries);
        Assert.Equal(refund.PrimaryKey, operation.TargetLedgerEntryFk);
        Assert.Equal(refund.PrimaryKey, reversal.ReversedEntryFk);
        Assert.Equal(4m, reversal.Amount);
        Assert.Equal(10m, reversal.BalanceAfter);
        Assert.Equal(3, await store.DbContext.HostingCostLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task CorrectionPairReversal_IsBalancedAppendOnlyAndExactRetryRestoresBothOwnersOnce()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var context = await SeedDeploymentCostAsync(store, completed: true, providerCost: 6m);
        var finance = CreateFinanceService(store);
        var correctionIdentifier = Guid.NewGuid();
        await finance.CorrectCompletedDeploymentCostOwnerAsync(
            context.Seed.CostOwner.PrimaryKey,
            context.Seed.Deployment!.PrimaryKey,
            correctionIdentifier,
            context.Destination.PrimaryKey,
            4m,
            "EUR",
            EffectiveAt,
            "Correct completed deployment payer",
            context.Seed.User.PrimaryKey);
        var correctionEntries = await store.DbContext.HostingCostLedgerEntries
            .Where(q => q.FinanceOperationFk == correctionIdentifier)
            .OrderBy(q => q.Kind)
            .ToArrayAsync();
        var credit = Assert.Single(
            correctionEntries,
            q => q.Kind == EHostingLedgerEntryKind.DeploymentCostCorrectionCredit);
        var reversalIdentifier = Guid.NewGuid();

        var first = await finance.ReverseLedgerEntryAsync(
            context.Seed.CostOwner.PrimaryKey,
            credit.PrimaryKey,
            reversalIdentifier,
            EffectiveAt,
            "The completed-deployment correction was mistaken",
            context.Seed.User.PrimaryKey);
        var retry = await finance.ReverseLedgerEntryAsync(
            context.Seed.CostOwner.PrimaryKey,
            credit.PrimaryKey,
            reversalIdentifier,
            EffectiveAt,
            "The completed-deployment correction was mistaken",
            context.Seed.User.PrimaryKey);

        Assert.Equal(first.PrimaryKey, retry.PrimaryKey);
        await ReloadOwnersAsync(store, context.Seed.CostOwner, context.Destination);
        Assert.Equal(-6m, context.Seed.CostOwner.Balance);
        Assert.Equal(0m, context.Destination.Balance);
        var reversal = await store.DbContext.HostingFinanceOperations
            .Include(q => q.Entries)
            .SingleAsync(q => q.PrimaryKey == reversalIdentifier);
        Assert.Equal(EHostingFinanceOperationKind.LedgerReversal, reversal.Kind);
        Assert.Null(reversal.TargetLedgerEntryFk);
        Assert.Equal(correctionIdentifier, reversal.TargetFinanceOperationFk);
        Assert.Equal(2, reversal.Entries.Count);
        Assert.Equal(0m, reversal.Entries.Sum(q => q.Amount));
        Assert.All(reversal.Entries, entry => Assert.Equal(EHostingLedgerEntryKind.LedgerReversal, entry.Kind));
        Assert.Equal(
            correctionEntries.Select(q => q.PrimaryKey).Order().ToArray(),
            reversal.Entries.Select(q => q.ReversedEntryFk!.Value).Order().ToArray());
        foreach (var original in correctionEntries)
        {
            await store.DbContext.Entry(original).ReloadAsync();
            Assert.Equal(
                original.Kind == EHostingLedgerEntryKind.DeploymentCostCorrectionCredit ? 4m : -4m,
                original.Amount);
        }
        Assert.Equal(2, await store.DbContext.HostingFinanceOperations.CountAsync());
        Assert.Equal(5, await store.DbContext.HostingCostLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task CorrectionPairReversal_RestoresCapacityForANewCorrection()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var context = await SeedDeploymentCostAsync(store, completed: true, providerCost: 6m);
        var finance = CreateFinanceService(store);
        var firstCorrectionIdentifier = Guid.NewGuid();
        await finance.CorrectCompletedDeploymentCostOwnerAsync(
            context.Seed.CostOwner.PrimaryKey,
            context.Seed.Deployment!.PrimaryKey,
            firstCorrectionIdentifier,
            context.Destination.PrimaryKey,
            6m,
            "EUR",
            EffectiveAt,
            "Initially correct all deployment cost",
            context.Seed.User.PrimaryKey);
        var firstCredit = await store.DbContext.HostingCostLedgerEntries.SingleAsync(q =>
            q.FinanceOperationFk == firstCorrectionIdentifier
            && q.Kind == EHostingLedgerEntryKind.DeploymentCostCorrectionCredit);
        await finance.ReverseLedgerEntryAsync(
            context.Seed.CostOwner.PrimaryKey,
            firstCredit.PrimaryKey,
            Guid.NewGuid(),
            EffectiveAt,
            "Restore the original attribution before correcting again",
            context.Seed.User.PrimaryKey);
        var replacementIdentifier = Guid.NewGuid();

        await finance.CorrectCompletedDeploymentCostOwnerAsync(
            context.Seed.CostOwner.PrimaryKey,
            context.Seed.Deployment.PrimaryKey,
            replacementIdentifier,
            context.Destination.PrimaryKey,
            6m,
            "EUR",
            EffectiveAt,
            "Apply the corrected attribution again",
            context.Seed.User.PrimaryKey);

        await ReloadOwnersAsync(store, context.Seed.CostOwner, context.Destination);
        Assert.Equal(0m, context.Seed.CostOwner.Balance);
        Assert.Equal(-6m, context.Destination.Balance);
        var replacement = await store.DbContext.HostingFinanceOperations
            .Include(q => q.AffectedLedgerEntries)
            .SingleAsync(q => q.PrimaryKey == replacementIdentifier);
        var allocation = Assert.Single(replacement.AffectedLedgerEntries);
        Assert.Equal(context.ProviderCostEntry.PrimaryKey, allocation.AffectedLedgerEntryFk);
        Assert.Equal(6m, allocation.Amount);
        Assert.Equal(3, await store.DbContext.HostingFinanceOperations.CountAsync());
    }

    private static HostingFinanceService CreateFinanceService(HostingTestStore store)
        => new(store.DbContext, new HostingAuditWriter());

    private static Task<HostingCostLedgerEntry> RecordPaymentAsync(
        HostingFinanceService finance,
        HostingTestSeed seed,
        decimal amount)
        => finance.RecordBankTransferPaymentAsync(
            seed.CostOwner.PrimaryKey,
            Guid.NewGuid(),
            amount,
            "EUR",
            EffectiveAt,
            $"BANK-PAYMENT-{Guid.NewGuid():N}",
            "Test prepaid credit",
            seed.User.PrimaryKey);

    private static async Task<DeploymentCostContext> SeedDeploymentCostAsync(
        HostingTestStore store,
        bool completed,
        decimal providerCost)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        if (completed)
        {
            seed.Deployment!.Status = EDeploymentStatus.Stopped;
            seed.Deployment.Stage = "stopped";
            seed.Deployment.PossibleCost = false;
            seed.Deployment.TimeStampResolved = now;
            seed.Deployment.TimeStampUpdated = now;
        }

        var destination = new CostOwner
        {
            PrimaryKey = Guid.NewGuid(),
            Kind = ECostOwnerKind.ExternalOrganization,
            DisplayName = "Correct payer",
            Currency = "EUR",
            TimeStampCreated = EffectiveAt,
            TimeStampUpdated = EffectiveAt,
        };
        store.DbContext.CostOwners.Add(destination);
        var providerCostEntry = AddProviderCost(store, seed, providerCost);
        await store.DbContext.SaveChangesAsync();
        return new DeploymentCostContext(seed, destination, providerCostEntry);
    }

    private static HostingCostLedgerEntry AddProviderCost(
        HostingTestStore store,
        HostingTestSeed seed,
        decimal providerCost)
    {
        var sliceStartsAt = EffectiveAt + Duration.FromMinutes(15);
        var recordedAt = NodaSystemClock.Instance.GetCurrentInstant();
        seed.CostOwner.Balance -= providerCost;
        seed.CostOwner.TimeStampUpdated = recordedAt;
        var entry = new HostingCostLedgerEntry
        {
            PrimaryKey = Guid.NewGuid(),
            CostOwnerFk = seed.CostOwner.PrimaryKey,
            DeploymentFk = seed.Deployment!.PrimaryKey,
            Kind = EHostingLedgerEntryKind.ProviderCostSlice,
            Amount = -providerCost,
            BalanceAfter = seed.CostOwner.Balance,
            EstimatedHourlyRate = providerCost,
            Currency = "EUR",
            ProviderIdentifier = TestRuntimeProvider.Identifier,
            SliceStartsAt = sliceStartsAt,
            SliceEndsAt = sliceStartsAt + Duration.FromHours(1),
            TimeStampEffective = sliceStartsAt,
            TimeStampCreated = recordedAt,
        };
        store.DbContext.HostingCostLedgerEntries.Add(entry);
        return entry;
    }

    private static async Task ReloadOwnersAsync(
        HostingTestStore store,
        CostOwner source,
        CostOwner destination)
    {
        await store.DbContext.Entry(source).ReloadAsync();
        await store.DbContext.Entry(destination).ReloadAsync();
    }

    private sealed record DeploymentCostContext(
        HostingTestSeed Seed,
        CostOwner Destination,
        HostingCostLedgerEntry ProviderCostEntry);
}
