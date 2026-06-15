using X39.UnitedTacticalForces.Api.Services.HostingRuntime;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>
/// Static, display-safe metadata supplied by one game implementation.
/// Runtime execution is owned by the dedicated deployment worker.
/// </summary>
public sealed record GameServerDefinition(
    string GameIdentifier,
    string DisplayName,
    IReadOnlyList<GameServerActionDefinition> Actions,
    IReadOnlyList<GameServerFileDefinition> FileCapabilities,
    IReadOnlyList<GameServerPreservationSeed> PreservationSeeds)
{
    /// <summary>
    /// Public inbound access required by this game. Runtime providers translate these requirements without
    /// embedding game-specific protocol or port knowledge.
    /// </summary>
    public IReadOnlyList<RuntimeInboundNetworkRequirement> InboundNetworkRequirements { get; init; } = [];

    /// <summary>
    /// User-facing primary endpoint within the declared inbound network plan, when the game exposes one.
    /// </summary>
    public GameServerConnectionDefinition? PrimaryConnection { get; init; }
}