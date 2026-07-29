using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

public sealed record ConfigurationValuePayload
{
    [Required, MaxLength(256)]
    public string Key { get; init; } = string.Empty;
    public JsonElement Value { get; init; }
}

public sealed record ServerConfigurationUpdatePayload
{
    public IReadOnlyList<ConfigurationValuePayload> Values { get; init; } = [];
    public IReadOnlyList<string> ResetKeys { get; init; } = [];
}

public sealed record ConfigurationProfileCreationPayload
{
    [Required, MaxLength(256)]
    public string Title { get; init; } = string.Empty;
    [Required, MaxLength(128)]
    public string GameIdentifier { get; init; } = string.Empty;
    public IReadOnlyList<ConfigurationValuePayload> Values { get; init; } = [];
}

public sealed record ConfigurationProfileRevisionCreationPayload
{
    public IReadOnlyList<ConfigurationValuePayload> Values { get; init; } = [];
}

public sealed record GameServerConfigurationProfileSelectionPayload
{
    public Guid? ProfileId { get; init; }
    public Guid? PinnedRevisionId { get; init; }
}
