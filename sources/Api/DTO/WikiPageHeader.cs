namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Partial WikiPage, containing the minimal information required to display a list of pages.
/// </summary>
/// <param name="PrimaryKey">The primary key of the WikiPage.</param>
/// <param name="Name">The name of the WikiPage.</param>
/// <param name="LastModified">Timestamp of when this page was last modified.</param>
public record WikiPageHeader(Guid PrimaryKey, string Name, DateTimeOffset LastModified);
