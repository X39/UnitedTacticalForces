using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerSecrets;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using GameDefinition = X39.UnitedTacticalForces.Api.Services.GameServerDefinition.GameServerDefinition;

namespace Api.Tests.GameServerHosting;

internal sealed class TestGameServerSecretService : IGameServerSecretService
{
    public void EnsureGeneratedSecrets(GameServer server, GameDefinition definition, Guid? actorId, Instant now)
    {
    }

    public Task<IReadOnlyCollection<GameServerSecretStatusDto>> GetStatusesAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<GameServerSecretStatusDto>>([]);

    public Task SetAsync(Guid serverId, string key, string value, EGameServerSecretOrigin origin, Guid actorId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<GameServerSecretValueDto> RevealAsync(Guid serverId, string key, Guid actorId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new GameServerSecretValueDto(key, string.Empty, 1));

    public Task RotateAsync(Guid serverId, string key, Guid actorId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyDictionary<string, string>> ResolveRequiredAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

    public Task<string> CreateProtectedSnapshotAsync(Guid serverId, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);

    public IReadOnlyDictionary<string, string> UnprotectSnapshot(string? protectedSnapshot)
        => new Dictionary<string, string>();

    public Task MigrateLegacyConfigurationSecretsAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
