namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Tunable control-plane lease and external-worker health settings.
/// </summary>
public sealed class LifecycleWorkerOptions
{
    public const string SectionName = "Hosting:Worker";

    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan StaleThreshold { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan JobLease { get; set; } = TimeSpan.FromSeconds(45);
    public TimeSpan LeaseRenewalInterval { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan BootstrapCredentialLifetime { get; set; } = TimeSpan.FromMinutes(45);
    public TimeSpan RegistrationGrace { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan WorkerCredentialLifetime { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan HealthReconciliationInterval { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan FaultEvidenceCollectionWindow { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan ProviderSettlementTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxConcurrentJobs { get; set; } = 4;
    public int MaxConcurrentNormalJobs { get; set; } = 3;
    public TimeSpan IdlePollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the largest text message, in bytes, the control plane accepts from a worker on the persistent
    /// worker channel. A larger message closes the channel with status 1009.
    /// </summary>
    /// <value>The message cap in bytes; defaults to 262144 (256 KiB).</value>
    public int ChannelMaxMessageBytes { get; set; } = 262144;

    /// <summary>
    /// Gets or sets how many frames per second a worker may send on the channel in steady state. Plan 67-06 enforces
    /// the limit.
    /// </summary>
    /// <value>The sustained frame rate; defaults to 20.</value>
    public int ChannelMaxFramesPerSecond { get; set; } = 20;

    /// <summary>
    /// Gets or sets how many frames a worker may send in a burst before the sustained rate applies. Plan 67-06
    /// enforces the limit.
    /// </summary>
    /// <value>The burst size in frames; defaults to 40.</value>
    public int ChannelFrameBurst { get; set; } = 40;

    /// <summary>
    /// Gets or sets the interval at which the control plane sends WebSocket keep-alive pings on a worker channel.
    /// </summary>
    /// <value>The ping interval; defaults to 15 seconds.</value>
    public TimeSpan ChannelKeepAliveInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets how long the control plane waits for a keep-alive answer before it aborts a worker channel.
    /// </summary>
    /// <value>The answer timeout; defaults to 30 seconds.</value>
    public TimeSpan ChannelKeepAliveTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long a single send to a worker may take before the channel is considered broken. Plan 67-06
    /// enforces the limit.
    /// </summary>
    /// <value>The send timeout; defaults to 10 seconds.</value>
    public TimeSpan ChannelSendTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
