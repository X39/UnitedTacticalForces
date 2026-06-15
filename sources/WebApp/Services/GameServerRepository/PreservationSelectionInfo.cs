namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record PreservationSelectionInfo
{
    public Guid PrimaryKey { get; init; }
    public string Path { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public bool SeededByGame { get; init; }
}
