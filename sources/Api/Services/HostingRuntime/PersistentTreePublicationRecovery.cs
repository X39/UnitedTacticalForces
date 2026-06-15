namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Recovers the narrow publication window where the prior persistent tree has been moved aside but the staged tree
/// has not yet reached its canonical path.
/// </summary>
internal static class PersistentTreePublicationRecovery
{
    public const string StagingDirectoryName = ".utf-preservation-staging";
    public const string BackupDirectoryName = ".utf-preservation-backup";

    public static string ResolveStagingBase(string persistentBasePath)
        => Path.Combine(persistentBasePath, StagingDirectoryName);

    public static string ResolveBackupRoot(string persistentBasePath, Guid serverId)
        => Path.Combine(persistentBasePath, BackupDirectoryName, serverId.ToString("N"));

    public static void Recover(
        string persistentBasePath,
        string persistentRoot,
        Guid serverId,
        bool removeStaleStaging)
    {
        SafeServerPath.RejectExistingReparsePoints(persistentBasePath, persistentBasePath);
        Directory.CreateDirectory(persistentBasePath);
        var backupBase = Path.Combine(persistentBasePath, BackupDirectoryName);
        SafeServerPath.RejectExistingReparsePoints(persistentBasePath, backupBase);
        Directory.CreateDirectory(backupBase);
        var backupRoot = ResolveBackupRoot(persistentBasePath, serverId);
        SafeServerPath.RejectExistingReparsePoints(persistentBasePath, backupBase);
        SafeServerPath.RejectExistingReparsePoints(persistentBasePath, backupRoot);
        SafeServerPath.RejectExistingReparsePoints(persistentBasePath, persistentRoot);

        if (Directory.Exists(backupRoot))
        {
            if (Directory.Exists(persistentRoot))
            {
                DeleteTreeWithoutFollowingLinks(backupRoot);
            }
            else
            {
                if (File.Exists(persistentRoot))
                    throw new IOException("The persistent server root conflicts with an existing file.");
                Directory.Move(backupRoot, persistentRoot);
            }
        }

        if (!removeStaleStaging)
            return;

        var stagingBase = ResolveStagingBase(persistentBasePath);
        SafeServerPath.RejectExistingReparsePoints(persistentBasePath, stagingBase);
        Directory.CreateDirectory(stagingBase);
        SafeServerPath.RejectExistingReparsePoints(persistentBasePath, stagingBase);
        foreach (var staleRoot in Directory.EnumerateDirectories(stagingBase, $"{serverId:N}-*"))
        {
            SafeServerPath.RejectExistingReparsePoints(stagingBase, staleRoot);
            DeleteTreeWithoutFollowingLinks(staleRoot);
        }
    }

    public static void TryDeleteTree(string path)
    {
        try
        {
            if (Directory.Exists(path))
                DeleteTreeWithoutFollowingLinks(path);
        }
        catch
        {
            // A later access performs deterministic recovery; cleanup is best effort here.
        }
    }

    private static void DeleteTreeWithoutFollowingLinks(string path)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                if ((attributes & FileAttributes.Directory) != 0)
                    Directory.Delete(entry, false);
                else
                    File.Delete(entry);
                continue;
            }

            if ((attributes & FileAttributes.Directory) != 0)
                DeleteTreeWithoutFollowingLinks(entry);
            else
                File.Delete(entry);
        }
        Directory.Delete(path, false);
    }
}
