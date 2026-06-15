using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Applies claim-authorized manual hosting finance mutations as append-only, retry-safe ledger entries.
/// </summary>
[Scoped<HostingFinanceService, IHostingFinanceService>]
internal sealed class HostingFinanceService(DatabaseContext dbContext, IHostingAuditWriter auditWriter)
    : IHostingFinanceService
{
    private const decimal MaximumAmount = 99_999_999_999_999.9999m;

    public async Task<HostingCostLedgerEntry> RecordBankTransferPaymentAsync(
        Guid costOwnerId,
        Guid recordIdentifier,
        decimal amount,
        string currency,
        Instant timeStampEffective,
        string externalReference,
        string? reason,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (recordIdentifier == Guid.Empty)
            throw new HostingValidationException("A non-empty record identifier is required.");
        if (amount is <= 0 or > MaximumAmount)
            throw new HostingValidationException("The payment amount must be positive and fit the finance ledger.");
        var normalizedCurrency = NormalizeCurrency(currency);
        var normalizedReference = NormalizeRequired(externalReference, 128, "An external bank-transfer reference is required.");
        var normalizedReason = NormalizeOptional(reason, 512);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        if (timeStampEffective > now)
            throw new HostingValidationException("A received payment cannot have a future effective time.");

        await using var transaction = await BeginFinanceTransactionAsync(cancellationToken).ConfigureAwait(false);
        var owner = await LockCostOwnerAsync(costOwnerId, cancellationToken).ConfigureAwait(false);
        var existing = await dbContext.HostingCostLedgerEntries
            .SingleOrDefaultAsync(q => q.RecordIdentifier == recordIdentifier, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            EnsureMatchingPayment(
                existing,
                costOwnerId,
                amount,
                normalizedCurrency,
                timeStampEffective,
                normalizedReference,
                normalizedReason,
                actorId);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return existing;
        }
        await EnsureNoFinanceOperationIdentifierAsync(recordIdentifier, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(owner.Currency, normalizedCurrency, StringComparison.OrdinalIgnoreCase))
            throw new HostingValidationException("The payment currency must match the cost owner's currency.");
        if (owner.Balance > MaximumAmount - amount)
            throw new HostingValidationException("The resulting cost-owner balance would exceed the finance ledger range.");

        owner.Balance += amount;
        owner.TimeStampUpdated = now;
        var entry = new HostingCostLedgerEntry
        {
            PrimaryKey = Guid.NewGuid(),
            CostOwnerFk = owner.PrimaryKey,
            Kind = EHostingLedgerEntryKind.BankTransferPayment,
            Amount = amount,
            BalanceAfter = owner.Balance,
            Currency = normalizedCurrency,
            RecordIdentifier = recordIdentifier,
            TimeStampEffective = timeStampEffective,
            RecordedByFk = actorId,
            ExternalReference = normalizedReference,
            Reason = normalizedReason,
            TimeStampCreated = now,
        };
        dbContext.HostingCostLedgerEntries.Add(entry);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.BankTransferPaymentRecorded,
            "An externally received bank-transfer payment was recorded for a cost owner.",
            actorId: actorId,
            costOwnerId: owner.PrimaryKey,
            details: $"LedgerEntry={entry.PrimaryKey}; Amount={amount:F4}; Currency={normalizedCurrency}",
            costImpact: ECostImpact.NoCostImpact);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return entry;
    }

    public async Task<HostingCostLedgerEntry> ChangeAllowanceAsync(
        Guid costOwnerId,
        Guid recordIdentifier,
        decimal? allowance,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (recordIdentifier == Guid.Empty)
            throw new HostingValidationException("A non-empty record identifier is required.");
        if (allowance is < 0 or > MaximumAmount)
            throw new HostingValidationException("The allowance must be null or a finite non-negative amount.");
        var normalizedReason = NormalizeRequired(reason, 512, "A reason is required when changing an allowance.");
        var now = NodaSystemClock.Instance.GetCurrentInstant();

        await using var transaction = await BeginFinanceTransactionAsync(cancellationToken).ConfigureAwait(false);
        var owner = await LockCostOwnerAsync(costOwnerId, cancellationToken).ConfigureAwait(false);
        var existing = await dbContext.HostingCostLedgerEntries
            .SingleOrDefaultAsync(q => q.RecordIdentifier == recordIdentifier, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            EnsureMatchingAllowance(existing, costOwnerId, allowance, normalizedReason, actorId);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return existing;
        }
        await EnsureNoFinanceOperationIdentifierAsync(recordIdentifier, cancellationToken).ConfigureAwait(false);
        if (owner.Allowance == allowance)
            throw new HostingValidationException("The cost owner already has the requested allowance.");

        var previousAllowance = owner.Allowance;
        owner.Allowance = allowance;
        owner.TimeStampUpdated = now;
        var entry = new HostingCostLedgerEntry
        {
            PrimaryKey = Guid.NewGuid(),
            CostOwnerFk = owner.PrimaryKey,
            Kind = EHostingLedgerEntryKind.AllowanceChanged,
            Amount = 0,
            BalanceAfter = owner.Balance,
            PreviousAllowance = previousAllowance,
            NewAllowance = allowance,
            Currency = owner.Currency,
            RecordIdentifier = recordIdentifier,
            TimeStampEffective = now,
            RecordedByFk = actorId,
            Reason = normalizedReason,
            TimeStampCreated = now,
        };
        dbContext.HostingCostLedgerEntries.Add(entry);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.CostOwnerAllowanceChanged,
            "A cost owner's finite hosting allowance was changed.",
            actorId: actorId,
            costOwnerId: owner.PrimaryKey,
            details: $"LedgerEntry={entry.PrimaryKey}; Previous={previousAllowance}; Current={allowance}",
            costImpact: ECostImpact.NoCostImpact);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return entry;
    }

    public async Task<HostingCostLedgerEntry> RecordExternalRefundAsync(
        Guid costOwnerId,
        Guid recordIdentifier,
        decimal amount,
        string currency,
        Instant timeStampEffective,
        string externalReference,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (recordIdentifier == Guid.Empty)
            throw new HostingValidationException("A non-empty record identifier is required.");
        EnsurePositiveAmount(amount, "The refund amount must be positive and fit the finance ledger.");
        var normalizedCurrency = NormalizeCurrency(currency);
        var normalizedReference = NormalizeRequired(
            externalReference,
            128,
            "An external refund reference is required.");
        var normalizedReason = NormalizeRequired(reason, 512, "A reason is required when recording a refund.");
        EnsureNotFuture(timeStampEffective, "A completed refund cannot have a future effective time.");

        await using var transaction = await BeginFinanceTransactionAsync(cancellationToken).ConfigureAwait(false);
        var owner = await LockCostOwnerAsync(costOwnerId, cancellationToken).ConfigureAwait(false);
        var existing = await dbContext.HostingCostLedgerEntries
            .SingleOrDefaultAsync(q => q.RecordIdentifier == recordIdentifier, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            EnsureMatchingRefund(
                existing,
                costOwnerId,
                amount,
                normalizedCurrency,
                timeStampEffective,
                normalizedReference,
                normalizedReason,
                actorId);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return existing;
        }
        await EnsureNoFinanceOperationIdentifierAsync(recordIdentifier, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(owner.Currency, normalizedCurrency, StringComparison.OrdinalIgnoreCase))
            throw new HostingValidationException("The refund currency must match the cost owner's currency.");

        var reservationAmounts = await dbContext.GameServerDeployments
            .Where(q => q.CostOwnerFk == owner.PrimaryKey && q.ReservedProviderCostEur != null)
            .Select(q => q.ReservedProviderCostEur!.Value)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var reservedCredit = reservationAmounts.Sum();
        var unusedPrepaidCredit = Math.Max(0m, owner.Balance - reservedCredit);
        if (amount > unusedPrepaidCredit)
        {
            throw new HostingValidationException(
                "The refund exceeds prepaid credit that is not reserved for an active deployment.");
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        owner.Balance -= amount;
        owner.TimeStampUpdated = now;
        var entry = new HostingCostLedgerEntry
        {
            PrimaryKey = Guid.NewGuid(),
            CostOwnerFk = owner.PrimaryKey,
            Kind = EHostingLedgerEntryKind.ExternalRefund,
            Amount = -amount,
            BalanceAfter = owner.Balance,
            Currency = normalizedCurrency,
            RecordIdentifier = recordIdentifier,
            TimeStampEffective = timeStampEffective,
            RecordedByFk = actorId,
            ExternalReference = normalizedReference,
            Reason = normalizedReason,
            TimeStampCreated = now,
        };
        dbContext.HostingCostLedgerEntries.Add(entry);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ExternalRefundRecorded,
            "An externally completed refund of unused prepaid hosting credit was recorded.",
            actorId: actorId,
            costOwnerId: owner.PrimaryKey,
            details: $"LedgerEntry={entry.PrimaryKey}; Amount={amount:F4}; Currency={normalizedCurrency}",
            costImpact: ECostImpact.NoCostImpact);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return entry;
    }

    public async Task<HostingFinanceOperation> CorrectCompletedDeploymentCostOwnerAsync(
        Guid sourceCostOwnerId,
        Guid deploymentId,
        Guid operationIdentifier,
        Guid destinationCostOwnerId,
        decimal amount,
        string currency,
        Instant timeStampEffective,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (operationIdentifier == Guid.Empty)
            throw new HostingValidationException("A non-empty operation identifier is required.");
        if (deploymentId == Guid.Empty)
            throw new HostingValidationException("A non-empty deployment identifier is required.");
        if (sourceCostOwnerId == destinationCostOwnerId)
            throw new HostingValidationException("The corrected cost owner must differ from the original owner.");
        EnsurePositiveAmount(amount, "The correction amount must be positive and fit the finance ledger.");
        var normalizedCurrency = NormalizeCurrency(currency);
        var normalizedReason = NormalizeRequired(reason, 512, "A reason is required for a ledger correction.");
        EnsureNotFuture(timeStampEffective, "A ledger correction cannot have a future effective time.");

        await using var transaction = await BeginFinanceTransactionAsync(cancellationToken).ConfigureAwait(false);
        var persistedSourceOwnerId = await dbContext.GameServerDeployments
            .AsNoTracking()
            .Where(q => q.PrimaryKey == deploymentId)
            .Select(q => (Guid?)q.CostOwnerFk)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
                                     ?? throw new HostingNotFoundException("The deployment does not exist.");
        if (persistedSourceOwnerId != sourceCostOwnerId)
        {
            throw new HostingConflictException(
                "The supplied source cost owner is not the deployment's immutable original cost owner.");
        }

        var owners = await LockCostOwnersAsync(
                [sourceCostOwnerId, destinationCostOwnerId],
                cancellationToken)
            .ConfigureAwait(false);
        var sourceOwner = owners[sourceCostOwnerId];
        var destinationOwner = owners[destinationCostOwnerId];
        var deployment = await LockDeploymentAsync(deploymentId, cancellationToken).ConfigureAwait(false);

        var existing = await LoadFinanceOperationAsync(operationIdentifier, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            EnsureMatchingCorrection(
                existing,
                deploymentId,
                sourceCostOwnerId,
                destinationCostOwnerId,
                amount,
                normalizedCurrency,
                timeStampEffective,
                normalizedReason,
                actorId);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return existing;
        }
        await EnsureNoLedgerRecordIdentifierAsync(operationIdentifier, cancellationToken).ConfigureAwait(false);

        EnsureCompletedAndNonBillable(deployment);
        if (!string.Equals(sourceOwner.Currency, normalizedCurrency, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(destinationOwner.Currency, normalizedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new HostingValidationException(
                "The correction currency must match both cost owners' currencies.");
        }
        EnsureBalanceRange(sourceOwner.Balance, amount);
        EnsureBalanceRange(destinationOwner.Balance, -amount);

        var providerEntries = await dbContext.HostingCostLedgerEntries
            .Where(q => q.DeploymentFk == deploymentId
                        && q.CostOwnerFk == sourceCostOwnerId
                        && q.Kind == EHostingLedgerEntryKind.ProviderCostSlice)
            .OrderBy(q => q.SliceStartsAt)
            .ThenBy(q => q.TimeStampCreated)
            .ThenBy(q => q.PrimaryKey)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var providerCost = providerEntries.Sum(q => -q.Amount);
        var netCorrectionAmounts = await dbContext.HostingCostLedgerEntries
            .Where(q => q.DeploymentFk == deploymentId
                        && q.CostOwnerFk == sourceCostOwnerId
                        && (q.Kind == EHostingLedgerEntryKind.DeploymentCostCorrectionCredit
                            || q.Kind == EHostingLedgerEntryKind.LedgerReversal))
            .Select(q => q.Amount)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var netCorrected = netCorrectionAmounts.Sum();
        if (netCorrected < 0 || amount > providerCost - netCorrected)
        {
            throw new HostingValidationException(
                "The correction exceeds provider cost still attributed to the deployment's original owner.");
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var operation = new HostingFinanceOperation
        {
            PrimaryKey = operationIdentifier,
            Kind = EHostingFinanceOperationKind.DeploymentCostOwnerCorrection,
            DeploymentFk = deployment.PrimaryKey,
            SourceCostOwnerFk = sourceOwner.PrimaryKey,
            DestinationCostOwnerFk = destinationOwner.PrimaryKey,
            Amount = amount,
            Currency = normalizedCurrency,
            TimeStampEffective = timeStampEffective,
            RecordedByFk = actorId,
            Reason = normalizedReason,
            TimeStampCreated = now,
        };
        AddCorrectionAllocations(operation, providerEntries, netCorrected, amount);

        sourceOwner.Balance += amount;
        sourceOwner.TimeStampUpdated = now;
        destinationOwner.Balance -= amount;
        destinationOwner.TimeStampUpdated = now;
        var credit = CreateOperationEntry(
            operation,
            sourceOwner,
            deployment.PrimaryKey,
            EHostingLedgerEntryKind.DeploymentCostCorrectionCredit,
            amount,
            sourceOwner.Balance,
            actorId,
            timeStampEffective,
            normalizedReason,
            now);
        var debit = CreateOperationEntry(
            operation,
            destinationOwner,
            deployment.PrimaryKey,
            EHostingLedgerEntryKind.DeploymentCostCorrectionDebit,
            -amount,
            destinationOwner.Balance,
            actorId,
            timeStampEffective,
            normalizedReason,
            now);
        operation.Entries.Add(credit);
        operation.Entries.Add(debit);
        dbContext.HostingFinanceOperations.Add(operation);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.DeploymentCostOwnerCorrected,
            "Completed-deployment provider cost was corrected between cost owners without rewriting attribution.",
            actorId: actorId,
            serverId: deployment.GameServerFk,
            deploymentId: deployment.PrimaryKey,
            costOwnerId: sourceOwner.PrimaryKey,
            details: $"Operation={operation.PrimaryKey}; Source={sourceOwner.PrimaryKey}; "
                     + $"Destination={destinationOwner.PrimaryKey}; Amount={amount:F4}; Currency={normalizedCurrency}",
            costImpact: ECostImpact.NoCostImpact);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return operation;
    }

    public async Task<HostingFinanceOperation> ReverseLedgerEntryAsync(
        Guid expectedCostOwnerId,
        Guid ledgerEntryId,
        Guid operationIdentifier,
        Instant timeStampEffective,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (operationIdentifier == Guid.Empty)
            throw new HostingValidationException("A non-empty operation identifier is required.");
        if (ledgerEntryId == Guid.Empty)
            throw new HostingValidationException("A non-empty ledger-entry identifier is required.");
        var normalizedReason = NormalizeRequired(reason, 512, "A reason is required for a ledger reversal.");
        EnsureNotFuture(timeStampEffective, "A ledger reversal cannot have a future effective time.");

        await using var transaction = await BeginFinanceTransactionAsync(cancellationToken).ConfigureAwait(false);
        var targetHint = await dbContext.HostingCostLedgerEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.PrimaryKey == ledgerEntryId, cancellationToken)
            .ConfigureAwait(false)
                         ?? throw new HostingNotFoundException("The ledger entry does not exist.");
        if (targetHint.CostOwnerFk != expectedCostOwnerId)
            throw new HostingConflictException("The ledger entry does not belong to the supplied cost owner.");

        Guid? targetOperationId = targetHint.Kind switch
        {
            EHostingLedgerEntryKind.BankTransferPayment or EHostingLedgerEntryKind.ExternalRefund => null,
            EHostingLedgerEntryKind.DeploymentCostCorrectionCredit
                or EHostingLedgerEntryKind.DeploymentCostCorrectionDebit
                or EHostingLedgerEntryKind.LedgerReversal => targetHint.FinanceOperationFk
                    ?? throw new HostingConflictException("The correction entry has no durable finance-operation link."),
            EHostingLedgerEntryKind.ProviderCostSlice => throw new HostingValidationException(
                "Provider-cost slices are corrected through a completed-deployment owner correction."),
            EHostingLedgerEntryKind.AllowanceChanged => throw new HostingValidationException(
                "Allowance mistakes are corrected by appending another allowance change."),
            _ => throw new HostingValidationException("This ledger-entry kind cannot be reversed."),
        };

        var targetHints = targetOperationId.HasValue
            ? await dbContext.HostingCostLedgerEntries
                .AsNoTracking()
                .Where(q => q.FinanceOperationFk == targetOperationId.Value)
                .OrderBy(q => q.PrimaryKey)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false)
            : [targetHint];
        EnsureReversibleEntrySet(targetHints, ledgerEntryId);

        var owners = await LockCostOwnersAsync(
                targetHints.Select(q => q.CostOwnerFk),
                cancellationToken)
            .ConfigureAwait(false);
        var targets = targetOperationId.HasValue
            ? await dbContext.HostingCostLedgerEntries
                .Where(q => q.FinanceOperationFk == targetOperationId.Value)
                .OrderBy(q => q.PrimaryKey)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false)
            :
            [
                await dbContext.HostingCostLedgerEntries
                    .SingleAsync(q => q.PrimaryKey == ledgerEntryId, cancellationToken)
                    .ConfigureAwait(false),
            ];
        foreach (var target in targets)
            await dbContext.Entry(target).ReloadAsync(cancellationToken).ConfigureAwait(false);
        EnsureReversibleEntrySet(targets, ledgerEntryId);

        HostingFinanceOperation? targetOperation = null;
        if (targetOperationId.HasValue)
        {
            targetOperation = await dbContext.HostingFinanceOperations
                .SingleOrDefaultAsync(q => q.PrimaryKey == targetOperationId.Value, cancellationToken)
                .ConfigureAwait(false)
                              ?? throw new HostingConflictException(
                                  "The correction entry's finance operation no longer exists.");
        }
        var amount = targets.Max(q => Math.Abs(q.Amount));
        var currency = targets.Select(q => q.Currency).Distinct(StringComparer.Ordinal).Single();
        var deploymentId = targets.Select(q => q.DeploymentFk).Distinct().Single();

        var existing = await LoadFinanceOperationAsync(operationIdentifier, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            EnsureMatchingReversal(
                existing,
                targetOperationId.HasValue ? null : ledgerEntryId,
                targetOperationId,
                amount,
                currency,
                timeStampEffective,
                normalizedReason,
                actorId,
                targets);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return existing;
        }
        await EnsureNoLedgerRecordIdentifierAsync(operationIdentifier, cancellationToken).ConfigureAwait(false);

        var alreadyReversed = targetOperationId.HasValue
            ? await dbContext.HostingFinanceOperations
                .AnyAsync(q => q.TargetFinanceOperationFk == targetOperationId.Value, cancellationToken)
                .ConfigureAwait(false)
            : await dbContext.HostingFinanceOperations
                .AnyAsync(q => q.TargetLedgerEntryFk == ledgerEntryId, cancellationToken)
                .ConfigureAwait(false);
        if (alreadyReversed)
            throw new HostingConflictException("The selected ledger mutation has already been reversed.");

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var operation = new HostingFinanceOperation
        {
            PrimaryKey = operationIdentifier,
            Kind = EHostingFinanceOperationKind.LedgerReversal,
            DeploymentFk = deploymentId,
            TargetLedgerEntryFk = targetOperationId.HasValue ? null : ledgerEntryId,
            TargetFinanceOperationFk = targetOperationId,
            Amount = amount,
            Currency = currency,
            TimeStampEffective = timeStampEffective,
            RecordedByFk = actorId,
            Reason = normalizedReason,
            TimeStampCreated = now,
        };
        foreach (var target in targets)
        {
            var owner = owners[target.CostOwnerFk];
            var reversalAmount = -target.Amount;
            EnsureBalanceRange(owner.Balance, reversalAmount);
            owner.Balance += reversalAmount;
            owner.TimeStampUpdated = now;
            operation.Entries.Add(new HostingCostLedgerEntry
            {
                PrimaryKey = Guid.NewGuid(),
                CostOwnerFk = owner.PrimaryKey,
                DeploymentFk = target.DeploymentFk,
                Kind = EHostingLedgerEntryKind.LedgerReversal,
                Amount = reversalAmount,
                BalanceAfter = owner.Balance,
                Currency = target.Currency,
                FinanceOperationFk = operation.PrimaryKey,
                ReversedEntryFk = target.PrimaryKey,
                TimeStampEffective = timeStampEffective,
                RecordedByFk = actorId,
                Reason = normalizedReason,
                TimeStampCreated = now,
            });
        }
        dbContext.HostingFinanceOperations.Add(operation);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.HostingLedgerEntryReversed,
            "A mistaken append-only finance mutation was reversed through linked compensating entries.",
            actorId: actorId,
            deploymentId: deploymentId,
            costOwnerId: expectedCostOwnerId,
            details: $"Operation={operation.PrimaryKey}; TargetEntry={ledgerEntryId}; "
                     + $"TargetOperation={targetOperation?.PrimaryKey}; Amount={amount:F4}; Currency={currency}",
            costImpact: ECostImpact.NoCostImpact);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return operation;
    }

    private Task<IDbContextTransaction> BeginFinanceTransactionAsync(CancellationToken cancellationToken)
        => dbContext.Database.BeginTransactionAsync(
            IsPostgreSql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable,
            cancellationToken);

    private async Task<Dictionary<Guid, CostOwner>> LockCostOwnersAsync(
        IEnumerable<Guid> costOwnerIds,
        CancellationToken cancellationToken)
    {
        var identifiers = costOwnerIds.Distinct().OrderBy(q => q).ToArray();
        if (identifiers.Any(q => q == Guid.Empty))
            throw new HostingValidationException("A non-empty cost-owner identifier is required.");
        if (IsPostgreSql)
        {
            foreach (var identifier in identifiers)
            {
                await dbContext.CostOwners
                    .FromSqlInterpolated($"""SELECT * FROM "CostOwners" WHERE "PrimaryKey" = {identifier} FOR UPDATE""")
                    .LoadAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var owners = await dbContext.CostOwners
            .Where(q => identifiers.Contains(q.PrimaryKey))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (owners.Length != identifiers.Length)
            throw new HostingNotFoundException("One or more cost owners do not exist.");
        foreach (var owner in owners)
            await dbContext.Entry(owner).ReloadAsync(cancellationToken).ConfigureAwait(false);
        return owners.ToDictionary(q => q.PrimaryKey);
    }

    private async Task<CostOwner> LockCostOwnerAsync(Guid costOwnerId, CancellationToken cancellationToken)
        => (await LockCostOwnersAsync([costOwnerId], cancellationToken).ConfigureAwait(false))[costOwnerId];

    private async Task<GameServerDeployment> LockDeploymentAsync(
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        if (IsPostgreSql)
        {
            await dbContext.GameServerDeployments
                .FromSqlInterpolated(
                    $"""SELECT * FROM "GameServerDeployments" WHERE "PrimaryKey" = {deploymentId} FOR UPDATE""")
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
            await dbContext.DeploymentResources
                .FromSqlInterpolated(
                    $"""SELECT * FROM "DeploymentResources" WHERE "DeploymentFk" = {deploymentId} FOR UPDATE""")
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        var deployment = await dbContext.GameServerDeployments
            .SingleOrDefaultAsync(q => q.PrimaryKey == deploymentId, cancellationToken)
            .ConfigureAwait(false)
                         ?? throw new HostingNotFoundException("The deployment does not exist.");
        await dbContext.Entry(deployment).ReloadAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.Entry(deployment).Collection(q => q.Resources).LoadAsync(cancellationToken).ConfigureAwait(false);
        foreach (var resource in deployment.Resources)
            await dbContext.Entry(resource).ReloadAsync(cancellationToken).ConfigureAwait(false);
        return deployment;
    }

    private Task<HostingFinanceOperation?> LoadFinanceOperationAsync(
        Guid operationIdentifier,
        CancellationToken cancellationToken)
        => dbContext.HostingFinanceOperations
            .Include(q => q.Entries)
            .Include(q => q.AffectedLedgerEntries)
            .AsSplitQuery()
            .SingleOrDefaultAsync(q => q.PrimaryKey == operationIdentifier, cancellationToken);

    private async Task EnsureNoFinanceOperationIdentifierAsync(
        Guid recordIdentifier,
        CancellationToken cancellationToken)
    {
        if (await dbContext.HostingFinanceOperations
                .AnyAsync(q => q.PrimaryKey == recordIdentifier, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new HostingConflictException(
                "The record identifier is already used by a different finance mutation.");
        }
    }

    private async Task EnsureNoLedgerRecordIdentifierAsync(
        Guid operationIdentifier,
        CancellationToken cancellationToken)
    {
        if (await dbContext.HostingCostLedgerEntries
                .AnyAsync(q => q.RecordIdentifier == operationIdentifier, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new HostingConflictException(
                "The operation identifier is already used by a different finance mutation.");
        }
    }

    private static void EnsureCompletedAndNonBillable(GameServerDeployment deployment)
    {
        if (deployment.Status is not (EDeploymentStatus.Stopped
                or EDeploymentStatus.Terminated
                or EDeploymentStatus.TerminatedDueToFault)
            || !deployment.TimeStampResolved.HasValue
            || deployment.PossibleCost
            || deployment.ReservedProviderCostEur.HasValue
            || deployment.Resources.Any(q =>
                q.PossibleCost
                || (q.TimeStampProvisioningRequested.HasValue && !q.TimeStampProvisioningResolved.HasValue)
                || (q.ProviderResourceIdentifier != null && !q.TimeStampReleased.HasValue)))
        {
            throw new HostingValidationException(
                "Cost attribution can only be corrected after provider cleanup is resolved and verified non-billable.");
        }
    }

    private static HostingCostLedgerEntry CreateOperationEntry(
        HostingFinanceOperation operation,
        CostOwner owner,
        Guid deploymentId,
        EHostingLedgerEntryKind kind,
        decimal amount,
        decimal balanceAfter,
        Guid actorId,
        Instant timeStampEffective,
        string reason,
        Instant now)
        => new()
        {
            PrimaryKey = Guid.NewGuid(),
            CostOwnerFk = owner.PrimaryKey,
            DeploymentFk = deploymentId,
            Kind = kind,
            Amount = amount,
            BalanceAfter = balanceAfter,
            Currency = operation.Currency,
            FinanceOperationFk = operation.PrimaryKey,
            TimeStampEffective = timeStampEffective,
            RecordedByFk = actorId,
            Reason = reason,
            TimeStampCreated = now,
        };

    private static void AddCorrectionAllocations(
        HostingFinanceOperation operation,
        IEnumerable<HostingCostLedgerEntry> providerEntries,
        decimal alreadyCorrected,
        decimal amount)
    {
        var offset = alreadyCorrected;
        var remaining = amount;
        foreach (var providerEntry in providerEntries)
        {
            var providerAmount = -providerEntry.Amount;
            if (offset >= providerAmount)
            {
                offset -= providerAmount;
                continue;
            }

            var available = providerAmount - offset;
            offset = 0;
            var allocated = Math.Min(available, remaining);
            operation.AffectedLedgerEntries.Add(new HostingFinanceCorrectionAllocation
            {
                PrimaryKey = Guid.NewGuid(),
                FinanceOperationFk = operation.PrimaryKey,
                AffectedLedgerEntryFk = providerEntry.PrimaryKey,
                Amount = allocated,
            });
            remaining -= allocated;
            if (remaining == 0)
                return;
        }
        throw new HostingConflictException("The deployment's provider-cost evidence changed during correction.");
    }

    private static void EnsureReversibleEntrySet(
        IReadOnlyCollection<HostingCostLedgerEntry> targets,
        Guid requestedLedgerEntryId)
    {
        if (targets.Count == 0 || targets.All(q => q.PrimaryKey != requestedLedgerEntryId))
            throw new HostingConflictException("The requested ledger entry is not part of the selected mutation.");
        if (targets.Any(q => q.Amount == 0) || targets.Select(q => q.Currency).Distinct().Count() != 1)
            throw new HostingConflictException("The selected ledger mutation has an invalid persisted shape.");

        var firstKind = targets.First().Kind;
        if (firstKind is EHostingLedgerEntryKind.BankTransferPayment or EHostingLedgerEntryKind.ExternalRefund)
        {
            if (targets.Count != 1)
                throw new HostingConflictException("A manual payment or refund must contain exactly one ledger entry.");
            return;
        }
        if (firstKind is EHostingLedgerEntryKind.DeploymentCostCorrectionCredit
            or EHostingLedgerEntryKind.DeploymentCostCorrectionDebit)
        {
            if (targets.Count != 2
                || targets.Count(q => q.Kind == EHostingLedgerEntryKind.DeploymentCostCorrectionCredit) != 1
                || targets.Count(q => q.Kind == EHostingLedgerEntryKind.DeploymentCostCorrectionDebit) != 1
                || targets.Sum(q => q.Amount) != 0
                || targets.Select(q => q.CostOwnerFk).Distinct().Count() != 2
                || targets.Select(q => q.DeploymentFk).Distinct().Count() != 1)
            {
                throw new HostingConflictException("The selected cost-owner correction has an invalid persisted pair.");
            }
            return;
        }
        if (firstKind == EHostingLedgerEntryKind.LedgerReversal
            && targets.All(q => q.Kind == EHostingLedgerEntryKind.LedgerReversal)
            && targets.Count is 1 or 2
            && (targets.Count == 1
                || (targets.Sum(q => q.Amount) == 0
                    && targets.Select(q => q.CostOwnerFk).Distinct().Count() == 2
                    && targets.Select(q => q.DeploymentFk).Distinct().Count() == 1)))
        {
            return;
        }
        throw new HostingValidationException("This finance mutation cannot be reversed.");
    }

    private static void EnsureMatchingPayment(
        HostingCostLedgerEntry existing,
        Guid costOwnerId,
        decimal amount,
        string currency,
        Instant timeStampEffective,
        string externalReference,
        string? reason,
        Guid actorId)
    {
        if (existing.Kind is not EHostingLedgerEntryKind.BankTransferPayment
            || existing.CostOwnerFk != costOwnerId
            || existing.Amount != amount
            || !string.Equals(existing.Currency, currency, StringComparison.Ordinal)
            || existing.TimeStampEffective != timeStampEffective
            || !string.Equals(existing.ExternalReference, externalReference, StringComparison.Ordinal)
            || !string.Equals(existing.Reason, reason, StringComparison.Ordinal)
            || existing.RecordedByFk != actorId)
        {
            throw new HostingConflictException("The record identifier is already used by a different finance mutation.");
        }
    }

    private static void EnsureMatchingAllowance(
        HostingCostLedgerEntry existing,
        Guid costOwnerId,
        decimal? allowance,
        string reason,
        Guid actorId)
    {
        if (existing.Kind is not EHostingLedgerEntryKind.AllowanceChanged
            || existing.CostOwnerFk != costOwnerId
            || existing.NewAllowance != allowance
            || !string.Equals(existing.Reason, reason, StringComparison.Ordinal)
            || existing.RecordedByFk != actorId)
        {
            throw new HostingConflictException("The record identifier is already used by a different finance mutation.");
        }
    }

    private static void EnsureMatchingRefund(
        HostingCostLedgerEntry existing,
        Guid costOwnerId,
        decimal amount,
        string currency,
        Instant timeStampEffective,
        string externalReference,
        string reason,
        Guid actorId)
    {
        if (existing.Kind is not EHostingLedgerEntryKind.ExternalRefund
            || existing.CostOwnerFk != costOwnerId
            || existing.Amount != -amount
            || !string.Equals(existing.Currency, currency, StringComparison.Ordinal)
            || existing.TimeStampEffective != timeStampEffective
            || !string.Equals(existing.ExternalReference, externalReference, StringComparison.Ordinal)
            || !string.Equals(existing.Reason, reason, StringComparison.Ordinal)
            || existing.RecordedByFk != actorId)
        {
            throw new HostingConflictException("The record identifier is already used by a different finance mutation.");
        }
    }

    private static void EnsureMatchingCorrection(
        HostingFinanceOperation existing,
        Guid deploymentId,
        Guid sourceCostOwnerId,
        Guid destinationCostOwnerId,
        decimal amount,
        string currency,
        Instant timeStampEffective,
        string reason,
        Guid actorId)
    {
        var entriesMatch = existing.Entries.Count == 2
                           && existing.Entries.Count(q =>
                               q.Kind == EHostingLedgerEntryKind.DeploymentCostCorrectionCredit
                               && q.CostOwnerFk == sourceCostOwnerId
                               && q.Amount == amount) == 1
                           && existing.Entries.Count(q =>
                               q.Kind == EHostingLedgerEntryKind.DeploymentCostCorrectionDebit
                               && q.CostOwnerFk == destinationCostOwnerId
                               && q.Amount == -amount) == 1
                           && existing.Entries.All(q =>
                               q.DeploymentFk == deploymentId
                               && q.TimeStampEffective == timeStampEffective
                               && q.RecordedByFk == actorId
                               && string.Equals(q.Currency, currency, StringComparison.Ordinal)
                               && string.Equals(q.Reason, reason, StringComparison.Ordinal));
        if (existing.Kind is not EHostingFinanceOperationKind.DeploymentCostOwnerCorrection
            || existing.DeploymentFk != deploymentId
            || existing.SourceCostOwnerFk != sourceCostOwnerId
            || existing.DestinationCostOwnerFk != destinationCostOwnerId
            || existing.TargetLedgerEntryFk.HasValue
            || existing.TargetFinanceOperationFk.HasValue
            || existing.Amount != amount
            || !string.Equals(existing.Currency, currency, StringComparison.Ordinal)
            || existing.TimeStampEffective != timeStampEffective
            || !string.Equals(existing.Reason, reason, StringComparison.Ordinal)
            || existing.RecordedByFk != actorId
            || !entriesMatch
            || existing.AffectedLedgerEntries.Count == 0
            || existing.AffectedLedgerEntries.Sum(q => q.Amount) != amount)
        {
            throw new HostingConflictException("The operation identifier is already used by a different finance mutation.");
        }
    }

    private static void EnsureMatchingReversal(
        HostingFinanceOperation existing,
        Guid? targetLedgerEntryId,
        Guid? targetFinanceOperationId,
        decimal amount,
        string currency,
        Instant timeStampEffective,
        string reason,
        Guid actorId,
        IReadOnlyCollection<HostingCostLedgerEntry> targets)
    {
        var entriesMatch = existing.Entries.Count == targets.Count;
        var targetDeploymentId = targets.Select(q => q.DeploymentFk).Distinct().Single();
        if (entriesMatch)
        {
            foreach (var target in targets)
            {
                entriesMatch &= existing.Entries.Count(q =>
                    q.Kind == EHostingLedgerEntryKind.LedgerReversal
                    && q.ReversedEntryFk == target.PrimaryKey
                    && q.CostOwnerFk == target.CostOwnerFk
                    && q.DeploymentFk == target.DeploymentFk
                    && q.Amount == -target.Amount
                    && q.TimeStampEffective == timeStampEffective
                    && q.RecordedByFk == actorId
                    && string.Equals(q.Currency, target.Currency, StringComparison.Ordinal)
                    && string.Equals(q.Reason, reason, StringComparison.Ordinal)) == 1;
            }
        }
        if (existing.Kind is not EHostingFinanceOperationKind.LedgerReversal
            || existing.DeploymentFk != targetDeploymentId
            || existing.TargetLedgerEntryFk != targetLedgerEntryId
            || existing.TargetFinanceOperationFk != targetFinanceOperationId
            || existing.Amount != amount
            || !string.Equals(existing.Currency, currency, StringComparison.Ordinal)
            || existing.TimeStampEffective != timeStampEffective
            || !string.Equals(existing.Reason, reason, StringComparison.Ordinal)
            || existing.RecordedByFk != actorId
            || existing.AffectedLedgerEntries.Count != 0
            || !entriesMatch)
        {
            throw new HostingConflictException("The operation identifier is already used by a different finance mutation.");
        }
    }

    private static void EnsurePositiveAmount(decimal amount, string message)
    {
        if (amount is <= 0 or > MaximumAmount)
            throw new HostingValidationException(message);
    }

    private static void EnsureNotFuture(Instant timeStampEffective, string message)
    {
        if (timeStampEffective > NodaSystemClock.Instance.GetCurrentInstant())
            throw new HostingValidationException(message);
    }

    private static void EnsureBalanceRange(decimal currentBalance, decimal delta)
    {
        var resultingBalance = currentBalance + delta;
        if (resultingBalance is < -MaximumAmount or > MaximumAmount)
            throw new HostingValidationException("The resulting cost-owner balance would exceed the finance ledger range.");
    }

    private static string NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new HostingValidationException("Currency must be a three-letter ISO currency code.");
        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(q => q is < 'A' or > 'Z'))
            throw new HostingValidationException("Currency must be a three-letter ISO currency code.");
        return normalized;
    }

    private static string NormalizeRequired(string value, int maximumLength, string emptyMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new HostingValidationException(emptyMessage);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new HostingValidationException($"The value must not exceed {maximumLength} characters.");
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new HostingValidationException($"The value must not exceed {maximumLength} characters.");
        return normalized;
    }

    private bool IsPostgreSql
        => dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
}
