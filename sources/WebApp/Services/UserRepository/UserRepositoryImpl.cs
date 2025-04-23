using System.Collections.Immutable;
using X39.UnitedTacticalForces.WebApp.Api.Models;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.UserRepository;

[Scoped<UserRepositoryImpl, IUserRepository>]
internal sealed class UserRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : RepositoryBase(httpClient, baseUrl),
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

    public async Task DeleteMeAsync(CancellationToken cancellationToken)
    {
        await Client.Users.Me.DeletePath.PostAsync(cancellationToken: cancellationToken);
    }

    public async Task UpdateUserAsync(Guid userId, UserUpdate payload, CancellationToken cancellationToken = default)
    {
        await Client.Users[userId]
            .Update
            .PostAsync(payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<FullUserDto> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var result = await Client.Users
            .Me
            .GetAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new NullReferenceException("Unable to get current user");
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
