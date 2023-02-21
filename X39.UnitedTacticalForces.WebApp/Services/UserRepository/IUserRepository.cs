using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.UserRepository;

public interface IUserRepository
{
    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<User> GetMeAsync(CancellationToken cancellationToken = default);
}

[Scoped<UserRepositoryImpl, IUserRepository>]
internal class UserRepositoryImpl : RepositoryBase, IUserRepository
{
    public async Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await Client.UsersAsync(userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<User> GetMeAsync(CancellationToken cancellationToken = default)
    {
        return await Client.UsersMeAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public UserRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : base(httpClient, baseUrl)
    {
    }
}