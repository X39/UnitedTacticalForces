using System.Text.Json;

namespace X39.UnitedTacticalForces.GameServerImplementations;

public interface IGameConfigurationImplementation
{
    string GameIdentifier { get; }
    IReadOnlyList<GameConfigurationDefinition> Definitions { get; }
    IReadOnlyList<GeneratedConfigurationTarget> Targets { get; }

    IReadOnlyList<RenderedConfigurationFile> Render(IReadOnlyDictionary<string, JsonElement> values);
}
