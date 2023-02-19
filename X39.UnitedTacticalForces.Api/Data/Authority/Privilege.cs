using Microsoft.EntityFrameworkCore;

namespace X39.UnitedTacticalForces.Api.Data.Authority;

[Index(nameof(ClaimCode), IsUnique = true)]
public class Privilege
{
    public long Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ClaimCode { get; set; } = string.Empty;

    internal static IEnumerable<Privilege> StaticData()
    {
        var id = 0;
        yield return new Privilege {Id = ++id, Category = "Events", Title = "Events erstellen", ClaimCode = "event-create"};
        yield return new Privilege {Id = ++id, Category = "Events", Title = "Alle events bearbeiten", ClaimCode = "event-modify"};
        yield return new Privilege {Id = ++id, Category = "Events", Title = "Alle events löschen", ClaimCode = "event-delete"};
        yield return new Privilege {Id = ++id, Category = "Terrains", Title = "Terrain anlegen", ClaimCode = "terrain-create"};
        yield return new Privilege {Id = ++id, Category = "Terrains", Title = "Terrain bearbeiten", ClaimCode = "terrain-modify"};
        yield return new Privilege {Id = ++id, Category = "Terrains", Title = "Terrain löschen", ClaimCode = "terrain-delete"};
        yield return new Privilege {Id = ++id, Category = "ModPacks", Title = "ModPack anlegen", ClaimCode = "modpack-create"};
        yield return new Privilege {Id = ++id, Category = "ModPacks", Title = "ModPack bearbeiten", ClaimCode = "modpack-modify"};
        yield return new Privilege {Id = ++id, Category = "ModPacks", Title = "ModPack löschen", ClaimCode = "modpack-delete"};
    }
}