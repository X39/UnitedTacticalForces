using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Filesystem adapter for API-owned persistent server data. Live deployment files are accessed
/// only through the authenticated external-worker protocol and never through an API-local runtime tree.
/// </summary>
[Singleton<FileSystemServerFileStorage, IServerFileStorage>]
public sealed class FileSystemServerFileStorage(
    IPersistentStoragePathResolver pathResolver,
    IServerFileClassifier classifier,
    IServerFileLockProvider lockProvider) : IServerFileStorage
{
    public async Task<IReadOnlyList<ServerFileEntry>> BrowseAsync(
        Guid serverId,
        Guid? deploymentId,
        EFileContext context,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        await using var serverLock = await lockProvider.AcquireAsync(serverId, cancellationToken).ConfigureAwait(false);
        var root = ResolveRoot(serverId, context);
        var normalized = SafeServerPath.Normalize(relativePath);
        var directory = SafeServerPath.ResolveWithinRoot(root, normalized);
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException("The requested server directory does not exist.");

        var entries = new List<ServerFileEntry>();
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0 || IsPlatformMetadata(path))
                continue;

            var isDirectory = (attributes & FileAttributes.Directory) != 0;
            var childRelativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
            try
            {
                childRelativePath = SafeServerPath.Normalize(childRelativePath, false);
            }
            catch (ArgumentException)
            {
                continue;
            }

            var modifiedAt = isDirectory
                ? Directory.GetLastWriteTimeUtc(path)
                : File.GetLastWriteTimeUtc(path);
            entries.Add(new ServerFileEntry(
                EFileContext.Persistent,
                childRelativePath,
                Path.GetFileName(path),
                isDirectory,
                isDirectory ? null : new FileInfo(path).Length,
                new DateTimeOffset(modifiedAt, TimeSpan.Zero),
                classifier.Classify(childRelativePath, isDirectory),
                classifier.IsWritable(EFileContext.Persistent, classifier.Classify(childRelativePath, isDirectory))));
        }

        return entries
            .OrderByDescending(q => q.IsDirectory)
            .ThenBy(q => q.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<ServerFileDownload> OpenReadAsync(
        Guid serverId,
        Guid? deploymentId,
        EFileContext context,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        await using var serverLock = await lockProvider.AcquireAsync(serverId, cancellationToken).ConfigureAwait(false);
        var root = ResolveRoot(serverId, context);
        var path = SafeServerPath.ResolveWithinRoot(root, relativePath, false);
        if (!File.Exists(path))
            throw new FileNotFoundException("The requested server file does not exist.");

        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return new ServerFileDownload(stream, Path.GetFileName(path), stream.Length);
    }

    public async Task<ServerFileEntry> UploadAsync(
        Guid serverId,
        Guid? deploymentId,
        EFileContext context,
        string relativePath,
        Stream content,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        await using var serverLock = await lockProvider.AcquireAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (content is null || !content.CanRead)
            throw new ArgumentException("A readable upload stream is required.", nameof(content));

        var root = ResolveRoot(serverId, context);
        var normalized = SafeServerPath.Normalize(relativePath, false);
        EnsureWritable(classifier.Classify(normalized, false));
        var path = SafeServerPath.ResolveWithinRoot(root, normalized, false);
        if (Directory.Exists(path))
            throw new IOException("A directory already exists at the requested server path.");
        if (!overwrite && File.Exists(path))
            throw new IOException("A file already exists at the requested server path.");

        var parent = Path.GetDirectoryName(path)
                     ?? throw new IOException("The upload path has no parent directory.");
        Directory.CreateDirectory(parent);
        SafeServerPath.RejectExistingReparsePoints(root, parent);
        var temporaryPath = Path.Combine(parent, $".utf-upload-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        return CreateEntry(root, path, false);
    }

    public async Task<ServerFileEntry> CreateDirectoryAsync(
        Guid serverId,
        Guid? deploymentId,
        EFileContext context,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        await using var serverLock = await lockProvider.AcquireAsync(serverId, cancellationToken).ConfigureAwait(false);
        var root = ResolveRoot(serverId, context);
        var normalized = SafeServerPath.Normalize(relativePath, false);
        EnsureWritable(classifier.Classify(normalized, true));
        var path = SafeServerPath.ResolveWithinRoot(root, normalized, false);
        if (File.Exists(path))
            throw new IOException("A file already exists at the requested server path.");
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(path);
        SafeServerPath.RejectExistingReparsePoints(root, path);
        return CreateEntry(root, path, true);
    }

    public async Task DeleteAsync(
        Guid serverId,
        Guid? deploymentId,
        EFileContext context,
        string relativePath,
        bool recursive,
        CancellationToken cancellationToken = default)
    {
        await using var serverLock = await lockProvider.AcquireAsync(serverId, cancellationToken).ConfigureAwait(false);
        var root = ResolveRoot(serverId, context);
        var normalized = SafeServerPath.Normalize(relativePath, false);
        var path = SafeServerPath.ResolveWithinRoot(root, normalized, false);
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(path))
        {
            EnsureWritable(classifier.Classify(normalized, false));
            File.Delete(path);
            return;
        }
        if (!Directory.Exists(path))
            throw new FileNotFoundException("The requested server path does not exist.");
        EnsureWritable(classifier.Classify(normalized, true));
        if (!recursive && Directory.EnumerateFileSystemEntries(path).Any())
            throw new IOException("The requested server directory is not empty.");
        if (recursive)
            EnsureTreeWritable(root, path, cancellationToken);
        DeleteDirectoryWithoutFollowingLinks(path, recursive, cancellationToken);
    }

    private string ResolveRoot(Guid serverId, EFileContext context)
    {
        if (context is EFileContext.Live)
        {
            throw new LiveWorkerFileAccessException(
                "Live cloud files require the active deployment worker and are not stored on the API host.");
        }
        if (context is not EFileContext.Persistent)
            throw new ArgumentOutOfRangeException(nameof(context), context, "Unknown server file context.");

        var root = pathResolver.ResolvePersistentRoot(serverId);
        PersistentTreePublicationRecovery.Recover(pathResolver.PersistentBasePath, root, serverId, false);
        Directory.CreateDirectory(root);
        SafeServerPath.RejectExistingReparsePoints(root, root);
        return root;
    }

    private ServerFileEntry CreateEntry(string root, string path, bool isDirectory)
    {
        var relativePath = Path.GetRelativePath(root, path).Replace('\\', '/');
        var classification = classifier.Classify(relativePath, isDirectory);
        var modifiedAt = isDirectory ? Directory.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path);
        return new ServerFileEntry(
            EFileContext.Persistent,
            relativePath,
            Path.GetFileName(path),
            isDirectory,
            isDirectory ? null : new FileInfo(path).Length,
            new DateTimeOffset(modifiedAt, TimeSpan.Zero),
            classification,
            classifier.IsWritable(EFileContext.Persistent, classification));
    }

    private static bool IsPlatformMetadata(string path)
        => Path.GetFileName(path).StartsWith(".utf-", StringComparison.OrdinalIgnoreCase);

    private void EnsureWritable(EFileClassification classification)
    {
        if (!classifier.IsWritable(EFileContext.Persistent, classification))
            throw new UnauthorizedAccessException("The requested server file classification is read-only.");
    }

    private void EnsureTreeWritable(string root, string directory, CancellationToken cancellationToken)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing to recursively delete a server directory containing a reparse point.");
            if (IsPlatformMetadata(entry))
                throw new UnauthorizedAccessException("Platform storage metadata cannot be deleted through file APIs.");

            var isDirectory = (attributes & FileAttributes.Directory) != 0;
            var relativePath = SafeServerPath.Normalize(Path.GetRelativePath(root, entry).Replace('\\', '/'), false);
            EnsureWritable(classifier.Classify(relativePath, isDirectory));
            if (isDirectory)
                EnsureTreeWritable(root, entry, cancellationToken);
        }
    }

    private static void DeleteDirectoryWithoutFollowingLinks(
        string directory,
        bool recursive,
        CancellationToken cancellationToken)
    {
        if (!recursive)
        {
            Directory.Delete(directory, false);
            return;
        }
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing to recursively delete a server directory containing a reparse point.");
            if ((attributes & FileAttributes.Directory) != 0)
                DeleteDirectoryWithoutFollowingLinks(entry, true, cancellationToken);
            else
                File.Delete(entry);
        }
        Directory.Delete(directory, false);
    }
}