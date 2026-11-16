using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerConfiguration;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.UnitedTacticalForces.GameServerImplementations;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Authorize]
[Route(Constants.Routes.GameServers + "/{gameServerId:guid}/configuration")]
public sealed class GameServerConfigurationController(
    DatabaseContext dbContext,
    IGameServerConfigurationService configurationService,
    IHostingAuthorizationService authorizationService,
    IHostingAuditWriter auditWriter) : ControllerBase
{
    [HttpGet]
    [Authorize(Claims.ResourceBased.Server.View)]
    public async Task<IActionResult> GetAsync(
        [FromRoute] Guid gameServerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var configuration = await configurationService.ResolveAsync(gameServerId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(Project(
                configuration,
                authorizationService.HasServerPermission(
                    User,
                    gameServerId,
                    Claims.ResourceBased.Server.Modify)));
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut]
    [Authorize(Claims.ResourceBased.Server.Modify)]
    public async Task<IActionResult> UpdateAsync(
        [FromRoute] Guid gameServerId,
        [FromBody] ServerConfigurationUpdatePayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        var server = await dbContext.GameServers
            .Include(q => q.ConfigurationValues)
            .SingleOrDefaultAsync(q => q.PrimaryKey == gameServerId && !q.IsArchived, cancellationToken)
            .ConfigureAwait(false);
        if (server is null)
            return NotFound();
        var updates = payload.Values
            .GroupBy(q => q.Key, StringComparer.Ordinal)
            .ToArray();
        if (updates.Any(q => q.Count() != 1)
            || payload.ResetKeys.Distinct(StringComparer.Ordinal).Count() != payload.ResetKeys.Count
            || updates.Any(q => payload.ResetKeys.Contains(q.Key, StringComparer.Ordinal)))
            return BadRequest(new { error = "Configuration keys must occur in exactly one update operation." });

        try
        {
            foreach (var update in updates.Select(q => q.Single()))
            {
                var value = configurationService.ValidateAndClone(server.GameIdentifier, update.Key, update.Value);
                var existing = server.ConfigurationValues.SingleOrDefault(q => q.DefinitionKey == update.Key);
                if (existing is null)
                {
                    server.ConfigurationValues.Add(new GameServerConfigurationValue
                    {
                        PrimaryKey = Guid.NewGuid(),
                        DefinitionKey = update.Key,
                        ValueJson = value.GetRawText(),
                        UpdatedByFk = actorId,
                        TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant(),
                    });
                }
                else
                {
                    existing.ValueJson = value.GetRawText();
                    existing.UpdatedByFk = actorId;
                    existing.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
                }
            }
            foreach (var key in payload.ResetKeys)
            {
                if (!GameConfigurationImplementations.Get(server.GameIdentifier).Definitions
                        .Any(q => q.Key == key))
                    return BadRequest(new { error = $"Unknown configuration key '{key}'." });
                var existing = server.ConfigurationValues.SingleOrDefault(q => q.DefinitionKey == key);
                if (existing is not null)
                    dbContext.GameServerConfigurationValues.Remove(existing);
            }
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        server.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ServerConfigurationChanged,
            "Server configuration changed.",
            actorId,
            gameServerId,
            details: $"UpdatedKeys={updates.Length}; ResetKeys={payload.ResetKeys.Count}.");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(Project(
            await configurationService.ResolveAsync(gameServerId, cancellationToken).ConfigureAwait(false),
            true));
    }

    [HttpPut("profile")]
    [Authorize(Claims.ResourceBased.Server.Modify)]
    public async Task<IActionResult> SelectProfileAsync(
        [FromRoute] Guid gameServerId,
        [FromBody] GameServerConfigurationProfileSelectionPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        if (payload.ProfileId is null && payload.PinnedRevisionId is not null)
            return BadRequest(new { error = "A pinned revision requires a selected profile." });
        var server = await dbContext.GameServers
            .SingleOrDefaultAsync(q => q.PrimaryKey == gameServerId && !q.IsArchived, cancellationToken)
            .ConfigureAwait(false);
        if (server is null)
            return NotFound();
        if (payload.ProfileId is { } profileId)
        {
            var profile = await dbContext.ConfigurationProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(q => q.PrimaryKey == profileId && !q.IsArchived, cancellationToken)
                .ConfigureAwait(false);
            if (profile is null)
                return BadRequest(new { error = "The selected configuration profile is unavailable." });
            if (!string.Equals(profile.GameIdentifier, server.GameIdentifier, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "The configuration profile belongs to a different game." });
            if (payload.PinnedRevisionId is { } revisionId
                && !await dbContext.ConfigurationProfileRevisions.AnyAsync(
                    q => q.PrimaryKey == revisionId && q.ConfigurationProfileFk == profileId,
                    cancellationToken).ConfigureAwait(false))
                return BadRequest(new { error = "The pinned revision does not belong to the selected profile." });
        }

        server.SelectedConfigurationProfileFk = payload.ProfileId;
        server.PinnedConfigurationProfileRevisionFk = payload.PinnedRevisionId;
        server.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.ServerConfigurationProfileChanged,
            "Server configuration profile changed.",
            actorId,
            gameServerId,
            details: $"ProfileId={payload.ProfileId?.ToString() ?? "none"}; Mode={(payload.PinnedRevisionId is null ? "latest" : "pinned")}.");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("profile-options")]
    [Authorize(Claims.ResourceBased.Server.Modify)]
    public async Task<IActionResult> GetProfileOptionsAsync(
        [FromRoute] Guid gameServerId,
        CancellationToken cancellationToken)
    {
        var gameIdentifier = await dbContext.GameServers
            .AsNoTracking()
            .Where(q => q.PrimaryKey == gameServerId && !q.IsArchived)
            .Select(q => q.GameIdentifier)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (gameIdentifier is null)
            return NotFound();
        return Ok(await dbContext.ConfigurationProfiles
            .AsNoTracking()
            .Where(q => q.GameIdentifier == gameIdentifier && !q.IsArchived)
            .OrderBy(q => q.Title)
            .Select(q => new ConfigurationProfileSummaryDto
            {
                PrimaryKey = q.PrimaryKey,
                Title = q.Title,
                GameIdentifier = q.GameIdentifier,
                IsArchived = q.IsArchived,
                LatestRevisionNumber = q.Revisions.Max(r => (int?) r.RevisionNumber) ?? 0,
                TimeStampUpdated = q.TimeStampUpdated,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false));
    }

    private static GameConfigurationDto Project(ResolvedGameConfiguration configuration, bool includeSensitive)
        => new()
        {
            ServerId = configuration.ServerId,
            ProfileId = configuration.ProfileId,
            ProfileRevisionId = configuration.ProfileRevisionId,
            ProfileRevisionNumber = configuration.ProfileRevisionNumber,
            IsComplete = configuration.IsComplete,
            Entries = configuration.Entries.Select(q => new GameConfigurationEntryDto
            {
                Key = q.Definition.Key,
                TargetPath = q.Definition.TargetPath,
                Group = q.Definition.Group,
                DisplayName = q.Definition.DisplayName,
                Description = q.Definition.Description,
                Kind = q.Definition.Kind,
                Required = q.Definition.Required,
                Sensitive = q.Definition.Sensitive,
                IsControlPlaneOnly = q.Definition.IsControlPlaneOnly,
                Order = q.Definition.Order,
                Minimum = q.Definition.Minimum,
                Maximum = q.Definition.Maximum,
                AllowedValues = q.Definition.AllowedValues ?? [],
                Choices = q.Definition.Choices ?? [],
                Value = q.Definition.Sensitive && !includeSensitive ? null : q.Value,
                Source = q.Source,
                IsCompatible = q.IsCompatible,
                IsRedacted = q.Definition.Sensitive && !includeSensitive && q.Value.HasValue,
                Warning = q.Warning,
            }).ToArray(),
        };

}
