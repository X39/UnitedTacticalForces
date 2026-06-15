namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record RuntimeResourceAttribution(
    Guid? ServerId,
    Guid? DeploymentId,
    Guid? ProvisioningIntentId,
    Guid? WorkerId)
{
    public bool IsComplete => ServerId.HasValue
                              && DeploymentId.HasValue
                              && ProvisioningIntentId.HasValue
                              && WorkerId.HasValue;
}