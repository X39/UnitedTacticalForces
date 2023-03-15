using JetBrains.Annotations;
using X39.UnitedTacticalForces.Api.Data.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController;

/// <summary>
/// Implements additional methods for <see cref="IGameServerController"/>.
/// </summary>
/// <remarks>
/// This should be used when implementing a <see cref="IGameServerController"/>.
/// </remarks>
[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public interface IGameServerControllerCreatable : IGameServerController
{
    /// <summary>
    /// The identifier of the <see cref="IGameServerControllerCreatable"/>.
    /// </summary>
    static abstract string Identifier { get; }

    /// <summary>
    /// Creates a new <see cref="IGameServerControllerCreatable"/> instance.
    /// </summary>
    static abstract Task<IGameServerController> CreateAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        GameServer gameServer);
}