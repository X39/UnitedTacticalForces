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
    /// <param name="redirectUrl">The url to get back to after the login process has completed.</param>
    [AllowAnonymous]
    [HttpGet("login/steam", Name = nameof(LoginSteamAsync))]
    [ProducesResponseType(typeof(void), (int)HttpStatusCode.TemporaryRedirect)]
    public async Task<IActionResult> LoginSteamAsync(
        [FromQuery] string redirectUrl)
    {
        Contract.Assert(await HttpContext.IsProviderSupportedAsync(Constants.AuthorizationSchemas.Steam));
        return Challenge(new AuthenticationProperties
            {
                RedirectUri = $"Users/login/steam/callback?redirectUrl={HttpUtility.UrlEncode(redirectUrl)}"
            },
            Constants.AuthorizationSchemas.Steam);
    }

    /// <summary>
    /// Not supposed to be called manually, will be called automatically when using the steam login.
    /// </summary>
    /// <param name="steamRepository">
    ///     The steam repository, injected via service,
    ///     used to get additional information about a user in the case of a registration.
    /// </param>
    /// <param name="httpClient">
    ///     The http client of the web-api, injected via service, used to receive the profile picture of a
    ///     new user.
    /// </param>
    /// <param name="redirectUrl">
    ///     The url to redirect to after the callback was handled.
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    [Authorize(AuthenticationSchemes = Constants.AuthorizationSchemas.Steam)]
    [HttpGet("login/steam/callback", Name = nameof(LoginSteamCallbackAsync))]
    [ProducesResponseType(typeof(void), (int)HttpStatusCode.TemporaryRedirect)]
    public async Task<IActionResult> LoginSteamCallbackAsync(
        [FromServices] SteamRepository steamRepository,
        [FromServices] HttpClient httpClient,
        [FromQuery] string redirectUrl,
        CancellationToken cancellationToken)
    {
        if (!HttpContext.User.TrySteamIdentity(out var steamIdentity))
            throw new Exception();
        if (!steamIdentity.IsAuthenticated)
            throw new Exception();
        if (!steamIdentity.TryGetSteamId64(out var steamId64))
            throw new Exception();

        var user = await _apiDbContext.Users
            .Include((e) => e.Privileges)
            .SingleOrDefaultAsync((user) => user.SteamId64 == steamId64, cancellationToken);
        if (user is null)
        {
            var profile = await steamRepository.GetProfileAsync(steamId64);
            var avatar = Array.Empty<byte>();
            var avatarMimeType = string.Empty;
            if (profile.AvatarUrl.IsNotNullOrWhiteSpace())
            {
                avatar = await httpClient.GetByteArrayAsync(profile.AvatarUrl, cancellationToken);
                avatarMimeType = "image/jpeg";
            }

            user = new User
            {
                Id = Guid.NewGuid(),
                Avatar = avatar,
                AvatarMimeType = avatarMimeType,
                EMail = null,
                Nickname = profile.Nickname,
                Privileges = new List<Privilege>(),
                SteamId64 = steamId64,
            };
            await _apiDbContext.Users.AddAsync(user, cancellationToken);
            await _apiDbContext.SaveChangesAsync(cancellationToken);
        }

        var identity = await user
            .ToIdentityAsync(cancellationToken: cancellationToken);
        HttpContext.User.AddIdentity(identity);
        
        //var jwtSettings = Configuration.GetSection("Jwt");
        //var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]));
        //var signingCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);
        //var claims = new[]
        //{
        //    new Claim(ClaimTypes.NameIdentifier, "admin"),
        //    new Claim(ClaimTypes.Role, "Admin")
        //};
        //var jwtToken = new JwtSecurityToken(
        //    issuer: jwtSettings["Issuer"],
        //    audience: jwtSettings["Audience"],
        //    claims: claims,
        //    expires: DateTime.Now.AddMinutes(30),
        //    signingCredentials: signingCredentials
        //);
    
        //// Return JWT token as response
        //return Ok(new { Token = new JwtSecurityTokenHandler().WriteToken(jwtToken) });
        
        
        await HttpContext.SignInAsync(JwtBearerDefaults.AuthenticationScheme, HttpContext.User);
        return Redirect(redirectUrl);
    }

    [Authorize]
    [HttpPost("{userId:guid}/update", Name = nameof(UpdateUserAsync))]
    public async Task UpdateUserAsync(
        [FromRoute] Guid userId,
        [FromBody] User updatedUser,
        CancellationToken cancellationToken)
    {
        var existingUser = await _apiDbContext.Users.SingleAsync((q) => q.Id == userId, cancellationToken);
        existingUser.Avatar = updatedUser.Avatar;
        existingUser.AvatarMimeType = updatedUser.AvatarMimeType;
        existingUser.Nickname = updatedUser.Nickname;
        existingUser.EMail = updatedUser.EMail;
        existingUser.SteamId64 = updatedUser.SteamId64;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
    }
}