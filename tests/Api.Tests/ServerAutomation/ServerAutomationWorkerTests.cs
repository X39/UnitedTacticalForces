using System.Security.Claims;
using Api.Tests.GameServerHosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.HostedServices;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.ServerAutomation;
using X39.UnitedTacticalForces.Api.Services.ServerMetrics;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// AUT-06 proof for the hosted automation loop: it is registered only while the control plane is not paused,
/// publishes after the evaluation returned, survives an evaluator exception, and one failing server does not stop the
/// evaluation of another.
/// </summary>
public sealed class ServerAutomationWorkerTests
{
    private static readonly Instant Started = Instant.FromUnixTimeSeconds(1_700_000_000);

    private static ServiceProvider BuildProvider(
        ScriptedEvaluator evaluator,
        RecordingPublisher publisher)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IServerAutomationEvaluator>(evaluator);
        services.AddSingleton<ICostAlertPublisher>(publisher);
        return services.BuildServiceProvider();
    }

    private static ServerAutomationWorker CreateWorker(ServiceProvider provider)
        => new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ServerAutomationOptions { EvaluationInterval = TimeSpan.FromMilliseconds(20) }),
            NullLogger<ServerAutomationWorker>.Instance);

    /// <summary>The worker is registered only while the control plane is not paused.</summary>
    /// <param name="paused">The pause setting.</param>
    [Theory]
    [InlineData("true", false)]
    [InlineData("false", true)]
    public void AddHostingControlPlane_RegistersTheWorkerOnlyWhenNotPaused(string paused, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [HostingControlPlaneState.ConfigurationKey] = paused,
        }).Build();
        var services = new ServiceCollection();

        services.AddHostingControlPlane(configuration);

        Assert.Equal(
            expected,
            services.Any(q => q.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                              && q.ImplementationType == typeof(ServerAutomationWorker)));
    }

    /// <summary>One pass publishes the raised and the withdrawn alerts after the evaluation returned.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Pass_PublishesRaisedAndResolvedAlertsAfterTheEvaluation()
    {
        var raised = new RaisedCostAlert(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Server",
            Guid.NewGuid(),
            0m,
            0m,
            "EUR",
            Started,
            ECostAlertKind.AutoStop,
            Started + Duration.FromMinutes(10),
            EAutoStopReason.Idle,
            30);
        var resolved = new ResolvedCostAlert(Guid.NewGuid(), raised.GameServerId, raised.CostOwnerId);
        var evaluator = new ScriptedEvaluator(
            _ => new ServerAutomationEvaluationResult([raised], [resolved], 0));
        var publisher = new RecordingPublisher(evaluator);
        await using var provider = BuildProvider(evaluator, publisher);
        var worker = CreateWorker(provider);

        await worker.StartAsync(CancellationToken.None);
        await publisher.Published.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(raised.CostAlertId, publisher.Raised.First().Single().CostAlertId);
        Assert.Equal(resolved.CostAlertId, publisher.Resolved.First().Single().CostAlertId);
        Assert.True(publisher.EvaluationFinishedBeforePublishing);
    }

    /// <summary>An exception of the evaluator is logged and the loop keeps running.</summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Loop_KeepsRunningAfterAnEvaluatorException()
    {
        var evaluator = new ScriptedEvaluator(call => call == 1
            ? throw new InvalidOperationException("boom")
            : new ServerAutomationEvaluationResult([], [], 0));
        var publisher = new RecordingPublisher(evaluator);
        await using var provider = BuildProvider(evaluator, publisher);
        var worker = CreateWorker(provider);

        await worker.StartAsync(CancellationToken.None);
        await evaluator.ThirdCall.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(evaluator.Calls >= 3);
    }

    /// <summary>
    /// A server whose evaluation throws does not stop the evaluation of the next server, and its own state stays
    /// untouched.
    /// </summary>
    /// <returns>A task completing after the assertions ran.</returns>
    [Fact]
    public async Task Evaluator_IsolatesAFailingServer()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var failing = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, failing.Server, q =>
        {
            q.IdleShutdownEnabled = true;
            q.IdleShutdownMinutes = 5;
        });
        var healthy = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        await ServerAutomationTestData.SeedSettingsAsync(store.DbContext, healthy.Server, q =>
        {
            q.MaxRuntimeEnabled = true;
            q.MaxRuntimeHours = 1;
            q.TimeStampUpdated = Started - Duration.FromDays(1);
        });
        var evaluator = new ServerAutomationEvaluator(
            store.DbContext,
            new TestGameServerDefinitionCatalog(),
            new ThrowingMetricsService(failing.Server.PrimaryKey),
            HostingTestData.CreateLifecycleService(store.DbContext),
            ServerAutomationTestData.CreateScheduleRunner(store.DbContext),
            new AutoStopAlertWriter(store.DbContext, new CostAlertRecipientResolver(store.DbContext)),
            new HostingAuditWriter(),
            Options.Create(new ServerAutomationOptions()),
            NullLogger<ServerAutomationEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(Started + Duration.FromMinutes(60), CancellationToken.None);

        Assert.Single(result.RaisedAlerts);
        Assert.Equal(healthy.Server.PrimaryKey, result.RaisedAlerts[0].GameServerId);
        var stop = await store.DbContext.AutomationPendingStops.AsNoTracking().SingleAsync();
        Assert.Equal(healthy.Server.PrimaryKey, stop.GameServerFk);
        Assert.Empty(await store.DbContext.ServerAutomationStates.AsNoTracking().ToArrayAsync());
    }

    /// <summary>The evaluator validates its options on construction because they are bound only in Program.cs.</summary>
    [Fact]
    public void Evaluator_RejectsInvalidOptions()
    {
        Assert.Throws<InvalidOperationException>(() => new ServerAutomationEvaluator(
            null!,
            new TestGameServerDefinitionCatalog(),
            null!,
            null!,
            null!,
            null!,
            new HostingAuditWriter(),
            Options.Create(new ServerAutomationOptions { EvaluationInterval = TimeSpan.Zero }),
            NullLogger<ServerAutomationEvaluator>.Instance));
    }

    /// <summary>An evaluator that returns scripted results by call number.</summary>
    private sealed class ScriptedEvaluator(Func<int, ServerAutomationEvaluationResult> script)
        : IServerAutomationEvaluator
    {
        private int _calls;

        /// <summary>Gets a source that completes with the third evaluation.</summary>
        /// <value>The completion source.</value>
        public TaskCompletionSource ThirdCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the number of evaluations so far.</summary>
        /// <value>The call count.</value>
        public int Calls => Volatile.Read(ref _calls);

        /// <summary>Gets the number of evaluations that returned.</summary>
        /// <value>The returned call count.</value>
        public int Returned { get; private set; }

        /// <inheritdoc />
        public Task<ServerAutomationEvaluationResult> EvaluateAsync(Instant now, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            if (call >= 3)
                ThirdCall.TrySetResult();
            var result = script(call);
            Returned = call;
            return Task.FromResult(result);
        }
    }

    /// <summary>A publisher that records what it was asked to publish.</summary>
    private sealed class RecordingPublisher(ScriptedEvaluator evaluator) : ICostAlertPublisher
    {
        private readonly List<IReadOnlyList<RaisedCostAlert>> _raised = [];
        private readonly List<IReadOnlyList<ResolvedCostAlert>> _resolved = [];

        /// <summary>Gets a source that completes once both publish calls arrived.</summary>
        /// <value>The completion source.</value>
        public TaskCompletionSource Published { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the raised batches in call order.</summary>
        /// <value>The batches.</value>
        public IEnumerable<IReadOnlyList<RaisedCostAlert>> Raised => _raised.Where(q => q.Count > 0);

        /// <summary>Gets the resolved batches in call order.</summary>
        /// <value>The batches.</value>
        public IEnumerable<IReadOnlyList<ResolvedCostAlert>> Resolved => _resolved.Where(q => q.Count > 0);

        /// <summary>Gets whether every publish call happened after an evaluation returned.</summary>
        /// <value><see langword="true" /> when publishing never preceded the evaluation.</value>
        public bool EvaluationFinishedBeforePublishing { get; private set; } = true;

        /// <inheritdoc />
        public Task PublishAccrualsAsync(IReadOnlyList<DeploymentAccrual> accruals, CancellationToken cancellationToken)
            => Task.CompletedTask;

        /// <inheritdoc />
        public Task PublishRaisedAsync(IReadOnlyList<RaisedCostAlert> alerts, CancellationToken cancellationToken)
        {
            EvaluationFinishedBeforePublishing &= evaluator.Returned >= 1;
            lock (_raised)
                _raised.Add(alerts);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task PublishDismissedAsync(
            Guid userId,
            IReadOnlyCollection<Guid> costAlertIds,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        /// <inheritdoc />
        public Task PublishResolvedAsync(IReadOnlyList<ResolvedCostAlert> alerts, CancellationToken cancellationToken)
        {
            EvaluationFinishedBeforePublishing &= evaluator.Returned >= 1;
            lock (_resolved)
                _resolved.Add(alerts);
            Published.TrySetResult();
            return Task.CompletedTask;
        }
    }

    /// <summary>A metrics service that fails for one server, to prove failure isolation.</summary>
    private sealed class ThrowingMetricsService(Guid failingServerId) : IServerMetricsService
    {
        /// <inheritdoc />
        public Task<GameServerMetricsDto> EvaluateCurrentAsync(
            Guid gameServerId,
            Instant now,
            CancellationToken cancellationToken)
            => gameServerId == failingServerId
                ? throw new InvalidOperationException("metrics failure")
                : Task.FromResult(new GameServerMetricsDto { GameServerId = gameServerId, StaleAfterSeconds = 45 });

        /// <inheritdoc />
        public Task<ServerMetricsIngestResult> IngestAsync(
            ServerMetricSampleSource source,
            X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol.WorkerMetricsSample sample,
            Instant receivedAt,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<GameServerMetricsDto> GetCurrentAsync(
            Guid gameServerId,
            ClaimsPrincipal principal,
            Instant now,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<GameServerMetricsHistoryDto> GetHistoryAsync(
            Guid gameServerId,
            ClaimsPrincipal principal,
            EServerMetricsRange range,
            Instant now,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<int> PruneAsync(Instant now, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
