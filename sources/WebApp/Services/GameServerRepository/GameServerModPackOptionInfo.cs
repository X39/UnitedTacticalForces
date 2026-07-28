namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record GameServerModPackOptionInfo
{
    public long PrimaryKey { get; init; }

    public string Title { get; init; } = string.Empty;
}
