namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

public interface IGameServerDefinitionCatalog
{
    IReadOnlyList<GameServerDefinition> GetDefinitions();

    GameServerDefinition? Find(string gameIdentifier)
        => GetDefinitions().FirstOrDefault(q => string.Equals(
            q.GameIdentifier,
            gameIdentifier,
            StringComparison.OrdinalIgnoreCase));
}
