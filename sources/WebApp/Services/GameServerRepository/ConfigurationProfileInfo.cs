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
    public bool CanModify { get; init; }
    public bool CanManageAccess { get; init; }
    public bool CanArchive { get; init; }
    public IReadOnlyList<GameConfigurationDefinitionInfo> Definitions { get; init; } = [];
    public IReadOnlyList<ConfigurationProfileRevisionInfo> Revisions { get; init; } = [];
}

public sealed record ConfigurationProfileDefinitionCatalogInfo
{
    public bool CanCreate { get; init; }
    public IReadOnlyList<GameConfigurationDefinitionSetInfo> Games { get; init; } = [];
}

public sealed record GameConfigurationDefinitionSetInfo
{
    public string GameIdentifier { get; init; } = string.Empty;
    public IReadOnlyList<GameConfigurationDefinitionInfo> Definitions { get; init; } = [];
}

public sealed record GameConfigurationDefinitionInfo
{
    public string Key { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public bool Required { get; init; }
    public bool Sensitive { get; init; }
    public int Order { get; init; }
    public JsonElement? DefaultValue { get; init; }
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
    public IReadOnlyList<string> AllowedValues { get; init; } = [];
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

public sealed record ConfigurationProfileAccessInfo
{
    public IReadOnlyList<string> AllowedPermissions { get; init; } = [];
    public IReadOnlyList<ConfigurationProfileUserAccessGrantInfo> Users { get; init; } = [];
    public IReadOnlyList<ConfigurationProfileRoleAccessGrantInfo> Roles { get; init; } = [];
}

public sealed record ConfigurationProfileUserAccessGrantInfo
{
    public Guid UserId { get; init; }
    public string Nickname { get; init; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; init; } = [];
}

public sealed record ConfigurationProfileRoleAccessGrantInfo
{
    public long RoleId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
