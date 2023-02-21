using System.Collections.Immutable;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

[Scoped<ModPackRepositoryImpl, IModPackRepository>]
internal class ModPackRepositoryImpl : RepositoryBase, IModPackRepository
{
    public ModPackRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : base(httpClient, baseUrl)
    {
    }

    public async Task<long> GetModPackCountAsync(bool myModPacksOnly, CancellationToken cancellationToken = default)
    {
        return await Client.ModPackAllCountAsync(myModPacksOnly, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ModPack>> GetModPacksAsync(int skip, int take, bool myModPacksOnly, CancellationToken cancellationToken = default)
    {
        var modPacks = await Client.ModPackAllAsync(skip, take, myModPacksOnly, cancellationToken)
            .ConfigureAwait(false);
        return modPacks.ToImmutableArray();
    }
    public async Task<ModPack> CreateModPackAsync(ModPack modPack, CancellationToken cancellationToken = default)
    {
        modPack = await Client.ModPackCreateAsync(modPack, cancellationToken)
            .ConfigureAwait(false);
        return modPack;
    }
    public async Task ModifyModPackAsync(ModPack modPack, CancellationToken cancellationToken = default)
    {
        if (modPack.PrimaryKey is null)
            throw new ArgumentException("ModPack.PrimaryKey is null.", nameof(modPack));
        await Client.ModPackUpdateAsync(modPack.PrimaryKey.Value, modPack, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteModPackAsync(ModPack modPack, CancellationToken cancellationToken = default)
    {
        if (modPack.PrimaryKey is null)
            throw new ArgumentException("ModPack.PrimaryKey is null.", nameof(modPack));
        await Client.ModPackDeleteAsync(modPack.PrimaryKey.Value, cancellationToken)
            .ConfigureAwait(false);
    }
}