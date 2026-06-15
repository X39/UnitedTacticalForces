namespace X39.UnitedTacticalForces.Database.Verein;

/// <summary>
/// Audit actions for public Verein settings changes.
/// </summary>
public enum EVereinSettingsAuditAction
{
    /// <summary>
    /// The settings row was created.
    /// </summary>
    Created,

    /// <summary>
    /// The settings row was updated.
    /// </summary>
    Updated,
}
