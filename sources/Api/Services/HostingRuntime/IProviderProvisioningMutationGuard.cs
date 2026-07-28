namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Serializes each provider-create mutation with the durable deployment provisioning barrier.
/// </summary>
public interface IProviderProvisioningMutationGuard
{
    /// <summary>
    /// Executes one provider-create mutation only while the deployment is still permitted to provision.
    /// </summary>
    Task<ProviderProvisioningMutationGuardResult<T>> ExecuteCreateAsync<T>(
        Guid serverId,
        Guid deploymentId,
        Func<CancellationToken, Task<T>> mutation,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of attempting to cross the durable provider-create barrier.
/// </summary>
public sealed record ProviderProvisioningMutationGuardResult<T>(
    bool IsAuthorized,
    T? Value,
    string? Failure);
