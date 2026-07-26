namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record RuntimeProviderInfo
{
    public string Identifier { get; init; } = string.Empty;
    public IReadOnlyCollection<RuntimeProfileInfo> Profiles { get; init; } = [];
}
