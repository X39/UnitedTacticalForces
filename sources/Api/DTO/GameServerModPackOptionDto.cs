namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Active provider-neutral game-content definition available to a durable server.
/// </summary>
public sealed record GameServerModPackOptionDto
{
    public long PrimaryKey { get; init; }

    public string Title { get; init; } = string.Empty;
}
