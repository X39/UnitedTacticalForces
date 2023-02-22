namespace X39.UnitedTacticalForces.WebApp.Services.UserRepository;

public interface IUserRepository
{
    Task<IReadOnlyCollection<User>> GetUsersAsync(
        int skip,
        int take,
        string? search = default,
        CancellationToken cancellationToken = default);

    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<User> GetMeAsync(CancellationToken cancellationToken = default);
}