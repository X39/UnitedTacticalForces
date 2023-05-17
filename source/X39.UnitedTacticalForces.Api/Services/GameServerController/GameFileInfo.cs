namespace X39.UnitedTacticalForces.Api.Services.GameServerController;

/// <summary>
/// Information about a file in a game folder.
/// </summary>
/// <param name="Name">The name of the file.</param>
/// <param name="Size">The size of the file in bytes.</param>
/// <param name="MimeType">The mime-type of the file.</param>
public record GameFileInfo(string Name, long Size, string MimeType);