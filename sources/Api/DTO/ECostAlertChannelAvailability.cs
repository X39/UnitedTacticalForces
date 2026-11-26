namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Tells whether a cost alert channel can reach the people a cost owner's alerts are addressed to.
/// </summary>
public enum ECostAlertChannelAvailability
{
    /// <summary>
    /// At least one recipient can be reached on the channel.
    /// </summary>
    Deliverable,

    /// <summary>
    /// The channel works on this installation, but no recipient has linked a Discord account.
    /// </summary>
    NotLinked,

    /// <summary>
    /// The channel works on this installation, but no recipient has an email address.
    /// </summary>
    NoAddress,

    /// <summary>
    /// The channel is switched off or not configured on this installation, so nothing can be delivered on it.
    /// </summary>
    ChannelUnavailable,
}
