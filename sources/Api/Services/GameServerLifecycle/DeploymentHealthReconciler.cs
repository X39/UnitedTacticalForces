using Microsoft.EntityFrameworkCore;
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
                Worker = q.Workers
                    .Where(worker => worker.TimeStampStopped == null)
                    .Select(worker => new
                    {
                        worker.Status,
                        worker.LastHeartbeatAt,
                        worker.ProcessId,
                        worker.ProcessStartedAt,
                    })
                    .SingleOrDefault(),
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? code = null;
            string? message = null;
            if (candidate.Worker is null)
            {
                code = "dedicated-worker-missing";
                message = "The running deployment has no allocated dedicated worker identity.";
            }
            else if (candidate.Worker.Status is EWorkerStatus.Offline or EWorkerStatus.Faulted)
            {
                code = "dedicated-worker-offline";
                message = "The dedicated worker is confirmed offline while its deployment remains allocated.";
            }
            else if (candidate.Worker.LastHeartbeatAt is null || candidate.Worker.LastHeartbeatAt < cutoff)
            {
                code = "dedicated-worker-heartbeat-stale";
                message = "The dedicated worker exceeded the configured heartbeat fault threshold.";
            }
            else if (candidate.Worker.ProcessId is null || candidate.Worker.ProcessStartedAt is null)
            {
                code = "game-process-missing";
                message = "The worker is online but no longer observes the required game process.";
            }

            if (code is not null)
            {
                await automaticCostStopService
                    .TriggerAsync(candidate.PrimaryKey, code, message!, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
