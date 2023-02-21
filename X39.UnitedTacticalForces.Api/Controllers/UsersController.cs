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
}