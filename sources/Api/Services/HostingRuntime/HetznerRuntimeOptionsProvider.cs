using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

[Singleton<HetznerRuntimeOptionsProvider, IHetznerRuntimeOptionsProvider>]
public sealed class HetznerRuntimeOptionsProvider : IHetznerRuntimeOptionsProvider
{
    public HetznerRuntimeOptionsProvider(IConfiguration configuration)
    {
        Value = configuration.GetSection(HetznerRuntimeOptions.SectionName).Get<HetznerRuntimeOptions>()
                ?? new HetznerRuntimeOptions();
    }

    public HetznerRuntimeOptions Value { get; }
}