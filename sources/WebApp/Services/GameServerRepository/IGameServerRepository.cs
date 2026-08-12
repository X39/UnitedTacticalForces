namespace X39.UnitedTacticalForces.WebApp.Services.GameServerRepository;

public interface IGameServerRepository
{
    Task<IReadOnlyList<GameServerInfo>> GetServersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameServerCapabilityInfo>> GetCapabilitiesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameServerNotificationInfo>> GetNotificationsAsync(
        CancellationToken cancellationToken = default);

    Task<GameServerInfo?> GetServerAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConfigurationProfileOptionInfo>> GetConfigurationProfileOptionsAsync(
        string gameIdentifier,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConfigurationProfileInfo>> GetConfigurationProfilesAsync(
        CancellationToken cancellationToken = default);

    Task<ConfigurationProfileDefinitionCatalogInfo> GetConfigurationProfileDefinitionsAsync(
        CancellationToken cancellationToken = default);

    Task<ConfigurationProfileDetailsInfo> GetConfigurationProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationProfileInfo> CreateConfigurationProfileAsync(
        string title,
        string gameIdentifier,
        IReadOnlyList<ConfigurationValueUpdateInfo> values,
        CancellationToken cancellationToken = default);

    Task CreateConfigurationProfileRevisionAsync(
        Guid profileId,
        IReadOnlyList<ConfigurationValueUpdateInfo> values,
        CancellationToken cancellationToken = default);

    Task ArchiveConfigurationProfileAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task ChangeConfigurationProfileUserAccessAsync(
        Guid profileId, Guid userId, string permission, bool grant,
        CancellationToken cancellationToken = default);

    Task ChangeConfigurationProfileRoleAccessAsync(
        Guid profileId, long roleId, string permission, bool grant,
        CancellationToken cancellationToken = default);

    Task<GameConfigurationInfo> GetConfigurationAsync(
        Guid serverId,
        CancellationToken cancellationToken = default);

    Task<GameConfigurationInfo> UpdateConfigurationAsync(
        Guid serverId,
        IReadOnlyList<ConfigurationValueUpdateInfo> values,
        IReadOnlyList<string> resetKeys,
        CancellationToken cancellationToken = default);

    Task SelectConfigurationProfileAsync(
        Guid serverId,
        Guid? profileId,
        Guid? pinnedRevisionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameServerModPackOptionInfo>> GetCreationModPackOptionsAsync(
        string gameIdentifier,
        CancellationToken cancellationToken = default);

    Task<StorageDemandInfo> EstimateStorageDemandAsync(
        StorageDemandEstimateRequestInfo payload,
        CancellationToken cancellationToken = default);

    Task<StorageDemandInfo> GetStorageDemandAsync(
        Guid serverId,
        CancellationToken cancellationToken = default);

    Task<GameServerInfo> CreateServerAsync(
        GameServerCreationInfo payload,
        CancellationToken cancellationToken = default);

    Task DeleteServerAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task ChangeRuntimeProfileAsync(
        Guid serverId,
        string runtimeProfileIdentifier,
        CancellationToken cancellationToken = default);

    Task ChangeRuntimeStorageAsync(
        Guid serverId,
        string strategy,
        long? externalStorageSizeGiB,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameServerModPackOptionInfo>> GetModPackOptionsAsync(
        Guid serverId,
        CancellationToken cancellationToken = default);

    Task ChangeModPackAsync(
        Guid serverId,
        long? modPackDefinitionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LifecycleJobInfo>> GetJobsAsync(
        Guid serverId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HostingOperationInfo>> GetOperationsAsync(
        Guid serverId,
        int take = 80,
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
        CancellationToken cancellationToken = default,
        bool confirmGeneratedOverride = false);

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
