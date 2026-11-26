namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// One content item of a flattened mod pack, keyed so that two packs can be compared item by item.
/// </summary>
public sealed record GameServerModPackContentItemDto
{
    /// <summary>
    /// Gets the comparison key in the form <c>{source}:{identifier}</c>, unique within one contents response.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display title of the content item.
    /// </summary>
    public string Title { get; init; } = string.Empty;
}
