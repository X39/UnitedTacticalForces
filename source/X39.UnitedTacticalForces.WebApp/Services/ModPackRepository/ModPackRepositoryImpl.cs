using System.Collections.Immutable;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

[Scoped<ModPackRepositoryImpl, IModPackRepository>]
internal class ModPackRepositoryImpl : RepositoryBase, IModPackRepository
{
    public ModPackRepositoryImpl(HttpClient httpClient, BaseUrl baseUrl) : base(httpClient, baseUrl)
    {
    }

    public async Task<long> GetModPackCountAsync(
        bool myModPacksOnly,
        CancellationToken cancellationToken = default)
    {
        return await Client.ModPacksAllCountAsync(myModPacksOnly, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ModPackDefinition>> GetModPacksAsync(
        int skip,
        int take,
        bool myModPacksOnly,
        string? search = default,
        CancellationToken cancellationToken = default)
    {
        var modPacks = await Client.ModPacksAllAsync(skip, take, search, myModPacksOnly, cancellationToken)
            .ConfigureAwait(false);
        return modPacks.ToImmutableArray();
    }

    public async Task<ModPackDefinition> CreateModPackAsync(
        ModPackDefinition modPack,
        CancellationToken cancellationToken = default)
    {
        modPack = await Client.ModPacksCreateAsync(modPack, cancellationToken)
            .ConfigureAwait(false);
        return modPack;
    }

    public async Task ModifyModPackAsync(
        ModPackDefinition modPack,
        string? title = null,
        string? html = null,
        CancellationToken cancellationToken = default)
    {
        if (modPack.PrimaryKey is null)
            throw new ArgumentException("ModPack.PrimaryKey is null.", nameof(modPack));
        await Client.ModPacksUpdateAsync(
                modPack.PrimaryKey.Value,
                new ModPackUpdate
                {
                    Html  = html,
                    Title = title,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteModPackAsync(ModPackDefinition modPack, CancellationToken cancellationToken = default)
    {
        if (modPack.PrimaryKey is null)
            throw new ArgumentException("ModPack.PrimaryKey is null.", nameof(modPack));
        await Client.ModPacksDeleteAsync(modPack.PrimaryKey.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ModPackDefinition?> GetModPackDefinitionAsync(
        long modPackDefinitionPk,
        CancellationToken cancellationToken = default)
    {
        return await Client.ModPacksAsync(modPackDefinitionPk, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ModPackRevision?> GetModPackRevisionAsync(
        long modPackRevisionPk,
        CancellationToken cancellationToken = default)
    {
        return await Client.ModPacksRevisionAsync(modPackRevisionPk, cancellationToken)
            .ConfigureAwait(false);
    }
}