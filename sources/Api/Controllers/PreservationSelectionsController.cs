using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Authorize]
[Route(Constants.Routes.GameServers + "/{gameServerId:guid}/preservation-selections")]
public sealed class PreservationSelectionsController(
    DatabaseContext dbContext,
    IGameServerDefinitionCatalog gameDefinitions,
    IHostingAuditWriter auditWriter) : ControllerBase
{
    [HttpGet]
    [Authorize(Claims.ResourceBased.Server.Modify)]
    [ProducesResponseType<IReadOnlyCollection<PreservationSelectionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(
        [FromRoute] Guid gameServerId,
        CancellationToken cancellationToken)
        => Ok(await dbContext.PreservationSelections
            .AsNoTracking()
            .Where(q => q.GameServerFk == gameServerId)
            .OrderBy(q => q.Path)
            .ThenBy(q => q.Scope)
            .Select(q => Project(q))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false));

    [HttpPost]
    [Authorize(Claims.ResourceBased.Server.Modify)]
    [ProducesResponseType<PreservationSelectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<PreservationSelectionDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddOrUpdateAsync(
        [FromRoute] Guid gameServerId,
        [FromBody] PreservationSelectionPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        var gameIdentifier = await dbContext.GameServers
            .AsNoTracking()
            .Where(q => q.PrimaryKey == gameServerId && !q.IsArchived)
            .Select(q => q.GameIdentifier)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (gameIdentifier is null)
            return NotFound();
        string normalized;
        try
        {
            normalized = SafeServerPath.Normalize(
                payload.Path,
                payload.Scope is not EPreservationScope.File);
            if (normalized.Length == 0)
                return BadRequest(new { error = "The runtime root cannot be selected for preservation." });
            var definition = gameDefinitions.GetDefinitions().Single(q => string.Equals(
                q.GameIdentifier,
                gameIdentifier,
                StringComparison.OrdinalIgnoreCase));
            if (!GameServerFileCapabilityPolicy.IsPreservationSelectionAllowed(
                    definition,
                    normalized,
                    payload.Scope))
            {
                return BadRequest(new
                {
                    error = "The preservation path is outside the game implementation's allowed persistent roots.",
                });
            }
            if (OperatingSystem.IsWindows())
                normalized = normalized.ToLowerInvariant();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var selection = await dbContext.PreservationSelections
            .SingleOrDefaultAsync(
                q => q.GameServerFk == gameServerId
                     && q.Path == normalized
                     && q.Scope == payload.Scope,
                cancellationToken)
            .ConfigureAwait(false);
        var created = selection is null;
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        if (selection is null)
        {
            selection = new PreservationSelection
            {
                PrimaryKey       = Guid.NewGuid(),
                GameServerFk     = gameServerId,
                Path             = normalized,
                Scope            = payload.Scope,
                Mode             = payload.Mode,
                CreatedByFk      = actorId,
                TimeStampCreated = now,
                TimeStampUpdated = now,
            };
            dbContext.PreservationSelections.Add(selection);
        }
        else
        {
            selection.Mode = payload.Mode;
            selection.TimeStampUpdated = now;
        }
        auditWriter.Add(
            dbContext,
            created
                ? EHostingAuditEventKind.PreservationSelectionAdded
                : EHostingAuditEventKind.PreservationSelectionUpdated,
            created ? "Preservation selection added." : "Preservation selection updated.",
            actorId,
            gameServerId,
            details: $"Path={normalized}; Scope={payload.Scope}; Mode={payload.Mode}");
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "An equivalent preservation selection already exists." });
        }
        return created
            ? Created(string.Empty, Project(selection))
            : Ok(Project(selection));
    }

    [HttpDelete("{selectionId:guid}")]
    [Authorize(Claims.ResourceBased.Server.Modify)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(
        [FromRoute] Guid gameServerId,
        [FromRoute] Guid selectionId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        var selection = await dbContext.PreservationSelections
            .SingleOrDefaultAsync(
                q => q.PrimaryKey == selectionId && q.GameServerFk == gameServerId,
                cancellationToken)
            .ConfigureAwait(false);
        if (selection is null)
            return NotFound();
        dbContext.PreservationSelections.Remove(selection);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.PreservationSelectionRemoved,
            "Preservation selection removed.",
            actorId,
            gameServerId,
            details: $"Path={selection.Path}; Scope={selection.Scope}");
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private static PreservationSelectionDto Project(PreservationSelection selection)
        => new()
        {
            PrimaryKey = selection.PrimaryKey,
            Path = selection.Path,
            Scope = selection.Scope,
            Mode = selection.Mode,
            SeededByGame = selection.IsGameSeeded,
        };
}
