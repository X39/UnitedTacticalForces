using X39.UnitedTacticalForces.Api.Services.HostingRuntime;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>
/// One independently provisioned worker node required by a game-server implementation.
/// </summary>
public sealed record GameServerNodeDefinition(
    string ServiceIdentifier,
    string DisplayName,
    int Count,
    bool IsPrimary,
    IReadOnlyList<RuntimeInboundNetworkRequirement> InboundNetworkRequirements)
{
    public const string PrimaryServiceIdentifier = "game-server";
}
