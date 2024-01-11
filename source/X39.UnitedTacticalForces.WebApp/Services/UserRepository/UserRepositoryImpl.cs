using System.Collections.Immutable;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.UserRepository;

[Scoped<UserRepositoryImpl, IUserRepository>]
internal class UserRepositoryImpl : RepositoryBase, IUserRepository
{
    public UserRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl)
        : base(httpClient, baseUrl) { }

    public async Task<IReadOnlyCollection<Role>> GetRolesOfUserAsync(
        Guid              userId,
        CancellationToken cancellationToken = default
    )
    {
        var roles = await Client.UsersRolesOfAsync(userId, cancellationToken).ConfigureAwait(false);
        return roles.ToImmutableArray();
    }

    public async Task<IReadOnlyCollection<Claim>> GetClaimsOfUserAsync(
        Guid              userId,
        int               skip,
        int               take,
        CancellationToken cancellationToken = default
    )
    {
        var roles = await Client.UsersClaimsOfAsync(userId, skip, take, cancellationToken).ConfigureAwait(false);
        return roles.ToImmutableArray();
    }


    public async Task<long> CountClaimsOfUserAsync(
        Guid              userId,
        CancellationToken cancellationToken = default
    )
    {
        var roles = await Client.UsersClaimsOfCountAsync(userId, cancellationToken).ConfigureAwait(false);
        return roles;
    }

    public async Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await Client.UsersAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> GetUserCountAsync(CancellationToken cancellationToken = default)
    {
        return await Client.UsersAllCountAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task SetUserRoleAsync(
        Guid              userId,
        long              roleId,
        bool              roleActive,
        CancellationToken cancellationToken = default
    )
    {
        await Client.RolesSetUserRoleToAsync(userId, roleId, roleActive, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Role>> GetRolesAvailableToMeAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = await Client.RolesAvailableAsync(cancellationToken).ConfigureAwait(false);
        return result.ToImmutableArray();
    }

    public async Task ToggleBanUserAsync(Guid userId, bool isBanned, CancellationToken cancellationToken)
    {
        var user = await Client.UsersAsync(userId, cancellationToken).ConfigureAwait(false);
        user.IsBanned = isBanned;
        await Client.UsersUpdateAsync(userId, user, cancellationToken).ConfigureAwait(false);
    }

    public async Task ToggleVerifiedUserAsync(Guid userId, bool isVerified, CancellationToken cancellationToken)
    {
        var user = await Client.UsersAsync(userId, cancellationToken).ConfigureAwait(false);
        user.IsVerified = isVerified;
        await Client.UsersUpdateAsync(userId, user, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteMeAsync(CancellationToken cancellationToken)
    {
        await Client.UsersMeDeleteAsync(null, cancellationToken);
    }

    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        if (user.PrimaryKey is null)
            throw new ArgumentException("User.PrimaryKey is null.", nameof(user));
        await Client.UsersUpdateAsync(user.PrimaryKey.Value, user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<User> GetMeAsync(CancellationToken cancellationToken = default)
    {
        return await Client.UsersMeAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<User>> GetUsersAsync(
        int               skip,
        int               take,
        string?           search            = default,
        bool              includeRoles      = false,
        bool              includeUnverified = false,
        CancellationToken cancellationToken = default
    )
    {
        var users = await Client.UsersAllAsync(skip, take, search, includeRoles, includeUnverified, cancellationToken)
                                .ConfigureAwait(false);
        return users.ToImmutableArray();
    }
}
