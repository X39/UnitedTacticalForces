using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace Api.Tests.GameServerHosting;

internal sealed class TestGameServerDefinitionCatalog : IGameServerDefinitionCatalog
{
    private readonly GameServerDefinition _definition;

    public TestGameServerDefinitionCatalog(
        IReadOnlyList<RuntimeInboundNetworkRequirement>? inboundNetworkRequirements = null)
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
                ERuntimeNetworkProtocol.Udp,
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