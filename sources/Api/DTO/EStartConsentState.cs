namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Describes whether scheduled starts of a server may currently run, because a scheduled start costs money and needs
/// the consent of the cost owner that pays for it.
/// </summary>
public enum EStartConsentState
{
    /// <summary>
    /// The server has no start rule, so no consent is needed.
    /// </summary>
    NotRequired = 0,

    /// <summary>
    /// The consent was recorded for the cost owner that currently pays for the server.
    /// </summary>
    Given = 1,

    /// <summary>
    /// A start rule exists but no consent was recorded for the current cost owner, so the rule does not run.
    /// </summary>
    Needed = 2,
}
