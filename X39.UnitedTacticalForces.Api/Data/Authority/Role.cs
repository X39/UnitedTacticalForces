using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace X39.UnitedTacticalForces.Api.Data.Authority;

[Index(nameof(Identifier), IsUnique = true)]
public class Role
{
    [Key]public long PrimaryKey { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    
    public ICollection<User>? Users { get; set; }

    internal static IEnumerable<Role> StaticData()
    {
        var id = 0;
        yield return new Role {PrimaryKey = ++id, Category = "General", Title = "Admin", Identifier = Constants.Roles.Admin};
        yield return new Role {PrimaryKey = ++id, Category = "Events", Title = "Events erstellen", Identifier = Constants.Roles.EventCreate};
        yield return new Role {PrimaryKey = ++id, Category = "Events", Title = "Alle events bearbeiten", Identifier = Constants.Roles.EventModify};
        yield return new Role {PrimaryKey = ++id, Category = "Events", Title = "Alle events löschen", Identifier = Constants.Roles.EventDelete};
        yield return new Role {PrimaryKey = ++id, Category = "Terrains", Title = "Terrain anlegen", Identifier = Constants.Roles.TerrainCreate};
        yield return new Role {PrimaryKey = ++id, Category = "Terrains", Title = "Terrain bearbeiten", Identifier = Constants.Roles.TerrainModify};
        yield return new Role {PrimaryKey = ++id, Category = "Terrains", Title = "Terrain löschen", Identifier = Constants.Roles.TerrainDelete};
        yield return new Role {PrimaryKey = ++id, Category = "ModPacks", Title = "ModPack anlegen", Identifier = Constants.Roles.ModPackCreate};
        yield return new Role {PrimaryKey = ++id, Category = "ModPacks", Title = "ModPack bearbeiten", Identifier = Constants.Roles.ModPackModify};
        yield return new Role {PrimaryKey = ++id, Category = "ModPacks", Title = "ModPack löschen", Identifier = Constants.Roles.ModPackDelete};
    }
}