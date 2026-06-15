using Microsoft.Extensions.Options;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

[Singleton<PersistentStoragePathResolver, IPersistentStoragePathResolver>]
public sealed class PersistentStoragePathResolver : IPersistentStoragePathResolver
{
    public PersistentStoragePathResolver(
        IOptions<PersistentServerStorageOptions> options,
        IWebHostEnvironment environment)
    {
        PersistentBasePath = SafeServerPath.ResolveConfiguredPath(
            environment.ContentRootPath,
            options.Value.BasePath,
            $"{PersistentServerStorageOptions.SectionName}:BasePath");
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (Path.GetPathRoot(PersistentBasePath)?.Equals(PersistentBasePath, comparison) == true)
            throw new InvalidOperationException("A filesystem root cannot be used as persistent server storage.");
    }

    public string PersistentBasePath { get; }

    public string ResolvePersistentRoot(Guid serverId)
        => SafeServerPath.ResolveWithinRoot(PersistentBasePath, serverId.ToString("N"));
}
