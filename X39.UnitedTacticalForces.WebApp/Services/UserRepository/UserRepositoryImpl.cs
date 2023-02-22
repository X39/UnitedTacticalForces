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
        CancellationToken cancellationToken = default)
    {
        var users = await Client.UsersAllAsync(skip, take, search, cancellationToken)
            .ConfigureAwait(false);
        return users.ToImmutableArray();
    }

}