using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
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
        yield return new Role {PrimaryKey = ++id, Category = "General",        Title = "Admin",                  Identifier = Roles.Admin};
        yield return new Role {PrimaryKey = ++id, Category = "Events",         Title = "Create events",          Identifier = Roles.EventCreate};
        yield return new Role {PrimaryKey = ++id, Category = "Events",         Title = "Modify events",          Identifier = Roles.EventModify};
        yield return new Role {PrimaryKey = ++id, Category = "Events",         Title = "Delete events",          Identifier = Roles.EventDelete};
        yield return new Role {PrimaryKey = ++id, Category = "Terrains",       Title = "Create terrain",         Identifier = Roles.TerrainCreate};
        yield return new Role {PrimaryKey = ++id, Category = "Terrains",       Title = "Modify terrain",         Identifier = Roles.TerrainModify};
        yield return new Role {PrimaryKey = ++id, Category = "Terrains",       Title = "Delete terrain",         Identifier = Roles.TerrainDelete};
        yield return new Role {PrimaryKey = ++id, Category = "ModPacks",       Title = "Create mod pack",        Identifier = Roles.ModPackCreate};
        yield return new Role {PrimaryKey = ++id, Category = "ModPacks",       Title = "Modify mod pack",        Identifier = Roles.ModPackModify};
        yield return new Role {PrimaryKey = ++id, Category = "ModPacks",       Title = "Delete mod pack",        Identifier = Roles.ModPackDelete};
        yield return new Role {PrimaryKey = ++id, Category = "User",           Title = "View SteamId64 of user", Identifier = Roles.UserViewSteamId64};
        yield return new Role {PrimaryKey = ++id, Category = "User",           Title = "View E-Mail of user",    Identifier = Roles.UserViewMail};
        yield return new Role {PrimaryKey = ++id, Category = "User",           Title = "Modify user",            Identifier = Roles.UserModify};
        yield return new Role {PrimaryKey = ++id, Category = "User",           Title = "(Un-)Ban user",          Identifier = Roles.UserBan};
        yield return new Role {PrimaryKey = ++id, Category = "User",           Title = "Manage user roles",      Identifier = Roles.UserManageRoles};
        yield return new Role {PrimaryKey = ++id, Category = "User",           Title = "List users",             Identifier = Roles.UserList};
        yield return new Role {PrimaryKey = ++id, Category = "User",           Title = "Verify users",           Identifier = Roles.UserVerify};
        yield return new Role {PrimaryKey = ++id, Category = "Event-Slotting", Title = "Ignore slot rules",      Identifier = Roles.EventSlotIgnore};
        yield return new Role {PrimaryKey = ++id, Category = "Event-Slotting", Title = "Assign slot",            Identifier = Roles.EventSlotAssign};
        yield return new Role {PrimaryKey = ++id, Category = "Event-Slotting", Title = "Create slot",            Identifier = Roles.EventSlotCreate};
        yield return new Role {PrimaryKey = ++id, Category = "Event-Slotting", Title = "Update slot",            Identifier = Roles.EventSlotUpdate};
        yield return new Role {PrimaryKey = ++id, Category = "Event-Slotting", Title = "Delete slot",            Identifier = Roles.EventSlotDelete};
        yield return new Role {PrimaryKey = ++id, Category = "Server",         Title = "Server Base Role",       Identifier = Roles.ServerAccess};
        yield return new Role {PrimaryKey = ++id, Category = "Server",         Title = "Server start/stop",      Identifier = Roles.ServerStartStop};
        yield return new Role {PrimaryKey = ++id, Category = "Server",         Title = "Server create/delete",   Identifier = Roles.ServerCreateOrDelete};
        yield return new Role {PrimaryKey = ++id, Category = "Server",         Title = "Server config",          Identifier = Roles.ServerUpdate};
        yield return new Role {PrimaryKey = ++id, Category = "Server",         Title = "Server upgrade",         Identifier = Roles.ServerUpgrade};
        yield return new Role {PrimaryKey = ++id, Category = "Server",         Title = "Server mod-pack",        Identifier = Roles.ServerChangeModPack};
        yield return new Role {PrimaryKey = ++id, Category = "Server",         Title = "Server files",           Identifier = Roles.ServerFiles};
        yield return new Role {PrimaryKey = ++id, Category = "Wiki",           Title = "Wiki Editor",            Identifier = Roles.WikiEditor};
        yield return new Role {PrimaryKey = ++id, Category = "Server",         Title = "Server access logs",     Identifier = Roles.ServerLogs};
        yield return new Role {PrimaryKey = ++id, Category = "Server",         Title = "Server clear logs",      Identifier = Roles.ServerLogsClear};
        // @formatter:max_line_length restore
    }
}