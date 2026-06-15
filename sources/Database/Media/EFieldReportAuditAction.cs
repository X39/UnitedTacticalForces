namespace X39.UnitedTacticalForces.Database.Media;

/// <summary>
/// Enum representing the possible actions that can be performed on a field report.
/// </summary>
public enum EFieldReportAuditAction
{
    /// <summary>
    /// The field report was created.
    /// </summary>
    Created,

    /// <summary>
    /// The field report content or metadata was updated.
    /// </summary>
    Updated,

    /// <summary>
    /// The field report was published.
    /// </summary>
    Published,

    /// <summary>
    /// The field report was unpublished.
    /// </summary>
    Unpublished,

    /// <summary>
    /// The field report was removed from active administration views.
    /// </summary>
    Deleted,

    /// <summary>
    /// The field report was restored from a deleted state.
    /// </summary>
    Restored,
}
