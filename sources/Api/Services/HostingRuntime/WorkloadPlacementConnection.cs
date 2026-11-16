namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>Describes the advertised public endpoint of the primary host.</summary>
/// <param name="Protocol">The transport protocol used by the endpoint.</param>
/// <param name="Port">The public port exposed by the primary host.</param>
/// <param name="Description">The display label for the endpoint.</param>
internal sealed record WorkloadPlacementConnection(ERuntimeNetworkProtocol Protocol, int Port, string Description);
