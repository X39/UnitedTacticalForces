using X39.UnitedTacticalForces.WebApp.Api.Models;

namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

public interface IModPackRepository
{
    Task<long> GetModPackCountAsync(bool myModPacksOnly, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FullModPackDefinitionDto>> GetModPacksAsync(
        int skip,
        int take,
        bool myModPacksOnly,
        string? search = default,
        CancellationToken cancellationToken = default
    );

    Task<PlainModPackDefinitionDto> CreateModPackAsync(
        string title,
        IReadOnlyCollection<long> modPackRevisionIds,
        CancellationToken cancellationToken = default
    );

    Task<PlainModPackDefinitionDto> CreateModPackAsync(
        PlainModPackDefinitionDto definition,
        PlainModPackRevisionDto revision,
        CancellationToken cancellationToken = default
    );

    Task ModifyModPackAsync(
        long modPackDefinitionId,
        string? title = null,
        string? html = null,
        CancellationToken cancellationToken = default
    );

    Task ModifyModPackAsync(
        long modPackDefinitionId,
        string? title = null,
        bool? useLatest = null,
        long[]? modPackRevisionIds = null,
        CancellationToken cancellationToken = default
    );

    Task DeleteModPackAsync(long modPackDefinitionId, CancellationToken cancellationToken = default);

    Task<ModPackDefinitionDto?> GetModPackDefinitionAsync(
        long modPackDefinitionId,
        CancellationToken cancellationToken = default
    );

    Task<ModPackRevisionDto?> GetModPackRevisionAsync(
        long modPackRevisionPk,
        CancellationToken cancellationToken = default
    );
}
