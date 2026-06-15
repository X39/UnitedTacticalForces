namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Retry-safe record of a bank transfer already received by the Verein for one cost owner.
/// </summary>
public sealed record CostOwnerPaymentPayload(
    Guid RecordIdentifier,
    decimal Amount,
    string Currency,
    Instant? TimeStampEffective,
    string ExternalReference,
    string? Reason);
