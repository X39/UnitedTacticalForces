using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace X39.UnitedTacticalForces.Api.Data.Authority.Extern;

/// <summary>
/// Represents a user's Steam information.
/// This class is utilized for associating a user's Steam identification with their profile.
/// </summary>
[Owned]
[Index(nameof(Id64))]
public class SteamUser
{
    /// <summary>
    /// Represents the unique 64-bit Steam ID of the user.
    /// </summary>
    /// <remarks>
    /// The Steam ID is a unique identifier provided by the Steam platform for each user.
    /// It is used to associate the user with their Steam account and perform operations
    /// such as authentication and data retrieval within the application.
    /// </remarks>
    public ulong Id64 { get; set; }
}
