namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// The flattened contents of one mod pack as the next Start would resolve them for a game server.
/// </summary>
public sealed record GameServerModPackContentsDto
{
    /// <summary>
    /// Gets the primary key of the mod pack definition the contents belong to.
    /// </summary>
    public long ModPackDefinitionId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the pack can be resolved. When <see langword="false"/>,
    /// <see cref="Items"/> is empty and <see cref="UnavailableReason"/> explains why.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Gets a safe English reason why the pack cannot be resolved, or <see langword="null"/> when it is available.
    /// </summary>
    public string? UnavailableReason { get; init; }

    /// <summary>
    /// Gets the number of items in <see cref="Items"/>.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Gets the content items in resolution order, unique by <see cref="GameServerModPackContentItemDto.Key"/>.
    /// </summary>
    public IReadOnlyList<GameServerModPackContentItemDto> Items { get; init; } = [];
}
