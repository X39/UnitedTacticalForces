namespace X39.UnitedTacticalForces.Api.HostedServices;

/// <summary>
/// Timing of the UAT-only worker emulation that feeds live readings through the worker channel.
/// </summary>
/// <remarks>
/// The defaults mirror a real worker: a heartbeat every 10 seconds and a metrics sample every 15 seconds. Tests lower
/// the values so a run finishes quickly. The options only take effect in the Development environment with the UAT
/// scenarios flag, because the emulator is not registered anywhere else.
/// </remarks>
public sealed class UatWorkerChannelEmulatorOptions
{
    /// <summary>The configuration section the options are bound from.</summary>
    public const string SectionName = "Testing:UatScenarios:WorkerChannelEmulator";

    /// <summary>
    /// Gets or sets how long the emulator waits after the API started before it connects, so seeding and the rest of the
    /// startup have finished.
    /// </summary>
    /// <value>The start delay; five seconds by default.</value>
    public TimeSpan StartDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the interval between two heartbeat frames.
    /// </summary>
    /// <value>The heartbeat interval; ten seconds by default.</value>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the interval between two metrics frames.
    /// </summary>
    /// <value>The metrics interval; fifteen seconds by default.</value>
    public TimeSpan MetricsInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets how long an emulated worker waits for a Wake frame before it claims its next command anyway, so a
    /// missed Wake only delays a command and never strands it.
    /// </summary>
    /// <value>The claim poll interval; five seconds by default.</value>
    public TimeSpan ClaimPollInterval { get; set; } = TimeSpan.FromSeconds(5);
}
