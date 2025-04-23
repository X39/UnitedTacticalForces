namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Partial WikiPageRevision, containing the minimal information required to display a list of revisions.
/// </summary>
/// <param name="PrimaryKey">The primary key of the WikiPageRevision.</param>
/// <param name="TimeStampCreated">Timestamp of when this revision was created.</param>
/// <param name="AuthorPrimaryKey">The primary key of the author of this revision.</param>
/// <param name="AuthorName">The name of the author of this revision.</param>
/// <param name="Comment">The comment of this revision.</param>
public record WikiPageRevisionHeader(
    Guid PrimaryKey,
    DateTimeOffset TimeStampCreated,
    Guid AuthorPrimaryKey,
    string AuthorName,
    string Comment);
