namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Retry-safe record of an externally completed refund or withdrawal of unused prepaid credit.
/// </summary>
public sealed record CostOwnerRefundPayload(
    Guid RecordIdentifier,
    decimal Amount,
    string Currency,
    Instant? TimeStampEffective,
    string ExternalReference,
    string Reason);
