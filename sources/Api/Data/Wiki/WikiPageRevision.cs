using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Meta;

namespace X39.UnitedTacticalForces.Api.Data.Wiki;

/// <summary>
/// Represents a single revision of a <see cref="WikiPage"/>,
/// allowing to track changes across time and, possibly, revert them.
/// </summary>
[Index(nameof(PageForeignKey), nameof(TimeStampCreated), AllDescending = true)]
public class WikiPageRevision : IPrimaryKey<Guid>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// The page this revision belongs to.
    /// </summary>
    [ForeignKey(nameof(PageForeignKey))]
    public WikiPage? Page { get; set; }

    /// <summary>
    /// Foreign key to <see cref="Page"/>.
    /// </summary>
    public Guid PageForeignKey { get; set; }

    /// <inheritdoc />
    public DateTimeOffset TimeStampCreated { get; set; }

    /// <summary>
    /// The markdown content of this revision.
    /// </summary>
    public string Markdown { get; set; } = string.Empty;

    /// <summary>
    /// The author of this revision.
    /// </summary>
    [ForeignKey(nameof(AuthorForeignKey))]
    public User? Author { get; set; }

    /// <summary>
    /// The foreign key to <see cref="Author"/>.
    /// </summary>
    public Guid AuthorForeignKey { get; set; }

    /// <summary>
    /// A comment for this revision, left by the author.
    /// </summary>
    /// <remarks>
    /// This is similar to a commit message in git.
    /// </remarks>
    public string Comment { get; set; } = string.Empty;
}
