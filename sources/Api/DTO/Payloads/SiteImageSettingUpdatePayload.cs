namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Payload for updating a public-site image slot.
/// </summary>
public record SiteImageSettingUpdatePayload
{
    /// <summary>
    /// Replacement media asset id.
    /// </summary>
    public Guid ImageAssetFk { get; init; }
}
