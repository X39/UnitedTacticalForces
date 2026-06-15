namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Balanced correction of provider cost from a completed deployment's original owner to the correct owner.
/// </summary>
public sealed record DeploymentCostOwnerCorrectionPayload(
    Guid OperationIdentifier,
    Guid CorrectCostOwnerId,
    decimal Amount,
    string Currency,
    Instant? TimeStampEffective,
    string Reason);
