using System.Security.Claims;
using X39.UnitedTacticalForces.Api.DTO;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Read-only projection of the flattened contents of a mod pack for a durable game server.
/// </summary>
public interface IGameServerModPackContentService
{
    /// <summary>
    /// Resolves the contents of one mod pack with the same resolver the Start snapshot uses.
    /// </summary>
    /// <param name="gameServerId">The game server the pack is inspected for.</param>
    /// <param name="modPackDefinitionId">The primary key of the mod pack definition.</param>
    /// <param name="principal">The caller, who needs the server view permission.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>
    /// The contents, which report <see cref="GameServerModPackContentsDto.IsAvailable"/> as <see langword="false"/>
    /// with a safe reason when the pack cannot be resolved; <see langword="null"/> when the server is missing or
    /// archived, the caller lacks the view permission, or the pack is unknown or belongs to another game.
    /// </returns>
    Task<GameServerModPackContentsDto?> GetContentsAsync(
        Guid gameServerId,
        long modPackDefinitionId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
