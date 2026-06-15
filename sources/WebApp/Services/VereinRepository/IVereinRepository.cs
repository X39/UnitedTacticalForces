using X39.UnitedTacticalForces.Common.Verein;

namespace X39.UnitedTacticalForces.WebApp.Services.VereinRepository;

public interface IVereinRepository
{
    Task<VereinDocumentInfo?> GetPublishedSatzungAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VereinDocumentInfo>> GetPublishedMinutesAsync(
        int skip = 0,
        int take = 24,
        CancellationToken cancellationToken = default);

    Task<VereinDocumentInfo?> GetPublishedMinuteAsync(long documentId, CancellationToken cancellationToken = default);

    Task<VereinSettingsInfo> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<VereinSettingsInfo> GetAdminSettingsAsync(CancellationToken cancellationToken = default);

    Task<VereinSettingsInfo> UpdateSettingsAsync(
        VereinSettingsUpdateInfo payload,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VereinDocumentInfo>> GetAdminDocumentsAsync(
        EVereinDocumentKind? kind = default,
        CancellationToken cancellationToken = default);

    Task<VereinDocumentInfo> CreateDocumentAsync(
        EVereinDocumentKind kind,
        VereinDocumentRevisionUpsertInfo payload,
        CancellationToken cancellationToken = default);

    Task<VereinDocumentInfo> CreateDocumentRevisionAsync(
        long documentId,
        VereinDocumentRevisionUpsertInfo payload,
        CancellationToken cancellationToken = default);

    Task<VereinDocumentInfo> PublishRevisionAsync(long revisionId, CancellationToken cancellationToken = default);

    Task<VereinDocumentInfo> UnpublishRevisionAsync(long revisionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VereinMembershipInfo>> GetMembershipsAsync(
        int take = 100,
        string? search = default,
        CancellationToken cancellationToken = default);

    Task<VereinMembershipInfo?> GetMembershipAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<VereinMembershipInfo> UpsertMembershipAsync(
        Guid userId,
        VereinMembershipUpdateInfo payload,
        CancellationToken cancellationToken = default);

    Task<VereinMembershipRequestInfo?> GetMyMembershipRequestAsync(CancellationToken cancellationToken = default);

    Task<VereinMembershipRequestInfo> CreateMembershipRequestAsync(
        VereinMembershipRequestCreateInfo payload,
        CancellationToken cancellationToken = default);

    Task<VereinMembershipRequestInfo> WithdrawMembershipRequestAsync(
        long requestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VereinMembershipRequestInfo>> GetAdminMembershipRequestsAsync(
        EVereinMembershipRequestStatus? status = default,
        int take = 100,
        string? search = default,
        CancellationToken cancellationToken = default);

    Task<VereinMembershipRequestInfo> TransitionMembershipRequestAsync(
        long requestId,
        VereinMembershipRequestTransitionInfo payload,
        CancellationToken cancellationToken = default);

    Task<KassenbuchPageInfo> GetKassenbuchAsync(
        int skip = 0,
        int take = 50,
        DateTimeOffset? from = default,
        DateTimeOffset? to = default,
        string? search = default,
        string? category = default,
        CancellationToken cancellationToken = default);

    Task<KassenbuchEntryInfo> CreateKassenbuchEntryAsync(
        KassenbuchEntryUpsertInfo payload,
        CancellationToken cancellationToken = default);

    Task<KassenbuchEntryInfo> UpdateKassenbuchEntryAsync(
        long entryId,
        KassenbuchEntryUpsertInfo payload,
        CancellationToken cancellationToken = default);

    Task<KassenbuchEntryInfo> DeleteKassenbuchEntryAsync(long entryId, CancellationToken cancellationToken = default);
}
