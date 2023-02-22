using System.Diagnostics.Contracts;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Core;
using X39.UnitedTacticalForces.Api.Data.Eventing;
using X39.UnitedTacticalForces.Api.ExtensionMethods;
using X39.UnitedTacticalForces.Api.Services;
using X39.Util;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;
    private readonly ApiDbContext _apiDbContext;

    public UsersController(ILogger<UsersController> logger, ApiDbContext apiDbContext)
    {
        _logger = logger;
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
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = returnUrl,
            IsPersistent = true,
            IssuedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddDays(Constants.Lifetime.SteamAuthDays),
        }, Constants.AuthorizationSchemas.Steam);
    }

    /// <summary>
    /// Allows to logout a user.
    /// </summary>
    /// <param name="returnUrl">The url to get back to after the logout process has completed.</param>
    [HttpGet("logout", Name = nameof(LogoutAsync))]
    [HttpPost("logout", Name = nameof(LogoutAsync))]
    public IActionResult LogoutAsync([FromQuery] string returnUrl)
    {
        return SignOut(new AuthenticationProperties
        {
            RedirectUri = returnUrl,
        }, Constants.AuthorizationSchemas.Cookie);
    }

    [Authorize]
    [HttpPost("{userId:guid}/update", Name = nameof(UpdateUserAsync))]
    public async Task UpdateUserAsync(
        [FromRoute] Guid userId,
        [FromBody] User updatedUser,
        CancellationToken cancellationToken)
    {
        var existingUser = await _apiDbContext.Users.SingleAsync((q) => q.PrimaryKey == userId, cancellationToken);
        existingUser.Avatar = updatedUser.Avatar;
        existingUser.AvatarMimeType = updatedUser.AvatarMimeType;
        existingUser.Nickname = updatedUser.Nickname;
        existingUser.EMail = updatedUser.EMail;
        existingUser.SteamId64 = updatedUser.SteamId64;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
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
    public async Task<User?> GetUserAsync(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var existingUser = await _apiDbContext.Users.SingleOrDefaultAsync((q) => q.PrimaryKey == userId, cancellationToken);
        // ToDo: Strip user info if not own user or admin.
        return existingUser;
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
        return user is null
            ? Unauthorized()
            : Ok(user);
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
        [FromQuery] string? search = null)
    {
        if (take > 500)
            throw new ArgumentOutOfRangeException(nameof(take), take, "Take has a hard-maximum of 500.");
        var users = _apiDbContext.Users
            .Skip(skip)
            .Take(take);
        if (search.IsNotNullOrWhiteSpace())
        {
            search   = search.Trim();
            search   = search.Replace("%", "\\%");
            search   = search.Replace(",", "\\,");
            search   = search.Replace("_", "\\_");
            search   = search.Replace(",", "\\,");
            search   = search.Replace("[", "\\[");
            search   = search.Replace(",", "\\,");
            search   = search.Replace("]", "\\]");
            search   = search.Replace(",", "\\,");
            search   = search.Replace("^", "\\^");
            search   = search.Replace("\\", "\\\\");
            search   = $"{search}%";
            users = users.Where((q) => EF.Functions.ILike(q.Nickname, search, "\\"));
        }

        var result = await users.ToArrayAsync(cancellationToken);

        return Ok(users);
    }

    /// <summary>
    /// Returns the count of all <see cref="User"/>'s available.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken"/> to cancel the operation.
    ///     Passed automatically by ASP.Net framework.
    /// </param>
    /// <returns>
    ///     The count of available <see cref="User"/>'s.
    /// </returns>
    [Authorize]
    [HttpPost("all/count", Name = nameof(GetUsersCountAsync))]
    public async Task<ActionResult<long>> GetUsersCountAsync(
        CancellationToken cancellationToken)
    {
        var count = await _apiDbContext.Users
            .LongCountAsync(cancellationToken);

        return count;
    }
}