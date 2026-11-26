using X39.UnitedTacticalForces.CloudWorker;
using X39.UnitedTacticalForces.Hosting.Contracts.WorkerProtocol;

namespace CloudWorker.Tests;

/// <summary>
/// A hand-written <see cref="IWorkerControlChannel"/> that records every heartbeat, claim, report and metrics
/// sample, answers claims from a queue and lets a test push wakes, supersede reports and fault the channel.
/// </summary>
internal sealed class FakeControlChannel : IWorkerControlChannel
{
    private readonly object _gate = new();
    private readonly Queue<WorkerCommandClaimResponse> _claims = new();
    private readonly Queue<Exception> _claimFailures = new();
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<CancellationToken, Task<string>>? _credentialProvider;
    private int _heartbeatCount;
    private int _claimCount;
    private int _runningReportCount;
    private int _reconnectCount;
    private int _waitUntilConnectedCount;

    /// <summary>Gets a task that faults when <see cref="FaultCompletion"/> is called.</summary>
    /// <value>The channel lifetime task.</value>
    public Task Completion => _completion.Task;

    /// <summary>Gets the connection the runtime started the channel with.</summary>
    /// <value>The connection, or <see langword="null"/> before the start.</value>
    public CloudWorkerConnectionConfig? StartedConnection { get; private set; }

    /// <summary>Gets every credential the channel would have presented on a connect, in order.</summary>
    /// <value>One entry for the start and one per reconnect.</value>
    public List<string> ConnectCredentials { get; } = [];

    /// <summary>Gets or sets a callback run when the runtime asks for a reconnect.</summary>
    /// <value>The callback, or <see langword="null"/>.</value>
    public Func<Task>? OnReconnect { get; set; }

    /// <summary>Gets every heartbeat attempt, including the ones that were answered with a failure.</summary>
    /// <value>The recorded heartbeat bodies.</value>
    public List<WorkerHeartbeatRequest> Heartbeats { get; } = [];

    /// <summary>Gets every report the runtime sent.</summary>
    /// <value>The recorded command id and report pairs.</value>
    public List<(Guid CommandId, WorkerCommandReportRequest Request)> Reports { get; } = [];

    /// <summary>Gets every metrics sample the runtime sent, including those answered with a failure.</summary>
    /// <value>The recorded samples.</value>
    public List<WorkerMetricsSample> Metrics { get; } = [];

    /// <summary>Gets or sets a callback run before a report is recorded and answered.</summary>
    /// <value>The callback, or <see langword="null"/>.</value>
    public Func<WorkerCommandReportRequest, Task>? OnReport { get; set; }

    /// <summary>Gets or sets the 1-based heartbeat number from which heartbeats fail as unavailable.</summary>
    /// <value>The first failing heartbeat number; the default never fails.</value>
    public int FailHeartbeatsFrom { get; set; } = int.MaxValue;

    /// <summary>Gets or sets the 1-based metrics sample number that fails as unavailable once.</summary>
    /// <value>The failing sample number; the default never fails.</value>
    public int FailMetricsAt { get; set; } = int.MaxValue;

    /// <summary>Gets or sets the 1-based Running report number that is answered with supersession.</summary>
    /// <value>The Running report number; the default never supersedes.</value>
    public int SupersedeOnRunningReport { get; set; } = int.MaxValue;

    /// <summary>Gets or sets whether terminal reports are answered with supersession.</summary>
    /// <value><see langword="true"/> to supersede every Succeeded or Failed report.</value>
    public bool SupersedeTerminalReports { get; set; }

    /// <summary>Gets the number of claims the runtime sent.</summary>
    /// <value>The claim count.</value>
    public int ClaimCount => Volatile.Read(ref _claimCount);

    /// <summary>Gets the number of heartbeat attempts.</summary>
    /// <value>The heartbeat count.</value>
    public int HeartbeatCount => Volatile.Read(ref _heartbeatCount);

    /// <summary>Gets the number of reconnects the runtime requested.</summary>
    /// <value>The reconnect count.</value>
    public int ReconnectCount => Volatile.Read(ref _reconnectCount);

    /// <summary>Gets the number of times the runtime waited for a connection.</summary>
    /// <value>The wait count.</value>
    public int WaitUntilConnectedCount => Volatile.Read(ref _waitUntilConnectedCount);

    /// <summary>Gets the number of Running reports sent.</summary>
    /// <value>The Running report count.</value>
    public int RunningReportCount => Volatile.Read(ref _runningReportCount);

    /// <summary>Queues a command that the next claim returns.</summary>
    /// <param name="command">The command to deliver.</param>
    public void EnqueueClaim(WorkerCommandClaimResponse command)
    {
        lock (_gate)
            _claims.Enqueue(command);
    }

    /// <summary>Queues an exception that the next claim throws before any command is returned.</summary>
    /// <param name="exception">The exception to throw.</param>
    public void EnqueueClaimFailure(Exception exception)
    {
        lock (_gate)
            _claimFailures.Enqueue(exception);
    }

    /// <summary>Pushes a wake to the runtime.</summary>
    public void PushWake()
    {
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // A pending wake is already latched.
        }
    }

    /// <summary>Faults the channel lifetime task, as the real channel does for a rejected credential.</summary>
    /// <param name="exception">The fault.</param>
    public void FaultCompletion(Exception exception)
        => _completion.TrySetException(exception);

    /// <inheritdoc />
    public async Task StartAsync(
        CloudWorkerConnectionConfig connection,
        Func<CancellationToken, Task<string>> credentialProvider,
        CancellationToken cancellationToken)
    {
        StartedConnection = connection;
        _credentialProvider = credentialProvider;
        var credential = await credentialProvider(cancellationToken);
        lock (_gate)
            ConnectCredentials.Add(credential);
    }

    /// <inheritdoc />
    public Task WaitUntilConnectedAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _waitUntilConnectedCount);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HeartbeatAsync(WorkerHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var number = Interlocked.Increment(ref _heartbeatCount);
        lock (_gate)
            Heartbeats.Add(request);
        return number >= FailHeartbeatsFrom
            ? Task.FromException(new WorkerChannelUnavailableException("The worker channel is down."))
            : Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkerCommandClaimResponse?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _claimCount);
        lock (_gate)
        {
            if (_claimFailures.TryDequeue(out var failure))
                return Task.FromException<WorkerCommandClaimResponse?>(failure);
            return Task.FromResult(_claims.TryDequeue(out var command) ? command : null);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ReportCommandAsync(
        Guid commandId,
        WorkerCommandReportRequest request,
        CancellationToken cancellationToken)
    {
        if (OnReport is { } onReport)
            await onReport(request);
        lock (_gate)
            Reports.Add((commandId, request));
        var running = request.State is EWorkerCommandReportState.Running
            ? Interlocked.Increment(ref _runningReportCount)
            : Volatile.Read(ref _runningReportCount);
        var terminalSuperseded = SupersedeTerminalReports
                                 && request.State is EWorkerCommandReportState.Succeeded
                                     or EWorkerCommandReportState.Failed;
        var runningSuperseded = request.State is EWorkerCommandReportState.Running
                                && running >= SupersedeOnRunningReport;
        return !terminalSuperseded && !runningSuperseded;
    }

    /// <inheritdoc />
    public Task SendMetricsAsync(WorkerMetricsSample sample, CancellationToken cancellationToken)
    {
        int number;
        lock (_gate)
        {
            Metrics.Add(sample);
            number = Metrics.Count;
        }
        return number == FailMetricsAt
            ? Task.FromException(new WorkerChannelUnavailableException("The worker channel is not connected."))
            : Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task WaitForWakeAsync(CancellationToken cancellationToken)
        => _wake.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _reconnectCount);
        if (OnReconnect is { } onReconnect)
            await onReconnect();
        if (_credentialProvider is { } provider)
        {
            var credential = await provider(cancellationToken);
            lock (_gate)
                ConnectCredentials.Add(credential);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _completion.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
