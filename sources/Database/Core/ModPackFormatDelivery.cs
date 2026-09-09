using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Eventing;

namespace X39.UnitedTacticalForces.Database.Core;

/// <summary>Records successful delivery of a generated mod-pack format.</summary>
[Index(nameof(ModPackRevisionFk), nameof(TimeStampCreated))]
[Index(nameof(EventFk), nameof(TimeStampCreated))]
public sealed class ModPackFormatDelivery
{
    /// <summary>Gets or sets the delivery identifier.</summary>
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>Gets or sets when the generated format was delivered.</summary>
    public Instant TimeStampCreated { get; set; }

    /// <summary>Gets or sets the immutable revision that was exported.</summary>
    [ForeignKey(nameof(ModPackRevisionFk))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public ModPackRevision? ModPackRevision { get; set; }

    /// <summary>Gets or sets the immutable revision identifier.</summary>
    public long ModPackRevisionFk { get; set; }

    /// <summary>Gets or sets the game-owned format identifier.</summary>
    [MaxLength(64)]
    public string FormatIdentifier { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional Event whose selected revision was delivered.</summary>
    [ForeignKey(nameof(EventFk))]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public Event? Event { get; set; }

    /// <summary>Gets or sets the optional Event identifier for Event-context delivery.</summary>
    public Guid? EventFk { get; set; }

    /// <summary>Gets or sets the authenticated recipient, or null for an anonymous public Event delivery.</summary>
    [ForeignKey(nameof(UserFk))]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public User? User { get; set; }

    /// <summary>Gets or sets the optional authenticated recipient identifier.</summary>
    public Guid? UserFk { get; set; }
}
