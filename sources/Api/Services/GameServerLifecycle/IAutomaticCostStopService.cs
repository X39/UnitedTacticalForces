namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Establishes a durable, system-initiated fault barrier and queues provider-independent cost-stop work.
/// </summary>
internal interface IAutomaticCostStopService
{
    Task<bool> TriggerAsync(
        Guid deploymentId,
        string faultCode,
        string faultMessage,
        CancellationToken cancellationToken = default);
}
