namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Provides verified removal of worker-owned provider staging directories.</summary>
internal sealed class ContentDirectoryCleaner : IContentDirectoryCleaner
{
    /// <inheritdoc />
    public void DeleteRequired(string path)
    {
        path = Path.GetFullPath(path);
        if (Directory.Exists(path)) Directory.Delete(path, true);
        else if (File.Exists(path)) File.Delete(path);
        if (Directory.Exists(path) || File.Exists(path))
            throw new IOException("Provider staging content remains after cleanup.");
    }
}
