namespace X39.UnitedTacticalForces.WebApp.Services.CommandCenterRepository;

public interface ICommandCenterRepository
{
    Task<CommandCenterCommunitySnapshotInfo> GetCommunitySnapshotAsync(
        int take = 80,
        string? search = default,
        CancellationToken cancellationToken = default
    );

    Task<CommandCenterAuthoritySnapshotInfo> GetAuthoritySnapshotAsync(
        int take = 120,
        string? search = default,
        CancellationToken cancellationToken = default
    );

    Task<CommandCenterWikiMediaSnapshotInfo> GetWikiMediaSnapshotAsync(
        int take = 80,
        string? search = default,
        CancellationToken cancellationToken = default
    );

    Task<CommandCenterFieldReportsSnapshotInfo> GetFieldReportsSnapshotAsync(
        int take = 120,
        string? search = default,
        CancellationToken cancellationToken = default
    );

    Task<CommandCenterSettingsSnapshotInfo> GetSettingsSnapshotAsync(
        CancellationToken cancellationToken = default
    );

    Task<CommandCenterEventAdminSnapshotInfo> GetEventAdminSnapshotAsync(
        int skip = 0,
        int take = 80,
        bool hostedByMeOnly = false,
        string? search = default,
        CancellationToken cancellationToken = default
    );
}
