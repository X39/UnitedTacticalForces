using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;

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

    public static bool TryApiIdentity(
        this ClaimsPrincipal self,
        [NotNullWhen(true)] out ClaimsIdentity? apiIdentity)
    {
        apiIdentity = self.Identities.FirstOrDefault((q) =>
            q.AuthenticationType == Constants.AuthorizationSchemas.Api);
        return apiIdentity is not null;
    }


    public static bool TryGetUserId(this ClaimsPrincipal self, out Guid userId)
    {
        if (!TryApiIdentity(self, out var apiIdentity))
        {
            userId = Guid.Empty;
            return false;
        }

        if (!apiIdentity.IsAuthenticated)
        {
            userId = Guid.Empty;
            return false;
        }

        if (apiIdentity.Name is null)
        {
            userId = Guid.Empty;
            return false;
        }

        return Guid.TryParse(apiIdentity.Name, out userId);
    }
    public static async Task<User?> GetUserAsync(
        this ClaimsPrincipal self,
        ApiDbContext apiDbContext,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(self, out var userId))
            return null;
        var user = await apiDbContext.Users
            .SingleAsync((q) => q.PrimaryKey == userId, cancellationToken);
        return user;
    }

    public static async Task<User?> GetUserWithRolesAsync(
        this ClaimsPrincipal self,
        ApiDbContext apiDbContext,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(self, out var userId))
            return null;
        var user = await apiDbContext.Users
            .Include((e) => e.Roles)
            .SingleAsync((q) => q.PrimaryKey == userId, cancellationToken);
        return user;
    }
}