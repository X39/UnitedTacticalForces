using System.Text.Json;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.Api.HostedServices;

/// <summary>
/// Builds the report payloads the UAT worker emulation sends for a command it claimed, so a Stop job can finish on a
/// stack without a real worker.
/// </summary>
/// <remarks>
/// The emulation only acknowledges the two stop actions. It executes no game process: a stop is reported as received
/// and succeeded with every assigned workload observed as stopped, and every other action is reported failed with the
/// fixed <see cref="UnsupportedActionCode"/> so the job never waits forever on an emulated worker. The control plane
/// still validates and stores every report through the worker protocol service.
/// </remarks>
internal static class UatEmulatedCommandReports
{
    /// <summary>The error code reported for a command whose action the emulation cannot perform.</summary>
    internal const string UnsupportedActionCode = "uat-emulated-unsupported-action";

    /// <summary>The stage reported when the emulation received a claimed command.</summary>
    private const string ReceivedStage = "uat-emulated-received";

    /// <summary>The stage reported when the emulation completed a stop command.</summary>
    private const string StoppedStage = "uat-emulated-stopped";

    /// <summary>The stage reported when the emulation refused a command.</summary>
    private const string UnsupportedStage = "uat-emulated-unsupported";

    /// <summary>The message reported with <see cref="UnsupportedActionCode"/>.</summary>
    private const string UnsupportedMessage = "The UAT worker emulation cannot perform this action.";

    /// <summary>
    /// Determines whether the emulation acknowledges the action as a successful stop.
    /// </summary>
    /// <param name="actionIdentifier">The action identifier of the claimed command.</param>
    /// <returns><see langword="true"/> for the ordinary stop and the internal fault stop; otherwise <see langword="false"/>.</returns>
    internal static bool IsEmulated(string actionIdentifier)
        => actionIdentifier is GameServerActions.Stop or GameServerActionIds.FaultStop;

    /// <summary>
    /// Builds the report that confirms the emulated worker received a claimed command.
    /// </summary>
    /// <param name="deploymentId">The deployment the emulated worker is bound to.</param>
    /// <returns>The received report payload.</returns>
    internal static WorkerCommandStageRequest Received(Guid deploymentId)
        => new() { DeploymentId = deploymentId, Stage = ReceivedStage };

    /// <summary>
    /// Builds the success report of a stop command: every workload assigned by the command is stopped and no process
    /// remains.
    /// </summary>
    /// <param name="deploymentId">The deployment the emulated worker is bound to.</param>
    /// <param name="claim">The claimed command whose payload lists the assigned workloads.</param>
    /// <param name="now">The instant the emulated worker observed the workloads.</param>
    /// <returns>
    /// The success report payload; it carries no workloads when the command payload is unreadable or assigns none.
    /// </returns>
    internal static WorkerCommandProcessRequest StoppedSucceeded(
        Guid deploymentId,
        WorkerCommandClaimResponse claim,
        DateTimeOffset now)
        => new()
        {
            DeploymentId = deploymentId,
            Stage = StoppedStage,
            ProcessId = null,
            ProcessStartedAt = null,
            Workloads = ReadAssignments(claim)
                .Select(assignment => new WorkerWorkloadObservation
                {
                    ServiceIdentifier = assignment.ServiceIdentifier,
                    ServiceOrdinal = assignment.ServiceOrdinal,
                    ProcessId = null,
                    ProcessStartedAt = null,
                    State = EWorkerWorkloadState.Stopped,
                    ObservedAt = now,
                })
                .ToArray(),
        };

    /// <summary>
    /// Builds the failure report of a command whose action the emulation cannot perform.
    /// </summary>
    /// <param name="deploymentId">The deployment the emulated worker is bound to.</param>
    /// <param name="claim">The claimed command; its action is not part of the report.</param>
    /// <returns>The failure report payload carrying <see cref="UnsupportedActionCode"/>.</returns>
    internal static WorkerCommandFailureRequest Unsupported(Guid deploymentId, WorkerCommandClaimResponse claim)
        => new()
        {
            DeploymentId = deploymentId,
            Stage = UnsupportedStage,
            ErrorCode = UnsupportedActionCode,
            ErrorMessage = UnsupportedMessage,
        };

    /// <summary>
    /// Reads the workload assignments of the command payload.
    /// </summary>
    /// <param name="claim">The claimed command whose payload is read.</param>
    /// <returns>The assigned workloads, or an empty list when the payload cannot be read.</returns>
    private static IReadOnlyCollection<WorkerWorkloadAssignment> ReadAssignments(WorkerCommandClaimResponse claim)
    {
        try
        {
            return JsonSerializer.Deserialize<WorkerCommandPayload>(claim.Payload, WorkerChannelJson.Options)
                       ?.Workloads
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
