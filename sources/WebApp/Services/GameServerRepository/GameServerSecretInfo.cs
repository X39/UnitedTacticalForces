namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record GameServerSecretInfo
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool Required { get; init; }
    public bool Generated { get; init; }
    public bool Revealable { get; init; }
    public bool IsPresent { get; init; }
    public string? Origin { get; init; }
    public int? Version { get; init; }
    public DateTimeOffset? TimeStampCreated { get; init; }
}

public sealed record GameServerSecretValueInfo(string Key, string Value, int Version);
