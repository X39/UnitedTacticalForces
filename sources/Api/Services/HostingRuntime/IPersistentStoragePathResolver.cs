namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public interface IPersistentStoragePathResolver
{
    string PersistentBasePath { get; }

    string ResolvePersistentRoot(Guid serverId);
}
