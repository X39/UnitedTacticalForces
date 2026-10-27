namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Holds the validated startup decision for hosting execution. Changing it requires an API restart so a pause
/// cannot race an already claimed job or provider mutation within this process.
/// </summary>
public sealed class HostingControlPlaneState
{
    /// <summary>The explicit operator setting that controls hosting execution.</summary>
    public const string ConfigurationKey = "Hosting:ControlPlane:Paused";

    /// <summary>The stable diagnostic reason exposed through existing hosting action contracts.</summary>
    public const string PausedReason = "Hosting lifecycle execution is paused by the operator.";

    /// <summary>Validates the operator setting and defaults to a paused control plane when it is absent.</summary>
    /// <param name="configuration">The final application configuration, including operator overrides.</param>
    /// <exception cref="InvalidOperationException">The configured value is not an explicit boolean.</exception>
    public HostingControlPlaneState(IConfiguration configuration)
    {
        var value = configuration[ConfigurationKey];
        if (value is null)
        {
            IsPaused = true;
            return;
        }
        if (!bool.TryParse(value, out var paused))
            throw new InvalidOperationException($"{ConfigurationKey} must be true or false.");
        IsPaused = paused;
    }

    /// <summary>Gets whether hosting background execution and runtime mutation requests are suspended.</summary>
    /// <value>True until the operator explicitly enables execution before starting the API.</value>
    public bool IsPaused { get; }
}
