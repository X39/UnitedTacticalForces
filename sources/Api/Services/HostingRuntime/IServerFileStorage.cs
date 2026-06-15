using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Server-scoped access to the durable and deployment-live file trees.
/// </summary>
public interface IServerFileStorage
{
    Task<IReadOnlyList<ServerFileEntry>> BrowseAsync(
        Guid serverId,
        Guid? deploymentId,
        EFileContext context,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<ServerFileDownload> OpenReadAsync(
        Guid serverId,
        Guid? deploymentId,
        EFileContext context,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<ServerFileEntry> UploadAsync(
        Guid serverId,
        Guid? deploymentId,
        EFileContext context,
        string relativePath,
        Stream content,
        bool overwrite,
        CancellationToken cancellationToken = default);

    Task<ServerFileEntry> CreateDirectoryAsync(
        Guid serverId,
        Guid? deploymentId,
        EFileContext context,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid serverId,
        Guid? deploymentId,
        EFileContext context,
        string relativePath,
        bool recursive,
        CancellationToken cancellationToken = default);
}