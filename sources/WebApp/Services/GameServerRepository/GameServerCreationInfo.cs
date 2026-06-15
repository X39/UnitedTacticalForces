namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

/// <summary>
/// Creates a durable record only; it deliberately does not start a deployment.
/// </summary>
public sealed record GameServerCreationInfo
{
    public string Title { get; init; } = string.Empty;
    public string GameIdentifier { get; init; } = string.Empty;
    public string RuntimeProviderIdentifier { get; init; } = string.Empty;
    public Guid? CostOwnerId { get; init; }
    public bool IsLaunchDisabled { get; init; }
}
