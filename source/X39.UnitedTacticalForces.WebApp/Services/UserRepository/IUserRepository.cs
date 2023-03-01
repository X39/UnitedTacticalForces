namespace X39.UnitedTacticalForces.WebApp.Services.UserRepository;

public interface IUserRepository
{
    Task<IReadOnlyCollection<User>> GetUsersAsync(
        int skip,
        int take,
        string? search = default,
        bool includeRoles = false,
        CancellationToken cancellationToken = default);

    Task<User> GetMeAsync(CancellationToken cancellationToken = default);
    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<long> GetUserCountAsync(CancellationToken cancellationToken = default);
    Task SetUserRoleAsync(Guid userId, long roleId, bool roleActive, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Role>> GetAllRolesAsync(CancellationToken cancellationToken = default);
    Task ToggleBanUserAsync(Guid userId, CancellationToken cancellationToken = default);
}