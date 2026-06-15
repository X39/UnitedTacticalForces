namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record RuntimeDanglingReleaseRequest(
    string ResourceIdentifier,
    string ResourceType);