namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

/// <summary>
/// Control-plane registration for metadata exposed by a worker-side game implementation.
/// </summary>
public interface IGameServerDefinitionRegistration
{
    GameServerDefinition Definition { get; }
}
