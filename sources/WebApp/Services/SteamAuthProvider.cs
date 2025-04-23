using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using X39.UnitedTacticalForces.WebApp.Api.Models;
using X39.Util.Collections;
using X39.Util.DependencyInjection.Attributes;
using SClaim = System.Security.Claims.Claim;

namespace X39.UnitedTacticalForces.WebApp.Services;

[Scoped<SteamAuthProvider, AuthenticationStateProvider>]
public sealed class SteamAuthProvider(MeService userService) : AuthenticationStateProvider
{
    private static IEnumerable<SClaim> GetClaims(FullUserDto user)
    {
        var userClaims = user.Claims
            ?.NotNull()
            .Select((q) => new SClaim(q.Identifier ?? string.Empty, q.Value ?? string.Empty));

        return new[]
            {
                new SClaim(ClaimTypes.Name, user.Nickname ?? string.Empty),
                new SClaim(ClaimTypes.NameIdentifier, (user.PrimaryKey ?? Guid.Empty).ToString()),
                user.IsVerified is true ? new SClaim(ClaimTypes.Role, Constants.Verified) : default,
            }.Concat(userClaims ?? [])
            .NotNull();
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ClaimsIdentity identity;
        if (userService.IsAuthenticated)
        {
            var claims = GetClaims(userService.User);
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
