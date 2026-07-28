namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record RuntimeProviderInfo
{
    public string Identifier { get; init; } = string.Empty;
    public string AvailabilityStatus { get; init; } = "available";
    public IReadOnlyCollection<RuntimeProfileInfo> Profiles { get; init; } = [];
}
