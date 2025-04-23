using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace X39.UnitedTacticalForces.Api.Data.Authority.Extern;

/// <summary>
/// Represents a Discord user associated with the application.
/// </summary>
/// <remarks>
/// This class is owned and managed as part of the application's data authority system.
/// </remarks>
[Microsoft.EntityFrameworkCore.Owned]
public class DiscordUser
{
    /// <summary>
    /// Represents the unique identifier of a Discord user.
    /// </summary>
    /// <remarks>
    /// This property stores the Discord User ID as a 64-bit unsigned integer.
    /// It is typically used to map and identify the user within the Discord platform.
    /// </remarks>
    public ulong Id { get; set; }

    /// <summary>
    /// Represents the username of a Discord user.
    /// </summary>
    /// <remarks>
    /// This property stores the Discord user's display name as a string.
    /// It is used to identify the user within the Discord platform and may
    /// not be unique as it is tied to the display name chosen by the user.
    /// </remarks>
    [MaxLength(1024)]
    public string Username { get; set; }
}
