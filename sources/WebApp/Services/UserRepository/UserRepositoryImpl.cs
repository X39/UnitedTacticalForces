using System.Collections.Immutable;
using System.Net;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.UserRepository;

[Scoped<UserRepositoryImpl, IUserRepository>]
internal sealed class UserRepositoryImpl(IHttpClientFactory httpClientFactory, BaseUrl baseUrl) : RepositoryBase(httpClientFactory, baseUrl),
    IUserRepository
{
    public async Task<IReadOnlyCollection<PlainRoleDto>> GetRolesOfUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        var results = await Client.Users[userId]
            .Roles
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return results?.ToImmutableArray() ?? [];
    }

    public async Task<long> CountRolesOfUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await Client.Users[userId]
            .Roles
            .Count
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result ?? default;
    }

    public async Task<IReadOnlyCollection<PlainClaimDto>> GetClaimsOfUserAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken = default
    )
    {
        var results = await Client.Users[userId]
            .Claims
            .GetAsync(
                conf =>
                {
                    conf.QueryParameters.Skip = skip;
                    conf.QueryParameters.Take = take;
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        return results?.ToImmutableArray() ?? [];
    }


    public async Task<long> CountClaimsOfUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await Client.Users[userId]
            .Claims
            .Count
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result ?? default;
    }

    public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await Client.Users[userId]
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    public async Task<long> GetUserCountAsync(CancellationToken cancellationToken = default)
    {
        var result = await Client.Users
            .All
            .Count
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result ?? default;
    }

    public async Task SetUserRoleAsync(
        Guid userId,
        long roleId,
        bool roleActive,
        CancellationToken cancellationToken = default
    )
    {
        await Client.Roles
            .Set
            .User[userId]
            .Role[roleId]
            .To[roleActive]
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SetUserClaimAsync(
        Guid userId,
        long claimId,
        bool claimActive,
        CancellationToken cancellationToken = default
    )
    {
        var response = await HttpClient
            .PostAsync(
                BaseUrl.ResolveApiUrl($"users/{userId}/claims/{claimId}/to/{claimActive.ToString().ToLowerInvariant()}"),
                content: null,
                cancellationToken
            )
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyCollection<PlainRoleDto>> GetRolesAvailableToMeAsync(
        CancellationToken cancellationToken = default
    )
    {
        var results = await Client.Roles
            .Available
            .PostAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return results?.ToImmutableArray() ?? [];
    }

    public async Task SetUserIsBannedAsync(Guid userId, bool isBanned, CancellationToken cancellationToken)
    {
        await Client.Users[userId]
            .Update
            .PostAsync(
                new UserUpdate
                {
                    IsBanned = isBanned,
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task SetUserIsVerifiedAsync(Guid userId, bool isVerified, CancellationToken cancellationToken)
    {
        await Client.Users[userId]
            .Update
            .PostAsync(
                new UserUpdate
                {
                    IsVerified = isVerified,
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task DeleteMeAsync(string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        await Client.Users.Me.DeletePath.PostAsync(
            conf => conf.QueryParameters.ReturnUrl = returnUrl,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }

    public async Task UpdateUserAsync(Guid userId, UserUpdate payload, CancellationToken cancellationToken = default)
    {
        await Client.Users[userId]
            .Update
            .PostAsync(payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<FullUserDto?> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var response = await HttpClient
            .GetAsync(BaseUrl.ResolveApiUrl("users/me"), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<FullUserDto>(response, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProfileSettingsInfo> GetProfileSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await GetFromJsonAsync<ProfileSettingsInfo>("users/me/settings", cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new NullReferenceException("Unable to get current profile settings");
    }

    public async Task<ProfileSettingsInfo> UpdateProfileSettingsAsync(
        ProfileSettingsUpdateInfo payload,
        CancellationToken cancellationToken = default
    )
    {
        var response = await PostAsJsonAsync("users/me/settings", payload, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadFromJsonAsync<ProfileSettingsInfo>(response, cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new InvalidOperationException("Profile settings command returned no body.");
    }

    public async Task<IReadOnlyCollection<FullUserDto>> GetUsersAsync(
        int skip,
        int take,
        string? search = default,
        bool includeRolesAndClaims = false,
        bool includeUnverified = false,
        CancellationToken cancellationToken = default
    )
    {
        var users = await Client.Users
            .All
            .PostAsync(
                conf =>
                {
                    conf.QueryParameters.Skip                  = skip;
                    conf.QueryParameters.Take                  = take;
                    conf.QueryParameters.Search                = search;
                    conf.QueryParameters.IncludeUnverified     = includeUnverified;
                    conf.QueryParameters.IncludeRolesAndClaims = includeRolesAndClaims;
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        return users?.ToImmutableArray() ?? [];
    }
}
