using System.Text.Json;
using X39.UnitedTacticalForces.GameServerImplementations;

namespace X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;

public sealed record ResolvedGameConfiguration(
    Guid ServerId,
    IGameConfigurationImplementation Implementation,
    Guid? ProfileId,
    Guid? ProfileRevisionId,
    int? ProfileRevisionNumber,
    IReadOnlyList<ResolvedConfigurationValue> Entries)
{
    public IReadOnlyDictionary<string, JsonElement> Values => Entries
        .Where(q => q.Value.HasValue && q.IsCompatible)
        .ToDictionary(q => q.Definition.Key, q => q.Value!.Value, StringComparer.Ordinal);

    public IReadOnlyList<ResolvedConfigurationValue> MissingRequired => Entries
        .Where(q => q.Definition.Required && (!q.Value.HasValue || !q.IsCompatible))
        .ToArray();

    public bool IsComplete => MissingRequired.Count == 0;
}
