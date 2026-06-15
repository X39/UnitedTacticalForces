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
    public int MaxConcurrentJobs { get; set; } = 4;
    public int MaxConcurrentNormalJobs { get; set; } = 3;
    public TimeSpan IdlePollInterval { get; set; } = TimeSpan.FromSeconds(2);
}
