using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

public interface IModPackRepository
{
    
}

[Scoped<ModPackRepositoryImpl, IModPackRepository>]
internal class ModPackRepositoryImpl : RepositoryBase, IModPackRepository
{
    public ModPackRepositoryImpl(HttpClient httpClient) : base(httpClient)
    {
    }
}