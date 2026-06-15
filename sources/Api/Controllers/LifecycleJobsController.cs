using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Authorize]
[Route("jobs")]
public sealed class LifecycleJobsController(
    DatabaseContext dbContext,
    IGameServerLifecycleService lifecycleService,
    IGameServerProjectionService projectionService,
    IHostingAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet("{lifecycleJobId:guid}", Name = "LifecycleJobs")]
    [ProducesResponseType<LifecycleJobDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(
        [FromRoute] Guid lifecycleJobId,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.LifecycleJobs
            .AsNoTracking()
            .Include(q => q.WorkerCommands)
            .SingleOrDefaultAsync(q => q.PrimaryKey == lifecycleJobId, cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
            return NotFound();
        if (!authorizationService.HasServerPermission(User, job.GameServerFk, Claims.ResourceBased.Server.View))
            return Forbid();
        return Ok(projectionService.ProjectJob(job, User));
    }

    [HttpPost("{lifecycleJobId:guid}/retry")]
    [ProducesResponseType<LifecycleJobDto>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RetryAsync(
        [FromRoute] Guid lifecycleJobId,
        CancellationToken cancellationToken)
    {
        var serverId = await GetServerIdAsync(lifecycleJobId, cancellationToken).ConfigureAwait(false);
        if (serverId is null)
            return NotFound();
        if (!authorizationService.HasServerPermission(User, serverId.Value, Claims.ResourceBased.Server.Operate))
            return Forbid();
        try
        {
            var job = await lifecycleService.RetryAsync(serverId.Value, lifecycleJobId, User, cancellationToken)
                .ConfigureAwait(false);
            return Accepted(projectionService.ProjectJob(job, User));
        }
        catch (HostingConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{lifecycleJobId:guid}/skip")]
    [ProducesResponseType<LifecycleJobDto>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> SkipAsync(
        [FromRoute] Guid lifecycleJobId,
        CancellationToken cancellationToken)
    {
        var serverId = await GetServerIdAsync(lifecycleJobId, cancellationToken).ConfigureAwait(false);
        if (serverId is null)
            return NotFound();
        if (!authorizationService.HasServerPermission(User, serverId.Value, Claims.ResourceBased.Server.Operate))
            return Forbid();
        try
        {
            var job = await lifecycleService.SkipAsync(serverId.Value, lifecycleJobId, User, cancellationToken)
                .ConfigureAwait(false);
            return Accepted(projectionService.ProjectJob(job, User));
        }
        catch (HostingConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    private Task<Guid?> GetServerIdAsync(Guid lifecycleJobId, CancellationToken cancellationToken)
        => dbContext.LifecycleJobs
            .AsNoTracking()
            .Where(q => q.PrimaryKey == lifecycleJobId)
            .Select(q => (Guid?) q.GameServerFk)
            .SingleOrDefaultAsync(cancellationToken);
}
