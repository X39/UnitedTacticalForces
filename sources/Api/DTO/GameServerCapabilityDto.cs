namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Game implementation metadata used by the generic creation flow.
/// </summary>
public sealed record GameServerCapabilityDto
{
    public string GameIdentifier { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyCollection<string> RuntimeProviders { get; init; } = [];
    public IReadOnlyCollection<GameServerActionDto> Actions { get; init; } = [];
}
