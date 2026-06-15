using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.UserRepository;

public interface IUserRepository
{
    Task<IReadOnlyCollection<PlainRoleDto>> GetRolesOfUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<PlainClaimDto>> GetClaimsOfUserAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken = default
    );

    Task<long> CountClaimsOfUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<long> GetUserCountAsync(CancellationToken cancellationToken = default);

    Task SetUserRoleAsync(Guid userId, long roleId, bool roleActive, CancellationToken cancellationToken = default);

    Task SetUserClaimAsync(Guid userId, long claimId, bool claimActive, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PlainRoleDto>> GetRolesAvailableToMeAsync(CancellationToken cancellationToken = default);

    Task SetUserIsBannedAsync(Guid userId, bool isBanned, CancellationToken cancellationToken);
    Task SetUserIsVerifiedAsync(Guid userId, bool isVerified, CancellationToken cancellationToken);
    Task DeleteMeAsync(string? returnUrl = null, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(Guid userId, UserUpdate payload, CancellationToken cancellationToken = default);
    Task<FullUserDto?> GetMeAsync(CancellationToken cancellationToken = default);
    Task<ProfileSettingsInfo> GetProfileSettingsAsync(CancellationToken cancellationToken = default);
    Task<ProfileSettingsInfo> UpdateProfileSettingsAsync(
        ProfileSettingsUpdateInfo payload,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<FullUserDto>> GetUsersAsync(
        int skip,
        int take,
        string? search = default,
        bool includeRolesAndClaims = false,
        bool includeUnverified = false,
        CancellationToken cancellationToken = default
    );

    Task<long> CountRolesOfUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
