using System.Security.Claims;

namespace X39.UnitedTacticalForces.Api.ExtensionMethods;

public static class ClaimsIdentityExtensions
{
    public static bool TryGetSteamId64(
        this ClaimsIdentity self,
        out ulong steamId64)
    {
        var steamIdClaim = self.Claims.FirstOrDefault((q) =>
            q.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (steamIdClaim is null)
        {
            steamId64 = default;
            return false;
        }

        var steamId64String = steamIdClaim.Value
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();
        if (steamId64String is null)
            throw new FormatException("Unexpected claim value format");
        if (!ulong.TryParse(steamId64String, out steamId64))
            throw new FormatException("Unexpected claim value format");
        return true;
    }
}