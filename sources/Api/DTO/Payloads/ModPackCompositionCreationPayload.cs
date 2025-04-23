namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Represents a payload for composing a ModPack.
/// </summary>
/// <remarks>
/// This class is used when creating a ModPackDefinition, representing
/// a collection of revisions associated with the ModPack. It holds information
/// about the linked ModPack revisions and the title of the composition.
/// </remarks>
public record ModPackCompositionCreationPayload
{
    /// <summary>
    /// Gets or sets the collection of identifiers for the revisions included in the ModPack composition.
    /// </summary>
    public long[] RevisionIds { get; init; } = [];

    /// <summary>
    /// Gets or sets the title of the ModPack composition.
    /// </summary>
    public string Title { get; set; } = string.Empty;
}
