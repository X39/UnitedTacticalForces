namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record RuntimeObservationRequest(
    Guid ServerId,
    Guid DeploymentId,
    string ResourceIdentifier,
    Guid ProvisioningIntentId = default,
    Guid WorkerId = default);