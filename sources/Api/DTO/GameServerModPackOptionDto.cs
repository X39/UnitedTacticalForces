namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Provider-neutral game-content definition offered to a durable server, with its availability for selection.
/// </summary>
public sealed record GameServerModPackOptionDto
{
    /// <summary>
    /// Gets the primary key of the mod pack definition.
    /// </summary>
    public long PrimaryKey { get; init; }

    /// <summary>
    /// Gets the display title of the mod pack.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the pack can be selected. A server's saved pack stays listed after it became
    /// unavailable so that the chooser can show and explain it.
    /// </summary>
    public bool IsAvailable { get; init; } = true;

    /// <summary>
    /// Gets a safe English reason why the pack is unavailable, or <see langword="null"/> when it is available.
    /// </summary>
    public string? UnavailableReason { get; init; }
}
