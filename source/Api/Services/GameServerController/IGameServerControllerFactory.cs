using X39.UnitedTacticalForces.Api.Data.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController;

public interface IGameServerControllerFactory
{
    /// <summary>
    /// Creates or receives a controller for a <see cref="GameServer"/>.
    /// </summary>
    /// <param name="gameServer">The <see cref="GameServer"/> to return the controller from.</param>
    /// <returns>A valid <see cref="IGameServerController"/></returns>
    Task<IGameServerController> GetGameControllerAsync(GameServer gameServer);

    /// <summary>
    /// Receives the available game controller types.
    /// </summary>
    /// <returns>All game controller types available.</returns>
    IEnumerable<string> GetGameControllers();
}