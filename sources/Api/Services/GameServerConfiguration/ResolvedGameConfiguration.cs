using System.Text.Json;
using X39.UnitedTacticalForces.GameServerImplementations;

namespace X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;

/// <summary>Contains the effective configuration for a server and the profile revision used to resolve it.</summary>
/// <param name="ServerId">The configured server.</param>
/// <param name="Implementation">The implementation that defines and renders the configuration.</param>
/// <param name="ProfileId">The selected configuration profile, if any.</param>
/// <param name="ProfileRevisionId">The resolved configuration profile revision, if any.</param>
/// <param name="ProfileRevisionNumber">The revision number shown to users, if any.</param>
/// <param name="Entries">The resolved settings, including settings consumed by the control plane.</param>
public sealed record ResolvedGameConfiguration(
    Guid ServerId,
    IGameConfigurationImplementation Implementation,
    Guid? ProfileId,
    Guid? ProfileRevisionId,
    int? ProfileRevisionNumber,
    IReadOnlyList<ResolvedConfigurationValue> Entries)
{
    /// <summary>Gets the compatible settings used by game file rendering and worker commands.</summary>
    public IReadOnlyDictionary<string, JsonElement> Values => Entries
        .Where(q => !q.Definition.IsControlPlaneOnly && q.Value.HasValue && q.IsCompatible)
        .ToDictionary(q => q.Definition.Key, q => q.Value!.Value, StringComparer.Ordinal);

    /// <summary>Gets settings that require a valid value before the game configuration can be rendered.</summary>
    public IReadOnlyList<ResolvedConfigurationValue> MissingRequired => Entries
        .Where(q => !q.Definition.IsControlPlaneOnly && q.Definition.Required
            && (!q.Value.HasValue || !q.IsCompatible))
        .ToArray();

    /// <summary>Gets whether all mandatory game file settings have been resolved.</summary>
    public bool IsComplete => MissingRequired.Count == 0;
}
