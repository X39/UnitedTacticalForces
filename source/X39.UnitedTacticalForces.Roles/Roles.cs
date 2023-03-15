namespace X39.UnitedTacticalForces;

public static class Roles
{
    public const string Admin             = "admin";
    public const string EventCreate       = "event-create";
    public const string EventModify       = "event-modify";
    public const string EventDelete       = "event-delete";
    public const string TerrainCreate     = "terrain-create";
    public const string TerrainModify     = "terrain-modify";
    public const string TerrainDelete     = "terrain-delete";
    public const string ModPackCreate     = "modpack-create";
    public const string ModPackModify     = "modpack-modify";
    public const string ModPackDelete     = "modpack-delete";
    public const string UserList          = "user-list";
    public const string UserViewSteamId64 = "user-view-steamid64";
    public const string UserViewMail      = "user-view-mail";
    public const string UserModify        = "user-modify";
    
    /// <summary>
    /// Allows a user to change the verified status (and view unverified users + the status) of another user
    /// </summary>
    public const string UserVerify        = "user-verify";
    
    /// <summary>
    /// Role that allows a user to use the web-application for role selection, accepting events and more.
    /// </summary>
    /// <remarks>
    /// This role exists to prevent unauthorized access from users.
    /// </remarks>
    public const string Verified = "verified";
    
    /// <summary>
    /// Allows a user to manage the ban status (and view the ban status) of another user.
    /// </summary>
    public const string UserBan           = "user-ban";
    /// <summary>
    /// Allows a user to manage the roles of other users.
    /// Available roles to manage depend on the own role unless admin role is given.
    /// </summary>
    public const string UserManageRoles   = "user-roles";

    /// <summary>
    /// Allows a user to ignore slotting rules.
    /// </summary>
    public const string EventSlotIgnore = "event-slot-ignore";

    /// <summary>
    /// Allows a user to assign other users into a slot regardless of slotting rules.
    /// </summary>
    public const string EventSlotAssign = "event-slot-assign";
    /// <summary>
    /// Allows a user to create new event slots for any events.
    /// </summary>
    public const string EventSlotCreate = "event-slot-create";
    /// <summary>
    /// Allows a user to update existing event slots for any events.
    /// </summary>
    public const string EventSlotUpdate = "event-slot-update";
    /// <summary>
    /// Allows a user to delete existing event slots for any events.
    /// </summary>
    public const string EventSlotDelete = "event-slot-delete";
    /// <summary>
    /// Base role to access game-server related things.
    /// </summary>
    public const string ServerAccess = "server-access";
    /// <summary>
    /// Allows a user to manage the started/stopped state of a server.
    /// </summary>
    public const string ServerStartStop = "server-start-stop";
    /// <summary>
    /// Allows a user to create or delete a server.
    /// </summary>
    public const string ServerCreateOrDelete = "server-create-delete";
    /// <summary>
    /// Allows a user to update the configuration files of a server.
    /// </summary>
    /// <remarks>
    /// This implies <see cref="ServerStartStop"/> to a certain degree as changing the mod pack requires restarting
    /// the server.
    /// </remarks>
    public const string ServerUpdate = "server-update";
    /// <summary>
    /// Allows a user to upgrade a server version and the mods version to the latest.
    /// </summary>
    /// <remarks>
    /// This implies <see cref="ServerStartStop"/> to a certain degree as changing the mod pack requires restarting
    /// the server.
    /// </remarks>
    public const string ServerUpgrade = "server-upgrade";
    /// <summary>
    /// Allows a user to change the active mod pack of a server.
    /// </summary>
    /// <remarks>
    /// This implies <see cref="ServerStartStop"/> to a certain degree as changing the mod pack requires restarting
    /// the server.
    /// </remarks>
    public const string ServerChangeModPack = "server-change-modpack";
}