namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// The identity of a worker whose credential was verified when its channel connected.
/// </summary>
/// <remarks>
/// This record is produced by authentication and is the only source of attribution for frames received on a worker
/// channel. A frame never names its own worker; routes read the worker and deployment from here.
/// </remarks>
/// <param name="WorkerId">The id of the authenticated worker.</param>
/// <param name="DeploymentId">The id of the deployment the worker is bound to.</param>
/// <param name="GameServerId">The id of the game server owning the deployment.</param>
/// <param name="ServiceIdentifier">The identifier of the service this worker hosts, for example game-server.</param>
/// <param name="ServiceOrdinal">The ordinal of the worker among workers hosting the same service.</param>
public sealed record AuthenticatedWorker(
    Guid WorkerId,
    Guid DeploymentId,
    Guid GameServerId,
    string ServiceIdentifier,
    int ServiceOrdinal)
{
    /// <summary>The service identifier of the primary game-server host.</summary>
    private const string GameServerServiceIdentifier = "game-server";

    /// <summary>Gets a value indicating whether this worker is the primary host of the game server process.</summary>
    /// <value>True when the service identifier is game-server and the ordinal is zero.</value>
    public bool IsPrimaryHost => ServiceIdentifier == GameServerServiceIdentifier && ServiceOrdinal == 0;
}
