namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Changes whether a cost owner may fund new hosting activity.
/// </summary>
public sealed record CostOwnerFreezeUpdatePayload
{
    /// <summary>
    /// Gets whether the cost owner should be frozen.
    /// </summary>
    public bool IsFrozen { get; init; }
}
