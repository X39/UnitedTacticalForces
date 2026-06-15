namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Provider-neutral public inbound network access required by a game implementation.
/// </summary>
public sealed record RuntimeInboundNetworkRequirement(
    ERuntimeNetworkProtocol Protocol,
    int StartPort,
    int EndPort,
    string Description);
