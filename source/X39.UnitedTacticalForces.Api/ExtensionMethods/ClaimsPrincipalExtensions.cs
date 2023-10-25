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
        steamIdentity = self.Identities.FirstOrDefault(
            (q) =>
                q.AuthenticationType == Constants.AuthorizationSchemas.Steam);
        return steamIdentity is not null;
    }

    public static bool TryDiscordIdentity(
        this ClaimsPrincipal self,
        [NotNullWhen(true)] out ClaimsIdentity? steamIdentity)
    {
        steamIdentity = self.Identities.FirstOrDefault(
            (q) =>
                q.AuthenticationType == Constants.AuthorizationSchemas.Discord);
        return steamIdentity is not null;
    }

    public static bool TryApiIdentity(
        this ClaimsPrincipal self,
        [NotNullWhen(true)] out ClaimsIdentity? apiIdentity)
    {
        apiIdentity = self.Identities.FirstOrDefault(
            (q) =>
                q.AuthenticationType == Constants.AuthorizationSchemas.Api);
        return apiIdentity is not null;
    }


    /// <summary>
    /// Tries to get the <see cref="Guid"/> of the <see cref="User"/> from the <see cref="ClaimsPrincipal"/>.
    /// </summary>
    /// <param name="self">The <see cref="ClaimsPrincipal"/> to get the <see cref="Guid"/> from.</param>
    /// <param name="userId">The <see cref="Guid"/> of the <see cref="User"/> if it could be found.</param>
    /// <returns>
    ///     <see langword="true"/> if the <see cref="Guid"/> could be found, otherwise <see langword="false"/>.
    /// </returns>
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

    /// <summary>
    /// Attempts to get the <see cref="User"/> from the database
    /// from the <see cref="ClaimsPrincipal"/>.
    /// </summary>
    /// <param name="self">The <see cref="ClaimsPrincipal"/> to get the <see cref="User"/> from.</param>
    /// <param name="apiDbContext">The <see cref="ApiDbContext"/> to use to query the database.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>
    ///     A <see cref="Task{TResult}"/> that resolves to a <see cref="User"/> or null if the user could not be found.
    /// </returns>
    public static async Task<User?> GetDatabaseUserAsync(
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
}