using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using GameDefinition = X39.UnitedTacticalForces.Api.Services.GameServerDefinition.GameServerDefinition;

namespace X39.UnitedTacticalForces.Api.Services.GameServerSecrets;

public interface IGameServerSecretService
{
    void EnsureGeneratedSecrets(GameServer server, GameDefinition definition, Guid? actorId, Instant now);
    Task<IReadOnlyCollection<GameServerSecretStatusDto>> GetStatusesAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task SetAsync(Guid serverId, string key, string value, EGameServerSecretOrigin origin, Guid actorId, CancellationToken cancellationToken = default);
    Task<GameServerSecretValueDto> RevealAsync(Guid serverId, string key, Guid actorId, CancellationToken cancellationToken = default);
    Task RotateAsync(Guid serverId, string key, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> ResolveRequiredAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task<string> CreateProtectedSnapshotAsync(Guid serverId, CancellationToken cancellationToken = default);
    IReadOnlyDictionary<string, string> UnprotectSnapshot(string? protectedSnapshot);
    Task MigrateLegacyConfigurationSecretsAsync(CancellationToken cancellationToken = default);
}
