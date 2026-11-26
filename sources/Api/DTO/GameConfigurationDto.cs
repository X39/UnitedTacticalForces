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
    /// <summary>Gets whether the setting controls orchestration instead of a generated game file.</summary>
    /// <value>True when file-conflict checks and file-target presentation do not apply.</value>
    public bool IsControlPlaneOnly { get; init; }
    /// <summary>Gets whether the game definition declares the setting as Common or Advanced.</summary>
    /// <value>The tier declared by the game definition; Advanced when the definition declares nothing.</value>
    public EGameConfigurationTier Tier { get; init; }
    public int Order { get; init; }
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
    public IReadOnlyList<string> AllowedValues { get; init; } = [];
    /// <summary>Gets the typed choices supported by the setting.</summary>
    public IReadOnlyList<GameConfigurationChoice> Choices { get; init; } = [];
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
    /// <summary>Gets the number of the newest revision, or zero when the profile has no revision.</summary>
    public int LatestRevisionNumber { get; init; }
    /// <summary>Gets the identifier of the newest revision so a selection can pin the current revision.</summary>
    /// <value>The primary key of the revision with the highest number, or null without revisions.</value>
    public Guid? LatestRevisionId { get; init; }
    public Instant TimeStampUpdated { get; init; }
}

public sealed record ConfigurationProfileRevisionDto
{
    public Guid PrimaryKey { get; init; }
    public int RevisionNumber { get; init; }
    public Instant TimeStampCreated { get; init; }
    /// <summary>Gets the identifier of the user who created the revision.</summary>
    public Guid CreatedById { get; init; }
    /// <summary>Gets the display name of the user who created the revision.</summary>
    public string CreatedByNickname { get; init; } = string.Empty;
    /// <summary>Gets the non-sensitive configuration snapshot.</summary>
    public IReadOnlyDictionary<string, JsonElement?> Values { get; init; } =
        new Dictionary<string, JsonElement?>(StringComparer.Ordinal);
    /// <summary>Gets the sensitive keys that have a stored value.</summary>
    public IReadOnlyList<string> SensitiveConfiguredKeys { get; init; } = [];
    /// <summary>Gets the changes relative to the preceding revision.</summary>
    public IReadOnlyList<ConfigurationProfileRevisionChangeDto> Changes { get; init; } = [];
}

public sealed record ConfigurationProfileDefinitionCatalogDto
{
    public bool CanCreate { get; init; }
    public IReadOnlyList<GameConfigurationDefinitionSetDto> Games { get; init; } = [];
}

public sealed record GameConfigurationDefinitionSetDto
{
    public string GameIdentifier { get; init; } = string.Empty;
    public IReadOnlyList<GameConfigurationDefinition> Definitions { get; init; } = [];
}
