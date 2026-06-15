namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Original provider-cost entry and amount affected by a completed-deployment attribution correction.
/// </summary>
public sealed record HostingFinanceAffectedLedgerEntryDto(Guid LedgerEntryId, decimal Amount);
