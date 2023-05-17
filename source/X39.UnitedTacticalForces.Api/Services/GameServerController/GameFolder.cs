namespace X39.UnitedTacticalForces.Api.Services.GameServerController;

/// <summary>
/// Information about game folder.
/// </summary>
public class GameFolder
{
    /// <summary>
    /// Unique identifier of the game folder.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// User facing name of the game folder.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// User facing description of the game folder.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// The allowed extensions for files in this folder.
    /// </summary>
    public ICollection<string>? AllowedExtensions { get; set; }
}