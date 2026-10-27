using X39.UnitedTacticalForces.Api.Services.HostingRuntime;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>
/// Static, display-safe metadata supplied by one game implementation.
/// Runtime execution is owned by the dedicated deployment worker.
/// </summary>
/// <param name="GameIdentifier">The stable executor and content namespace retained in worker commands.</param>
/// <param name="DisplayName">The game label exposed by the metadata catalog.</param>
/// <param name="Actions">The lifecycle operations supported by this game.</param>
/// <param name="FileCapabilities">The classified files available through authorized file workflows.</param>
/// <param name="PreservationSeeds">The initial preservation rules for game state.</param>
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
    /// <value>The provider-neutral ports and protocols for this game's deployment.</value>
    public IReadOnlyList<RuntimeInboundNetworkRequirement> InboundNetworkRequirements { get; init; } = [];

    /// <summary>
    /// User-facing primary endpoint within the declared inbound network plan, when the game exposes one.
    /// </summary>
    /// <value>The advertised player endpoint, or no endpoint when the game does not expose one.</value>
    public GameServerConnectionDefinition? PrimaryConnection { get; init; }

    /// <summary>Gets the game installation and credential requirements used by the dedicated worker.</summary>
    /// <value>Installation metadata; no game process is executed by the API catalog.</value>
    public GameRuntimeInstallationDefinition? RuntimeInstallation { get; init; }

    /// <summary>Gets the terrain, scenario, mod, and launch configuration capabilities.</summary>
    /// <value>The game's public content model, with unsupported capabilities disabled by default.</value>
    public GameContentCapabilityDefinition ContentCapabilities { get; init; } = new(
        false,
        "Terrain",
        false,
        "Scenario",
        false,
        false,
        [],
        []);

    /// <summary>Gets the secret definitions available through the existing authorization-sensitive secret workflow.</summary>
    /// <value>Secret metadata without credential values.</value>
    public IReadOnlyList<GameSecretDefinition> Secrets { get; init; } = [];

    /// <summary>
    /// Independently provisioned worker nodes that form one game-server deployment. Definitions that do not
    /// declare a topology retain the traditional single primary worker.
    /// </summary>
    /// <value>The deployment's worker topology, using one shared image and a game-selected executor per node.</value>
    public IReadOnlyList<GameServerNodeDefinition> Nodes { get; init; } =
    [
        new(
            GameServerNodeDefinition.PrimaryServiceIdentifier,
            "Game server",
            1,
            true,
            []),
    ];
}
