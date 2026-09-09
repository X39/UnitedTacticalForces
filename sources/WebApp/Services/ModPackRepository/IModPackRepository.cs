namespace X39.UnitedTacticalForces.WebApp.Services.ModPackRepository;

public interface IModPackRepository
{
    /// <summary>Gets normalized catalog mods for one game.</summary>
    /// <param name="gameIdentifier">Game identifier.</param>
    /// <param name="search">Optional title search.</param>
    /// <param name="skip">Number of entries to skip.</param>
    /// <param name="take">Maximum entries to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching mods.</returns>
    Task<IReadOnlyList<ModCatalogItemInfo>> GetModsAsync(
        string gameIdentifier,
        string? search = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves source URLs without changing the catalog.</summary>
    /// <param name="gameIdentifier">Game identifier.</param>
    /// <param name="sources">Source URLs or locators.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Canonical preview results.</returns>
    Task<IReadOnlyList<ModSourcePreviewInfo>> PreviewSourcesAsync(
        string gameIdentifier,
        IReadOnlyList<string> sources,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a reusable provider-backed catalog mod.</summary>
    /// <param name="payload">Mod definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created identifiers.</returns>
    Task<CreatedModResult> CreateModAsync(CreateModPayload payload, CancellationToken cancellationToken = default);

    /// <summary>Creates a normalized mod pack and its first revision.</summary>
    /// <param name="payload">Definition and membership.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created identifiers.</returns>
    Task<NormalizedModPackResult> CreateNormalizedModPackAsync(
        NormalizedModPackPayload payload,
        CancellationToken cancellationToken = default);

    /// <summary>Appends an immutable revision.</summary>
    /// <param name="modPackDefinitionId">Definition identifier.</param>
    /// <param name="payload">Complete new membership.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created revision identifier.</returns>
    Task<long> AppendRevisionAsync(
        long modPackDefinitionId,
        NormalizedModPackRevisionPayload payload,
        CancellationToken cancellationToken = default);

    /// <summary>Gets direct or recursively flattened revision membership.</summary>
    /// <param name="revisionId">Revision identifier.</param>
    /// <param name="effective">Whether nested memberships are flattened.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Revision membership.</returns>
    Task<ModPackMembershipInfo> GetMembershipAsync(
        long revisionId,
        bool effective,
        CancellationToken cancellationToken = default);

    /// <summary>Previews a game-owned mod-pack format.</summary>
    Task<ModPackFormatPreviewInfo> PreviewFormatAsync(string gameIdentifier, string formatIdentifier, string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Gets formats available for a revision.</summary>
    Task<IReadOnlyList<X39.UnitedTacticalForces.WebApp.Services.GameServerRepository.ModPackFormatInfo>> GetRevisionFormatsAsync(long revisionId, CancellationToken cancellationToken = default);

    /// <summary>Gets formats available for an Event's selected revision.</summary>
    Task<IReadOnlyList<X39.UnitedTacticalForces.WebApp.Services.GameServerRepository.ModPackFormatInfo>> GetEventFormatsAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Creates a reusable uploaded-artifact mod.</summary>
    Task<CreatedModResult> CreateUploadedArtifactModAsync(string gameIdentifier, string title, X39.UnitedTacticalForces.Hosting.Contracts.EModUsageRole usageRole, string installerIdentifier, string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Lists active normalized mod packs for one game.</summary>
    Task<IReadOnlyList<NormalizedModPackListItemInfo>> GetNormalizedModPacksAsync(string gameIdentifier, CancellationToken cancellationToken = default);

    /// <summary>Gets a normalized mod-pack workspace.</summary>
    Task<NormalizedModPackWorkspaceInfo?> GetNormalizedModPackAsync(long modPackDefinitionId, CancellationToken cancellationToken = default);
}
