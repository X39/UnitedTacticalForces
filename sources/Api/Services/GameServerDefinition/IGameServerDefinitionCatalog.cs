namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

public interface IGameServerDefinitionCatalog
{
    IReadOnlyList<GameServerDefinition> GetDefinitions();
}
