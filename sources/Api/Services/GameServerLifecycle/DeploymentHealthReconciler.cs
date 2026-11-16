using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Detects confirmed idle worker/process loss independently of an executing lifecycle request.
/// </summary>
[Scoped<DeploymentHealthReconciler, IDeploymentHealthReconciler>]
internal sealed class DeploymentHealthReconciler(
    DatabaseContext dbContext,
    IAutomaticCostStopService automaticCostStopService,
    IOptions<LifecycleWorkerOptions> options) : IDeploymentHealthReconciler
{
    private readonly LifecycleWorkerOptions _options = options.Value;

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        if (_options.StaleThreshold <= TimeSpan.Zero)
            throw new InvalidOperationException("Hosting:Worker:StaleThreshold must be greater than zero.");

        var cutoff = NodaSystemClock.Instance.GetCurrentInstant()
                     - Duration.FromTimeSpan(_options.StaleThreshold);
        var candidates = await dbContext.GameServerDeployments
            .AsNoTracking()
            .Where(q => q.TimeStampResolved == null && q.Status == EDeploymentStatus.Running)
            .Select(q => new
            {
                q.PrimaryKey,
                q.PlacementSnapshot,
                Workers = q.Workers
                    .Where(worker => worker.TimeStampStopped == null)
                    .Select(worker => new
                    {
                        worker.Status,
                        worker.ServiceIdentifier,
                        worker.ServiceOrdinal,
                        worker.WorkloadObservations,
                        HasActiveProcessTransition = worker.Commands.Any(command =>
                            (command.Status == EWorkerCommandStatus.Received
                             || command.Status == EWorkerCommandStatus.Running)
                            && (command.ActionIdentifier == "restart" || command.ActionIdentifier == "install"
                                || command.ActionIdentifier == "update")),
                        worker.LastHeartbeatAt,
                        worker.ProcessId,
                        worker.ProcessStartedAt,
                    })
                    .ToArray(),
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? code = null;
            string? message = null;
            if (candidate.Workers.Length == 0)
            {
                code = "dedicated-worker-missing";
                message = "The running deployment has no allocated dedicated worker identity.";
            }
            else if (candidate.Workers.Any(q => q.Status is EWorkerStatus.Offline or EWorkerStatus.Faulted))
            {
                code = "dedicated-worker-offline";
                message = "A dedicated service worker is confirmed offline while its deployment remains allocated.";
            }
            else if (candidate.Workers.Any(q => q.LastHeartbeatAt is null || q.LastHeartbeatAt < cutoff))
            {
                code = "dedicated-worker-heartbeat-stale";
                message = "A dedicated service worker exceeded the configured heartbeat fault threshold.";
            }
            else if (candidate.PlacementSnapshot is not null
                     && (WorkloadPlacementPlan.TryDeserialize(candidate.PlacementSnapshot) is not { } placement
                         || placement.Workloads.Any(assignment => !candidate.Workers.Any(worker =>
                             worker.ServiceIdentifier == assignment.HostKey.ServiceIdentifier
                             && worker.ServiceOrdinal == assignment.HostKey.ServiceOrdinal
                             && (worker.HasActiveProcessTransition
                                 || HasRunningWorkload(worker.WorkloadObservations, assignment.WorkloadKey, cutoff)
                                 || (placement.Workloads.Count(other => other.HostKey == assignment.HostKey) == 1
                                     && worker.WorkloadObservations == "[]"
                                     && worker.ProcessId is > 0 && worker.ProcessStartedAt is not null))))))
            {
                code = "game-workload-missing";
                message = "A required workload has no current attributed running-process observation.";
            }
            else if (candidate.PlacementSnapshot is null
                     && candidate.Workers.Any(q => !q.HasActiveProcessTransition
                                                   && (q.ProcessId is null || q.ProcessStartedAt is null)))
            {
                code = "game-process-missing";
                message = "A service worker is online but no longer observes its required game process.";
            }

            if (code is not null)
            {
                await automaticCostStopService
                    .TriggerAsync(candidate.PrimaryKey, code, message!, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>Checks fresh process evidence without asserting application readiness or admission.</summary>
    /// <param name="json">Persisted host observations.</param>
    /// <param name="key">Required workload identity.</param>
    /// <param name="cutoff">Oldest acceptable observation instant.</param>
    /// <returns>Whether exactly one current running-process observation exists.</returns>
    private static bool HasRunningWorkload(string json, WorkloadPlacementKey key, Instant cutoff)
    {
        try
        {
            var observations = JsonSerializer.Deserialize<WorkerWorkloadObservation[]>(
                json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var matches = observations?.Where(observation => observation.ServiceIdentifier == key.ServiceIdentifier
                                                            && observation.ServiceOrdinal == key.ServiceOrdinal)
                .ToArray();
            return matches is { Length: 1 }
                   && matches[0].State is EWorkerWorkloadState.Running
                   && matches[0].ProcessId > 0
                   && matches[0].ProcessStartedAt is not null
                   && matches[0].ObservedAt >= cutoff.ToDateTimeOffset();
        }
        catch (JsonException)
        {
            return false;
        }
    }

}
