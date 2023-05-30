namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

public interface IModPackRepository
{
    Task<long> GetModPackCountAsync(
        bool myModPacksOnly,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ModPackDefinition>> GetModPacksAsync(
        int skip,
        int take,
        bool myModPacksOnly,
        string? search = default,
        CancellationToken cancellationToken = default);

    Task<ModPackDefinition> CreateModPackAsync(
        ModPackDefinition modPack,
        CancellationToken cancellationToken = default);

    Task ModifyModPackAsync(
        ModPackDefinition modPack,
        string? title = null,
        string? html = null,
        CancellationToken cancellationToken = default);

    Task DeleteModPackAsync(
        ModPackDefinition modPack,
        CancellationToken cancellationToken = default);

    Task<ModPackDefinition?> GetModPackDefinitionAsync(
        long modPackDefinitionPk,
        CancellationToken cancellationToken = default);

    Task<ModPackRevision?> GetModPackRevisionAsync(
        long modPackRevisionPk,
        CancellationToken cancellationToken = default);
}