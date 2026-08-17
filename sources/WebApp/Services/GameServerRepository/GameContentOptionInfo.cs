namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record GameContentOptionInfo
{
    public string PrimaryKey { get; init; } = string.Empty;
    public string GameIdentifier { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public IReadOnlyCollection<GameContentVersionOptionInfo> Versions { get; init; } = [];
}

public sealed record GameContentVersionOptionInfo
{
    public Guid PrimaryKey { get; init; }
    public int VersionNumber { get; init; }
    public string VersionLabel { get; init; } = string.Empty;
}
