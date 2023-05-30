using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using X39.UnitedTacticalForces.Api.Data.Authority;

namespace X39.UnitedTacticalForces.Api.Data.Core;

public class ModPackDefinition
{
    [Key]
    public long PrimaryKey { get; set; }
    public DateTimeOffset TimeStampCreated { get; set; }
    public string Title { get; set; } = string.Empty;

    [ForeignKey(nameof(OwnerFk))]
    public User? Owner { get; set; }

    public Guid OwnerFk { get; set; }
    public bool IsActive { get; set; }
    public ICollection<ModPackRevision>? ModPackRevisions { get; set; }
}