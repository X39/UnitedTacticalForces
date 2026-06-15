using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.HostedServices;

/// <summary>
/// Claims durable lifecycle jobs and coordinates provider and external-worker work.
/// This service is part of the control plane and never executes a game process.
/// </summary>
internal sealed class GameServerControlPlaneWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<LifecycleWorkerOptions> options,
    ILogger<GameServerControlPlaneWorker> logger) : BackgroundService
{
    private readonly string _leaseOwner = $"control-plane:{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    private readonly LifecycleWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateOptions();
        logger.LogInformation("Durable game-server control-plane processor started as {LeaseOwner}", _leaseOwner);
        var running = new List<RunningJob>();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RemoveCompletedAsync(running).ConfigureAwait(false);
                var claimedAny = false;
                while (running.Count < _options.MaxConcurrentJobs)
                {
                    var normalCount = running.Count(q => q.IsNormal);
                    var terminateOnly = normalCount >= _options.MaxConcurrentNormalJobs;
                    var claimed = await TryClaimAsync(terminateOnly, stoppingToken).ConfigureAwait(false);
                    if (claimed is null)
                        break;
                    running.Add(claimed);
                    claimedAny = true;
                }

                if (claimedAny)
                    continue;

                var delay = Task.Delay(_options.IdlePollInterval, stoppingToken);
                if (running.Count == 0)
                {
                    await delay.ConfigureAwait(false);
                }
                else
                {
                    await Task.WhenAny(Task.WhenAny(running.Select(q => q.Execution)), delay)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "The lifecycle control-plane loop failed and will retry: {Failure}",
                    HostingFailureSanitizer.Sanitize(ex.Message));
                await DelayAfterFailureAsync(_options.IdlePollInterval, stoppingToken).ConfigureAwait(false);
            }
        }

        await ObserveAllAsync(running).ConfigureAwait(false);
    }

    private async Task<RunningJob?> TryClaimAsync(bool terminateOnly, CancellationToken stoppingToken)
    {
        var scope = scopeFactory.CreateScope();
        try
        {
            var queue = scope.ServiceProvider.GetRequiredService<ILifecycleJobQueue>();
            var job = await queue
                .ClaimNextAsync(_leaseOwner, _options.JobLease, stoppingToken, terminateOnly)
                .ConfigureAwait(false);
            if (job is null)
            {
                scope.Dispose();
                return null;
            }

            return new RunningJob(
                RunClaimedAsync(scope, queue, job, stoppingToken),
                job.Kind is not ELifecycleJobKind.Terminate);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    private async Task RunClaimedAsync(
        IServiceScope scope,
        ILifecycleJobQueue queue,
        LifecycleJob job,
        CancellationToken stoppingToken)
    {
        try
        {
            await ProcessJobAsync(scope.ServiceProvider, queue, job, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(
                "Lifecycle job {LifecycleJobId} coordination failed and will recover from its lease: {Failure}",
                job.PrimaryKey,
                HostingFailureSanitizer.Sanitize(ex.Message));
        }
        finally
        {
            scope.Dispose();
        }
    }

    private static async Task RemoveCompletedAsync(IList<RunningJob> running)
    {
        for (var index = running.Count - 1; index >= 0; index--)
        {
            if (!running[index].Execution.IsCompleted)
                continue;
            await running[index].Execution.ConfigureAwait(false);
            running.RemoveAt(index);
        }
    }

    private static async Task ObserveAllAsync(IEnumerable<RunningJob> running)
    {
        foreach (var execution in running.Select(q => q.Execution))
            await execution.ConfigureAwait(false);
    }

    private async Task ProcessJobAsync(
        IServiceProvider serviceProvider,
        ILifecycleJobQueue queue,
        LifecycleJob job,
        CancellationToken stoppingToken)
    {
        var executor = serviceProvider.GetRequiredService<ILifecycleJobExecutor>();
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var renewalCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var leaseLost = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var renewal = RenewLeaseAsync(
            queue,
            job.PrimaryKey,
            executionCancellation,
            leaseLost,
            renewalCancellation.Token);
        Exception? executionFailure = null;
        try
        {
            await executor.ExecuteAsync(job, executionCancellation.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            executionFailure = ex;
        }

        await renewalCancellation.CancelAsync().ConfigureAwait(false);
        await ObserveRenewalCompletionAsync(renewal).ConfigureAwait(false);
        if (leaseLost.Task.IsCompleted)
        {
            var renewalFailure = await leaseLost.Task.ConfigureAwait(false);
            logger.LogWarning(
                renewalFailure,
                "Stopped coordinating lifecycle job {LifecycleJobId} after its durable lease was lost.",
                job.PrimaryKey);
            return;
        }

        if (executionFailure is null)
        {
            await queue.CompleteAsync(job.PrimaryKey, _leaseOwner, stoppingToken).ConfigureAwait(false);
            return;
        }
        if (executionFailure is OperationCanceledException && stoppingToken.IsCancellationRequested)
            throw executionFailure;

        var failure = HostingFailureSanitizer.Sanitize(executionFailure.Message);
        logger.LogError(
            "Lifecycle job {LifecycleJobId} failed in the control plane: {Failure}",
            job.PrimaryKey,
            failure);
        await queue
            .FailAsync(
                job.PrimaryKey,
                _leaseOwner,
                failure,
                _options.RetryDelay,
                executionFailure is not PermanentLifecycleExecutionException,
                stoppingToken)
            .ConfigureAwait(false);
    }

    private async Task RenewLeaseAsync(
        ILifecycleJobQueue queue,
        Guid lifecycleJobId,
        CancellationTokenSource executionCancellation,
        TaskCompletionSource<Exception?> leaseLost,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_options.LeaseRenewalInterval, cancellationToken).ConfigureAwait(false);
                if (await queue
                        .RenewLeaseAsync(lifecycleJobId, _leaseOwner, _options.JobLease, cancellationToken)
                        .ConfigureAwait(false))
                    continue;

                leaseLost.TrySetResult(null);
                await executionCancellation.CancelAsync().ConfigureAwait(false);
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when execution finishes or the host stops.
        }
        catch (Exception ex)
        {
            leaseLost.TrySetResult(ex);
            await executionCancellation.CancelAsync().ConfigureAwait(false);
        }
    }

    private static async Task ObserveRenewalCompletionAsync(Task renewal)
    {
        try
        {
            await renewal.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected after the linked renewal token is stopped.
        }
    }

    private static async Task DelayAfterFailureAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller exits on the next loop condition.
        }
    }

    private void ValidateOptions()
    {
        if (_options.JobLease <= _options.LeaseRenewalInterval
            || _options.LeaseRenewalInterval <= TimeSpan.Zero
            || _options.RetryDelay < TimeSpan.Zero
            || _options.RegistrationGrace <= TimeSpan.Zero
            || _options.BootstrapCredentialLifetime <= _options.RegistrationGrace
            || _options.FaultEvidenceCollectionWindow <= TimeSpan.Zero
            || _options.FaultEvidenceCollectionWindow > TimeSpan.FromMinutes(10)
            || _options.MaxConcurrentJobs <= 0
            || _options.MaxConcurrentNormalJobs <= 0
            || _options.MaxConcurrentNormalJobs >= _options.MaxConcurrentJobs
            || _options.IdlePollInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "Hosting:Worker contains invalid control-plane lease or polling durations.");
        }
    }

    private sealed record RunningJob(Task Execution, bool IsNormal);
}
