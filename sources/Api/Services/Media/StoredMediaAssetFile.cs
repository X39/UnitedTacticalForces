namespace X39.UnitedTacticalForces.Api.Services.Media;

/// <summary>
/// Safe physical file information for a stored media asset.
/// </summary>
/// <param name="FilePath">Resolved physical file path.</param>
/// <param name="ContentType">Normalized image content type.</param>
/// <param name="Sha256Hash">Hash of the source bytes used to key derived variants.</param>
public sealed record StoredMediaAssetFile(string FilePath, string ContentType, string Sha256Hash);
