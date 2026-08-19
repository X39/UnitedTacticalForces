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
                Workers = q.Workers
                    .Where(worker => worker.TimeStampStopped == null)
                    .Select(worker => new
                    {
                        worker.Status,
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
            else if (candidate.Workers.Any(q => q.ProcessId is null || q.ProcessStartedAt is null))
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
}
