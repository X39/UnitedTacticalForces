using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using X39.UnitedTacticalForces.Api.Data.Meta;

namespace X39.UnitedTacticalForces.Api.Data.Wiki;

/// <summary>
/// Represents a page in the wiki, full with revision support.
/// </summary>
[Index(nameof(Title))]
public class WikiPage : IPrimaryKey<Guid>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// The title of the page.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <inheritdoc />
    public DateTimeOffset TimeStampCreated { get; set; }

    /// <summary>
    /// The content of the page and its revisions.
    /// The latest revision is always the one with the highest <see cref="WikiPageRevision.TimeStampCreated"/>.
    /// </summary>
    public ICollection<WikiPageRevision>? Revisions { get; set; }

    /// <summary>
    /// Whether this page is deleted.
    /// </summary>
    /// <remarks>
    /// Deleted pages are left in the database for archival purposes.
    /// </remarks>
    public bool IsDeleted { get; set; }
}