namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record RuntimeProvisionRequest(
    Guid ServerId,
    Guid DeploymentId,
    Guid WorkerId,
    string GameIdentifier,
    Guid ProvisioningIntentId = default,
    string? CloudInit = null,
    IReadOnlyList<RuntimeInboundNetworkRequirement>? InboundNetworkRequirements = null);