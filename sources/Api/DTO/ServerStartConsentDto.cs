namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The start consent of a game server. It carries the state only, never the identity of the cost owner.
/// </summary>
public sealed record ServerStartConsentDto
{
    /// <summary>
    /// Gets whether scheduled starts may run.
    /// </summary>
    /// <value>Not required without a start rule, given for the current cost owner, otherwise needed.</value>
    public required EStartConsentState State { get; init; }
}
