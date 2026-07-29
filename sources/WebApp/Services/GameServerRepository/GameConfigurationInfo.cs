using System.Text.Json;

namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record GameConfigurationInfo
{
    public Guid ServerId { get; init; }
    public Guid? ProfileId { get; init; }
    public Guid? ProfileRevisionId { get; init; }
    public int? ProfileRevisionNumber { get; init; }
    public bool IsComplete { get; init; }
    public IReadOnlyList<GameConfigurationEntryInfo> Entries { get; init; } = [];
}

public sealed record GameConfigurationEntryInfo
{
    public string Key { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public bool Required { get; init; }
    public bool Sensitive { get; init; }
    public JsonElement? Value { get; init; }
    public string Source { get; init; } = string.Empty;
    public bool IsCompatible { get; init; }
    public string? Warning { get; init; }
}

public sealed record ConfigurationProfileOptionInfo
{
    public Guid PrimaryKey { get; init; }
    public string Title { get; init; } = string.Empty;
    public string GameIdentifier { get; init; } = string.Empty;
    public int LatestRevisionNumber { get; init; }
}

public sealed record ConfigurationValueUpdateInfo(string Key, JsonElement Value);
