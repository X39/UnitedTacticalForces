using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace Api.Tests.GameServerHosting;

internal sealed class TestGameServerDefinitionCatalog : IGameServerDefinitionCatalog
{
    private readonly GameServerDefinition _definition;

    /// <summary>
    /// Creates the catalog with its single test game.
    /// </summary>
    /// <param name="inboundNetworkRequirements">The inbound network requirements, or <see langword="null"/> for the default UDP range.</param>
    /// <param name="reportsPlayerCount">Whether the test game reports its player count, which enables idle shutdown.</param>
    public TestGameServerDefinitionCatalog(
        IReadOnlyList<RuntimeInboundNetworkRequirement>? inboundNetworkRequirements = null,
        bool reportsPlayerCount = true)
    {
        _definition = new GameServerDefinition(
            HostingTestData.GameIdentifier,
            "Test game",
            [
                Action(GameServerActions.Start, ECostImpact.StartsCost, 10),
                Action(GameServerActions.Stop, ECostImpact.StopsCost, 20, requiresConfirmation: true),
                Action(GameServerActions.Restart, ECostImpact.KeepsCostRunning, 30, requiresConfirmation: true),
                Action(GameServerActions.CollectLogs, ECostImpact.NoCostImpact, 40),
            ],
            [],
            [])
        {
            ReportsPlayerCount = reportsPlayerCount,
            InboundNetworkRequirements = inboundNetworkRequirements
                                         ??
                                         [
                                             new(
                                                 ERuntimeNetworkProtocol.Udp,
                                                 32000,
                                                 32002,
                                                 "Test game traffic"),
                                         ],
            PrimaryConnection = new(
                inboundNetworkRequirements?.FirstOrDefault()?.Protocol ?? ERuntimeNetworkProtocol.Udp,
                inboundNetworkRequirements?.FirstOrDefault()?.StartPort ?? 32000,
                "Test game server"),
        };
    }

    public IReadOnlyList<GameServerDefinition> GetDefinitions() => [_definition];

    private static GameServerActionDefinition Action(
        string actionId,
        ECostImpact costImpact,
        int order,
        bool requiresConfirmation = false)
        => new(
            actionId,
            actionId,
            $"{actionId} the test server.",
            actionId,
            "Lifecycle",
            order,
            false,
            requiresConfirmation,
            TimeSpan.FromSeconds(30),
            costImpact,
            "Test cost effect.");
}