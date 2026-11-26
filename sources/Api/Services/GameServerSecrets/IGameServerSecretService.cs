using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using GameDefinition = X39.UnitedTacticalForces.Api.Services.GameServerDefinition.GameServerDefinition;

namespace X39.UnitedTacticalForces.Api.Services.GameServerSecrets;

/// <summary>
/// Stores, rotates, reveals and resolves the encrypted secrets of game servers. Every change is audited and recorded
/// in the pending-start signal.
/// </summary>
public interface IGameServerSecretService
{
    /// <summary>
    /// Adds the first version of every platform-generated secret that the game declares to a new server. The
    /// versions are added to the tracked entity and saved by the caller.
    /// </summary>
    /// <param name="server">The tracked game server that receives the secrets.</param>
    /// <param name="definition">The game definition that declares the generated secrets.</param>
    /// <param name="actorId">The identifier of the user that creates the server, if any.</param>
    /// <param name="now">The creation instant of the versions.</param>
    void EnsureGeneratedSecrets(GameServer server, GameDefinition definition, Guid? actorId, Instant now);

    /// <summary>
    /// Lists every secret slot the game declares for a server together with its configuration state. No value is
    /// part of the result.
    /// </summary>
    /// <param name="serverId">The identifier of the game server.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The status of every declared secret slot.</returns>
    /// <exception cref="HostingNotFoundException">The game server does not exist.</exception>
    /// <exception cref="HostingValidationException">The game of the server is unsupported.</exception>
    Task<IReadOnlyCollection<GameServerSecretStatusDto>> GetStatusesAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new current version of a user-provided or externally issued secret and records the credentials area
    /// as waiting for the next start.
    /// </summary>
    /// <param name="serverId">The identifier of the game server.</param>
    /// <param name="key">The secret key as declared by the game definition.</param>
    /// <param name="value">The new secret value. It is never logged or audited.</param>
    /// <param name="origin">The origin of the value; generated secrets cannot be supplied.</param>
    /// <param name="actorId">The identifier of the user that performs the change.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the new version, the pending mark and the audit record are saved.</returns>
    /// <exception cref="HostingNotFoundException">The game server does not exist.</exception>
    /// <exception cref="HostingValidationException">The key, origin or value is not acceptable.</exception>
    /// <exception cref="HostingConflictException">The game server is archived.</exception>
    Task SetAsync(Guid serverId, string key, string value, EGameServerSecretOrigin origin, Guid actorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current value of a revealable secret and records an audit entry.
    /// </summary>
    /// <param name="serverId">The identifier of the game server.</param>
    /// <param name="key">The secret key as declared by the game definition.</param>
    /// <param name="actorId">The identifier of the user that reveals the value.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The current value and its version.</returns>
    /// <exception cref="HostingNotFoundException">The game server does not exist or the secret is not configured.</exception>
    /// <exception cref="HostingForbiddenException">The secret cannot be revealed.</exception>
    /// <exception cref="HostingValidationException">The key is not supported by the game.</exception>
    Task<GameServerSecretValueDto> RevealAsync(Guid serverId, string key, Guid actorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a platform-generated secret with a freshly generated version and records the credentials area as
    /// waiting for the next start.
    /// </summary>
    /// <param name="serverId">The identifier of the game server.</param>
    /// <param name="key">The key of the generated secret as declared by the game definition.</param>
    /// <param name="actorId">The identifier of the user that performs the rotation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>
    /// The newly generated value and its version. The caller shows the value once to the acting user; earlier
    /// versions are never returned.
    /// </returns>
    /// <exception cref="HostingNotFoundException">The game server does not exist.</exception>
    /// <exception cref="HostingValidationException">The key is unknown or the secret is not platform-generated.</exception>
    /// <exception cref="HostingConflictException">The game server is archived.</exception>
    Task<GameServerSecretValueDto> RotateAsync(Guid serverId, string key, Guid actorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the plaintext of every current secret and verifies that all secrets required for start exist.
    /// </summary>
    /// <param name="serverId">The identifier of the game server.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The current plaintext values by secret key.</returns>
    /// <exception cref="HostingNotFoundException">The game server does not exist.</exception>
    /// <exception cref="HostingValidationException">A secret required for start is not configured.</exception>
    Task<IReadOnlyDictionary<string, string>> ResolveRequiredAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an encrypted snapshot of the current secrets that a start job carries to the worker.
    /// </summary>
    /// <param name="serverId">The identifier of the game server.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The protected snapshot payload.</returns>
    /// <exception cref="HostingNotFoundException">The game server does not exist.</exception>
    /// <exception cref="HostingValidationException">A secret required for start is not configured.</exception>
    Task<string> CreateProtectedSnapshotAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts a snapshot created by <see cref="CreateProtectedSnapshotAsync" />.
    /// </summary>
    /// <param name="protectedSnapshot">The protected snapshot payload, or <see langword="null" /> when absent.</param>
    /// <returns>The plaintext values by secret key; empty when no snapshot is supplied.</returns>
    IReadOnlyDictionary<string, string> UnprotectSnapshot(string? protectedSnapshot);

    /// <summary>
    /// Moves sensitive values from legacy configuration storage into encrypted secrets exactly once.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the migration has run or was already recorded.</returns>
    Task MigrateLegacyConfigurationSecretsAsync(CancellationToken cancellationToken = default);
}
