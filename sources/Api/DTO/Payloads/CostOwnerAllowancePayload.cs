namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Retry-safe change to one cost owner's finite spending allowance. A null allowance revokes it.
/// </summary>
public sealed record CostOwnerAllowancePayload(
    Guid RecordIdentifier,
    decimal? Allowance,
    string Reason);
