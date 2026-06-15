namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Optional confirmation data supplied when invoking a projected action.
/// </summary>
public sealed record GameServerActionPayload
{
    /// <summary>
    /// Typed confirmation challenge for dangerous actions.
    /// </summary>
    public string? Confirmation { get; init; }
}
