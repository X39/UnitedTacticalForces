using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace X39.UnitedTacticalForces.Api.Data.Authority;

[Index(nameof(Identifier), IsUnique = true)]
public class Role
{
    [Key]
    public long PrimaryKey { get; set; }

    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;

    public ICollection<User>? Users { get; set; }

    internal static IEnumerable<Role> StaticData()
    {
        var id = 0;
        // @formatter:max_line_length 2000
        yield return new Role {PrimaryKey = ++id, Category = "General", Title = "Admin", Identifier = Roles.Admin};
        yield return new Role {PrimaryKey = ++id, Category = "Events", Title = "Create events", Identifier = Roles.EventCreate};
        yield return new Role {PrimaryKey = ++id, Category = "Events", Title = "Modify events", Identifier = Roles.EventModify};
        yield return new Role {PrimaryKey = ++id, Category = "Events", Title = "Delete events", Identifier = Roles.EventDelete};
        yield return new Role {PrimaryKey = ++id, Category = "Terrains", Title = "Create terrain", Identifier = Roles.TerrainCreate};
        yield return new Role {PrimaryKey = ++id, Category = "Terrains", Title = "Modify terrain", Identifier = Roles.TerrainModify};
        yield return new Role {PrimaryKey = ++id, Category = "Terrains", Title = "Delete terrain", Identifier = Roles.TerrainDelete};
        yield return new Role {PrimaryKey = ++id, Category = "ModPacks", Title = "Create mod pack", Identifier = Roles.ModPackCreate};
        yield return new Role {PrimaryKey = ++id, Category = "ModPacks", Title = "Modify mod pack", Identifier = Roles.ModPackModify};
        yield return new Role {PrimaryKey = ++id, Category = "ModPacks", Title = "Delete mod pack", Identifier = Roles.ModPackDelete};
        yield return new Role {PrimaryKey = ++id, Category = "User", Title = "View SteamId64 of user", Identifier = Roles.UserViewSteamId64};
        yield return new Role {PrimaryKey = ++id, Category = "User", Title = "View E-Mail of user", Identifier = Roles.UserViewMail};
        yield return new Role {PrimaryKey = ++id, Category = "User", Title = "Modify user", Identifier = Roles.UserModify};
        yield return new Role {PrimaryKey = ++id, Category = "User", Title = "(Un-)Ban user", Identifier = Roles.UserBan};
        yield return new Role {PrimaryKey = ++id, Category = "User", Title = "Manage user roles", Identifier = Roles.UserManageRoles};
        yield return new Role {PrimaryKey = ++id, Category = "User", Title = "List users", Identifier = Roles.UserList};
        // @formatter:max_line_length restore
    }
}