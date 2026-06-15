using System.Reflection;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Media;

namespace X39.UnitedTacticalForces.Api.Services.Media;

/// <summary>
/// Seeds public-site image settings from API-bundled media assets.
/// </summary>
internal static class SiteImageSettingSeeder
{
    private static readonly SeedAsset[] Assets =
    [
        new("briefing-room-signals", "SiteImageDefaults.BriefingRoomSignals.jpg", "briefing-room-signals.jpg", "image/jpeg"),
        new("convoy-crossing-at-first-light", "SiteImageDefaults.ConvoyCrossingAtFirstLight.jpg", "convoy-crossing-at-first-light.jpg", "image/jpeg"),
        new("field-reports-table-hero", "SiteImageDefaults.FieldReportsTableHero.jpg", "field-reports-table-hero.jpg", "image/jpeg"),
        new("gdpr-privacy-hero", "SiteImageDefaults.GdprPrivacyHero.png", "gdpr-privacy-hero.png", "image/png"),
        new("join-recruitment-staging-hero", "SiteImageDefaults.JoinRecruitmentStagingHero.jpg", "join-recruitment-staging-hero.jpg", "image/jpeg"),
        new("mission-overwatch-banner", "SiteImageDefaults.MissionOverwatchBanner.jpg", "mission-overwatch-banner.jpg", "image/jpeg"),
        new("night-cache-resupply", "SiteImageDefaults.NightCacheResupply.jpg", "night-cache-resupply.jpg", "image/jpeg"),
        new("operations-convoy-hero", "SiteImageDefaults.OperationsConvoyHero.jpg", "operations-convoy-hero.jpg", "image/jpeg"),
        new("route-security-after-contact", "SiteImageDefaults.RouteSecurityAfterContact.jpg", "route-security-after-contact.jpg", "image/jpeg"),
        new("unit-training-assembly-hero", "SiteImageDefaults.UnitTrainingAssemblyHero.jpg", "unit-training-assembly-hero.jpg", "image/jpeg"),
        new("verein-finance-ledger-hero", "SiteImageDefaults.VereinFinanceLedgerHero.jpg", "verein-finance-ledger-hero.jpg", "image/jpeg"),
        new("verein-governance-table-hero", "SiteImageDefaults.VereinGovernanceTableHero.jpg", "verein-governance-table-hero.jpg", "image/jpeg"),
        new("wiki-reference-table-hero", "SiteImageDefaults.WikiReferenceTableHero.jpg", "wiki-reference-table-hero.jpg", "image/jpeg"),
    ];

    private static readonly SeedSetting[] Settings =
    [
        new("home-operation-hero", "Home operation hero", "Default home page operation hero image.", "home", 10, "operations-convoy-hero"),
        new("home-operation-card", "Home operation card", "Default home page operation card image.", "home", 20, "mission-overwatch-banner"),
        new("home-report-card", "Home report card", "Default home page field report card image.", "home", 30, "route-security-after-contact"),
        new("home-field-report-fallback-1", "Home report fallback 1", "Fallback report image for the home page.", "home", 40, "convoy-crossing-at-first-light"),
        new("home-field-report-fallback-2", "Home report fallback 2", "Fallback report image for the home page.", "home", 50, "night-cache-resupply"),
        new("home-field-report-fallback-3", "Home report fallback 3", "Fallback report image for the home page.", "home", 60, "briefing-room-signals"),
        new("media-hero", "Media hero", "Default media and field reports page hero image.", "media", 10, "field-reports-table-hero"),
        new("media-field-report-card", "Media report card", "Default media page report card image.", "media", 20, "convoy-crossing-at-first-light"),
        new("media-field-report-fallback-1", "Media report fallback 1", "Fallback report image for the media page.", "media", 30, "route-security-after-contact"),
        new("media-field-report-fallback-2", "Media report fallback 2", "Fallback report image for the media page.", "media", 40, "night-cache-resupply"),
        new("media-field-report-fallback-3", "Media report fallback 3", "Fallback report image for the media page.", "media", 50, "briefing-room-signals"),
        new("wiki-hero", "Wiki hero", "Default wiki landing page hero image.", "wiki", 10, "wiki-reference-table-hero"),
        new("join-hero", "Join hero", "Default recruitment page hero image.", "join", 10, "join-recruitment-staging-hero"),
        new("unit-hero", "Unit hero", "Default unit page hero image.", "unit", 10, "unit-training-assembly-hero"),
        new("verein-hero", "Verein hero", "Default Verein page hero image.", "verein", 10, "verein-governance-table-hero"),
        new("satzung-hero", "Satzung hero", "Default Satzung page hero image.", "satzung", 10, "verein-governance-table-hero"),
        new("beschluesse-hero", "Beschluesse hero", "Default Beschluesse page hero image.", "beschluesse", 10, "briefing-room-signals"),
        new("kassenbuch-hero", "Kassenbuch hero", "Default Kassenbuch page hero image.", "kassenbuch", 10, "verein-finance-ledger-hero"),
        new("gdpr-hero", "GDPR hero", "Default GDPR page hero image.", "gdpr", 10, "gdpr-privacy-hero"),
        new("public-page-default-hero", "Public page default hero", "Fallback hero image for editable public pages.", "public-pages", 10, "gdpr-privacy-hero"),
    ];

    public static async Task SeedAsync(
        DatabaseContext dbContext,
        IMediaAssetService mediaAssetService,
        CancellationToken cancellationToken = default)
    {
        var assetIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var assembly = typeof(SiteImageSettingSeeder).Assembly;
        foreach (var asset in Assets)
        {
            await using var stream = assembly.GetManifestResourceStream(asset.ResourceName)
                                     ?? throw new InvalidOperationException($"Seed image resource '{asset.ResourceName}' was not found.");
            var dto = await mediaAssetService.ImportAsync(
                    stream,
                    asset.FileName,
                    asset.ContentType,
                    ["site-image", "public-default", asset.Key],
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);
            assetIds[asset.Key] = dto.PrimaryKey;
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        foreach (var seed in Settings)
        {
            var setting = await dbContext.SiteImageSettings
                .SingleOrDefaultAsync(q => q.PrimaryKey == seed.Key, cancellationToken)
                .ConfigureAwait(false);
            if (setting is null)
            {
                dbContext.SiteImageSettings.Add(new SiteImageSetting
                {
                    PrimaryKey       = seed.Key,
                    Title            = seed.Title,
                    Description      = seed.Description,
                    Group            = seed.Group,
                    SortOrder        = seed.SortOrder,
                    ImageAssetFk     = assetIds[seed.AssetKey],
                    TimeStampCreated = now,
                    TimeStampUpdated = now,
                });
                continue;
            }

            setting.Title       = seed.Title;
            setting.Description = seed.Description;
            setting.Group       = seed.Group;
            setting.SortOrder   = seed.SortOrder;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record SeedAsset(string Key, string ResourceName, string FileName, string ContentType);

    private sealed record SeedSetting(
        string Key,
        string Title,
        string Description,
        string Group,
        int SortOrder,
        string AssetKey);
}
