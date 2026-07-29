using System.Text.Json;

namespace X39.UnitedTacticalForces.GameServerImplementations;

/// <summary>
/// Represents a supported game definition that has no structured configuration targets.
/// </summary>
public sealed class EmptyGameConfigurationImplementation(string gameIdentifier) : IGameConfigurationImplementation
{
    public string GameIdentifier { get; } = gameIdentifier;
    public IReadOnlyList<GameConfigurationDefinition> Definitions { get; } = [];
    public IReadOnlyList<GeneratedConfigurationTarget> Targets { get; } = [];
    public IReadOnlyList<RenderedConfigurationFile> Render(IReadOnlyDictionary<string, JsonElement> values)
        => values.Count == 0
            ? []
            : throw new InvalidDataException("A game without structured configuration cannot render values.");
}
