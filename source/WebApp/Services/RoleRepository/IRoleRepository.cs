namespace X39.UnitedTacticalForces.WebApp.Services.RoleRepository;

public interface IRoleRepository
{
    Task<IReadOnlyCollection<Role>> GetRolesAsync(
        int skip,
        int take,
        string? search = null,
        CancellationToken cancellationToken = default);
}
