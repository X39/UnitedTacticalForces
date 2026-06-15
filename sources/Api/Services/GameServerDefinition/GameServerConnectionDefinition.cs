using X39.UnitedTacticalForces.Api.Services.HostingRuntime;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

public sealed record GameServerConnectionDefinition(
    ERuntimeNetworkProtocol Protocol,
    int Port,
    string Description);