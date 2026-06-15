using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Extends estimated cost authorization while resources may remain billable and initiates cost-stop on exhaustion.
/// </summary>
[Scoped<HostingCostReconciler, IHostingCostReconciler>]
internal sealed class HostingCostReconciler(
    DatabaseContext dbContext,
    IHostingCostSliceService costSliceService,
    IAutomaticCostStopService automaticCostStopService,
    IOptions<HostingFinanceOptions> options,
    ILogger<HostingCostReconciler> logger) : IHostingCostReconciler
{
    private readonly HostingFinanceOptions _options = options.Value;

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        _options.Validate();
        var renewBefore = NodaSystemClock.Instance.GetCurrentInstant()
                          + Duration.FromTimeSpan(_options.ReconciliationInterval);
        var deployments = await dbContext.GameServerDeployments
            .AsNoTracking()
            .Where(q => q.TimeStampResolved == null
                        && q.PossibleCost
                        && (q.CostAuthorizationExpiresAt == null || q.CostAuthorizationExpiresAt <= renewBefore))
            .Select(q => new
            {
                q.PrimaryKey,
                ProviderIdentifier = q.GameServer!.RuntimeProviderIdentifier,
                q.IsProvisioningBlocked,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var deployment in deployments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await costSliceService.AuthorizeNextSliceAsync(
                        deployment.PrimaryKey,
                        deployment.ProviderIdentifier,
                        enforceAffordability: !deployment.IsProvisioningBlocked,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                !deployment.IsProvisioningBlocked
                && ex is HostingAffordabilityException or InvalidOperationException)
            {
                await automaticCostStopService.TriggerAsync(
                        deployment.PrimaryKey,
                        ex is HostingAffordabilityException
                            ? "cost-owner-credit-exhausted"
                            : "provider-cost-policy-invalid",
                        HostingFailureSanitizer.Sanitize(ex.Message, 2048),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (deployment.IsProvisioningBlocked)
            {
                // Teardown must continue even if accounting configuration is broken. Keep the
                // failure visible without allowing one deployment to block reconciliation of all others.
                logger.LogError(
                    "Could not extend cleanup cost accounting for deployment {DeploymentId}: {Failure}",
                    deployment.PrimaryKey,
                    HostingFailureSanitizer.Sanitize(ex.Message, 2048));
            }
        }
    }
}
