namespace X39.UnitedTacticalForces.Api.Data.Wiki;

/// <summary>
/// Enum representing the possible actions that can be performed on a <see cref="WikiPage"/>.
/// </summary>
/// <remarks>
/// Relevant for <see cref="WikiPageAudit.Action"/>.
/// </remarks>
/// <seealso cref="WikiPageAudit"/>
public enum EWikiPageAuditAction
{
    /// <summary>
    /// The page was created.
    /// </summary>
    Created,

    /// <summary>
    /// The page title was changed.
    /// </summary>
    TitleChanged,

    /// <summary>
    /// The page got deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// The page got restored from a deleted state.
    /// </summary>
    Restored,
}