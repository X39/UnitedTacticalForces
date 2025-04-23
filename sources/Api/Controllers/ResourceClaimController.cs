using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.DTO;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Provides API endpoints for resource based claims.
/// </summary>
[ApiController]
[Route(Constants.Routes.ResourceClaims)]
[Authorize(Claims.Administrative.Role)]
public class ResourceClaimController : ControllerBase
{
    private readonly ILogger<ResourceClaimController> _logger;
    private readonly ApiDbContext                     _apiDbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceClaimController"/> class.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/> to use.</param>
    /// <param name="apiDbContext">The <see cref="ApiDbContext"/> to use.</param>
    public ResourceClaimController(ILogger<ResourceClaimController> logger, ApiDbContext apiDbContext)
    {
        _logger       = logger;
        _apiDbContext = apiDbContext;
    }

    /// <summary>
    /// Gets all users and their claims for a specific resource.
    /// </summary>
    /// <param name="resourcePrefix">The prefix of the resource.</param>
    /// <param name="resourceIdentifier">The identifier of the resource.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, yielding an <see cref="IActionResult"/> containing the users and their claims.</returns>
    [HttpGet("{resourcePrefix}/{resourceIdentifier}/users-and-roles")]
    [ProducesResponseType<UserAndRoleTuple>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsersAndRolesResourceClaims(
        [FromRoute] string resourcePrefix,
        [FromRoute] string resourceIdentifier
    )
    {
        var users = await _apiDbContext.Users
            .Include(e => e.Claims!.Where(claim
                    => claim.Identifier.StartsWith(resourcePrefix) && claim.Value == resourceIdentifier
                )
            )
            .AsNoTracking()
            .Where(user => user.Claims!.Any(claim
                    => claim.Identifier.StartsWith(resourcePrefix) && claim.Value == resourceIdentifier
                )
            )
            .Select(e => new PlainUserDto
                {
                    PrimaryKey     = e.PrimaryKey,
                    Nickname       = e.Nickname,
                    Avatar         = e.Avatar,
                    AvatarMimeType = e.AvatarMimeType,
                }
            )
            .ToArrayAsync();

        var roles = await _apiDbContext.Roles
            .Include(e => e.Claims!.Where(claim
                    => claim.Identifier.StartsWith(resourcePrefix) && claim.Value == resourceIdentifier
                )
            )
            .AsNoTracking()
            .Where(role => role.Claims!.Any(claim
                    => claim.Identifier.StartsWith(resourcePrefix) && claim.Value == resourceIdentifier
                )
            )
            .ToArrayAsync();
        return Ok(
            new UserAndRoleTuple
            {
                Users = users,
                Roles = roles.Select(role => new RoleDto
                        {
                            PrimaryKey  = role.PrimaryKey,
                            Title       = role.Title,
                            Description = role.Description,
                            Claims = role.Claims!.Select(claim => claim.ToPlainDto())
                                .ToArray(),
                        }
                    )
                    .ToArray(),
            }
        );
    }

    /// <summary>
    /// Adds a claim to a user for a specific resource.
    /// </summary>
    /// <param name="resourcePrefix">The prefix of the resource.</param>
    /// <param name="resourceIdentifier">The identifier of the resource.</param>
    /// <param name="userId">The id of the user to add the claim to.</param>
    /// <param name="resourceClaim">The claim to add.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    [HttpGet("{resourcePrefix}/{resourceIdentifier}/users/{userId}/add/{resourceClaim}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddUserResourceClaim(
        [FromRoute] string resourcePrefix,
        [FromRoute] string resourceIdentifier,
        [FromRoute] Guid userId,
        [FromRoute] string resourceClaim
    )
    {
        if (!resourceClaim.StartsWith(resourcePrefix))
            return BadRequest();
        var user = await _apiDbContext.Users
            .Include(e => e.Claims!.Where(claim
                    => claim.Identifier == resourceClaim && claim.Value == resourceIdentifier
                )
            )
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.PrimaryKey == userId);
        if (user is null)
            return BadRequest();
        if (user.Claims!.Any(claim => claim.Identifier == resourceClaim && claim.Value == resourceIdentifier))
            return NoContent();
        user.Claims ??= new List<Claim>();
        user.Claims.Add(
            new Claim
            {
                Identifier = resourceClaim,
                Value      = resourceIdentifier,
            }
        );
        await _apiDbContext.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Removes a claim from a user for a specific resource.
    /// </summary>
    /// <param name="resourcePrefix">The prefix of the resource.</param>
    /// <param name="resourceIdentifier">The identifier of the resource.</param>
    /// <param name="userId">The id of the user to remove the claim from.</param>
    /// <param name="resourceClaim">The claim to remove.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    [HttpGet("{resourcePrefix}/{resourceIdentifier}/users/{userId}/remove/{resourceClaim}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveUserResourceClaim(
        [FromRoute] string resourcePrefix,
        [FromRoute] string resourceIdentifier,
        [FromRoute] Guid userId,
        [FromRoute] string resourceClaim
    )
    {
        if (!resourceClaim.StartsWith(resourcePrefix))
            return BadRequest();
        var user = await _apiDbContext.Users
            .Include(e => e.Claims!.Where(claim
                    => claim.Identifier == resourceClaim && claim.Value == resourceIdentifier
                )
            )
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.PrimaryKey == userId);
        if (user is null)
            return BadRequest();
        if (!user.Claims?.Any(claim => claim.Identifier == resourceClaim && claim.Value == resourceIdentifier) ?? true)
            return NoContent();
        var claim = user.Claims!.First(claim => claim.Identifier == resourceClaim && claim.Value == resourceIdentifier);
        user.Claims.Remove(claim);
        await _apiDbContext.SaveChangesAsync();
        return NoContent();
    }


    /// <summary>
    /// Removes a claim from the database by its PrimaryKey.
    /// </summary>
    /// <param name="claimId">The PrimaryKey of the claim to remove.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    [HttpGet("claims/{claimId}/remove", Name = nameof(RemoveClaim))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveClaim([FromRoute] long claimId)
    {
        var claim = await _apiDbContext.Claims
            .AsNoTracking()
            .FirstOrDefaultAsync(claim => claim.PrimaryKey == claimId);
        if (claim is null)
            return BadRequest();
        _apiDbContext.Claims.Remove(claim);
        await _apiDbContext.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Adds a claim to a role for a specific resource.
    /// </summary>
    /// <param name="resourcePrefix">The prefix of the resource.</param>
    /// <param name="resourceIdentifier">The identifier of the resource.</param>
    /// <param name="roleId">The id of the role to add the claim to.</param>
    /// <param name="resourceClaim">The claim to add.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    [HttpGet("{resourcePrefix}/{resourceIdentifier}/roles/{roleId}/add/{resourceClaim}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddRoleResourceClaim(
        [FromRoute] string resourcePrefix,
        [FromRoute] string resourceIdentifier,
        [FromRoute] int roleId,
        [FromRoute] string resourceClaim
    )
    {
        if (!resourceClaim.StartsWith(resourcePrefix))
            return BadRequest();
        var role = await _apiDbContext.Roles
            .Include(e => e.Claims!.Where(claim
                    => claim.Identifier == resourceClaim && claim.Value == resourceIdentifier
                )
            )
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.PrimaryKey == roleId);
        if (role is null)
            return BadRequest();
        if (role.Claims!.Any(claim => claim.Identifier == resourceClaim && claim.Value == resourceIdentifier))
            return NoContent();
        role.Claims ??= new List<Claim>();
        role.Claims.Add(
            new Claim
            {
                Identifier = resourceClaim,
                Value      = resourceIdentifier,
            }
        );
        await _apiDbContext.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Removes a claim from a role for a specific resource.
    /// </summary>
    /// <param name="resourcePrefix">The prefix of the resource.</param>
    /// <param name="resourceIdentifier">The identifier of the resource.</param>
    /// <param name="roleId">The id of the role to remove the claim from.</param>
    /// <param name="resourceClaim">The claim to remove.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    [HttpGet("{resourcePrefix}/{resourceIdentifier}/roles/{roleId}/remove/{resourceClaim}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveRoleResourceClaim(
        [FromRoute] string resourcePrefix,
        [FromRoute] string resourceIdentifier,
        [FromRoute] int roleId,
        [FromRoute] string resourceClaim
    )
    {
        if (!resourceClaim.StartsWith(resourcePrefix))
            return BadRequest();
        var role = await _apiDbContext.Roles
            .Include(e => e.Claims!.Where(claim
                    => claim.Identifier == resourceClaim && claim.Value == resourceIdentifier
                )
            )
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.PrimaryKey == roleId);
        if (role is null)
            return BadRequest();
        if (!role.Claims?.Any(claim => claim.Identifier == resourceClaim && claim.Value == resourceIdentifier) ?? true)
            return NoContent();
        var claim = role.Claims!.First(claim => claim.Identifier == resourceClaim && claim.Value == resourceIdentifier);
        role.Claims.Remove(claim);
        await _apiDbContext.SaveChangesAsync();
        return NoContent();
    }
}
