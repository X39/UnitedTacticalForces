using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves the two safety paths of push-driven delivery: a missed push is recovered by the Wake that follows a
/// heartbeat acknowledgement, and a cost-stop decided while a command runs reaches the worker as a Wake followed by a
/// Superseded answer to its own Running probe, without any polling.
/// </summary>
public sealed class WorkerChannelSupersessionTests
{
    /// <summary>
    /// How long a test waits before concluding that no further frame is on its way.
    /// </summary>
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Without the change interceptor registered (a missed push), a heartbeat acknowledgement is followed by a Wake
    /// while a command is deliverable and by nothing once none is.
    /// </summary>
    /// <returns>A task completing once both heartbeats were verified.</returns>
    [Fact]
    public async Task Heartbeat_IsFollowedByAWakeOnlyWhileACommandIsDeliverable()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, commandId) = await host.SeedRegisteredWorkerAsync();
        await using var client = await host.ConnectAsync();
        Assert.Equal(EWorkerChannelFrameType.Wake, (await client.ReceiveAsync()).Type);

        var ack = await client.RequestAsync(Heartbeat(worker));

        Assert.Equal(EWorkerChannelOutcome.Accepted, ack.Outcome);
        var wake = await client.ReceiveAsync();
        Assert.Equal(EWorkerChannelFrameType.Wake, wake.Type);
        Assert.Null(wake.Id);

        var command = await host.Store.DbContext.WorkerCommands.SingleAsync(q => q.PrimaryKey == commandId);
        command.Status = EWorkerCommandStatus.Succeeded;
        await host.Store.DbContext.SaveChangesAsync();

        ack = await client.RequestAsync(Heartbeat(worker));

        Assert.Equal(EWorkerChannelOutcome.Accepted, ack.Outcome);
        var next = client.ReceiveAsync();
        Assert.False(await ArrivesAsync(next));
    }

    /// <summary>
    /// A cost-stop that cancels a running command and terminates its deployment in one save wakes the worker, and
    /// the worker's Running probe after that Wake is answered Superseded.
    /// </summary>
    /// <returns>A task completing once the Wake and the Superseded probe answer were verified.</returns>
    [Fact]
    public async Task CostStopDuringARunningCommand_WakesTheWorker_AndTheRunningProbeIsSuperseded()
    {
        var forwarding = new ForwardingSignal();
        var interceptor = new WorkerChannelSignalInterceptor(
            forwarding,
            NullLogger<WorkerChannelSignalInterceptor>.Instance);
        await using var store = await HostingTestStore.CreateAsync([interceptor]);
        await using var host = await WorkerChannelTestHost.CreateAsync(
            store,
            configure: services =>
            {
                services.AddSingleton<WorkerChannelSignal>();
                services.AddSingleton<IWorkerChannelSignal>(
                    serviceProvider => serviceProvider.GetRequiredService<WorkerChannelSignal>());
            });
        var signal = host.Services.GetRequiredService<WorkerChannelSignal>();
        forwarding.Inner = signal;
        await signal.StartAsync(CancellationToken.None);
        try
        {
            var (worker, credential, commandId) = await host.SeedRegisteredWorkerAsync();
            await ClaimAndReportRunningAsync(host, worker, credential, commandId);
            // The signals of the claim and running report are queued behind the save; let them drain before the
            // worker connects so the Wake observed below belongs to the cost-stop save.
            await Task.Delay(300);
            await using var client = await host.ConnectAsync();
            Assert.Equal(EWorkerChannelFrameType.Wake, (await client.ReceiveAsync()).Type);
            var next = client.ReceiveAsync();
            Assert.False(await ArrivesAsync(next));

            var dbContext = host.Store.DbContext;
            var command = await dbContext.WorkerCommands.SingleAsync(q => q.PrimaryKey == commandId);
            var deployment = await dbContext.GameServerDeployments
                .SingleAsync(q => q.PrimaryKey == worker.DeploymentId);
            command.Status = EWorkerCommandStatus.CancelledByCostStop;
            deployment.IsProvisioningBlocked = true;
            deployment.Status = EDeploymentStatus.Terminating;
            await dbContext.SaveChangesAsync();

            var wake = await next;
            Assert.Equal(EWorkerChannelFrameType.Wake, wake.Type);

            var ack = await client.RequestAsync(new WorkerChannelFrame
            {
                Type = EWorkerChannelFrameType.ReportRunning,
                Id = Guid.NewGuid(),
                CommandId = commandId,
                Payload = JsonSerializer.SerializeToElement(
                    new WorkerCommandProcessRequest
                    {
                        DeploymentId = worker.DeploymentId,
                        Stage = "still-running",
                        ProcessId = 1,
                        ProcessStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                    },
                    WorkerChannelJson.Options),
            });

            Assert.Equal(EWorkerChannelOutcome.Superseded, ack.Outcome);
            Assert.Equal(EWorkerCommandStatus.CancelledByCostStop, command.Status);
        }
        finally
        {
            await signal.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// After a cost-stop no command is deliverable, so a later heartbeat does not wake the worker again.
    /// </summary>
    /// <returns>A task completing once the heartbeat was verified.</returns>
    [Fact]
    public async Task Heartbeat_AfterACostStop_DoesNotWakeTheWorker()
    {
        await using var host = await WorkerChannelTestHost.CreateAsync();
        var (worker, _, commandId) = await host.SeedRegisteredWorkerAsync();
        var dbContext = host.Store.DbContext;
        var command = await dbContext.WorkerCommands.SingleAsync(q => q.PrimaryKey == commandId);
        var deployment = await dbContext.GameServerDeployments.SingleAsync(q => q.PrimaryKey == worker.DeploymentId);
        command.Status = EWorkerCommandStatus.CancelledByCostStop;
        deployment.IsProvisioningBlocked = true;
        deployment.Status = EDeploymentStatus.Terminating;
        await dbContext.SaveChangesAsync();
        await using var client = await host.ConnectAsync();
        Assert.Equal(EWorkerChannelFrameType.Wake, (await client.ReceiveAsync()).Type);

        var ack = await client.RequestAsync(Heartbeat(worker));

        Assert.Equal(EWorkerChannelOutcome.Accepted, ack.Outcome);
        var next = client.ReceiveAsync();
        Assert.False(await ArrivesAsync(next));
    }

    /// <summary>
    /// Claims the seeded command and reports it Running through the protocol service, as the worker would.
    /// </summary>
    /// <param name="host">The host providing the protocol service.</param>
    /// <param name="worker">The seeded worker identity.</param>
    /// <param name="credential">The worker credential.</param>
    /// <param name="commandId">The seeded command id.</param>
    /// <returns>A task completing once the command is Running.</returns>
    private static async Task ClaimAndReportRunningAsync(
        WorkerChannelTestHost host,
        AuthenticatedWorker worker,
        string credential,
        Guid commandId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var protocol = scope.ServiceProvider.GetRequiredService<IWorkerProtocolService>();
        var claim = await protocol.ClaimCommandAsync(
            worker.WorkerId,
            new WorkerCommandClaimRequest { DeploymentId = worker.DeploymentId },
            credential);
        Assert.Equal(commandId, claim!.CommandId);
        await protocol.ReportRunningAsync(
            worker.WorkerId,
            commandId,
            new WorkerCommandProcessRequest
            {
                DeploymentId = worker.DeploymentId,
                Stage = "running",
                ProcessId = 1,
                ProcessStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            },
            credential);
    }

    /// <summary>
    /// Builds a heartbeat request frame for a worker.
    /// </summary>
    /// <param name="worker">The authenticated worker identity.</param>
    /// <returns>The heartbeat frame with a fresh correlation id.</returns>
    private static WorkerChannelFrame Heartbeat(AuthenticatedWorker worker)
        => new()
        {
            Type = EWorkerChannelFrameType.Heartbeat,
            Id = Guid.NewGuid(),
            Payload = JsonSerializer.SerializeToElement(
                new WorkerHeartbeatRequest { DeploymentId = worker.DeploymentId },
                WorkerChannelJson.Options),
        };

    /// <summary>
    /// Reports whether a pending receive completes within the quiet period.
    /// </summary>
    /// <param name="pending">The receive task started before the action under test.</param>
    /// <returns>True when a frame arrived within the quiet period.</returns>
    private static async Task<bool> ArrivesAsync(Task<WorkerChannelFrame> pending)
    {
        try
        {
            await pending.WaitAsync(QuietPeriod);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Forwards to a signal assigned after construction, because the interceptor must exist before the store and the
    /// signal needs the host built on that store.
    /// </summary>
    private sealed class ForwardingSignal : IWorkerChannelSignal
    {
        /// <summary>Gets or sets the signal that receives the forwarded calls.</summary>
        /// <value>The real signal, or null before it was assigned.</value>
        public IWorkerChannelSignal? Inner { get; set; }

        /// <inheritdoc />
        public void WakeWorkers(IReadOnlyCollection<Guid> workerIds) => Inner?.WakeWorkers(workerIds);

        /// <inheritdoc />
        public void WakeDeployments(IReadOnlyCollection<Guid> deploymentIds) => Inner?.WakeDeployments(deploymentIds);
    }
}
