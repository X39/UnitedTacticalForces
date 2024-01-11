using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using X39.Util.Collections;
using X39.Util.DependencyInjection.Attributes;
using SClaim = System.Security.Claims.Claim;

namespace X39.UnitedTacticalForces.WebApp.Services;

[Scoped<SteamAuthProvider, AuthenticationStateProvider>]
public class SteamAuthProvider : AuthenticationStateProvider
{
    private readonly MeService _userService;

    public SteamAuthProvider(MeService userService)
    {
        _userService = userService;
    }

    private static IEnumerable<SClaim> GetClaims(User user)
    {
        var userClaims = user.Claims
                             ?.NotNull()
                             .Select((q) => new SClaim(q.Identifier ?? string.Empty, q.Value ?? string.Empty));

        return new[]
            {
                new SClaim(ClaimTypes.Name,           user.Nickname ?? string.Empty),
                new SClaim(ClaimTypes.NameIdentifier, (user.PrimaryKey ?? Guid.Empty).ToString()),
                user.IsVerified is true ? new SClaim(ClaimTypes.Role, Constants.Verified) : default,
            }.Concat(userClaims ?? Enumerable.Empty<SClaim>())
             .NotNull();
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

        var claimsPrincipal     = new ClaimsPrincipal(identity);
        var authenticationState = new AuthenticationState(claimsPrincipal);
        return Task.FromResult(authenticationState);
    }
}
