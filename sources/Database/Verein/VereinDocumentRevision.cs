using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Verein;

/// <summary>
/// Represents one markdown revision of a Verein document.
/// </summary>
[Index(nameof(DocumentFk), nameof(Version), IsUnique = true)]
[Index(nameof(DocumentFk), nameof(IsPublished), nameof(TimeStampPublished))]
public class VereinDocumentRevision : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// Document this revision belongs to.
    /// </summary>
    [ForeignKey(nameof(DocumentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public VereinDocument? Document { get; set; }

    /// <summary>
    /// Foreign key to <see cref="Document"/>.
    /// </summary>
    public long DocumentFk { get; set; }

    /// <summary>
    /// Monotonic document-local revision number.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Display title.
    /// </summary>
    [MaxLength(180)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Short document summary.
    /// </summary>
    [MaxLength(512)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Markdown body.
    /// </summary>
    public string Markdown { get; set; } = string.Empty;

    /// <summary>
    /// Revision comment, similar to a commit message.
    /// </summary>
    [MaxLength(512)]
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Whether this revision is visible to public readers.
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Publication timestamp for public ordering.
    /// </summary>
    public Instant? TimeStampPublished { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// User who authored this revision.
    /// </summary>
    [ForeignKey(nameof(AuthorFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? Author { get; set; }

    /// <summary>
    /// Foreign key to <see cref="Author"/>.
    /// </summary>
    public Guid? AuthorFk { get; set; }
}
