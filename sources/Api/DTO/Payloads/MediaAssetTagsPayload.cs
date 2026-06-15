namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Payload used to replace the tags assigned to a media asset.
/// </summary>
public record MediaAssetTagsPayload
{
    /// <summary>
    /// Desired normalized tag set. Invalid or blank entries are ignored.
    /// </summary>
    public string[] Tags { get; init; } = [];
}
