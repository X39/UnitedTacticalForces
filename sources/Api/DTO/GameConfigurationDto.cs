using System.Text.Json;
using X39.UnitedTacticalForces.GameServerImplementations;

namespace X39.UnitedTacticalForces.Api.DTO;

public sealed record GameConfigurationDto
{
    public Guid ServerId { get; init; }
    public Guid? ProfileId { get; init; }
    public Guid? ProfileRevisionId { get; init; }
    public int? ProfileRevisionNumber { get; init; }
    public bool IsComplete { get; init; }
    public IReadOnlyList<GameConfigurationEntryDto> Entries { get; init; } = [];
}

public sealed record GameConfigurationEntryDto
{
    public string Key { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public EGameConfigurationValueKind Kind { get; init; }
    public bool Required { get; init; }
    public bool Sensitive { get; init; }
    public int Order { get; init; }
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
    public IReadOnlyList<string> AllowedValues { get; init; } = [];
    public JsonElement? Value { get; init; }
    public string Source { get; init; } = string.Empty;
    public bool IsCompatible { get; init; }
    public bool IsRedacted { get; init; }
    public string? Warning { get; init; }
}

public sealed record ConfigurationProfileSummaryDto
{
    public Guid PrimaryKey { get; init; }
    public string Title { get; init; } = string.Empty;
    public string GameIdentifier { get; init; } = string.Empty;
    public bool IsArchived { get; init; }
    public int LatestRevisionNumber { get; init; }
    public Instant TimeStampUpdated { get; init; }
}

public sealed record ConfigurationProfileRevisionDto
{
    public Guid PrimaryKey { get; init; }
    public int RevisionNumber { get; init; }
    public Instant TimeStampCreated { get; init; }
    public IReadOnlyDictionary<string, JsonElement> Values { get; init; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}
