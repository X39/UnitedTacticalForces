using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
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

    /// <summary>
    /// Whether the game's worker can report a player count. This is the structural availability of idle shutdown;
    /// a transient unavailable reading does not change it.
    /// </summary>
    /// <value><see langword="true" /> when the worker reports player counts; <see langword="false" /> by default.</value>
    public bool ReportsPlayerCount { get; init; }

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
    /// Workloads that form one game-server deployment. By default, each workload has its own worker host.
    /// </summary>
    /// <value>The declaration of workload identities, instance counts, and ingress requirements.</value>
    public IReadOnlyList<GameServerNodeDefinition> Nodes { get; init; } =
    [
        new(
            GameServerNodeDefinition.PrimaryServiceIdentifier,
            "Game server",
            1,
            true,
            []),
    ];

    /// <summary>Gets the internal worker action that verifies readiness after all deployment workloads start.</summary>
    /// <value>An optional primary-worker verification action, excluded from user-invokable actions.</value>
    internal string? ReadinessVerificationActionIdentifier { get; init; }

    /// <summary>Resolves the host assignment of a workload from effective control-plane configuration.</summary>
    /// <value>An optional policy. When unset, each workload gets its own host with the same key.</value>
    internal Func<ResolvedGameConfiguration, WorkloadPlacementKey, WorkloadPlacementKey>? ResolveWorkloadHost
    {
        get;
        init;
    }
}
