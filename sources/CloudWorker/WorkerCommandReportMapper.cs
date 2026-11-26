using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>
/// Maps the worker-internal command report record to the protocol contracts, shared by the HTTP client and the
/// persistent channel client so both transports send byte-identical bodies.
/// </summary>
internal static class WorkerCommandReportMapper
{
    /// <summary>
    /// Builds the protocol body for a command report.
    /// </summary>
    /// <param name="request">The worker-internal report.</param>
    /// <returns>
    /// A <see cref="WorkerCommandStageRequest"/> for received reports, a <see cref="WorkerCommandProcessRequest"/>
    /// for running and succeeded reports, or a <see cref="WorkerCommandFailureRequest"/> for failed reports.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">The report state is not a known state.</exception>
    public static object ToContract(WorkerCommandReportRequest request)
        => request.State switch
        {
            EWorkerCommandReportState.Received => new WorkerCommandStageRequest
            {
                DeploymentId = request.DeploymentId,
                Stage        = request.Stage,
            },
            EWorkerCommandReportState.Running or EWorkerCommandReportState.Succeeded => new WorkerCommandProcessRequest
            {
                DeploymentId            = request.DeploymentId,
                Stage                   = request.Stage,
                ProcessId               = request.ProcessId,
                ProcessStartedAt        = request.ProcessStartedAt,
                Output                  = request.Output,
                StorageMeasurements     = request.StorageMeasurements ?? [],
                ObservedContentVersions = request.ObservedContentVersions ?? [],
                Workloads               = request.Workloads,
            },
            EWorkerCommandReportState.Failed => new WorkerCommandFailureRequest
            {
                DeploymentId = request.DeploymentId,
                Stage        = request.Stage,
                ErrorCode    = request.ErrorCode ?? "worker-command-failed",
                ErrorMessage = request.ErrorMessage ?? "Worker command failed.",
                Output       = request.Output,
                Workloads    = request.Workloads,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.State, "Unknown command report state."),
        };

    /// <summary>
    /// Selects the channel frame type that carries a command report state.
    /// </summary>
    /// <param name="state">The report state.</param>
    /// <returns>The matching report frame type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The report state is not a known state.</exception>
    public static EWorkerChannelFrameType ToFrameType(EWorkerCommandReportState state)
        => state switch
        {
            EWorkerCommandReportState.Received  => EWorkerChannelFrameType.ReportReceived,
            EWorkerCommandReportState.Running   => EWorkerChannelFrameType.ReportRunning,
            EWorkerCommandReportState.Succeeded => EWorkerChannelFrameType.ReportSucceeded,
            EWorkerCommandReportState.Failed    => EWorkerChannelFrameType.ReportFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown command report state."),
        };
}
