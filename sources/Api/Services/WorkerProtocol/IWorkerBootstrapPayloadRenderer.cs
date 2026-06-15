using X39.UnitedTacticalForces.Api.Services.HostingRuntime;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Renders provider bootstrap data for one deployment-bound external worker.
/// </summary>
public interface IWorkerBootstrapPayloadRenderer
{
    WorkerBootstrapPayload Render(
        WorkerBootstrapConfiguration bootstrap,
        Guid serverId,
        string gameIdentifier,
        IReadOnlyList<RuntimeInboundNetworkRequirement> inboundNetworkRequirements);
}
