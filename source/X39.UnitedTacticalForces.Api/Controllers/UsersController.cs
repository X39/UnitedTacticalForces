using System.Diagnostics.Contracts;
using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Common;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route(Constants.Routes.Users)]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;
    private readonly ApiDbContext             _apiDbContext;

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
    public async Task<IActionResult> LoginSteamAsync(
        [FromQuery] string returnUrl)
    {
        Contract.Assert(await HttpContext.IsProviderSupportedAsync(Constants.AuthorizationSchemas.Steam));
        return Challenge(
            new AuthenticationProperties
            {
                RedirectUri  = returnUrl,
                IsPersistent = true,
                IssuedUtc    = DateTime.UtcNow,
                ExpiresUtc   = DateTime.UtcNow.AddDays(Constants.Lifetime.SteamAuthDays),
            },
            Constants.AuthorizationSchemas.Steam);
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
    public async Task<IActionResult> LoginDiscordAsync(
        [FromQuery] string returnUrl)
    {
        Contract.Assert(await HttpContext.IsProviderSupportedAsync(Constants.AuthorizationSchemas.Discord));
        return Challenge(
            new AuthenticationProperties
            {
                RedirectUri  = returnUrl,
                IsPersistent = true,
                IssuedUtc    = DateTime.UtcNow,
                ExpiresUtc   = DateTime.UtcNow.AddDays(Constants.Lifetime.DiscordAuthDays),
            },
            Constants.AuthorizationSchemas.Discord);
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
            new AuthenticationProperties
            {
                RedirectUri = returnUrl,
            },
            Constants.AuthorizationSchemas.Cookie);
    }

    /// <summary>
    /// Deletes the current user.
    /// </summary>
    /// <param name="returnUrl">The url to get back to after the logout process has completed.</param>
    [HttpPost("me/delete", Name = nameof(MeDeleteAsync))]
    [Authorize]
    public async Task<IActionResult> MeDeleteAsync([FromQuery] string returnUrl)
    {
        if (!User.TryGetUserId(out var userId))
            return Unauthorized();
        var user = await _apiDbContext.Users.SingleOrDefaultAsync((q) => q.PrimaryKey == userId);
        if (user is null)
            return NotFound();
        user.Steam          = new();
        user.Discord        = new();
        user.Nickname       = "[anonymous]";
        user.Avatar         = Array.Empty<byte>();
        user.AvatarMimeType = string.Empty;
        user.EMail          = null;
        user.IsDeleted      = true;
        await _apiDbContext.SaveChangesAsync();
        return SignOut(
            new AuthenticationProperties
            {
                RedirectUri = returnUrl,
            },
            Constants.AuthorizationSchemas.Cookie);
    }

    [Authorize]
    [HttpPost("{userId:guid}/update", Name = nameof(UpdateUserAsync))]
    [ProducesResponseType((int) HttpStatusCode.NoContent)]
    public async Task<IActionResult> UpdateUserAsync(
        [FromRoute] Guid userId,
        [FromBody] User updatedUser,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
            return Unauthorized();
        var existingUser = await _apiDbContext.Users.SingleAsync((q) => q.PrimaryKey == userId, cancellationToken);
        var isSelf = existingUser.PrimaryKey != currentUserId;
        if (!User.IsInRoleOrAdmin(Roles.UserModify) && isSelf)
            return Unauthorized();
        existingUser.Avatar         = updatedUser.Avatar;
        existingUser.AvatarMimeType = updatedUser.AvatarMimeType;
        existingUser.Nickname       = updatedUser.Nickname;
        if (User.IsInRoleOrAdmin(Roles.UserViewMail) || isSelf)
            existingUser.EMail = updatedUser.EMail;
        if (User.IsInRoleOrAdmin(Roles.UserBan))
            existingUser.IsBanned = updatedUser.IsBanned;
        if (User.IsInRoleOrAdmin(Roles.UserVerify))
            existingUser.IsVerified = updatedUser.IsVerified;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Returns all roles which are available to the current user for addition.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>
    /// All <see cref="Role"/> which are accessible by the current user deducted by the role held. 
    /// </returns>
    [Authorize(Roles = Roles.Admin + "," + Roles.UserManageRoles)]
    [HttpPost("roles/available", Name = nameof(GetAllRolesAsync))]
    public async Task<IEnumerable<Role>> GetAllRolesAsync(
        CancellationToken cancellationToken)
    {
        var user = await User.GetUserWithRolesAsync(_apiDbContext, cancellationToken);
        if (user is null)
            throw new UnauthorizedAccessException();
        return User.IsAdmin()
            ? await _apiDbContext.Roles.ToArrayAsync(cancellationToken)
            : user.Roles ?? Enumerable.Empty<Role>();
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
    public async Task<ActionResult<User?>> GetUserAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
            return Unauthorized();

        var existingUser = await _apiDbContext.Users
            .SingleOrDefaultAsync((user) => user.PrimaryKey == userId, cancellationToken);
        if (existingUser is not null && !User.IsInRole(Roles.Admin) && existingUser.PrimaryKey != currentUserId)
        {
            if (!User.IsInRoleOrAdmin(Roles.UserViewMail))
                existingUser.EMail = string.Empty;
            if (!User.IsInRoleOrAdmin(Roles.UserViewSteamId64))
                existingUser.Steam = new();
            if (!User.IsInRoleOrAdmin(Roles.UserViewDiscordId))
                existingUser.Discord = new();
            if (!User.IsInRoleOrAdmin(Roles.UserBan))
                existingUser.IsBanned = false;
        }

        return Ok(existingUser);
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
    [Authorize(
        Roles = Roles.Admin + "," + Roles.UserManageRoles)]
    [HttpPost("{userId:guid}/set-role/{roleId:long}/{mode:bool}", Name = nameof(SetUserRoleActiveAsync))]
    [ProducesResponseType((int) HttpStatusCode.NoContent)]
    public async Task<ActionResult> SetUserRoleActiveAsync(
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
        var isInRole = User.IsInRole(Roles.Admin) || await _apiDbContext.Users
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
                (existingUser.Roles ??= new List<Role>()).Add(existingRole);
        }
        else
        {
            var role = existingUser.Roles?.FirstOrDefault((q) => q.PrimaryKey == roleId);
            if (role is not null)
                existingUser.Roles!.Remove(role);
        }

        await _apiDbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return NoContent();
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
    public async Task<ActionResult<User>> GetMeAsync(
        CancellationToken cancellationToken)
    {
        var user = await User.GetUserWithRolesAsync(_apiDbContext, cancellationToken);
        if (user is null)
            return Unauthorized();
        foreach (var userRole in user.Roles ?? Enumerable.Empty<Role>())
        {
            userRole.Users?.Clear();
        }

        return Ok(user);
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
    /// <param name="includeRoles">
    ///     If <see langword="true"/>, the users returned will contain their roles.
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
    public async Task<ActionResult<IEnumerable<User>>> GetUsersAsync(
        [FromQuery] int skip,
        [FromQuery] int take,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] bool? includeRoles = false,
        [FromQuery] bool? includeUnverified = false)
    {
        if (take > 500)
            throw new ArgumentOutOfRangeException(nameof(take), take, "Take has a hard-maximum of 500.");

        IQueryable<User> users = _apiDbContext.Users.AsNoTracking();
        User? currentUser = null;
        if (includeRoles ?? false)
        {
            currentUser ??= await User.GetUserWithRolesAsync(_apiDbContext, cancellationToken);
            if (currentUser is null)
                return Unauthorized();

            if (User.IsAdmin())
            {
                users = users.Include((e) => e.Roles);
            }
            else
            {
                var roleIds = currentUser.Roles?.Select((q) => q.PrimaryKey).ToArray() ?? Array.Empty<long>();
                users = users.Include((e) => e.Roles!.Where((q) => roleIds.Contains(q.PrimaryKey)));
            }
        }

        if (!(includeUnverified ?? false))
        {
            users = users.Where((q) => q.IsVerified);
        }
        else
        {
            if (!User.IsInRoleOrAdmin(Roles.UserVerify))
                return Unauthorized();
        }

        if (search.IsNotNullOrWhiteSpace())
        {
            search = search.Trim();
            search = search.Replace("%", "\\%");
            search = search.Replace(",", "\\,");
            search = search.Replace("_", "\\_");
            search = search.Replace(",", "\\,");
            search = search.Replace("[", "\\[");
            search = search.Replace(",", "\\,");
            search = search.Replace("]", "\\]");
            search = search.Replace(",", "\\,");
            search = search.Replace("^", "\\^");
            search = search.Replace("\\", "\\\\");
            search = $"{search}%";
            users  = users.Where((q) => EF.Functions.ILike(q.Nickname, search, "\\"));
        }

        users = users
            .OrderBy((q) => q.Nickname)
            .Skip(skip)
            .Take(take);

        var result = await users.ToArrayAsync(cancellationToken);

        foreach (var role in result.SelectMany((q) => q.Roles ?? Enumerable.Empty<Role>()))
        {
            role.Users?.Clear();
        }

        return Ok(result);
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
    /// <param name="includeRoles">
    ///     If <see langword="true"/>, the users returned will contain their roles.
    /// </param>
    /// <param name="includeUnverified">
    ///     If <see langword="true"/>, the users returned will also contain unverified users.
    /// </param>
    /// <returns>
    ///     The count of available <see cref="User"/>'s.
    /// </returns>
    [Authorize]
    [HttpPost("all/count", Name = nameof(GetUsersCountAsync))]
    public async Task<ActionResult<long>> GetUsersCountAsync(
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] bool? includeRoles = false,
        [FromQuery] bool? includeUnverified = false)
    {
        IQueryable<User> users = _apiDbContext.Users;
        User? currentUser = null;
        if (includeRoles ?? false)
        {
            currentUser ??= await User.GetUserWithRolesAsync(_apiDbContext, cancellationToken);
            if (currentUser is null)
                return Unauthorized();

            if (User.IsAdmin())
            {
                users = users.Include((e) => e.Roles);
            }
            else
            {
                var roleIds = currentUser.Roles?.Select((q) => q.PrimaryKey).ToArray() ?? Array.Empty<long>();
                users = users.Include((e) => e.Roles!.Where((q) => roleIds.Contains(q.PrimaryKey)));
            }
        }

        if (!(includeUnverified ?? false))
        {
            users = users.Where((q) => q.IsVerified);
        }
        else
        {
            if (User.IsInRoleOrAdmin(Roles.UserVerify))
                return Unauthorized();
        }

        if (search.IsNotNullOrWhiteSpace())
        {
            search = search.Trim();
            search = search.Replace("%", "\\%");
            search = search.Replace(",", "\\,");
            search = search.Replace("_", "\\_");
            search = search.Replace(",", "\\,");
            search = search.Replace("[", "\\[");
            search = search.Replace(",", "\\,");
            search = search.Replace("]", "\\]");
            search = search.Replace(",", "\\,");
            search = search.Replace("^", "\\^");
            search = search.Replace("\\", "\\\\");
            search = $"{search}%";
            users  = users.Where((q) => EF.Functions.ILike(q.Nickname, search, "\\"));
        }

        var result = await users.CountAsync(cancellationToken);

        return Ok(result);
    }
}