using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Services.WorkerProtocol;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that committed worker command and deployment changes wake the affected connected workers through the
/// channel registry after the commit, never for a rolled-back or failed save, and that signal failures never disturb
/// a save or later signals.
/// </summary>
public sealed class WorkerChannelSignalTests
{
    /// <summary>
    /// How long a test waits before concluding that no Wake frame is on its way.
    /// </summary>
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Adding a command for a connected worker and saving pushes a Wake frame to its socket.
    /// </summary>
    /// <returns>A task completing once the Wake was observed.</returns>
    [Fact]
    public async Task AddedCommand_WakesTheConnectedWorkerAfterTheSave()
    {
        await using var fixture = await SignalFixture.CreateAsync();
        var next = fixture.Client.ReceiveAsync();

        await fixture.AddCommandAsync(sequence: 2, EWorkerCommandStatus.Queued);

        var frame = await next;
        Assert.Equal(EWorkerChannelFrameType.Wake, frame.Type);
        Assert.Null(frame.Id);
    }

    /// <summary>
    /// Inside an explicit transaction the Wake arrives only after the commit, and a rolled-back transaction sends
    /// nothing.
    /// </summary>
    /// <returns>A task completing once both transactions were verified.</returns>
    [Fact]
    public async Task TransactionalSave_WakesOnlyAfterCommit_AndNeverAfterRollback()
    {
        await using var fixture = await SignalFixture.CreateAsync();
        var dbContext = fixture.Host.Store.DbContext;

        var next = fixture.Client.ReceiveAsync();
        await using (var rolledBack = await dbContext.Database.BeginTransactionAsync())
        {
            await fixture.AddCommandAsync(sequence: 2, EWorkerCommandStatus.Queued);
            await rolledBack.RollbackAsync();
        }

        Assert.False(await ArrivesAsync(next));
        dbContext.ChangeTracker.Clear();

        await using (var committed = await dbContext.Database.BeginTransactionAsync())
        {
            await fixture.AddCommandAsync(sequence: 3, EWorkerCommandStatus.Queued);
            Assert.False(await ArrivesAsync(next));
            await committed.CommitAsync();
        }

        var frame = await next;
        Assert.Equal(EWorkerChannelFrameType.Wake, frame.Type);
    }

    /// <summary>
    /// A command status change wakes the worker of the command, while a stage-only change sends nothing.
    /// </summary>
    /// <returns>A task completing once both changes were verified.</returns>
    [Fact]
    public async Task CommandStatusChange_Wakes_StageOnlyChange_StaysSilent()
    {
        await using var fixture = await SignalFixture.CreateAsync();
        var dbContext = fixture.Host.Store.DbContext;
        var command = await dbContext.WorkerCommands.SingleAsync(q => q.PrimaryKey == fixture.CommandId);

        var next = fixture.Client.ReceiveAsync();
        command.Stage = "preparing";
        await dbContext.SaveChangesAsync();
        Assert.False(await ArrivesAsync(next));

        command.Status = EWorkerCommandStatus.Received;
        await dbContext.SaveChangesAsync();
        var frame = await next;
        Assert.Equal(EWorkerChannelFrameType.Wake, frame.Type);
    }

    /// <summary>
    /// A deployment status or provisioning-block change wakes the connected workers of that deployment, while a
    /// stage-only change sends nothing.
    /// </summary>
    /// <returns>A task completing once the changes were verified.</returns>
    [Fact]
    public async Task DeploymentStatusAndProvisioningBlock_WakeTheDeploymentsWorkers_StageStaysSilent()
    {
        await using var fixture = await SignalFixture.CreateAsync();
        var dbContext = fixture.Host.Store.DbContext;
        var deployment = await dbContext.GameServerDeployments
            .SingleAsync(q => q.PrimaryKey == fixture.Worker.DeploymentId);

        var next = fixture.Client.ReceiveAsync();
        deployment.Stage = "draining";
        await dbContext.SaveChangesAsync();
        Assert.False(await ArrivesAsync(next));

        deployment.Status = EDeploymentStatus.Terminating;
        await dbContext.SaveChangesAsync();
        Assert.Equal(EWorkerChannelFrameType.Wake, (await next).Type);

        next = fixture.Client.ReceiveAsync();
        deployment.IsProvisioningBlocked = true;
        await dbContext.SaveChangesAsync();
        Assert.Equal(EWorkerChannelFrameType.Wake, (await next).Type);
    }

    /// <summary>
    /// A save that fails on a unique violation sends no Wake.
    /// </summary>
    /// <returns>A task completing once the failed save and the following good save were verified.</returns>
    [Fact]
    public async Task FailedSave_SendsNothing()
    {
        await using var fixture = await SignalFixture.CreateAsync();
        var dbContext = fixture.Host.Store.DbContext;
        var command = await dbContext.WorkerCommands.SingleAsync(q => q.PrimaryKey == fixture.CommandId);
        command.Status = EWorkerCommandStatus.Received;
        await dbContext.SaveChangesAsync();
        Assert.Equal(EWorkerChannelFrameType.Wake, (await fixture.Client.ReceiveAsync()).Type);

        var next = fixture.Client.ReceiveAsync();
        var duplicate = fixture.CreateCommand(sequence: 2, EWorkerCommandStatus.Received);
        dbContext.WorkerCommands.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        Assert.False(await ArrivesAsync(next));

        dbContext.Entry(duplicate).State = EntityState.Detached;
        command.Status = EWorkerCommandStatus.Running;
        await dbContext.SaveChangesAsync();
        Assert.Equal(EWorkerChannelFrameType.Wake, (await next).Type);
    }

    /// <summary>
    /// A registry that throws costs one warning with ids only and never stops the drainer from delivering later
    /// signals.
    /// </summary>
    /// <returns>A task completing once the later signal was delivered.</returns>
    [Fact]
    public async Task ThrowingRegistry_LogsOneWarningPerRequest_AndKeepsDraining()
    {
        var workerId = Guid.NewGuid();
        var laterWorkerId = Guid.NewGuid();
        var registry = new FakeRegistry { ThrowForWorker = workerId };
        var logger = new RecordingLogger();
        var signal = new WorkerChannelSignal(registry, logger);
        await signal.StartAsync(CancellationToken.None);
        try
        {
            signal.WakeWorkers([workerId]);
            signal.WakeWorkers([laterWorkerId]);
            await WaitUntilAsync(() => registry.SentToWorkers.Contains(laterWorkerId));
        }
        finally
        {
            await signal.StopAsync(CancellationToken.None);
        }

        var warning = Assert.Single(logger.Messages);
        Assert.Contains(workerId.ToString(), warning);
        Assert.DoesNotContain("boom", warning);
        Assert.Equal([laterWorkerId], registry.SentToWorkers);
    }

    /// <summary>
    /// A deployment wake is delivered through the registry's deployment push, once per deployment id.
    /// </summary>
    /// <returns>A task completing once the push was recorded.</returns>
    [Fact]
    public async Task WakeDeployments_PushesToTheDeployment()
    {
        var deploymentId = Guid.NewGuid();
        var registry = new FakeRegistry();
        var signal = new WorkerChannelSignal(registry, new RecordingLogger());
        await signal.StartAsync(CancellationToken.None);
        try
        {
            signal.WakeDeployments([deploymentId]);
            signal.WakeDeployments([]);
            await WaitUntilAsync(() => registry.SentToDeployments.Contains(deploymentId));
        }
        finally
        {
            await signal.StopAsync(CancellationToken.None);
        }

        Assert.Equal([deploymentId], registry.SentToDeployments);
    }

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
    /// Polls a condition until it holds or a generous bound elapses.
    /// </summary>
    /// <param name="condition">The condition to await.</param>
    /// <returns>A task completing once the condition held.</returns>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The awaited condition did not hold in time.");
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// A seeded, connected worker whose store carries the real interceptor wired to the real signal.
    /// </summary>
    private sealed class SignalFixture : IAsyncDisposable
    {
        private readonly WorkerChannelSignal _signal;

        private SignalFixture(
            WorkerChannelTestHost host,
            WorkerChannelSignal signal,
            AuthenticatedWorker worker,
            Guid commandId,
            WorkerChannelTestHost.WorkerChannelTestClient client)
        {
            Host = host;
            _signal = signal;
            Worker = worker;
            CommandId = commandId;
            Client = client;
        }

        /// <summary>Gets the host over the store.</summary>
        /// <value>The channel test host.</value>
        public WorkerChannelTestHost Host { get; }

        /// <summary>Gets the connected worker identity.</summary>
        /// <value>The identity the channel was authenticated with.</value>
        public AuthenticatedWorker Worker { get; }

        /// <summary>Gets the seeded command id.</summary>
        /// <value>The id of the Queued command seeded for the worker.</value>
        public Guid CommandId { get; }

        /// <summary>Gets the worker end of the channel; the connect Wake was already consumed.</summary>
        /// <value>The connected test client.</value>
        public WorkerChannelTestHost.WorkerChannelTestClient Client { get; }

        /// <summary>
        /// Creates the fixture: seeds a worker, connects it and consumes the connect Wake.
        /// </summary>
        /// <returns>The ready fixture.</returns>
        public static async Task<SignalFixture> CreateAsync()
        {
            var forwarding = new ForwardingSignal();
            var interceptor = new WorkerChannelSignalInterceptor(
                forwarding,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkerChannelSignalInterceptor>.Instance);
            var store = await HostingTestStore.CreateAsync([interceptor]);
            var host = await WorkerChannelTestHost.CreateAsync(
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
            var (worker, _, commandId) = await host.SeedRegisteredWorkerAsync();
            var client = await host.ConnectAsync();
            Assert.Equal(EWorkerChannelFrameType.Wake, (await client.ReceiveAsync()).Type);
            return new SignalFixture(host, signal, worker, commandId, client);
        }

        /// <summary>
        /// Builds a command for the seeded worker, job and deployment without saving it.
        /// </summary>
        /// <param name="sequence">The command sequence.</param>
        /// <param name="status">The initial command status.</param>
        /// <returns>The unsaved command.</returns>
        public WorkerCommand CreateCommand(long sequence, EWorkerCommandStatus status)
        {
            var seeded = Host.Store.DbContext.WorkerCommands.Single(q => q.PrimaryKey == CommandId);
            return new WorkerCommand
            {
                PrimaryKey = Guid.NewGuid(),
                LifecycleJobFk = seeded.LifecycleJobFk,
                DeploymentFk = seeded.DeploymentFk,
                WorkerFk = seeded.WorkerFk,
                Sequence = sequence,
                ActionIdentifier = seeded.ActionIdentifier,
                Payload = "{}",
                Status = status,
                Stage = "queued",
                TimeStampCreated = Instant.FromUnixTimeSeconds(1_700_000_000),
            };
        }

        /// <summary>
        /// Adds a command for the seeded worker and saves it.
        /// </summary>
        /// <param name="sequence">The command sequence.</param>
        /// <param name="status">The initial command status.</param>
        /// <returns>A task completing once the save finished.</returns>
        public async Task AddCommandAsync(long sequence, EWorkerCommandStatus status)
        {
            Host.Store.DbContext.WorkerCommands.Add(CreateCommand(sequence, status));
            await Host.Store.DbContext.SaveChangesAsync();
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await _signal.StopAsync(CancellationToken.None);
            await Host.DisposeAsync();
            await Host.Store.DisposeAsync();
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

    /// <summary>
    /// A registry double that records pushes and throws for one configured worker.
    /// </summary>
    private sealed class FakeRegistry : IWorkerChannelRegistry
    {
        private readonly object _lock = new();
        private readonly List<Guid> _workers = [];
        private readonly List<Guid> _deployments = [];

        /// <summary>Gets or sets the worker whose push throws.</summary>
        /// <value>The worker id that triggers an exception, or null for none.</value>
        public Guid? ThrowForWorker { get; init; }

        /// <summary>Gets the workers pushed to successfully.</summary>
        /// <value>A snapshot of the recorded worker ids.</value>
        public IReadOnlyList<Guid> SentToWorkers
        {
            get
            {
                lock (_lock)
                    return _workers.ToArray();
            }
        }

        /// <summary>Gets the deployments pushed to.</summary>
        /// <value>A snapshot of the recorded deployment ids.</value>
        public IReadOnlyList<Guid> SentToDeployments
        {
            get
            {
                lock (_lock)
                    return _deployments.ToArray();
            }
        }

        /// <inheritdoc />
        public int ConnectionCount => 0;

        /// <inheritdoc />
        public IDisposable Register(IWorkerChannelConnection connection) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<bool> TrySendAsync(Guid workerId, WorkerChannelFrame frame, CancellationToken cancellationToken)
        {
            if (workerId == ThrowForWorker)
                throw new InvalidOperationException("boom");
            lock (_lock)
                _workers.Add(workerId);
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        public Task SendToDeploymentAsync(
            Guid deploymentId,
            WorkerChannelFrame frame,
            CancellationToken cancellationToken)
        {
            lock (_lock)
                _deployments.Add(deploymentId);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A logger double that records the formatted warnings.
    /// </summary>
    private sealed class RecordingLogger : ILogger<WorkerChannelSignal>
    {
        private readonly List<string> _messages = [];

        /// <summary>Gets the formatted warning messages logged so far.</summary>
        /// <value>A snapshot of the warnings.</value>
        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                    return _messages.ToArray();
            }
        }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel != LogLevel.Warning)
                return;
            lock (_messages)
                _messages.Add(formatter(state, exception));
        }
    }
}
