namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public interface IGameServerRepository
{
    Task<IReadOnlyList<GameServerInfo>> GetServersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameServerCapabilityInfo>> GetCapabilitiesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameServerNotificationInfo>> GetNotificationsAsync(
        CancellationToken cancellationToken = default);

    Task<GameServerInfo?> GetServerAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<GameServerInfo> CreateServerAsync(
        GameServerCreationInfo payload,
        CancellationToken cancellationToken = default);

    Task DeleteServerAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LifecycleJobInfo>> GetJobsAsync(
        Guid serverId,
        CancellationToken cancellationToken = default);

    Task<LifecycleJobInfo> ExecuteActionAsync(
        Guid serverId,
        string actionId,
        string? confirmation = null,
        CancellationToken cancellationToken = default);

    Task<LifecycleJobInfo> RetryJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<LifecycleJobInfo> SkipJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HostingFileInfo>> GetFilesAsync(
        Guid serverId,
        EHostingFileContext context,
        string path = "",
        CancellationToken cancellationToken = default);

    Task<byte[]> GetFileContentAsync(
        Guid serverId,
        EHostingFileContext context,
        string path,
        CancellationToken cancellationToken = default);

    Task<HostingFileInfo> UploadFileAsync(
        Guid serverId,
        EHostingFileContext context,
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);

    Task<HostingFileInfo> CreateDirectoryAsync(
        Guid serverId,
        EHostingFileContext context,
        string path,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(
        Guid serverId,
        EHostingFileContext context,
        string path,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PreservationSelectionInfo>> GetPreservationSelectionsAsync(
        Guid serverId,
        CancellationToken cancellationToken = default);

    Task<PreservationSelectionInfo> AddPreservationSelectionAsync(
        Guid serverId,
        PreservationSelectionInfoRequest payload,
        CancellationToken cancellationToken = default);

    Task DeletePreservationSelectionAsync(
        Guid serverId,
        Guid selectionId,
        CancellationToken cancellationToken = default);
}
