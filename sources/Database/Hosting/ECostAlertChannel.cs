namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// The outbound channel a cost alert is delivered through. Values are persisted as integers and must never be
/// renumbered once rows exist.
/// </summary>
public enum ECostAlertChannel
{
    /// <summary>
    /// A direct message through the Discord bot to the recipient's linked Discord account.
    /// </summary>
    Discord = 0,

    /// <summary>
    /// An email message to the recipient's stored address.
    /// </summary>
    Email = 1,
}
