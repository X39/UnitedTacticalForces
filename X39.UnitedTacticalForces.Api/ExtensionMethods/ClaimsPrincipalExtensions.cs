using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace X39.UnitedTacticalForces.Api.ExtensionMethods;

public static class ClaimsPrincipalExtensions
{
    public static bool TrySteamIdentity(
        this ClaimsPrincipal self,
        [NotNullWhen(true)] out ClaimsIdentity? steamIdentity)
    {
        steamIdentity = self.Identities.FirstOrDefault((q) =>
            q.AuthenticationType == Constants.AuthorizationSchemas.Steam);
        return steamIdentity is not null;
    }
}