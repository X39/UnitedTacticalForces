namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The automation of one game server: the saved settings, the limits the control plane enforces, what the game
/// supports, the live status and the start consent.
/// </summary>
/// <remarks>
/// The block is part of the server detail only. Every instant in it is calculated by the control plane; clients
/// display the values and never derive them.
/// </remarks>
public sealed record ServerAutomationDto
{
    /// <summary>
    /// Gets the saved automation settings, or the defaults when nothing was saved yet.
    /// </summary>
    /// <value>The editable settings including the schedule rules and the revision to send back on save.</value>
    public required ServerAutomationSettingsDto Settings { get; init; }

    /// <summary>
    /// Gets the limits the control plane enforces on the settings.
    /// </summary>
    /// <value>The allowed ranges and counts, so clients never hard-code them.</value>
    public required ServerAutomationLimitsDto Limits { get; init; }

    /// <summary>
    /// Gets whether the game of the server supports idle shutdown.
    /// </summary>
    /// <value>The idle shutdown capability of the game.</value>
    public required ServerIdleShutdownCapabilityDto IdleShutdown { get; init; }

    /// <summary>
    /// Gets the live automation status of the server.
    /// </summary>
    /// <value>The idle clock, the maximum runtime stop, the next scheduled action and a pending stop.</value>
    public required ServerAutomationStatusDto Status { get; init; }

    /// <summary>
    /// Gets whether scheduled starts of the server may run.
    /// </summary>
    /// <value>The start consent state for the cost owner that currently pays for the server.</value>
    public required ServerStartConsentDto StartConsent { get; init; }
}
