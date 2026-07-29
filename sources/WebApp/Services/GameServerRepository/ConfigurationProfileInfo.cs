using System.Text.Json;

namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public sealed record ConfigurationProfileInfo
{
    public Guid PrimaryKey { get; init; }
    public string Title { get; init; } = string.Empty;
    public string GameIdentifier { get; init; } = string.Empty;
    public bool IsArchived { get; init; }
    public int LatestRevisionNumber { get; init; }
}

public sealed record ConfigurationProfileDetailsInfo
{
    public ConfigurationProfileInfo Profile { get; init; } = new();
    public JsonElement Definitions { get; init; }
    public IReadOnlyList<ConfigurationProfileRevisionInfo> Revisions { get; init; } = [];
}

public sealed record ConfigurationProfileRevisionInfo
{
    public Guid PrimaryKey { get; init; }
    public int RevisionNumber { get; init; }
    public DateTimeOffset TimeStampCreated { get; init; }
    public IReadOnlyDictionary<string, JsonElement?> Values { get; init; }
        = new Dictionary<string, JsonElement?>(StringComparer.Ordinal);
    public IReadOnlyList<string> RedactedKeys { get; init; } = [];
}
