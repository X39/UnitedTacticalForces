namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record RuntimeReleaseRequest(
    Guid ServerId,
    Guid DeploymentId,
    string ResourceIdentifier,
    Guid ProvisioningIntentId = default,
    Guid WorkerId = default);