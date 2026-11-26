using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Database;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Projects the flattened contents of a mod pack for a game server through the same resolver the Start snapshot
/// uses, so a review of a pack change matches what will actually run.
/// </summary>
/// <param name="dbContext">The database context used to load the server and the pack header.</param>
/// <param name="authorizationService">Evaluates the server view permission of the caller.</param>
/// <param name="contentSnapshotResolver">The resolver shared with the Start content snapshot.</param>
[Scoped<GameServerModPackContentService, IGameServerModPackContentService>]
internal sealed class GameServerModPackContentService(
    DatabaseContext dbContext,
    IHostingAuthorizationService authorizationService,
    IGameServerContentSnapshotResolver contentSnapshotResolver) : IGameServerModPackContentService
{
    /// <inheritdoc />
    public async Task<GameServerModPackContentsDto?> GetContentsAsync(
        Guid gameServerId,
        long modPackDefinitionId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (!authorizationService.HasServerPermission(principal, gameServerId, Claims.ResourceBased.Server.View))
            return null;

        var gameIdentifier = await dbContext.GameServers
            .AsNoTracking()
            .Where(q => q.PrimaryKey == gameServerId && !q.IsArchived)
            .Select(q => q.GameIdentifier)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (gameIdentifier is null)
            return null;

        var definition = await dbContext.ModPackDefinitions
            .AsNoTracking()
            .Where(q => q.PrimaryKey == modPackDefinitionId)
            .Select(q => new { q.GameIdentifier, q.IsActive })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (definition is null
            || !string.Equals(definition.GameIdentifier, gameIdentifier, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!definition.IsActive)
            return Unavailable(modPackDefinitionId, ModPackAvailabilityReasons.Inactive);

        var hasActiveRevision = await dbContext.ModPackRevisions
            .AsNoTracking()
            .AnyAsync(q => q.DefinitionFk == modPackDefinitionId && q.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (!hasActiveRevision)
            return Unavailable(modPackDefinitionId, ModPackAvailabilityReasons.NoActiveRevision);

        try
        {
            var references = await contentSnapshotResolver
                .ResolveAsync(gameIdentifier, modPackDefinitionId, cancellationToken)
                .ConfigureAwait(false);
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var items = new List<GameServerModPackContentItemDto>(references.Count);
            foreach (var reference in references)
            {
                var key = $"{reference.Source}:{reference.Identifier}";
                if (!seenKeys.Add(key))
                    continue;
                items.Add(new GameServerModPackContentItemDto
                {
                    Key = key,
                    Title = reference.DisplayName,
                });
            }

            return new GameServerModPackContentsDto
            {
                ModPackDefinitionId = modPackDefinitionId,
                IsAvailable = true,
                Total = items.Count,
                Items = items,
            };
        }
        catch (PermanentLifecycleExecutionException ex)
        {
            return Unavailable(modPackDefinitionId, ex.Message);
        }
        catch (InvalidOperationException)
        {
            return Unavailable(modPackDefinitionId, ModPackAvailabilityReasons.Unresolvable);
        }
    }

    private static GameServerModPackContentsDto Unavailable(long modPackDefinitionId, string reason)
        => new()
        {
            ModPackDefinitionId = modPackDefinitionId,
            IsAvailable = false,
            UnavailableReason = reason,
            Total = 0,
            Items = [],
        };
}
