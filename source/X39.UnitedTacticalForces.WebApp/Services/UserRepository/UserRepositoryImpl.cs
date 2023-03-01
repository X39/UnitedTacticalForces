using System.Collections.Immutable;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.UserRepository;

[Scoped<UserRepositoryImpl, IUserRepository>]
internal class UserRepositoryImpl : RepositoryBase, IUserRepository
{
    public UserRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : base(httpClient, baseUrl)
    {
    }

    public async Task<User?> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await Client.UsersAsync(userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<long> GetUserCountAsync(CancellationToken cancellationToken = default)
    {
        return await Client.UsersAllCountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SetUserRoleAsync(Guid userId, long roleId, bool roleActive, CancellationToken cancellationToken = default)
    {
        await Client.UsersSetRoleAsync(userId, roleId, roleActive, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Role>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        var result = await Client.UsersRolesAvailableAsync(cancellationToken)
            .ConfigureAwait(false);
        return result.ToImmutableArray();
    }

    public async Task ToggleBanUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await Client.UsersAsync(userId, cancellationToken).ConfigureAwait(false);
        user.IsBanned = !user.IsBanned;
        await Client.UsersUpdateAsync(userId, user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<User> GetMeAsync(
        CancellationToken cancellationToken = default)
    {
        return await Client.UsersMeAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<User>> GetUsersAsync(
        int skip,
        int take,
        string? search = default,
        bool includeRoles = false,
        CancellationToken cancellationToken = default)
    {
        var users = await Client.UsersAllAsync(skip, take, search, includeRoles, cancellationToken)
            .ConfigureAwait(false);
        return users.ToImmutableArray();
    }

}