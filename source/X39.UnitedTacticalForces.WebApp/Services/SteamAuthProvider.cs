using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using X39.Util.Collections;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services;

[Scoped<SteamAuthProvider, AuthenticationStateProvider>]
public class SteamAuthProvider : AuthenticationStateProvider
{
    private readonly MeService _userService;

    public SteamAuthProvider(MeService userService)
    {
        _userService = userService;
    }

    private static IEnumerable<Claim> GetClaims(User user)
    {
        var userClaims = user.Roles
            ?.Select((q) => q.Identifier)
            .NotNull()
            .Select((q) => new Claim(ClaimTypes.Role, q));
        return new[]
        {
            new Claim(ClaimTypes.Name, user.Nickname ?? string.Empty),
            new Claim(ClaimTypes.NameIdentifier, (user.PrimaryKey ?? Guid.Empty).ToString()),
            user.IsVerified is true ? new Claim(ClaimTypes.Role, Claims.Verified) : default,
        }.Concat(userClaims ?? Enumerable.Empty<Claim>()).NotNull();
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ClaimsIdentity identity;
        if (_userService.IsAuthenticated)
        {
            var claims = GetClaims(_userService.User);
            identity = new ClaimsIdentity(claims, Constants.AuthenticationTypes.Steam);
        }
        else
        {
            identity = new ClaimsIdentity();
        }

        var claimsPrincipal = new ClaimsPrincipal(identity);
        var authenticationState = new AuthenticationState(claimsPrincipal);
        return Task.FromResult(authenticationState);
    }
}