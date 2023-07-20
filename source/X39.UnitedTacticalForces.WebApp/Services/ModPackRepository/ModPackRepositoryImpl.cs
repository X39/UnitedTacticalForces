using System.Collections.Immutable;
using X39.Util.Collections;
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
        if (modPack.IsComposition ?? false)
        {
            var modPackRevisionIds = (modPack.ModPackRevisions ?? ArraySegment<ModPackRevision>.Empty)
                .Select((q) => q.PrimaryKey)
                .NotNull()
                .ToArray();
            modPack = modPack.ShallowCopy();
            modPack = await Client.ModPacksCreateCompositionAsync(modPackRevisionIds, modPack, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            modPack = await Client.ModPacksCreateStandaloneAsync(modPack, cancellationToken)
                .ConfigureAwait(false);
        }

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
        if (modPack.IsComposition ?? false)
            throw new ArgumentException("ModPack.IsComposition is true.", nameof(modPack));
        await Client.ModPacksUpdateStandaloneAsync(
                modPack.PrimaryKey.Value,
                new ModPackStandaloneUpdate()
                {
                    Html  = html,
                    Title = title,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ModifyModPackAsync(
        ModPackDefinition modPack,
        string? title = null,
        bool? useLatest = null,
        long[]? modPackRevisionIds = null,
        CancellationToken cancellationToken = default)
    {
        if (modPack.PrimaryKey is null)
            throw new ArgumentException("ModPack.PrimaryKey is null.", nameof(modPack));
        if (!(modPack.IsComposition ?? false))
            throw new ArgumentException("ModPack.IsComposition is false.", nameof(modPack));
        await Client.ModPacksUpdateCompositionAsync(
                modPack.PrimaryKey.Value,
                new ModPackCompositionUpdate()
                {
                    Title = title,
                    UseLatest = useLatest,
                    ModPackRevisionIds = modPackRevisionIds,
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