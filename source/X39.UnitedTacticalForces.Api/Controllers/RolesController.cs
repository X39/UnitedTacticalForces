using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.ExtensionMethods;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Controller for managing roles.
/// </summary>
[ApiController]
[Route(Constants.Routes.Roles)]
public class RolesController : ControllerBase
{
    private readonly ILogger<RolesController> _logger;
    private readonly ApiDbContext             _apiDbContext;

    /// <summary>
    /// Creates a new instance of <see cref="RolesController"/>.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> to use for logging.</param>
    /// <param name="apiDbContext">The <see cref="ApiDbContext"/> to use for database access.</param>
    public RolesController(ILogger<RolesController> logger, ApiDbContext apiDbContext)
    {
        _logger       = logger;
        _apiDbContext = apiDbContext;
    }

    /// <summary>
    /// Returns all roles which are available to the current user for addition.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>
    /// All <see cref="Claim"/> which are accessible by the current user deducted by the role held. 
    /// </returns>
    [Authorize]
    [HttpPost("available", Name = nameof(GetAllRolesAsync))]
    public async Task<IEnumerable<Role>> GetAllRolesAsync(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
            throw new UnauthorizedAccessException();
        var roles = User.HasClaim(Claims.Administrative.All, string.Empty)
            ? await _apiDbContext.Roles.ToListAsync(cancellationToken)
            : await _apiDbContext.Users
                .Where((q) => q.PrimaryKey == currentUserId)
                .SelectMany((q) => q.Roles!)
                .ToListAsync(cancellationToken);
        return roles;
    }

    /// <summary>
    /// Updates the roles of a given <see cref="User"/>.
    /// </summary>
    /// <remarks>
    /// Method checks whether a role is already part of a user and will not add it twice or error in those cases.
    /// </remarks>
    /// <param name="userId">The <see cref="Guid"/> of the <see cref="User"/> to change the roles of.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="roleId">
    ///     The role id to change on the <see cref="User"/> with the given <paramref name="userId"/>.
    /// </param>
    /// <param name="mode">
    ///     If <see langword="true"/>, the <paramref name="roleId"/> will be given to the <see cref="User"/>.
    ///     If  <see langword="false"/>, the <paramref name="roleId"/> will be removed from the <see cref="User"/>.
    /// </param>
    [Authorize(Claims.Administrative.Role)]
    [HttpPost("set/user/{userId:guid}/role/{roleId:long}/to/{mode:bool}", Name = nameof(SetUserRoleToAsync))]
    [ProducesResponseType((int) HttpStatusCode.NoContent)]
    public async Task<ActionResult> SetUserRoleToAsync(
        [FromRoute] Guid userId,
        [FromRoute] long roleId,
        [FromRoute] bool mode,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
            return Unauthorized();
        await using var dbTransaction = await _apiDbContext.Database.BeginTransactionAsync(cancellationToken);
        var existingRole = await _apiDbContext.Roles
            .SingleAsync((q) => q.PrimaryKey == roleId, cancellationToken);
        var isInRole = User.HasClaim(Claims.Administrative.Role, string.Empty) || await _apiDbContext.Users
            .Where((q) => q.PrimaryKey == currentUserId)
            .SelectMany((q) => q.Roles!)
            .AnyAsync((q) => q.PrimaryKey == roleId, cancellationToken);
        if (!isInRole)
            return Unauthorized();
        var existingUser = await _apiDbContext.Users
            .Include((e) => e.Roles!.Where((q) => q.PrimaryKey == roleId))
            .SingleAsync((q) => q.PrimaryKey == userId, cancellationToken);

        if (mode)
        {
            var role = existingUser.Roles?.FirstOrDefault((q) => q.PrimaryKey == roleId);
            if (role is null)
            {
                (existingUser.Roles ??= new List<Role>()).Add(existingRole);
                _logger.LogInformation(
                    "User {UserId} added role {RoleId} to user {TargetUserId}",
                    currentUserId,
                    roleId,
                    userId);
            }
        }
        else
        {
            var role = existingUser.Roles?.FirstOrDefault((q) => q.PrimaryKey == roleId);
            if (role is not null)
            {
                existingUser.Roles!.Remove(role);
                _logger.LogInformation(
                    "User {UserId} removed role {RoleId} from user {TargetUserId}",
                    currentUserId,
                    roleId,
                    userId);
            }
        }

        await _apiDbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Updates the claims of a given <see cref="Role"/>.
    /// </summary>
    /// <remarks>
    /// Method checks whether a claim is already part of a role and will not add it twice or error in those cases.
    /// </remarks>
    /// <param name="roleId">The <see cref="long"/> of the <see cref="Role"/> to change the claims of.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="claimId">
    ///     The claim id to change on the <see cref="Role"/> with the given <paramref name="roleId"/>.
    /// </param>
    /// <param name="mode">
    ///     If <see langword="true"/>, the <paramref name="claimId"/> will be given to the <see cref="Role"/>.
    ///     If  <see langword="false"/>, the <paramref name="claimId"/> will be removed from the <see cref="Role"/>.
    /// </param>
    [Authorize(Claims.Administrative.Role)]
    [HttpPost("set/role/{roleId:long}/claim/{claimId:long}/to/{mode:bool}", Name = nameof(SetRoleClaimToAsync))]
    [ProducesResponseType((int) HttpStatusCode.NoContent)]
    public async Task<ActionResult> SetRoleClaimToAsync(
        [FromRoute] long roleId,
        [FromRoute] long claimId,
        [FromRoute] bool mode,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
            return Unauthorized();
        await using var dbTransaction = await _apiDbContext.Database.BeginTransactionAsync(cancellationToken);
        var existingClaim = await _apiDbContext.Claims
            .SingleAsync((q) => q.PrimaryKey == roleId, cancellationToken);
        var isInRole = User.HasClaim(Claims.Administrative.Role, string.Empty)
                       || await _apiDbContext.Users
                           .Where((q) => q.PrimaryKey == currentUserId)
                           .SelectMany((q) => q.Roles!)
                           .SelectMany((q) => q.Claims!)
                           .AnyAsync((q) => q.PrimaryKey == claimId, cancellationToken)
                       || await _apiDbContext.Users
                           .Where((q) => q.PrimaryKey == currentUserId)
                           .SelectMany((q) => q.Claims!)
                           .AnyAsync((q) => q.PrimaryKey == claimId, cancellationToken);
        if (!isInRole)
            return Unauthorized();
        var existingRole = await _apiDbContext.Roles
            .Include((e) => e.Claims!.Where((q) => q.PrimaryKey == claimId))
            .SingleAsync((q) => q.PrimaryKey == roleId, cancellationToken);

        if (mode)
        {
            var claim = existingRole.Claims?.FirstOrDefault((q) => q.PrimaryKey == claimId);
            if (claim is null)
            {
                (existingRole.Claims ??= new List<Claim>()).Add(existingClaim);
                _logger.LogInformation(
                    "User {UserId} added claim {ClaimId} to role {RoleId}",
                    currentUserId,
                    claimId,
                    roleId);
            }
        }
        else
        {
            var claim = existingRole.Claims?.FirstOrDefault((q) => q.PrimaryKey == claimId);
            if (claim is not null)
            {
                existingRole.Claims!.Remove(claim);
                _logger.LogInformation(
                    "User {UserId} removed claim {ClaimId} from role {RoleId}",
                    currentUserId,
                    claimId,
                    roleId);
            }
        }

        await _apiDbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Creates a new <see cref="Role"/> with the given <paramref name="title"/> and <paramref name="description"/>.
    /// </summary>
    /// <param name="title">The title of the new <see cref="Role"/>.</param>
    /// <param name="description">The description of the new <see cref="Role"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>No content.</returns>
    [Authorize(Claims.Creation.Role)]
    [HttpPost("create/role", Name = nameof(CreateRoleAsync))]
    [ProducesResponseType((int) HttpStatusCode.NoContent)]
    public async Task<ActionResult> CreateRoleAsync(
        [FromForm] string? title,
        [FromForm] string? description,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
            return Unauthorized();
        var role = new Role
        {
            Title       = title ?? string.Empty,
            Description = description ?? string.Empty,
        };
        await _apiDbContext.Roles.AddAsync(
            role,
            cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "User {UserId} created role {RoleId} with title {Title} with description {Description}",
            currentUserId,
            role.PrimaryKey,
            title,
            description);
        return NoContent();
    }

    /// <summary>
    /// Deletes a <see cref="Role"/> with the given <paramref name="roleId"/>.
    /// </summary>
    /// <param name="roleId">The id of the <see cref="Role"/> to delete.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>No content on success, not found if the <see cref="Role"/> does not exist.</returns>
    [Authorize(Claims.Creation.Role)]
    [HttpPost("delete/role/{roleId:long}", Name = nameof(DeleteRoleAsync))]
    [ProducesResponseType((int) HttpStatusCode.NotFound)]
    [ProducesResponseType((int) HttpStatusCode.NoContent)]
    public async Task<ActionResult> DeleteRoleAsync(
        [FromRoute] long roleId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
            return Unauthorized();
        var role = await _apiDbContext.Roles.SingleOrDefaultAsync((q) => q.PrimaryKey == roleId, cancellationToken);
        if (role is null)
            return NotFound();
        _apiDbContext.Roles.Remove(role);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "User {UserId} deleted role {RoleId} with title {Title} with description {Description}",
            currentUserId,
            role.PrimaryKey,
            role.Title,
            role.Description);
        return NoContent();
    }
}