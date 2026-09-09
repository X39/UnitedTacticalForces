namespace X39.UnitedTacticalForces.CloudWorker;

/// <summary>Removes provider staging directories whose retention is unsafe.</summary>
internal interface IContentDirectoryCleaner
{
    /// <summary>Removes a directory recursively or fails if any part remains.</summary>
    /// <param name="path">Exact worker-owned directory to remove.</param>
    void DeleteRequired(string path);
}
