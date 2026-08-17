using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace X39.UnitedTacticalForces.Database.Core;

[Index(nameof(GameIdentifier), nameof(IsActive))]
public sealed class Mod
{
    [Key] public Guid PrimaryKey { get; set; }
    [MaxLength(64)] public string GameIdentifier { get; set; } = string.Empty;
    [MaxLength(256)] public string Title { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Instant TimeStampCreated { get; set; }
    public ICollection<ModVersion> Versions { get; set; } = [];
}
