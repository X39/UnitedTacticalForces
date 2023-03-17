namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

public interface IModPackRepository
{
    Task<long> GetModPackCountAsync(
        bool myModPacksOnly,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ModPack>> GetModPacksAsync(
        int skip,
        int take,
        bool myModPacksOnly,
        string? search = default,
        CancellationToken cancellationToken = default);

    Task<ModPack> CreateModPackAsync(
        ModPack modPack,
        CancellationToken cancellationToken = default);

    Task ModifyModPackAsync(
        ModPack modPack,
        CancellationToken cancellationToken = default);

    Task DeleteModPackAsync(
        ModPack modPack,
        CancellationToken cancellationToken = default);

    Task<ModPack?> GetModPackAsync(long modPackPk, CancellationToken cancellationToken = default);
}