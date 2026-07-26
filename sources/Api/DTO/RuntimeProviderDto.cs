namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Runtime provider metadata shown while configuring a server.
/// </summary>
public sealed record RuntimeProviderDto
{
    public string Identifier { get; init; } = string.Empty;
    public IReadOnlyCollection<RuntimeProfileDto> Profiles { get; init; } = [];
}
