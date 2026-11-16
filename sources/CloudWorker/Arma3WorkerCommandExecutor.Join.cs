using System.Diagnostics;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Verifies server-observed HC admission after dependent processes have been launched.</summary>
public sealed partial class Arma3WorkerCommandExecutor
{
    /// <summary>Reads launch-bound engine evidence and tracks freshness independently of process liveness.</summary>
    private readonly Arma3JoinMonitor _joinMonitor;

    /// <summary>Identifies a primary launch requiring the automatically installed HC instrumentation.</summary>
    /// <param name="payload">The frozen workload assignment and topology.</param>
    /// <returns>Whether this primary is responsible for observing the configured HC.</returns>
    private static bool RequiresJoinEvidence(Arma3CommandPayload payload)
        => payload.Command.IsPrimaryService
           && (payload.Command.Topology.Any(q => q.ServiceIdentifier == "headless-client")
               || payload.Command.Workloads.Any(q => q.ServiceIdentifier == "headless-client"));

    /// <summary>Waits for a new positive server observation without blocking the earlier primary launch command.</summary>
    /// <param name="payload">The attributed command delivered after all workload start commands.</param>
    /// <param name="cancellationToken">Cancels verification when the command is superseded or the worker stops.</param>
    /// <returns>A joined outcome, or an explicit failure with no claim that a live PID implies admission.</returns>
    private async Task<WorkerCommandExecutionResult> VerifyJoinAsync(
        Arma3CommandPayload payload, CancellationToken cancellationToken)
    {
        if (!payload.Command.IsPrimaryService)
            return JoinFailure("hc-verification-role-invalid", "Only the primary Arma worker verifies HC admission.");
        if (!RequiresJoinEvidence(payload))
            return WorkerCommandExecutionResult.Success("hc-not-required");
        var colocated = HasColocatedWorkloads(payload);
        if (colocated)
            payload = WithColocatedTopology(payload);
        if (!await Arma3LaunchTopologyContext.MatchesAsync(payload, _paths, cancellationToken).ConfigureAwait(false))
            return TopologyRestartRequired();
        var session = await _joinMonitor.ReadSessionAsync(cancellationToken).ConfigureAwait(false);
        if (session is null)
            return JoinFailure("hc-instrumentation-missing", "Restart is required to install HC join instrumentation.");
        using var initial = await _processController.ObserveAsync(cancellationToken).ConfigureAwait(false);
        if (!initial.IsRunning || initial.Error is not null || initial.Identity is null)
            return JoinFailure("hc-primary-not-running", "The primary process is unavailable for HC verification.");
        var identity = initial.Identity;
        var baseline = await _joinMonitor.ReadLatestAsync(session.Nonce, cancellationToken).ConfigureAwait(false);
        var sequence = baseline?.Sequence ?? 0;
        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < _options.Arma3.HeadlessClientJoinTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var current = await _processController.ObserveAsync(cancellationToken).ConfigureAwait(false);
            if (!current.IsRunning || current.Error is not null || current.Identity != identity)
                return JoinFailure("hc-primary-changed", "The primary process exited or changed during HC verification.");
            if (colocated)
            {
                using var auxiliary = await _auxiliaryProcessController.ObserveAsync(cancellationToken).ConfigureAwait(false);
                if (!auxiliary.IsRunning || auxiliary.Error is not null)
                    return JoinFailure("hc-process-missing", "The local HC exited before admission was verified.");
            }
            var latest = await _joinMonitor.ReadLatestAsync(session.Nonce, cancellationToken).ConfigureAwait(false);
            if (latest is not null && latest.Sequence > sequence)
            {
                sequence = latest.Sequence;
                if (latest.Ready)
                {
                    await _joinMonitor.SaveAsync(session with
                    {
                        VerifiedProcessId = identity.ProcessId, VerifiedStartedAt = identity.ProcessStartedAt,
                    }, cancellationToken).ConfigureAwait(false);
                    return WorkerCommandExecutionResult.Success("hc-joined",
                        "The server engine confirms the expected Headless Client is ready in the current mission.",
                        identity.ProcessId, identity.ProcessStartedAt);
                }
            }
            var remaining = _options.Arma3.HeadlessClientJoinTimeout - Stopwatch.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(250) ? remaining : TimeSpan.FromMilliseconds(250),
                    cancellationToken).ConfigureAwait(false);
        }
        return JoinFailure("hc-join-timeout",
            "No fresh server-confirmed HC join was observed. Check the mission HC slot, password, mods and addon logs.");
    }

    /// <summary>Reports lost engine evidence through the existing generic failed-workload observation.</summary>
    /// <param name="status">Truthful process and storage observations.</param>
    /// <param name="identity">The currently verified primary process incarnation.</param>
    /// <param name="cancellationToken">Cancels local file inspection.</param>
    /// <returns>The same PID evidence, with primary workload failure after an established join expires.</returns>
    private async Task<WorkerRuntimeStatus> WithJoinHealthAsync(
        WorkerRuntimeStatus status, Arma3ProcessIdentity? identity, CancellationToken cancellationToken)
    {
        if (identity is null || status.ProcessId is null
            || !await _joinMonitor.HasExpiredAsync(identity, _options.Arma3.HeadlessClientEvidenceGrace, cancellationToken)
                .ConfigureAwait(false))
            return status;
        var workloads = status.Workloads.ToList();
        var primary = workloads.FindIndex(q => q.ServiceIdentifier == "game-server" && q.ServiceOrdinal == 0);
        var failure = new WorkerWorkloadObservation
        {
            ServiceIdentifier = "game-server", ServiceOrdinal = 0,
            ProcessId = identity.ProcessId, ProcessStartedAt = identity.ProcessStartedAt,
            State = EWorkerWorkloadState.Failed, ObservedAt = DateTimeOffset.UtcNow,
        };
        if (primary < 0)
            workloads.Add(failure);
        else
            workloads[primary] = failure;
        return status with
        {
            State = "hc-join-evidence-expired", Detail = "Server-confirmed HC evidence expired.", Workloads = workloads,
        };
    }

    /// <summary>Creates a bounded, non-secret readiness failure for normal lifecycle fault handling.</summary>
    /// <param name="code">The stable Arma-specific diagnostic code.</param>
    /// <param name="message">The operator-safe explanation.</param>
    /// <returns>A failed command without fabricated process state.</returns>
    private static WorkerCommandExecutionResult JoinFailure(string code, string message)
        => WorkerCommandExecutionResult.Failure("hc-readiness-failed", code, message);
}
