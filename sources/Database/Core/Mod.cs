using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Core;

[Index(nameof(GameIdentifier), nameof(IsActive))]
[Index(nameof(GameIdentifier), nameof(ProviderIdentifier), nameof(CanonicalSourceIdentifier), IsUnique = true)]
public sealed class Mod
{
    [Key] public Guid PrimaryKey { get; set; }
    [MaxLength(64)] public string GameIdentifier { get; set; } = string.Empty;
    /// <summary>Gets or sets the registered provider identifier.</summary>
    [MaxLength(64)] public string ProviderIdentifier { get; set; } = string.Empty;
    /// <summary>Gets or sets the provider-normalized stable source identifier.</summary>
    [MaxLength(512)] public string CanonicalSourceIdentifier { get; set; } = string.Empty;
    [MaxLength(256)] public string Title { get; set; } = string.Empty;
    /// <summary>Gets or sets how the mod is used by clients and servers.</summary>
    public EModUsageRole UsageRole { get; set; }
    public bool IsActive { get; set; } = true;
    public Instant TimeStampCreated { get; set; }
    public ICollection<ModVersion> Versions { get; set; } = [];
}
