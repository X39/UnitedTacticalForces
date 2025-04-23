using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// The UsersController class is a part of the API that manages user-related operations.
/// It provides endpoints for user authentication, user information retrieval,
/// and user data modification.
/// Here's a brief overview of its responsibilities:
/// <list type="bullet">
///     <item>
///         It allows users to log in via Steam or Discord.
///         The login process is initiated by redirecting the user to the respective login page of the service.
///     </item>
///     <item>
///         It provides a logout functionality that signs out the user from the application.
///     </item>
///     <item>
///         It allows the current logged-in user to delete their own account.
///         This operation anonymizes the user's data and marks the account as deleted.
///     </item>
///     <item>
///         It provides endpoints to update user information.
///         This can be done either for the current logged-in user or for any user,
///         given the user performing the operation has the necessary permissions.
///     </item>
///     <item>
///         It provides endpoints to retrieve user information.
///         This can be done for a specific user by their ID, for the current logged-in user,
///         or for all users with optional filters and pagination.
///     </item>
///     <item>
///         It also provides an endpoint to get the total count of users, with optional filters.
///     </item>
/// </list> 
/// Please note that some operations require the user to have specific permissions.
/// These permissions are checked using claims associated with the user's account.
/// </summary>
[ApiController]
[Route(Constants.Routes.Users)]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;
    private readonly ApiDbContext             _apiDbContext;

    /// <summary>
    /// Creates a new <see cref="UsersController"/> instance.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> to use for logging.</param>
    /// <param name="apiDbContext">The <see cref="ApiDbContext"/> to use for database access.</param>
    public UsersController(ILogger<UsersController> logger, ApiDbContext apiDbContext)
    {
        _logger       = logger;
        _apiDbContext = apiDbContext;
    }

    /// <summary>
    /// Change location to this URL to start the steam login process.
    /// Only valid for browser clients as a redirect to the steam login page is mandatory.
    /// If a user logs in using steam and was not yet registered, he will be auto-registered.
    /// </summary>
    /// <param name="returnUrl">The url to get back to after the login process has completed.</param>
    [AllowAnonymous]
    [HttpGet("login/steam", Name = nameof(LoginSteamAsync))]
    [HttpPost("login/steam", Name = nameof(LoginSteamAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.TemporaryRedirect)]
    [ResponseCache(NoStore = true, Duration = 0)]
    public async Task<IActionResult> LoginSteamAsync([FromQuery] string returnUrl)
    {
        System.Diagnostics.Contracts.Contract.Assert(
            await HttpContext.IsProviderSupportedAsync(Constants.AuthorizationSchemas.Steam)
        );
        return Challenge(
            new AuthenticationProperties
            {
                RedirectUri  = returnUrl,
                IsPersistent = true,
                IssuedUtc    = DateTime.UtcNow,
                ExpiresUtc   = DateTime.UtcNow.AddDays(Constants.Lifetime.SteamAuthDays),
            },
            Constants.AuthorizationSchemas.Steam
        );
    }

    /// <summary>
    /// Change location to this URL to start the discord login process.
    /// Only valid for browser clients as a redirect to the discord login page is mandatory.
    /// If a user logs in using discord and was not yet registered, he will be auto-registered.
    /// </summary>
    /// <param name="returnUrl">The url to get back to after the login process has completed.</param>
    [AllowAnonymous]
    [HttpGet("login/discord", Name = nameof(LoginDiscordAsync))]
    [HttpPost("login/discord", Name = nameof(LoginDiscordAsync))]
    [ProducesResponseType(typeof(void), (int) HttpStatusCode.TemporaryRedirect)]
    [ResponseCache(NoStore = true, Duration = 0)]
    public async Task<IActionResult> LoginDiscordAsync([FromQuery] string returnUrl)
    {
        System.Diagnostics.Contracts.Contract.Assert(
            await HttpContext.IsProviderSupportedAsync(Constants.AuthorizationSchemas.Discord)
        );
        return Challenge(
            new AuthenticationProperties
            {
                RedirectUri  = returnUrl,
                IsPersistent = true,
                IssuedUtc    = DateTime.UtcNow,
                ExpiresUtc   = DateTime.UtcNow.AddDays(Constants.Lifetime.DiscordAuthDays),
            },
            Constants.AuthorizationSchemas.Discord
        );
    }

    /// <summary>
    /// Allows to logout a user.
    /// </summary>
    /// <param name="returnUrl">The url to get back to after the logout process has completed.</param>
    [HttpGet("logout", Name = nameof(LogoutAsync))]
    [HttpPost("logout", Name = nameof(LogoutAsync))]
    public IActionResult LogoutAsync([FromQuery] string returnUrl)
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = returnUrl, },
            Constants.AuthorizationSchemas.Cookie
        );
    }

    /// <summary>
    /// Deletes the current user.
    /// </summary>
    /// <param name="returnUrl">The url to get back to after the logout process has completed.</param>
    [Authorize]
    [HttpPost("me/delete", Name = nameof(MeDeleteAsync))]
    public async Task<IActionResult> MeDeleteAsync([FromQuery] string returnUrl)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var user = await _apiDbContext.Users.SingleOrDefaultAsync(q => q.PrimaryKey == userId);
        if (user is null)
            return NotFound();
        user.Steam          = new();
        user.Discord        = new();
        user.Nickname       = "[anonymous]";
        user.Avatar         = [];
        user.AvatarMimeType = string.Empty;
        user.EMail          = null;
        user.IsDeleted      = true;
        await _apiDbContext.SaveChangesAsync();
        return SignOut(
            new AuthenticationProperties { RedirectUri = returnUrl, },
            Constants.AuthorizationSchemas.Cookie
        );
    }

    /// <summary>
    /// Updates a <see cref="User"/>.
    /// </summary>
    /// <param name="userId">The <see cref="Guid"/> of the <see cref="User"/>.</param>
    /// <param name="updatedUser">The updated <see cref="User"/>.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns></returns>
    [Authorize]
    [HttpPost("{userId:guid}/update", Name = nameof(UpdateUserAsync))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateUserAsync(
        [FromRoute] Guid userId,
        [FromBody] UserUpdate updatedUser,
        CancellationToken cancellationToken
    )
    {
        if (!User.TryGetUserId(out var currentUserId))
            return Unauthorized();
        await DoUpdateUserAsync(userId, updatedUser, cancellationToken, currentUserId);
        return NoContent();
    }

    /// <summary>
    /// Updates the <see cref="User"/> that is currently logged in.
    /// </summary>
    /// <param name="updatedUser">The updated <see cref="User"/>.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns></returns>
    [Authorize]
    [HttpPost("me/update", Name = nameof(UpdateMeAsync))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateMeAsync(
        [FromBody] UserUpdate updatedUser,
        CancellationToken cancellationToken
    )
    {
        if (!User.TryGetUserId(out var currentUserId))
            return Unauthorized();
        await DoUpdateUserAsync(currentUserId, updatedUser, cancellationToken, currentUserId);
        return NoContent();
    }

    /// <summary>
    /// Returns a <see cref="User"/> if one exists with the given id.
    /// </summary>
    /// <remarks>
    /// Full user information is only available if it is the own <see cref="User"/> or the user
    /// requesting has the admin role.
    /// </remarks>
    /// <param name="userId">The <see cref="Guid"/> of the <see cref="User"/>.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>
    ///     A valid <see cref="User"/> if one was found, <see langkeyword="null"/> if not.
    /// </returns>
    [Authorize]
    [HttpGet("{userId:guid}", Name = nameof(GetUserAsync))]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetUserAsync([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
            return Unauthorized();

        var existingUser = await _apiDbContext.Users.SingleOrDefaultAsync(
            user => user.PrimaryKey == userId,
            cancellationToken
        );
        if (existingUser is null)
            return NoContent();

        var isSelf = existingUser.PrimaryKey == currentUserId;
        if (!User.HasClaim(Claims.User.EMail, string.Empty)
            && !User.HasClaim(Claims.Administrative.User, string.Empty)
            && !isSelf)
            existingUser.EMail = string.Empty;
        if (!User.HasClaim(Claims.User.ViewSteamId64, string.Empty)
            && !User.HasClaim(Claims.Administrative.User, string.Empty)
            && !isSelf)
            existingUser.Steam = new();
        if (!User.HasClaim(Claims.User.ViewDiscordId, string.Empty)
            && !User.HasClaim(Claims.Administrative.User, string.Empty)
            && !isSelf)
            existingUser.Discord = new();
        if (!User.HasClaim(Claims.User.Ban, string.Empty)
            && !User.HasClaim(Claims.Administrative.User, string.Empty)
            && !isSelf)
            existingUser.IsBanned = false;
        if (!User.HasClaim(Claims.User.Verify, string.Empty)
            && !User.HasClaim(Claims.Administrative.User, string.Empty)
            && !isSelf)
            existingUser.IsVerified = false;

        return Ok(existingUser.ToDto());
    }

    /// <summary>
    /// Returns the <see cref="User"/> of the authorized user.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>
    ///     The <see cref="User"/> of the current user.
    /// </returns>
    [HttpGet("me", Name = nameof(GetMeAsync))]
    [ProducesResponseType<FullUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetMeAsync(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var existingUser = await _apiDbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.PrimaryKey == userId, cancellationToken);
        if (existingUser is null)
            return NotFound();
        var roles = new List<Role>();
        var claims = new List<Claim>();
        await foreach (var role in _apiDbContext.Roles
                           .AsNoTracking()
                           .Include(e => e.Claims)
                           .Where(role => role.Users!.Any(user => user.PrimaryKey == userId))
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            roles.Add(role);
            if (role.Claims is null)
                continue;
            claims.AddRange(role.Claims);
            role.Claims = null;
        }

        await foreach (var claim in _apiDbContext.Claims
                           .AsNoTracking()
                           .Where(claim => claim.Users!.Any(user => user.PrimaryKey == userId))
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            claims.Add(claim);
        }

        existingUser.Claims = claims;
        existingUser.Roles  = roles;
        return Ok(
            new FullUserDto
            {
                PrimaryKey      = existingUser.PrimaryKey,
                Nickname        = existingUser.Nickname,
                EMail           = existingUser.EMail,
                IsBanned        = existingUser.IsBanned,
                Avatar          = existingUser.Avatar,
                AvatarMimeType  = existingUser.AvatarMimeType,
                IsVerified      = existingUser.IsVerified,
                IsDeleted       = existingUser.IsDeleted,
                SteamId64       = existingUser.Steam.Id64,
                DiscordId       = existingUser.Discord.Id,
                DiscordUsername = existingUser.Discord.Username,
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
                Claims = claims.Select(claim => claim.ToPlainDto())
                    .ToArray(),
            }
        );
    }

    /// <summary>
    /// Gets all claims of the user identified by <paramref name="userId"/>.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="userId">The id of the user.</param>
    /// <param name="skip">The amount of <see cref="Claim"/>'s to skip. Paging argument.</param>
    /// <param name="take">The amount of <see cref="Claim"/>'s to take after skip. Paging argument.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, yielding an <see cref="IActionResult"/> containing the claims.</returns>
    [HttpGet("claims-of/{userId}", Name = nameof(GetClaimsOfUser))]
    [ProducesResponseType<PlainClaimDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Claims.Administrative.Role)]
    public async Task<IActionResult> GetClaimsOfUser(
        CancellationToken cancellationToken,
        [FromRoute] Guid userId,
        [FromQuery] int? skip = null,
        [FromQuery] int? take = null
    )
    {
        var userExists = await _apiDbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.PrimaryKey == userId, cancellationToken);
        if (!userExists)
            return NotFound();
        var claims = _apiDbContext.Claims
            .AsNoTracking()
            .Where(claim => claim.Users!.Any(user => user.PrimaryKey == userId))
            .OrderBy(claim => claim.Title)
            .Skip(skip ?? 0)
            .Take(take ?? 10);

        var result = await claims.ToArrayAsync(cancellationToken);
        return Ok(result.Select(e => e.ToPlainDto()).ToArray());
    }

    /// <summary>
    /// Gets the count of all claims of the user identified by <paramref name="userId"/>.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="userId">The id of the user.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, yielding an <see cref="IActionResult"/> containing the count.</returns>
    [HttpGet("claims-of/{userId}/count", Name = nameof(GetClaimsOfUserCount))]
    [ProducesResponseType<long>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Claims.Administrative.Role)]
    public async Task<IActionResult> GetClaimsOfUserCount(CancellationToken cancellationToken, [FromRoute] Guid userId)
    {
        var userExists = await _apiDbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.PrimaryKey == userId, cancellationToken);
        if (!userExists)
            return NotFound();
        var result = await _apiDbContext.Claims
            .AsNoTracking()
            .Where(claim => claim.Users!.Any(user => user.PrimaryKey == userId))
            .CountAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets all roles related to the user identified by <paramref name="userId"/>.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="userId">The id of the user.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, yielding an <see cref="IActionResult"/> containing the roles.</returns>
    [HttpGet("roles-of/{userId}", Name = nameof(GetRolesOfUser))]
    [ProducesResponseType<PlainRoleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Claims.Administrative.Role)]
    public async Task<IActionResult> GetRolesOfUser(CancellationToken cancellationToken, [FromRoute] Guid userId)
    {
        var user = await _apiDbContext.Users
            .Include(e => e.Roles)
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.PrimaryKey == userId, cancellationToken);
        if (user is null)
            return NotFound();
        return Ok(user.Roles!.Select(e => e.ToPlainDto()).ToArray());
    }

    /// <summary>
    /// Returns all <see cref="User"/>'s available.
    /// </summary>
    /// <param name="skip">The amount of <see cref="User"/>'s to skip. Paging argument.</param>
    /// <param name="take">The amount of <see cref="User"/>'s to take after skip. Paging argument.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="search">
    ///     Searches the <see cref="User.Nickname"/> with a function akin to <see cref="string.StartsWith(string)"/>
    /// </param>
    /// <param name="includeRolesAndClaims">
    ///     If <see langword="true"/>, the <see cref="User"/>'s returned will contain their <see cref="Role"/>'s
    ///     and <see cref="Claim"/>'s.
    /// </param>
    /// <param name="includeUnverified">
    ///     If <see langword="true"/>, the users returned will also contain unverified users.
    /// </param>
    /// <returns>
    ///     The available <see cref="User"/>'s.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="take"/> is greater then 500.</exception>
    [Authorize]
    [HttpPost("all", Name = nameof(GetUsersAsync))]
    [ProducesResponseType<FullUserDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUsersAsync(
        [FromQuery] int skip,
        [FromQuery] int take,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] bool? includeRolesAndClaims = false,
        [FromQuery] bool? includeUnverified = false
    )
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        if (take > 500)
            // ReSharper disable once LocalizableElement
            throw new ArgumentOutOfRangeException(nameof(take), take, "Take has a hard-maximum of 500.");

        var users = _apiDbContext.Users.AsNoTracking();
        if (includeRolesAndClaims ?? false)
        {
            if (User.HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.Role))
            {
                users = users.Include(e => e.Claims)
                    .Include(e => e.Roles!)
                    .ThenInclude(e => e.Claims);
            }
            else
            {
                var roleClaimIds = await _apiDbContext.Roles
                    .Where(role => role.Users!.Any(user => user.PrimaryKey == userId))
                    .SelectMany(role => role.Claims!)
                    .Select(claim => claim.PrimaryKey)
                    .ToArrayAsync(cancellationToken);
                var claimIds = await _apiDbContext.Claims
                    .Where(claim => claim.Users!.Any(user => user.PrimaryKey == userId))
                    .Select(claim => claim.PrimaryKey)
                    .ToArrayAsync(cancellationToken);
                users = users.Include(e => e.Claims!.Where(claim => claimIds.Contains(claim.PrimaryKey)))
                    .Include(e => e.Roles!.Where(role => roleClaimIds.Contains(role.PrimaryKey)))
                    .ThenInclude(e => e.Claims);
            }
        }

        if (!(includeUnverified ?? false))
        {
            users = users.Where(q => q.IsVerified);
        }
        else
        {
            if (!User.HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.User, Claims.User.Verify))
                return Unauthorized();
        }

        if (search.IsNotNullOrWhiteSpace())
        {
            search = search.Trim();
            search = search.Replace("%", @"\%");
            search = search.Replace(",", @"\,");
            search = search.Replace("_", @"\_");
            search = search.Replace(",", @"\,");
            search = search.Replace("[", @"\[");
            search = search.Replace(",", @"\,");
            search = search.Replace("]", @"\]");
            search = search.Replace(",", @"\,");
            search = search.Replace("^", @"\^");
            search = search.Replace(@"\", @"\\");
            search = $"%{search}%";
            users  = users.Where(q => EF.Functions.ILike(q.Nickname, search, "\\"));
        }

        users = users.OrderBy(q => q.Nickname)
            .Skip(skip)
            .Take(take);

        var result = await users.ToArrayAsync(cancellationToken);

        foreach (var role in result.SelectMany(q => q.Claims ?? Enumerable.Empty<Claim>()))
        {
            role.Users?.Clear();
        }

        return Ok(result.Select(existingUser => 
            new FullUserDto
            {
                PrimaryKey      = existingUser.PrimaryKey,
                Nickname        = existingUser.Nickname,
                EMail           = existingUser.EMail,
                IsBanned        = existingUser.IsBanned,
                Avatar          = existingUser.Avatar,
                AvatarMimeType  = existingUser.AvatarMimeType,
                IsVerified      = existingUser.IsVerified,
                IsDeleted       = existingUser.IsDeleted,
                SteamId64       = existingUser.Steam.Id64,
                DiscordId       = existingUser.Discord.Id,
                DiscordUsername = existingUser.Discord.Username,
                Roles = existingUser.Roles!.Select(role => new RoleDto
                        {
                            PrimaryKey  = role.PrimaryKey,
                            Title       = role.Title,
                            Description = role.Description,
                            Claims = role.Claims!.Select(claim => claim.ToPlainDto())
                                .ToArray(),
                        }
                    )
                    .ToArray(),
                Claims = existingUser.Claims!.Select(claim => claim.ToPlainDto())
                    .ToArray(),
            }));
    }

    /// <summary>
    /// Returns the count of all <see cref="User"/>'s available.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <param name="search">
    ///     Searches the <see cref="User.Nickname"/> with a function akin to <see cref="string.StartsWith(string)"/>
    /// </param>
    /// <param name="includeUnverified">
    ///     If <see langword="true"/>, the users returned will also contain unverified users.
    /// </param>
    /// <returns>
    ///     The count of available <see cref="User"/>'s.
    /// </returns>
    [Authorize]
    [HttpPost("all/count", Name = nameof(GetUsersCountAsync))]
    [ProducesResponseType<long>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUsersCountAsync(
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] bool? includeUnverified = false
    )
    {
        IQueryable<User> users = _apiDbContext.Users;

        if (!(includeUnverified ?? false))
        {
            users = users.Where(q => q.IsVerified);
        }
        else
        {
            if (!User.HasAnyEmptyClaim(Claims.Administrative.All, Claims.Administrative.User, Claims.User.Verify))
                return Unauthorized();
        }

        if (search.IsNotNullOrWhiteSpace())
        {
            search = search.Trim();
            search = search.Replace("%", @"\%");
            search = search.Replace(",", @"\,");
            search = search.Replace("_", @"\_");
            search = search.Replace(",", @"\,");
            search = search.Replace("[", @"\[");
            search = search.Replace(",", @"\,");
            search = search.Replace("]", @"\]");
            search = search.Replace(",", @"\,");
            search = search.Replace("^", @"\^");
            search = search.Replace(@"\", @"\\");
            search = $"%{search}%";
            users  = users.Where(q => EF.Functions.ILike(q.Nickname, search, "\\"));
        }

        var result = await users.CountAsync(cancellationToken);

        return Ok(result);
    }

    #region Helper Methods

    private async Task DoUpdateUserAsync(
        Guid userId,
        UserUpdate updatedUser,
        CancellationToken cancellationToken,
        Guid currentUserId
    )
    {
        var existingUser = await _apiDbContext.Users.SingleAsync(q => q.PrimaryKey == userId, cancellationToken);
        var isSelf = existingUser.PrimaryKey != currentUserId;
        if (updatedUser.Avatar is not null && updatedUser.AvatarMimeType is not null)
        {
            existingUser.Avatar         = updatedUser.Avatar;
            existingUser.AvatarMimeType = updatedUser.AvatarMimeType;
        }

        if (updatedUser.Nickname is not null)
            if (User.HasClaim(Claims.User.Nickname, string.Empty)
                || User.HasClaim(Claims.Administrative.User, string.Empty)
                || isSelf)
                existingUser.Nickname = updatedUser.Nickname;
        if (updatedUser.EMail is not null)
            if (User.HasClaim(Claims.User.EMail, string.Empty)
                || User.HasClaim(Claims.Administrative.User, string.Empty)
                || isSelf)
                existingUser.EMail = updatedUser.EMail;
        if (updatedUser.IsBanned is not null)
            if (User.HasClaim(Claims.User.Ban, string.Empty) || User.HasClaim(Claims.Administrative.User, string.Empty))
                existingUser.IsBanned = updatedUser.IsBanned.Value;
        if (updatedUser.IsVerified is not null)
            if (User.HasClaim(Claims.User.Verify, string.Empty)
                || User.HasClaim(Claims.Administrative.User, string.Empty))
                existingUser.IsVerified = updatedUser.IsVerified.Value;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
    }

    #endregion
}
