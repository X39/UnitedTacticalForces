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
public class WikiPageAudit : IPrimaryKey<long>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryKey { get; set; }

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
    /// The author of this revision.
    /// </summary>
    [ForeignKey(nameof(UserForeignKey))]
    public User? User { get; set; }

    /// <summary>
    /// The foreign key to <see cref="User"/>.
    /// </summary>
    public Guid UserForeignKey { get; set; }

    /// <summary>
    /// The action that was performed on the page.
    /// </summary>
    public EWikiPageAuditAction Action { get; set; }

    /// <summary>
    /// Data associated with the audit.
    /// </summary>
    /// <remarks>
    /// This is always a JSON object.
    /// </remarks>
    [Column(TypeName = "jsonb")]
    public string Data { get; set; } = "{}";
}