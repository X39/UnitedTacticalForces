using System.Text.Json;
using X39.UnitedTacticalForces.GameServerImplementations;

namespace X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;

public interface IGameServerConfigurationService
{
    Task<ResolvedGameConfiguration> ResolveAsync(Guid serverId, CancellationToken cancellationToken = default);
    IReadOnlyList<RenderedConfigurationFile> Render(ResolvedGameConfiguration configuration);
    JsonElement ValidateAndClone(string gameIdentifier, string key, JsonElement value);
}
