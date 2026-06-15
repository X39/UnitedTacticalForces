using System.Security.Claims;
using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Content;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Database.Eventing;
using X39.UnitedTacticalForces.Database.Media;
using X39.UnitedTacticalForces.Database.Verein;
using X39.UnitedTacticalForces.Database.Wiki;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Api.Services.Media;
using X39.UnitedTacticalForces.Common.Event;
using X39.UnitedTacticalForces.Common.PublicPages;
using X39.UnitedTacticalForces.Common.Verein;
using Claim = X39.UnitedTacticalForces.Database.Authority.Claim;
using Event = X39.UnitedTacticalForces.Database.Eventing.Event;

namespace X39.UnitedTacticalForces.Api.Data;

internal static class DebugDemoDataSeeder
{
    private static readonly Guid DefaultDebugUserId = Guid.Parse("b6aa28a3-a740-4e0c-b5dc-157c04881872");
    private static readonly Guid DebugEventId = Guid.Parse("9c61b11d-5ed6-43b3-99d4-6bb889f0e1a1");
    private static readonly Guid StormRelayEventId = Guid.Parse("1c66f9c5-9ed9-4d08-89fe-61f11b39a4a1");
    private static readonly Guid AirstripLogisticsEventId = Guid.Parse("b41b53a1-ece5-4d1c-a117-e4c4a09e68d6");
    private static readonly byte[] DebugUserAvatarJpeg = ReadSeedAsset("DebugDemoData.X39Avatar.jpg");
    private static readonly byte[] DebugRavenAvatarJpeg = ReadSeedAsset("DebugDemoData.RavenAvatar.jpg");
    private static readonly byte[] DebugSignalAvatarJpeg = ReadSeedAsset("DebugDemoData.SignalAvatar.jpg");
    private static readonly byte[] DebugHammerAvatarJpeg = ReadSeedAsset("DebugDemoData.HammerAvatar.jpg");
    private static readonly byte[] DebugSlateAvatarJpeg = ReadSeedAsset("DebugDemoData.SlateAvatar.jpg");
    private static readonly byte[] DebugFoxAvatarJpeg = ReadSeedAsset("DebugDemoData.FoxAvatar.jpg");
    private static readonly byte[] DebugViperAvatarJpeg = ReadSeedAsset("DebugDemoData.ViperAvatar.jpg");
    private static readonly byte[] DebugAltisTerrainJpeg = ReadSeedAsset("DebugDemoData.AltisTerrain.jpg");
    private static readonly byte[] DebugTanoaTerrainJpeg = ReadSeedAsset("DebugDemoData.TanoaTerrain.jpg");
    private static readonly byte[] DebugLivoniaTerrainJpeg = ReadSeedAsset("DebugDemoData.LivoniaTerrain.jpg");
    private static readonly byte[] DebugMaldenTerrainJpeg = ReadSeedAsset("DebugDemoData.MaldenTerrain.jpg");
    private static readonly byte[] DebugEventImageJpeg = ReadSeedAsset("DebugDemoData.CopperTrailEvent.jpg");
    private static readonly byte[] DebugStormRelayEventJpeg = ReadSeedAsset("DebugDemoData.StormRelayEvent.jpg");
    private static readonly byte[] DebugAirstripLogisticsEventJpeg =
        ReadSeedAsset("DebugDemoData.AirstripLogisticsEvent.jpg");
    private static readonly IReadOnlyDictionary<string, PublicSeedAsset> PublicSeedAssetsByPath =
        new Dictionary<string, PublicSeedAsset>(StringComparer.OrdinalIgnoreCase)
        {
            ["gdpr-privacy-hero.png"] = new("SiteImageDefaults.GdprPrivacyHero.png", "gdpr-privacy-hero.png", "image/png"),
            ["media/field-reports/briefing-room-signals.jpg"] = new("SiteImageDefaults.BriefingRoomSignals.jpg", "briefing-room-signals.jpg", SeedImageMimeType),
            ["media/field-reports/convoy-crossing-at-first-light.jpg"] = new("SiteImageDefaults.ConvoyCrossingAtFirstLight.jpg", "convoy-crossing-at-first-light.jpg", SeedImageMimeType),
            ["media/field-reports/night-cache-resupply.jpg"] = new("SiteImageDefaults.NightCacheResupply.jpg", "night-cache-resupply.jpg", SeedImageMimeType),
            ["media/field-reports/route-security-after-contact.jpg"] = new("SiteImageDefaults.RouteSecurityAfterContact.jpg", "route-security-after-contact.jpg", SeedImageMimeType),
        };

    private const ulong DefaultDebugSteamId64 = 76561197994307486;
    private const string SeedImageMimeType = "image/jpeg";
    private const string DebugAdminRoleTitle = "Debug Administrator";
    private const string DebugModPackTitle = "Debug Arma 3 Mod Pack";
    private const string DebugTrainingModPackTitle = "Debug Training Essentials";
    private const string DebugReconModPackTitle = "Debug Recon Composition";
    private const string DebugTerrainAltisTitle = "Altis (Debug)";
    private const string DebugTerrainTanoaTitle = "Tanoa (Debug)";
    private const string DebugTerrainLivoniaTitle = "Livonia (Debug)";
    private const string DebugTerrainMaldenTitle = "Malden (Debug)";
    private const string OperationCopperTrailDescription = """
                                                           SITREP
                                                           Reconnaissance flights over Tanoa's northern interior have confirmed renewed activity around a shuttered copper processing station. Local security forces report generator noise, fresh vehicle tracks, and a missing relief convoy last seen on the jungle road west of the facility.

                                                           Mission
                                                           Callsign Copper will insert before dusk, establish a quiet observation point, identify the source of the activity, recover convoy records if present, and withdraw before the storm front closes the valley road.

                                                           Execution Notes
                                                           Expect low visibility, wet roads, and limited radio range inside the valley. The platoon lead controls tempo; Alpha secures the refinery perimeter while Bravo checks the convoy route and prepares vehicle recovery if the road remains passable.

                                                           Demo Scope
                                                           This is fictional debug seed content for local UI, scheduling, and lifecycle testing. It does not create provider resources, expose credentials, or represent a live operation.
                                                           """;
    private const string OperationStormRelayDescription = """
                                                          SITREP
                                                          Intermittent traffic from a coastal relay station suggests a hostile listening post is forwarding patrol routes before the next island rotation. Weather cleared just enough for one reconnaissance window before another storm cell reaches the ridge line.

                                                          Mission
                                                          Callsign Relay will insert from the western coast, confirm the relay site's equipment state, collect signal notes if safe, and mark extraction lanes before the weather closes the landing zone.

                                                          Execution Notes
                                                          Expect wet vegetation, short sight lines, and degraded long-range communications. Recon controls the approach; Alpha secures the lower switchback while Bravo screens the ridge and keeps the extraction route open.

                                                          Demo Scope
                                                          This fictional debug event exists to exercise richer event cards, attendance states, slotting, and image handling in local development.
                                                          """;
    private const string TrainingAirstripLogisticsDescription = """
                                                               Training Intent
                                                               The quartermaster cell is running a short logistics night focused on convoy staging, vehicle recovery, and resupply discipline before the next full operation.

                                                               Tasks
                                                               Teams will prepare a temporary airstrip supply point, package repair loads, rehearse radio handoffs between drivers and security, and run a controlled recovery lane with time pressure.

                                                               Success Criteria
                                                               The session is complete when each team can stage, dispatch, recover, and document a small convoy without exposing route plans or delaying the next wave.

                                                               Demo Scope
                                                               This fictional debug training event fills local scheduling, role, slot, and mod-pack data without starting a live game server.
                                                               """;
    public static async Task SeedAsync(
        DatabaseContext dbContext,
        IMediaAssetService mediaAssetService,
        CancellationToken cancellationToken = default)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var debugUser = await EnsureDebugUserAsync(dbContext, cancellationToken).ConfigureAwait(false);
        var debugUsers = await EnsureAdditionalDebugUsersAsync(dbContext, cancellationToken).ConfigureAwait(false);
        var allDebugUsers = new Dictionary<string, User>(debugUsers, StringComparer.OrdinalIgnoreCase)
        {
            ["X39"] = debugUser,
        };
        var claimCatalog = await EnsureClaimCatalogAsync(dbContext, cancellationToken).ConfigureAwait(false);
        await EnsureDebugAdminRoleAsync(
                dbContext,
                debugUser,
                claimCatalog[X39.UnitedTacticalForces.Claims.Administrative.All],
                cancellationToken
            )
            .ConfigureAwait(false);
        await EnsureDebugRolesAsync(dbContext, allDebugUsers, claimCatalog, cancellationToken).ConfigureAwait(false);
        await EnsureDebugUserProfilesAsync(dbContext, allDebugUsers, cancellationToken).ConfigureAwait(false);

        var altis = await EnsureTerrainAsync(dbContext, mediaAssetService, DebugTerrainAltisTitle, cancellationToken).ConfigureAwait(false);
        var tanoa = await EnsureTerrainAsync(dbContext, mediaAssetService, DebugTerrainTanoaTitle, cancellationToken).ConfigureAwait(false);
        var livonia = await EnsureTerrainAsync(dbContext, mediaAssetService, DebugTerrainLivoniaTitle, cancellationToken).ConfigureAwait(false);
        var malden = await EnsureTerrainAsync(dbContext, mediaAssetService, DebugTerrainMaldenTitle, cancellationToken).ConfigureAwait(false);
        var modPack = await EnsureModPackAsync(
                dbContext,
                debugUser,
                DebugModPackTitle,
                isComposition: false,
                now,
                cancellationToken
            )
            .ConfigureAwait(false);
        var modPackRevision = await EnsureActiveModPackRevisionAsync(
                dbContext,
                modPack,
                debugUser,
                now,
                CreateDemoModPackHtml(),
                cancellationToken
            )
            .ConfigureAwait(false);
        var trainingModPack = await EnsureModPackAsync(
                dbContext,
                debugUser,
                DebugTrainingModPackTitle,
                isComposition: false,
                now,
                cancellationToken
            )
            .ConfigureAwait(false);
        var trainingModPackRevision = await EnsureActiveModPackRevisionAsync(
                dbContext,
                trainingModPack,
                debugUser,
                now,
                CreateTrainingModPackHtml(),
                cancellationToken
            )
            .ConfigureAwait(false);
        var reconModPack = await EnsureModPackAsync(
                dbContext,
                debugUser,
                DebugReconModPackTitle,
                isComposition: false,
                now,
                cancellationToken
            )
            .ConfigureAwait(false);
        var reconModPackRevision = await EnsureActiveModPackRevisionAsync(
                dbContext,
                reconModPack,
                debugUser,
                now,
                CreateReconModPackHtml(),
                cancellationToken
            )
            .ConfigureAwait(false);
        await EnsureEventsAsync(
                dbContext,
                mediaAssetService,
                allDebugUsers,
                altis,
                tanoa,
                livonia,
                malden,
                modPackRevision,
                trainingModPackRevision,
                reconModPackRevision,
                now,
                cancellationToken
            )
            .ConfigureAwait(false);
        await EnsureFieldReportsAsync(dbContext, mediaAssetService, debugUser, now, cancellationToken).ConfigureAwait(false);
        await EnsureWikiPagesAsync(dbContext, allDebugUsers, now, cancellationToken).ConfigureAwait(false);
        await EnsurePublicPagesAsync(dbContext, mediaAssetService, allDebugUsers, now, cancellationToken).ConfigureAwait(false);
        await EnsureVereinDataAsync(dbContext, allDebugUsers, now, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<User> EnsureDebugUserAsync(DatabaseContext dbContext, CancellationToken cancellationToken)
    {
        var debugUser = await dbContext.Users
            .Include(q => q.Roles!)
            .ThenInclude(q => q.Claims)
            .Include(q => q.Claims)
            .FirstOrDefaultAsync(
                q => q.PrimaryKey == DefaultDebugUserId || q.Steam.Id64 == DefaultDebugSteamId64,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (debugUser is null)
        {
            debugUser = new User
            {
                PrimaryKey = DefaultDebugUserId,
                Nickname = "X39",
                EMail = null,
                IsBanned = false,
                Avatar = DebugUserAvatarJpeg,
                AvatarMimeType = SeedImageMimeType,
                IsVerified = true,
                IsDeleted = false,
                Steam = new SteamUser
                {
                    Id64 = DefaultDebugSteamId64,
                },
                Discord = new DiscordUser
                {
                    Id = 0,
                    Username = string.Empty,
                },
                Roles = [],
                Claims = [],
            };
            dbContext.Users.Add(debugUser);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return debugUser;
        }

        if (string.IsNullOrWhiteSpace(debugUser.Nickname))
            debugUser.Nickname = "X39";
        if (debugUser.Steam.Id64 == 0 || debugUser.PrimaryKey == DefaultDebugUserId)
            debugUser.Steam.Id64 = DefaultDebugSteamId64;
        debugUser.IsBanned = false;
        debugUser.IsDeleted = false;
        debugUser.IsVerified = true;
        debugUser.Discord ??= new DiscordUser();
        debugUser.Discord.Username ??= string.Empty;
        debugUser.Roles ??= [];
        debugUser.Claims ??= [];
        if (debugUser.Avatar.Length == 0)
        {
            debugUser.Avatar = DebugUserAvatarJpeg;
            debugUser.AvatarMimeType = SeedImageMimeType;
        }
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return debugUser;
    }

    private static async Task<IReadOnlyDictionary<string, User>> EnsureAdditionalDebugUsersAsync(
        DatabaseContext dbContext,
        CancellationToken cancellationToken)
    {
        var users = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in DebugUserSeeds)
        {
            var user = await dbContext.Users
                .FirstOrDefaultAsync(
                    q => q.PrimaryKey == seed.PrimaryKey || q.Steam.Id64 == seed.SteamId64,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (user is null)
            {
                user = new User
                {
                    PrimaryKey = seed.PrimaryKey,
                    Roles = [],
                    Claims = [],
                };
                dbContext.Users.Add(user);
            }

            user.Nickname = seed.Nickname;
            user.EMail = seed.Email;
            user.IsBanned = seed.IsBanned;
            user.IsDeleted = false;
            user.IsVerified = seed.IsVerified;
            user.Steam ??= new SteamUser();
            user.Steam.Id64 = seed.SteamId64;
            user.Discord ??= new DiscordUser();
            user.Discord.Id = seed.DiscordId;
            user.Discord.Username = seed.DiscordUsername;
            if (user.Avatar.Length < 1024)
            {
                user.Avatar = seed.Avatar;
                user.AvatarMimeType = SeedImageMimeType;
            }

            users[seed.Key] = user;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return users;
    }

    private static async Task<IReadOnlyDictionary<string, Claim>> EnsureClaimCatalogAsync(
        DatabaseContext dbContext,
        CancellationToken cancellationToken)
    {
        var claims = new Dictionary<string, Claim>(StringComparer.Ordinal);
        foreach (var seed in ClaimSeeds)
        {
            var claim = await EnsureClaimAsync(dbContext, seed, cancellationToken).ConfigureAwait(false);
            claims[seed.Identifier] = claim;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return claims;
    }

    private static async Task<Claim> EnsureClaimAsync(
        DatabaseContext dbContext,
        ClaimSeed seed,
        CancellationToken cancellationToken)
    {
        var value = seed.Value ?? string.Empty;
        var valueType = seed.ValueType ?? ClaimValueTypes.String;
        var claim = await dbContext.Claims
            .FirstOrDefaultAsync(
                q => q.Identifier == seed.Identifier
                     && (q.Value ?? string.Empty) == value
                     && (q.ValueType ?? ClaimValueTypes.String) == valueType,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (claim is null)
        {
            claim = new Claim
            {
                Identifier = seed.Identifier,
                Value = value,
                ValueType = valueType,
            };
            dbContext.Claims.Add(claim);
        }

        claim.Category = seed.Category;
        claim.Title = seed.Title;
        claim.Description = seed.Description;
        claim.IsPrefix = seed.IsPrefix;
        claim.Value = value;
        claim.ValueType = valueType;
        return claim;
    }

    private static async Task EnsureDebugAdminRoleAsync(
        DatabaseContext dbContext,
        User debugUser,
        Claim adminClaim,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .Include(q => q.Claims)
            .Include(q => q.Users)
            .FirstOrDefaultAsync(q => q.Title == DebugAdminRoleTitle, cancellationToken)
            .ConfigureAwait(false);
        if (role is null)
        {
            role = new Role
            {
                Title = DebugAdminRoleTitle,
                Description = "Debug-only administrator role seeded for local development.",
                Claims = [],
                Users = [],
            };
            dbContext.Roles.Add(role);
        }

        role.Description = "Debug-only administrator role seeded for local development.";
        role.Claims ??= [];
        if (!role.Claims.Any(q => q.PrimaryKey == adminClaim.PrimaryKey || q.Identifier == adminClaim.Identifier))
            role.Claims.Add(adminClaim);

        debugUser.Roles ??= [];
        if (!debugUser.Roles.Any(q => q.Title == DebugAdminRoleTitle))
            debugUser.Roles.Add(role);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureDebugRolesAsync(
        DatabaseContext dbContext,
        IReadOnlyDictionary<string, User> users,
        IReadOnlyDictionary<string, Claim> claimCatalog,
        CancellationToken cancellationToken)
    {
        foreach (var seed in DebugRoleSeeds)
        {
            var role = await dbContext.Roles
                .Include(q => q.Claims)
                .Include(q => q.Users)
                .FirstOrDefaultAsync(q => q.Title == seed.Title, cancellationToken)
                .ConfigureAwait(false);
            if (role is null)
            {
                role = new Role
                {
                    Title = seed.Title,
                    Claims = [],
                    Users = [],
                };
                dbContext.Roles.Add(role);
            }

            role.Description = seed.Description;
            role.Claims ??= [];
            foreach (var claimIdentifier in seed.ClaimIdentifiers)
            {
                var claim = claimCatalog[claimIdentifier];
                if (!role.Claims.Any(q => q.PrimaryKey == claim.PrimaryKey || q.Identifier == claim.Identifier))
                    role.Claims.Add(claim);
            }

            role.Users ??= [];
            foreach (var userKey in seed.UserKeys)
            {
                if (!users.TryGetValue(userKey, out var user))
                    continue;
                if (!role.Users.Any(q => q.PrimaryKey == user.PrimaryKey))
                    role.Users.Add(user);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Terrain> EnsureTerrainAsync(
        DatabaseContext dbContext,
        IMediaAssetService mediaAssetService,
        string title,
        CancellationToken cancellationToken)
    {
        var imageAsset = await EnsureSeedMediaAssetAsync(
                mediaAssetService,
                GetTerrainImage(title),
                $"{CreateFileSlug(title)}.jpg",
                ["debug", "terrain"],
                cancellationToken
            )
            .ConfigureAwait(false);
        var terrain = await dbContext.Terrains
            .FirstOrDefaultAsync(q => q.Title == title, cancellationToken)
            .ConfigureAwait(false);
        if (terrain is null)
        {
            terrain = new Terrain
            {
                Title        = title,
                ImageAssetFk = imageAsset.PrimaryKey,
                IsActive     = true,
            };
            dbContext.Terrains.Add(terrain);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return terrain;
        }

        terrain.IsActive = true;
        if (terrain.ImageAssetFk == default)
            terrain.ImageAssetFk = imageAsset.PrimaryKey;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return terrain;
    }

    private static async Task<ModPackDefinition> EnsureModPackAsync(
        DatabaseContext dbContext,
        User debugUser,
        string title,
        bool isComposition,
        Instant now,
        CancellationToken cancellationToken)
    {
        var modPack = await dbContext.ModPackDefinitions
            .Include(q => q.ModPackRevisions)
            .FirstOrDefaultAsync(q => q.Title == title, cancellationToken)
            .ConfigureAwait(false);
        if (modPack is null)
        {
            modPack = new ModPackDefinition
            {
                TimeStampCreated = now,
                Title = title,
                OwnerFk = debugUser.PrimaryKey,
                IsActive = true,
                IsComposition = isComposition,
                ModPackRevisions = [],
                ModPackRevisionsOwned = [],
            };
            dbContext.ModPackDefinitions.Add(modPack);
        }

        modPack.OwnerFk = debugUser.PrimaryKey;
        modPack.IsActive = true;
        modPack.IsComposition = isComposition;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return modPack;
    }

    private static async Task<ModPackRevision> EnsureActiveModPackRevisionAsync(
        DatabaseContext dbContext,
        ModPackDefinition modPack,
        User debugUser,
        Instant now,
        string html,
        CancellationToken cancellationToken)
    {
        var revision = await dbContext.ModPackRevisions
            .FirstOrDefaultAsync(q => q.DefinitionFk == modPack.PrimaryKey && q.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (revision is null)
        {
            revision = new ModPackRevision
            {
                DefinitionFk = modPack.PrimaryKey,
                TimeStampCreated = now,
                UpdatedByFk = debugUser.PrimaryKey,
                IsActive = true,
                Html = html,
            };
            dbContext.ModPackRevisions.Add(revision);
        }

        revision.UpdatedByFk = debugUser.PrimaryKey;
        if (string.IsNullOrWhiteSpace(revision.Html))
            revision.Html = html;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        modPack.ModPackRevisions ??= [];
        if (!modPack.ModPackRevisions.Any(q => q.PrimaryKey == revision.PrimaryKey))
            modPack.ModPackRevisions.Add(revision);
        modPack.ModPackRevisionsOwned ??= [];
        if (!modPack.ModPackRevisionsOwned.Any(q => q.PrimaryKey == revision.PrimaryKey))
            modPack.ModPackRevisionsOwned.Add(revision);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return revision;
    }

    private static async Task EnsureEventsAsync(
        DatabaseContext dbContext,
        IMediaAssetService mediaAssetService,
        IReadOnlyDictionary<string, User> users,
        Terrain altis,
        Terrain tanoa,
        Terrain livonia,
        Terrain malden,
        ModPackRevision modPackRevision,
        ModPackRevision trainingModPackRevision,
        ModPackRevision reconModPackRevision,
        Instant now,
        CancellationToken cancellationToken)
    {
        _ = livonia;
        var baseDate = now.InUtc().Date;
        var seeds = new[]
        {
            new EventSeed(
                DebugEventId,
                "Operation Copper Trail",
                OperationCopperTrailDescription,
                DebugEventImageJpeg,
                altis,
                modPackRevision,
                CreateUtcInstant(baseDate, 7, 19),
                "X39",
                "Raven",
                true,
                6,
                [
                    new("X39", EEventAcceptance.Accepted),
                    new("Raven", EEventAcceptance.Accepted),
                    new("Signal", EEventAcceptance.Accepted),
                    new("Hammer", EEventAcceptance.Accepted),
                    new("Slate", EEventAcceptance.Accepted),
                    new("Fox", EEventAcceptance.Accepted),
                    new("Viper", EEventAcceptance.Maybe),
                ],
                [
                    new(1, "Command", "Platoon Lead", EArmaSide.West, "X39"),
                    new(2, "Command", "Radio Telephone Operator", EArmaSide.West, "Signal"),
                    new(3, "Command", "Combat Medic", EArmaSide.West, "Slate"),
                    new(4, "Alpha", "Squad Lead", EArmaSide.West, "Raven"),
                    new(5, "Alpha", "Automatic Rifleman", EArmaSide.West, "Hammer"),
                    new(6, "Alpha", "Grenadier", EArmaSide.West, "Fox"),
                    new(7, "Bravo", "Team Lead", EArmaSide.West),
                    new(8, "Bravo", "Rifleman AT", EArmaSide.West, "Viper"),
                    new(9, "Bravo", "Engineer", EArmaSide.West),
                    new(10, "Support", "Driver", EArmaSide.West),
                    new(11, "Support", "Crewman", EArmaSide.West),
                    new(12, "Reserve", "Rifleman", EArmaSide.West),
                ]
            ),
            new EventSeed(
                StormRelayEventId,
                "Operation Storm Relay",
                OperationStormRelayDescription,
                DebugStormRelayEventJpeg,
                tanoa,
                reconModPackRevision,
                CreateUtcInstant(baseDate, 3, 18),
                "X39",
                "Raven",
                true,
                5,
                [
                    new("X39", EEventAcceptance.Accepted),
                    new("Raven", EEventAcceptance.Accepted),
                    new("Signal", EEventAcceptance.Accepted),
                    new("Fox", EEventAcceptance.Accepted),
                    new("Slate", EEventAcceptance.Maybe),
                    new("Viper", EEventAcceptance.Maybe),
                    new("Hammer", EEventAcceptance.Rejected),
                ],
                [
                    new(1, "Recon", "Recon Lead", EArmaSide.West, "Raven"),
                    new(2, "Recon", "Signals Specialist", EArmaSide.West, "Signal"),
                    new(3, "Recon", "Scout", EArmaSide.West, "Fox"),
                    new(4, "Recon", "Marksman", EArmaSide.West),
                    new(5, "Security", "Team Lead", EArmaSide.West, "X39"),
                    new(6, "Security", "Rifleman", EArmaSide.West, "Viper"),
                    new(7, "Support", "Medic", EArmaSide.West, "Slate"),
                    new(8, "Support", "Boat Crew", EArmaSide.West),
                ]
            ),
            new EventSeed(
                AirstripLogisticsEventId,
                "Training Night: Airstrip Logistics",
                TrainingAirstripLogisticsDescription,
                DebugAirstripLogisticsEventJpeg,
                malden,
                trainingModPackRevision,
                CreateUtcInstant(baseDate, 11, 20),
                "X39",
                "Hammer",
                true,
                4,
                [
                    new("X39", EEventAcceptance.Accepted),
                    new("Hammer", EEventAcceptance.Accepted),
                    new("Slate", EEventAcceptance.Accepted),
                    new("Viper", EEventAcceptance.Accepted),
                    new("Signal", EEventAcceptance.Maybe),
                    new("Fox", EEventAcceptance.Rejected),
                ],
                [
                    new(1, "Instruction", "Training Lead", EArmaSide.West, "Hammer"),
                    new(2, "Instruction", "Safety Medic", EArmaSide.West, "Slate"),
                    new(3, "Convoy", "Convoy Commander", EArmaSide.West, "X39"),
                    new(4, "Convoy", "Lead Driver", EArmaSide.West, "Viper"),
                    new(5, "Convoy", "Recovery Driver", EArmaSide.West),
                    new(6, "Logistics", "Quartermaster", EArmaSide.West, "Signal"),
                    new(7, "Logistics", "Repair Specialist", EArmaSide.West),
                    new(8, "Security", "Airstrip Security", EArmaSide.West),
                ]
            ),
        };

        foreach (var seed in seeds)
        {
            await EnsureEventAsync(dbContext, mediaAssetService, users, seed, now, cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Instant CreateUtcInstant(LocalDate baseDate, int days, int hour)
        => baseDate.PlusDays(days)
            .AtStartOfDayInZone(DateTimeZone.Utc)
            .ToInstant()
            .Plus(Duration.FromHours(hour));

    private static async Task EnsureEventAsync(
        DatabaseContext dbContext,
        IMediaAssetService mediaAssetService,
        IReadOnlyDictionary<string, User> users,
        EventSeed seed,
        Instant now,
        CancellationToken cancellationToken)
    {
        var imageAsset = await EnsureSeedMediaAssetAsync(
                mediaAssetService,
                seed.Image,
                $"{CreateFileSlug(seed.Title)}.jpg",
                ["debug", "event"],
                cancellationToken
            )
            .ConfigureAwait(false);
        var existingEvent = await dbContext.Events
            .FirstOrDefaultAsync(q => q.PrimaryKey == seed.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        if (existingEvent is null)
        {
            existingEvent = new Event
            {
                PrimaryKey = seed.PrimaryKey,
                TimeStampCreated = now,
                ImageAssetFk = imageAsset.PrimaryKey,
            };
            dbContext.Events.Add(existingEvent);
        }

        existingEvent.Title = seed.Title;
        existingEvent.Description = seed.Description;
        if (existingEvent.ImageAssetFk == default)
            existingEvent.ImageAssetFk = imageAsset.PrimaryKey;
        existingEvent.TerrainFk = seed.Terrain.PrimaryKey;
        existingEvent.ModPackRevisionFk = seed.ModPackRevision.PrimaryKey;
        existingEvent.ScheduledForOriginal = seed.ScheduledFor;
        existingEvent.ScheduledFor = seed.ScheduledFor;
        existingEvent.OwnerFk = ResolveUserId(users, seed.OwnerUserKey);
        existingEvent.HostedByFk = ResolveUserId(users, seed.HostedByUserKey);
        existingEvent.IsVisible = seed.IsVisible;
        existingEvent.AcceptedCount = seed.Attendances.Count(q => q.Acceptance == EEventAcceptance.Accepted);
        existingEvent.MaybeCount = seed.Attendances.Count(q => q.Acceptance == EEventAcceptance.Maybe);
        existingEvent.RejectedCount = seed.Attendances.Count(q => q.Acceptance == EEventAcceptance.Rejected);
        existingEvent.MinimumAccepted = seed.MinimumAccepted;

        foreach (var slotSeed in seed.Slots)
        {
            await EnsureEventSlotAsync(dbContext, users, seed.PrimaryKey, slotSeed, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var attendance in seed.Attendances)
        {
            var userId = ResolveUserId(users, attendance.UserKey);
            await EnsureUserEventMetaAsync(dbContext, seed.PrimaryKey, userId, attendance.Acceptance, cancellationToken)
                .ConfigureAwait(false);
            if (attendance.Acceptance != EEventAcceptance.Rejected)
                await EnsureUserModPackMetaAsync(dbContext, seed.ModPackRevision, userId, now, cancellationToken)
                    .ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureEventSlotAsync(
        DatabaseContext dbContext,
        IReadOnlyDictionary<string, User> users,
        Guid eventId,
        EventSlotSeed seed,
        CancellationToken cancellationToken)
    {
        var slot = await dbContext.EventSlots
            .FirstOrDefaultAsync(
                q => q.EventFk == eventId && q.SlotNumber == seed.SlotNumber,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (slot is null)
        {
            slot = new EventSlot
            {
                EventFk = eventId,
                SlotNumber = seed.SlotNumber,
            };
            dbContext.EventSlots.Add(slot);
        }

        slot.AssignedToFk = string.IsNullOrWhiteSpace(seed.AssignedUserKey)
            ? null
            : ResolveUserId(users, seed.AssignedUserKey);
        slot.IsSelfAssignable = seed.IsSelfAssignable;
        slot.IsVisible = seed.IsVisible;
        slot.Group = seed.Group;
        slot.Title = seed.Title;
        slot.Side = seed.Side;
    }

    private static async Task EnsureUserEventMetaAsync(
        DatabaseContext dbContext,
        Guid eventId,
        Guid userId,
        EEventAcceptance acceptance,
        CancellationToken cancellationToken)
    {
        var meta = await dbContext.UserEventMetas
            .FirstOrDefaultAsync(
                q => q.EventFk == eventId && q.UserFk == userId,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (meta is null)
        {
            meta = new UserEventMeta
            {
                EventFk = eventId,
                UserFk = userId,
            };
            dbContext.UserEventMetas.Add(meta);
        }

        meta.Acceptance = acceptance;
    }

    private static async Task EnsureUserModPackMetaAsync(
        DatabaseContext dbContext,
        ModPackRevision modPackRevision,
        Guid userId,
        Instant now,
        CancellationToken cancellationToken)
    {
        var meta = await dbContext.UserModPackMetas
            .FirstOrDefaultAsync(
                q => q.UserFk == userId
                     && q.ModPackDefinitionFk == modPackRevision.DefinitionFk
                     && q.ModPackRevisionFk == modPackRevision.PrimaryKey,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (meta is null)
        {
            meta = new UserModPackMeta
            {
                UserFk = userId,
                ModPackDefinitionFk = modPackRevision.DefinitionFk,
                ModPackRevisionFk = modPackRevision.PrimaryKey,
            };
            dbContext.UserModPackMetas.Add(meta);
        }

        if (meta.TimeStampDownloaded == default)
            meta.TimeStampDownloaded = now;
    }

    private static Guid ResolveUserId(IReadOnlyDictionary<string, User> users, string userKey)
    {
        if (!users.TryGetValue(userKey, out var user))
            throw new InvalidOperationException($"Debug seed user '{userKey}' was not configured.");
        return user.PrimaryKey;
    }

    private static async Task EnsureDebugUserProfilesAsync(
        DatabaseContext dbContext,
        IReadOnlyDictionary<string, User> users,
        CancellationToken cancellationToken)
    {
        foreach (var seed in UserProfileSeeds)
        {
            if (!users.TryGetValue(seed.UserKey, out var user))
                continue;

            user.FirstName = seed.FirstName;
            user.LastName = seed.LastName;
            user.PhoneNumber = seed.PhoneNumber;
            user.AddressLine1 = seed.AddressLine1;
            user.AddressLine2 = seed.AddressLine2;
            user.PostalCode = seed.PostalCode;
            user.City = seed.City;
            user.Country = seed.Country;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureWikiPagesAsync(
        DatabaseContext dbContext,
        IReadOnlyDictionary<string, User> users,
        Instant now,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new WikiPageSeed(
                Guid.Parse("7f6770c3-9fb0-402d-89f9-936888c5e131"),
                "Cloud server operations runbook",
                false,
                now.Plus(Duration.FromDays(-18)),
                [
                    new(
                        Guid.Parse("80d8a844-a7ac-47e5-b584-3d168c05beca"),
                        now.Plus(Duration.FromDays(-18)),
                        "X39",
                        """
                        # Cloud server operations runbook

                        This debug page documents the local control-plane workflow for planning a cloud-hosted Arma 3 session.

                        ## Checklist

                        - Confirm a billing owner is assigned.
                        - Review the dry-run plan and cost estimate.
                        - Record live preflight evidence before any provider mutation.
                        - Keep provider tokens out of client-visible notes and logs.
                        """,
                        "Initial debug operations page."
                    ),
                    new(
                        Guid.Parse("df6efda1-c7b3-4402-93a1-eb4d34748252"),
                        now.Plus(Duration.FromDays(-12)),
                        "Signal",
                        """
                        # Cloud server operations runbook

                        This debug page documents the local control-plane workflow for planning a cloud-hosted Arma 3 session.

                        ## Checklist

                        - Confirm a billing owner is assigned.
                        - Review the dry-run plan, spending policy gate, and estimated cost.
                        - Record live preflight evidence before any provider mutation.
                        - Keep provider tokens out of client-visible notes and logs.

                        ## Recovery

                        If a worker disappears, inspect lifecycle jobs, worker heartbeat status, and hosting audit records before retrying.
                        """,
                        "Add worker recovery notes."
                    ),
                ],
                [
                    new(EWikiPageAuditAction.Created, "X39", now.Plus(Duration.FromDays(-18))),
                    new(EWikiPageAuditAction.TitleChanged, "Signal", now.Plus(Duration.FromDays(-12))),
                ]
            ),
            new WikiPageSeed(
                Guid.Parse("0c4ad2f5-2f04-46f6-8619-a4f8c2ab0f44"),
                "Arma 3 mod-pack intake",
                false,
                now.Plus(Duration.FromDays(-15)),
                [
                    new(
                        Guid.Parse("bb256d89-067b-45f1-9c54-63d2f708ce61"),
                        now.Plus(Duration.FromDays(-15)),
                        "Hammer",
                        """
                        # Arma 3 mod-pack intake

                        Use this page to stage mod-pack review notes before creating or updating a public mod-pack revision.

                        ## Review points

                        - Workshop links are visible and reachable.
                        - Composition packs point to the expected child packs.
                        - Required server-side configuration is represented in the runtime profile.
                        """,
                        "Seed content-maintainer wiki page."
                    ),
                ],
                [
                    new(EWikiPageAuditAction.Created, "Hammer", now.Plus(Duration.FromDays(-15))),
                ]
            ),
            new WikiPageSeed(
                Guid.Parse("c08e512b-5dd8-4a58-8f2d-675d5d9fb58c"),
                "Deprecated radio notes",
                true,
                now.Plus(Duration.FromDays(-30)),
                [
                    new(
                        Guid.Parse("0e18cd47-826e-4bd1-8a4e-2da2cfaa91c5"),
                        now.Plus(Duration.FromDays(-30)),
                        "Fox",
                        """
                        # Deprecated radio notes

                        This deleted debug page exists so deleted wiki-page administration paths have realistic local data.
                        """,
                        "Seed deleted wiki page."
                    ),
                ],
                [
                    new(EWikiPageAuditAction.Created, "Fox", now.Plus(Duration.FromDays(-30))),
                    new(EWikiPageAuditAction.Deleted, "Signal", now.Plus(Duration.FromDays(-21))),
                ]
            ),
        };

        foreach (var seed in seeds)
        {
            var page = await dbContext.WikiPages
                .Include(q => q.Revisions)
                .FirstOrDefaultAsync(
                    q => q.PrimaryKey == seed.PrimaryKey || q.Title == seed.Title,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (page is null)
            {
                page = new WikiPage
                {
                    PrimaryKey = seed.PrimaryKey,
                    TimeStampCreated = seed.TimeStampCreated,
                    Revisions = [],
                };
                dbContext.WikiPages.Add(page);
            }

            page.Title = seed.Title;
            page.IsDeleted = seed.IsDeleted;
            if (page.TimeStampCreated == default)
                page.TimeStampCreated = seed.TimeStampCreated;

            foreach (var revisionSeed in seed.Revisions)
            {
                var revision = await dbContext.WikiPageRevisions
                    .FirstOrDefaultAsync(
                        q => q.PrimaryKey == revisionSeed.PrimaryKey
                             || (q.PageForeignKey == page.PrimaryKey && q.Comment == revisionSeed.Comment),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (revision is null)
                {
                    revision = new WikiPageRevision
                    {
                        PrimaryKey = revisionSeed.PrimaryKey,
                        PageForeignKey = page.PrimaryKey,
                    };
                    dbContext.WikiPageRevisions.Add(revision);
                }

                revision.TimeStampCreated = revisionSeed.TimeStampCreated;
                revision.Markdown = revisionSeed.Markdown;
                revision.AuthorForeignKey = ResolveUserId(users, revisionSeed.AuthorUserKey);
                revision.Comment = revisionSeed.Comment;
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            foreach (var auditSeed in seed.Audits)
            {
                await EnsureWikiPageAuditAsync(
                        dbContext,
                        page.PrimaryKey,
                        ResolveUserId(users, auditSeed.UserKey),
                        auditSeed.Action,
                        auditSeed.TimeStampCreated,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureWikiPageAuditAsync(
        DatabaseContext dbContext,
        Guid pageId,
        Guid userId,
        EWikiPageAuditAction action,
        Instant timeStamp,
        CancellationToken cancellationToken)
    {
        if (await dbContext.WikiPageAudits
                .AnyAsync(q => q.PageForeignKey == pageId && q.Action == action, cancellationToken)
                .ConfigureAwait(false))
            return;

        dbContext.WikiPageAudits.Add(new WikiPageAudit
        {
            PageForeignKey = pageId,
            UserForeignKey = userId,
            Action = action,
            Data = JsonSerializer.Serialize(new
            {
                Source = "debug-demo-data",
                action,
            }),
            TimeStampCreated = timeStamp,
        });
    }

    private static async Task EnsurePublicPagesAsync(
        DatabaseContext dbContext,
        IMediaAssetService mediaAssetService,
        IReadOnlyDictionary<string, User> users,
        Instant now,
        CancellationToken cancellationToken)
    {
        var gdprContent = CreateSeedGdprContent();
        var seeds = new[]
        {
            new PublicPageSeed(
                "gdpr",
                false,
                now.Plus(Duration.FromDays(-7)),
                [
                    new(
                        1,
                        "Privacy and GDPR",
                        "How United Tactical Forces handles member, account, and operations data.",
                        "Privacy",
                        "Privacy and GDPR",
                        "Transparent notes on what data we process, why we process it, and how members can request changes.",
                        "gdpr-privacy-hero.png",
                        GdprPageContentMarkdown.ToMarkdown(gdprContent),
                        JsonSerializer.Serialize(gdprContent),
                        "Initial structured command-center editable GDPR page.",
                        true,
                        now.Plus(Duration.FromDays(-7)),
                        "X39"
                    ),
                ]
            ),
        };

        foreach (var seed in seeds)
        {
            var page = await dbContext.PublicPages
                .Include(q => q.Revisions)
                .SingleOrDefaultAsync(q => q.Slug == seed.Slug, cancellationToken)
                .ConfigureAwait(false);
            if (page is null)
            {
                page = new PublicPage
                {
                    Slug = seed.Slug,
                    TimeStampCreated = seed.TimeStampCreated,
                    CreatedByFk = ResolveUserId(users, seed.Revisions[0].AuthorUserKey),
                    Revisions = [],
                };
                dbContext.PublicPages.Add(page);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            page.Slug = seed.Slug;
            page.IsDeleted = seed.IsDeleted;
            page.TimeStampUpdated = now;
            page.UpdatedByFk = ResolveUserId(users, seed.Revisions[0].AuthorUserKey);
            if (page.TimeStampCreated == default)
                page.TimeStampCreated = seed.TimeStampCreated;

            foreach (var revisionSeed in seed.Revisions)
            {
                var heroImageAsset = await EnsurePublicSeedMediaAssetAsync(
                        mediaAssetService,
                        revisionSeed.HeroImagePath,
                        ["debug", "public-page"],
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                var revision = await dbContext.PublicPageRevisions
                    .FirstOrDefaultAsync(
                        q => q.PageFk == page.PrimaryKey && q.Version == revisionSeed.Version,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (revision is null)
                {
                    revision = new PublicPageRevision
                    {
                        PageFk = page.PrimaryKey,
                        Version = revisionSeed.Version,
                        TimeStampCreated = revisionSeed.TimeStampPublished ?? seed.TimeStampCreated,
                    };
                    dbContext.PublicPageRevisions.Add(revision);
                }

                revision.Title = revisionSeed.Title;
                revision.Summary = revisionSeed.Summary;
                revision.HeroKicker = revisionSeed.HeroKicker;
                revision.HeroTitle = revisionSeed.HeroTitle;
                revision.HeroDescription = revisionSeed.HeroDescription;
                revision.HeroImageAssetFk = heroImageAsset.PrimaryKey;
                revision.Markdown = revisionSeed.Markdown;
                revision.ContentJson = revisionSeed.ContentJson;
                revision.Comment = revisionSeed.Comment;
                revision.IsPublished = revisionSeed.IsPublished;
                revision.TimeStampPublished = revisionSeed.TimeStampPublished;
                revision.AuthorFk = ResolveUserId(users, revisionSeed.AuthorUserKey);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var revisions = await dbContext.PublicPageRevisions
                .Where(q => q.PageFk == page.PrimaryKey)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var revision in revisions)
            {
                await EnsurePublicPageAuditAsync(
                        dbContext,
                        page.PrimaryKey,
                        revision.PrimaryKey,
                        page.CreatedByFk,
                        revision.Version == 1 ? EPublicPageAuditAction.Created : EPublicPageAuditAction.RevisionCreated,
                        revision.TimeStampCreated,
                        new
                        {
                            page.Slug,
                            revision.Version,
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (revision.IsPublished)
                {
                    await EnsurePublicPageAuditAsync(
                            dbContext,
                            page.PrimaryKey,
                            revision.PrimaryKey,
                            revision.AuthorFk,
                            EPublicPageAuditAction.Published,
                            revision.TimeStampPublished ?? revision.TimeStampCreated,
                            new
                            {
                                revision.Version,
                                revision.TimeStampPublished,
                            },
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static GdprPageContent CreateSeedGdprContent()
        => new()
        {
            Intro = new GdprIntroSection
            {
                Kicker            = "Living privacy notice",
                Title             = "How UTF handles community data",
                Description       = "This page explains the practical data flows behind accounts, events, servers, content, and association administration.",
                LastReviewedLabel = "Last reviewed",
                LastReviewedDate  = "June 2026",
                ProcessNote       = "When a workflow changes, publish a new revision so members can see the current operating practice.",
            },
            OpeningCards =
            [
                new()
                {
                    Kicker     = "Responsibility",
                    Title      = "Community operators",
                    Paragraphs =
                    [
                        "United Tactical Forces operates the website, command-center workflows, event planning, wiki content, server orchestration, and Verein administration features for the community.",
                        "Administrators keep this notice aligned with real operational practice and handle member privacy requests.",
                    ],
                },
                new()
                {
                    Kicker     = "Legal basis",
                    Title      = "Community operations and obligations",
                    Paragraphs =
                    [
                        "Data is processed to authenticate members, authorize access, coordinate events, run infrastructure, attribute costs, protect the platform, and document association administration where applicable.",
                        "Content submitted by members or editors is processed because it was provided for community use.",
                    ],
                },
            ],
            Lifecycle = new GdprTimelineSection
            {
                Kicker = "What happens when",
                Title  = "Data lifecycle",
                Steps =
                [
                    new()
                    {
                        Title      = "Visit",
                        Paragraphs =
                        [
                            "Public pages can be read without signing in.",
                            "Browser-local preferences are client-side and are not required to identify you.",
                        ],
                    },
                    new()
                    {
                        Title      = "Sign in",
                        Paragraphs =
                        [
                            "Steam or Discord redirects establish the external identity used for login.",
                            "The application stores the identifiers needed to link that external account to the local UTF account.",
                        ],
                    },
                    new()
                    {
                        Title      = "Participate",
                        Paragraphs =
                        [
                            "Events, slots, roles, wiki edits, field reports, and membership workflows create operational records so the community can coordinate across sessions.",
                        ],
                    },
                    new()
                    {
                        Title      = "Operate servers",
                        Paragraphs =
                        [
                            "Server lifecycle actions create jobs, worker records, audit entries, and cost attribution records.",
                            "Credentials and provider tokens stay server-side and are not exposed to browser clients.",
                        ],
                    },
                    new()
                    {
                        Title      = "Request changes",
                        Paragraphs =
                        [
                            "Members can ask an administrator for access, correction, export, or deletion.",
                            "Some audit, billing, security, and association records may need to be retained or anonymized instead of deleted outright.",
                        ],
                    },
                ],
            },
            DataMap = new GdprDataMapSection
            {
                Kicker = "What is stored and why",
                Title  = "Data map",
                Cards =
                [
                    new()
                    {
                        Title        = "Account and identity",
                        DataLabel    = "Data",
                        Data         = "Local user ID, nickname, optional email, avatar, Steam ID64, Discord ID and username, roles, claims, verification, ban state, deleted marker.",
                        PurposeLabel = "Purpose",
                        Purpose      = "Authentication, authorization, profile display, moderation, role-based access, and account support.",
                        WhereLabel   = "Where",
                        Where        = "Stored in the API database. Authentication state is reflected in a browser cookie.",
                    },
                    new()
                    {
                        Title        = "Events and participation",
                        DataLabel    = "Data",
                        Data         = "Event owners and hosts, attendance choices, slot assignment, timestamps, terrain and mod-pack references, event images and descriptions.",
                        PurposeLabel = "Purpose",
                        Purpose      = "Planning operations, showing who is attending, organizing slots, and preserving event history.",
                        WhereLabel   = "Where",
                        Where        = "Stored in the API database and shown through operations pages according to permissions and event visibility.",
                    },
                    new()
                    {
                        Title        = "Content and assets",
                        DataLabel    = "Data",
                        Data         = "Wiki pages, revisions, audits, field reports, terrain entries, mod-pack definitions, and configured media references.",
                        PurposeLabel = "Purpose",
                        Purpose      = "Publishing community information, maintaining review history, attributing edits, and recovering from mistakes.",
                        WhereLabel   = "Where",
                        Where        = "Stored in the API database and configured server or WebApp asset storage.",
                    },
                    new()
                    {
                        Title        = "Game-server operations",
                        DataLabel    = "Data",
                        Data         = "Server records, configuration entries, lifecycle events, jobs, worker registrations, heartbeats, logs, failure summaries, and audit records.",
                        PurposeLabel = "Purpose",
                        Purpose      = "Running servers reliably, recovering from API restarts, tracing requested actions, diagnosing failures, and protecting secrets.",
                        WhereLabel   = "Where",
                        Where        = "Stored in the API database. Workers receive only the data needed to execute assigned lifecycle jobs.",
                    },
                    new()
                    {
                        Title        = "Cloud and billing attribution",
                        DataLabel    = "Data",
                        Data         = "Billing accounts, owners, server assignment intervals, cloud plans, cost estimates, provider metadata, inventory, policy checks, approvals, imports, reconciliation, and adjustments.",
                        PurposeLabel = "Purpose",
                        Purpose      = "Ensuring every cost-generating session has an owner, keeping reviewable cost records, and preventing accidental or unauthorized spend.",
                        WhereLabel   = "Where",
                        Where        = "Stored in the API database. Provider access tokens are not exposed to frontend clients.",
                    },
                    new()
                    {
                        Title        = "Technical logs and diagnostics",
                        DataLabel    = "Data",
                        Data         = "HTTP access data, application logs, game-server logs, worker failure messages, provider verification status, and operational timestamps.",
                        PurposeLabel = "Purpose",
                        Purpose      = "Security monitoring, troubleshooting, abuse prevention, service reliability, and accountability.",
                        WhereLabel   = "Where",
                        Where        = "Stored in server logs, the API database, and the configured hosting environment.",
                    },
                ],
            },
            Visibility = new GdprCardSection
            {
                Kicker = "Who can see what",
                Title  = "Visibility",
                Cards =
                [
                    new() { Title = "Public", Body = "Only intentionally published pages, event summaries, wiki content, and other public community information are visible without member access." },
                    new() { Title = "Members", Body = "Authenticated members can see community workflows according to their roles and claims." },
                    new() { Title = "Administrators", Body = "Authorized administrators can manage records, review audits, manage billing attribution, inspect logs, and resolve incidents." },
                ],
            },
            Partners = new GdprCardSection
            {
                Kicker = "Third parties and processors",
                Title  = "External services",
                Cards =
                [
                    new() { Title = "Steam", Body = "Steam sign-in confirms identity and the API stores the Steam ID64 plus public profile information needed for the local account." },
                    new() { Title = "Discord", Body = "Discord sign-in and bot integrations use Discord account identifiers and operate under Discord's own terms." },
                    new() { Title = "Hosting providers", Body = "Cloud and hosting providers may process infrastructure metadata for planning, operation, reconciliation, and cost attribution." },
                ],
            },
            PolicyCards =
            [
                new()
                {
                    Kicker     = "Retention",
                    Title      = "Kept only while useful or required",
                    Paragraphs =
                    [
                        "Operational records are retained while needed for community administration, security, billing attribution, or legal documentation.",
                        "Retention can differ by workflow because event history, audit records, billing documents, and association records serve different purposes.",
                    ],
                },
                new()
                {
                    Kicker     = "Deletion",
                    Title      = "Deletion and anonymization",
                    Paragraphs =
                    [
                        "Members can request deletion of personal data through an administrator.",
                        "Some records may be anonymized or retained where audit, accounting, security, or legal duties require a durable record.",
                    ],
                },
                new()
                {
                    Kicker     = "Security",
                    Title      = "Secret handling",
                    Paragraphs =
                    [
                        "Steam credentials, provider tokens, worker credentials, and similar secrets are server-side only.",
                        "Secrets must not be exposed to frontend clients, public pages, screenshots, or unredacted logs.",
                    ],
                },
                new()
                {
                    Kicker     = "Member rights",
                    Title      = "Access, correction, export",
                    Paragraphs =
                    [
                        "Members can ask for access to their personal data, correction of inaccurate data, export where practical, or restriction of processing.",
                        "Administrators should document the request and outcome so privacy handling remains auditable.",
                    ],
                },
            ],
        };
    private static async Task EnsurePublicPageAuditAsync(
        DatabaseContext dbContext,
        long pageId,
        long? revisionId,
        Guid? userId,
        EPublicPageAuditAction action,
        Instant timeStamp,
        object data,
        CancellationToken cancellationToken)
    {
        if (await dbContext.PublicPageAudits
                .AnyAsync(
                    q => q.PageFk == pageId && q.RevisionFk == revisionId && q.Action == action,
                    cancellationToken
                )
                .ConfigureAwait(false))
            return;

        dbContext.PublicPageAudits.Add(new PublicPageAudit
        {
            PageFk = pageId,
            RevisionFk = revisionId,
            UserForeignKey = userId,
            Action = action,
            Data = JsonSerializer.Serialize(data),
            TimeStampCreated = timeStamp,
        });
    }

    private static async Task EnsureVereinDataAsync(
        DatabaseContext dbContext,
        IReadOnlyDictionary<string, User> users,
        Instant now,
        CancellationToken cancellationToken)
    {
        await EnsureVereinMembershipsAsync(dbContext, users, now, cancellationToken).ConfigureAwait(false);
        await EnsureVereinDocumentsAsync(dbContext, users, now, cancellationToken).ConfigureAwait(false);
        await EnsureKassenbuchEntriesAsync(dbContext, users, now, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureVereinMembershipsAsync(
        DatabaseContext dbContext,
        IReadOnlyDictionary<string, User> users,
        Instant now,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new VereinMembershipSeed("X39", "UTF-0001", EVereinMembershipStatus.Active, PlusUtcYears(now, -4), null, "Founding debug board member."),
            new VereinMembershipSeed("Raven", "UTF-0042", EVereinMembershipStatus.Active, PlusUtcYears(now, -2), null, "Active member used for member-only Kassenbuch reads."),
            new VereinMembershipSeed("Signal", "UTF-0043", EVereinMembershipStatus.Active, PlusUtcYears(now, -1), null, "Active member with media and document permissions."),
            new VereinMembershipSeed("Slate", "UTF-0044", EVereinMembershipStatus.Suspended, PlusUtcMonths(now, -10), null, "Suspended debug member for status filtering."),
            new VereinMembershipSeed("Viper", "UTF-0045", EVereinMembershipStatus.Ended, PlusUtcYears(now, -1), PlusUtcMonths(now, -2), "Ended debug membership for archive workflows."),
        };

        foreach (var seed in seeds)
        {
            var userId = ResolveUserId(users, seed.UserKey);
            var membership = await dbContext.VereinMemberships
                .SingleOrDefaultAsync(q => q.UserFk == userId, cancellationToken)
                .ConfigureAwait(false);
            var isNew = membership is null;
            if (membership is null)
            {
                membership = new VereinMembership
                {
                    UserFk = userId,
                    TimeStampCreated = seed.TimeStampStarted,
                    CreatedByFk = ResolveUserId(users, "X39"),
                };
                dbContext.VereinMemberships.Add(membership);
            }

            membership.MemberNumber = seed.MemberNumber;
            membership.Status = seed.Status;
            membership.TimeStampStarted = seed.TimeStampStarted;
            membership.TimeStampEnded = seed.TimeStampEnded;
            membership.Notes = seed.Notes;
            membership.TimeStampUpdated = now;
            membership.UpdatedByFk = ResolveUserId(users, "X39");
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await EnsureVereinMembershipAuditAsync(
                    dbContext,
                    membership.PrimaryKey,
                    ResolveUserId(users, "X39"),
                    isNew ? EVereinMembershipAuditAction.Created : EVereinMembershipAuditAction.Updated,
                    now.Plus(Duration.FromMinutes(-45)),
                    new
                    {
                        seed.Status,
                        seed.MemberNumber,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Instant PlusUtcYears(Instant instant, int years)
        => instant.InUtc()
            .LocalDateTime
            .PlusYears(years)
            .InZoneStrictly(DateTimeZone.Utc)
            .ToInstant();

    private static Instant PlusUtcMonths(Instant instant, int months)
        => instant.InUtc()
            .LocalDateTime
            .PlusMonths(months)
            .InZoneStrictly(DateTimeZone.Utc)
            .ToInstant();

    private static async Task EnsureVereinMembershipAuditAsync(
        DatabaseContext dbContext,
        long membershipId,
        Guid userId,
        EVereinMembershipAuditAction action,
        Instant timeStamp,
        object data,
        CancellationToken cancellationToken)
    {
        if (await dbContext.VereinMembershipAudits
                .AnyAsync(q => q.MembershipFk == membershipId && q.Action == action, cancellationToken)
                .ConfigureAwait(false))
            return;

        dbContext.VereinMembershipAudits.Add(new VereinMembershipAudit
        {
            MembershipFk = membershipId,
            UserForeignKey = userId,
            Action = action,
            Data = JsonSerializer.Serialize(data),
            TimeStampCreated = timeStamp,
        });
    }

    private static async Task EnsureVereinDocumentsAsync(
        DatabaseContext dbContext,
        IReadOnlyDictionary<string, User> users,
        Instant now,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new VereinDocumentSeed(
                EVereinDocumentKind.Satzung,
                false,
                PlusUtcMonths(now, -8),
                [
                    new(
                        1,
                        "UTF e.V. debug Satzung",
                        "Public debug Satzung revision for local Verein document screens.",
                        """
                        # Satzung des United Tactical Forces e.V. (Debug)

                        Diese Demo-Satzung ist ausschliesslich lokaler Beispieldatensatz.

                        ## Zweck

                        Der Verein foerdert organisierte taktische Gemeinschaftsarbeit, technische Infrastruktur und transparente Verwaltung.

                        ## Mitgliedschaft

                        Mitgliedsdaten werden nur fuer Vereinszwecke verarbeitet und in dieser Demo ohne echte personenbezogene Daten dargestellt.
                        """,
                        "Initial debug Satzung.",
                        true,
                        PlusUtcMonths(now, -8),
                        "X39"
                    ),
                ]
            ),
            new VereinDocumentSeed(
                EVereinDocumentKind.Minutes,
                false,
                now.Plus(Duration.FromDays(-28)),
                [
                    new(
                        1,
                        "Debug Vorstand minutes - cloud worker budget",
                        "Published minutes covering the debug cloud-worker budget decision.",
                        """
                        # Vorstand minutes - cloud worker budget

                        The board reviewed the local-worker foundation and approved a small monthly debug budget for validating cost attribution screens.

                        ## Decisions

                        - Keep all provider tokens in server-side configuration.
                        - Require a billing owner before live cloud operations.
                        - Review imported provider billing rows before applying adjustments.
                        """,
                        "Publish cloud budget minutes.",
                        true,
                        now.Plus(Duration.FromDays(-28)),
                        "Signal"
                    ),
                    new(
                        2,
                        "Debug Vorstand minutes - cloud worker budget",
                        "Draft follow-up notes for the same minutes document.",
                        """
                        # Vorstand minutes - cloud worker budget

                        Draft follow-up: document operator review steps for automatic spending policy actions before the next milestone.
                        """,
                        "Draft follow-up note.",
                        false,
                        null,
                        "Signal"
                    ),
                ]
            ),
            new VereinDocumentSeed(
                EVereinDocumentKind.Minutes,
                false,
                now.Plus(Duration.FromDays(-9)),
                [
                    new(
                        1,
                        "Debug Mitgliederversammlung minutes - training servers",
                        "Published member meeting minutes for training-server operations.",
                        """
                        # Mitgliederversammlung minutes - training servers

                        Members reviewed the training server workflow and asked for clearer operator notes around mod-pack updates, log collection, and event slotting.
                        """,
                        "Publish member meeting minutes.",
                        true,
                        now.Plus(Duration.FromDays(-9)),
                        "Raven"
                    ),
                ]
            ),
        };

        foreach (var seed in seeds)
        {
            var documents = await dbContext.VereinDocuments
                .Include(q => q.Revisions)
                .Where(q => q.Kind == seed.Kind)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            var document = seed.Kind == EVereinDocumentKind.Satzung
                ? documents.FirstOrDefault(q => !q.IsDeleted)
                : documents.FirstOrDefault(q => q.Revisions?.Any(r => r.Title == seed.Revisions[0].Title) == true);
            if (document is null)
            {
                document = new VereinDocument
                {
                    Kind = seed.Kind,
                    TimeStampCreated = seed.TimeStampCreated,
                    CreatedByFk = ResolveUserId(users, seed.Revisions[0].AuthorUserKey),
                    Revisions = [],
                };
                dbContext.VereinDocuments.Add(document);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            document.Kind = seed.Kind;
            document.IsDeleted = seed.IsDeleted;
            document.TimeStampUpdated = now;
            document.UpdatedByFk = ResolveUserId(users, seed.Revisions[0].AuthorUserKey);
            if (document.TimeStampCreated == default)
                document.TimeStampCreated = seed.TimeStampCreated;

            foreach (var revisionSeed in seed.Revisions)
            {
                var revision = await dbContext.VereinDocumentRevisions
                    .FirstOrDefaultAsync(
                        q => q.DocumentFk == document.PrimaryKey && q.Version == revisionSeed.Version,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (revision is null)
                {
                    revision = new VereinDocumentRevision
                    {
                        DocumentFk = document.PrimaryKey,
                        Version = revisionSeed.Version,
                        TimeStampCreated = revisionSeed.TimeStampPublished ?? seed.TimeStampCreated,
                    };
                    dbContext.VereinDocumentRevisions.Add(revision);
                }

                revision.Title = revisionSeed.Title;
                revision.Summary = revisionSeed.Summary;
                revision.Markdown = revisionSeed.Markdown;
                revision.Comment = revisionSeed.Comment;
                revision.IsPublished = revisionSeed.IsPublished;
                revision.TimeStampPublished = revisionSeed.TimeStampPublished;
                revision.AuthorFk = ResolveUserId(users, revisionSeed.AuthorUserKey);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var revisions = await dbContext.VereinDocumentRevisions
                .Where(q => q.DocumentFk == document.PrimaryKey)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var revision in revisions)
            {
                await EnsureVereinDocumentAuditAsync(
                        dbContext,
                        document.PrimaryKey,
                        revision.PrimaryKey,
                        document.CreatedByFk,
                        revision.Version == 1 ? EVereinDocumentAuditAction.Created : EVereinDocumentAuditAction.RevisionCreated,
                        revision.TimeStampCreated,
                        new
                        {
                            document.Kind,
                            revision.Version,
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (revision.IsPublished)
                {
                    await EnsureVereinDocumentAuditAsync(
                            dbContext,
                            document.PrimaryKey,
                            revision.PrimaryKey,
                            revision.AuthorFk,
                            EVereinDocumentAuditAction.Published,
                            revision.TimeStampPublished ?? revision.TimeStampCreated,
                            new
                            {
                                revision.Version,
                                revision.TimeStampPublished,
                            },
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureVereinDocumentAuditAsync(
        DatabaseContext dbContext,
        long documentId,
        long? revisionId,
        Guid? userId,
        EVereinDocumentAuditAction action,
        Instant timeStamp,
        object data,
        CancellationToken cancellationToken)
    {
        if (await dbContext.VereinDocumentAudits
                .AnyAsync(
                    q => q.DocumentFk == documentId && q.RevisionFk == revisionId && q.Action == action,
                    cancellationToken
                )
                .ConfigureAwait(false))
            return;

        dbContext.VereinDocumentAudits.Add(new VereinDocumentAudit
        {
            DocumentFk = documentId,
            RevisionFk = revisionId,
            UserForeignKey = userId,
            Action = action,
            Data = JsonSerializer.Serialize(data),
            TimeStampCreated = timeStamp,
        });
    }

    private static async Task EnsureKassenbuchEntriesAsync(
        DatabaseContext dbContext,
        IReadOnlyDictionary<string, User> users,
        Instant now,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new KassenbuchEntrySeed(
                now.Plus(Duration.FromDays(-24)),
                EKassenbuchEntryDirection.Income,
                180.00m,
                "EUR",
                "Membership Fees",
                "Debug monthly membership contributions for active Verein members.",
                "DEBUG-MEMBER-2026-06",
                "KB-DEBUG-0001",
                false
            ),
            new KassenbuchEntrySeed(
                now.Plus(Duration.FromDays(-18)),
                EKassenbuchEntryDirection.Expense,
                42.50m,
                "EUR",
                "Server Hosting",
                "Debug staging server and backup storage estimate.",
                "DEBUG-HOSTING-2026-06",
                "KB-DEBUG-0002",
                false
            ),
            new KassenbuchEntrySeed(
                now.Plus(Duration.FromDays(-12)),
                EKassenbuchEntryDirection.Expense,
                16.20m,
                "EUR",
                "Operations",
                "Printed checklists and meeting supplies for a debug training night.",
                "DEBUG-OPS-2026-06",
                "KB-DEBUG-0003",
                false
            ),
            new KassenbuchEntrySeed(
                now.Plus(Duration.FromDays(-6)),
                EKassenbuchEntryDirection.Income,
                25.00m,
                "EUR",
                "Donation",
                "Deleted duplicate donation row retained for audit demo coverage.",
                "DEBUG-DONATION-DUPLICATE",
                "KB-DEBUG-0004",
                true
            ),
        };

        foreach (var seed in seeds)
        {
            var entry = await dbContext.KassenbuchEntries
                .FirstOrDefaultAsync(q => q.DocumentNumber == seed.DocumentNumber, cancellationToken)
                .ConfigureAwait(false);
            if (entry is null)
            {
                entry = new KassenbuchEntry
                {
                    TimeStampCreated = seed.TimeStampBooked,
                    CreatedByFk = ResolveUserId(users, "X39"),
                };
                dbContext.KassenbuchEntries.Add(entry);
            }

            entry.TimeStampBooked = seed.TimeStampBooked;
            entry.Direction = seed.Direction;
            entry.Amount = seed.Amount;
            entry.Currency = seed.Currency;
            entry.Category = seed.Category;
            entry.Description = seed.Description;
            entry.Reference = seed.Reference;
            entry.DocumentNumber = seed.DocumentNumber;
            entry.IsDeleted = seed.IsDeleted;
            entry.TimeStampUpdated = now;
            entry.UpdatedByFk = ResolveUserId(users, "X39");
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await EnsureKassenbuchAuditAsync(
                    dbContext,
                    entry.PrimaryKey,
                    ResolveUserId(users, "X39"),
                    EKassenbuchAuditAction.Created,
                    seed.TimeStampBooked,
                    new
                    {
                        seed.Direction,
                        seed.Amount,
                        seed.Currency,
                        seed.Category,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (seed.IsDeleted)
            {
                await EnsureKassenbuchAuditAsync(
                        dbContext,
                        entry.PrimaryKey,
                        ResolveUserId(users, "X39"),
                        EKassenbuchAuditAction.Deleted,
                        now.Plus(Duration.FromDays(-5)),
                        new
                        {
                            seed.Amount,
                            seed.Currency,
                            seed.Category,
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureKassenbuchAuditAsync(
        DatabaseContext dbContext,
        long entryId,
        Guid userId,
        EKassenbuchAuditAction action,
        Instant timeStamp,
        object data,
        CancellationToken cancellationToken)
    {
        if (await dbContext.KassenbuchEntryAudits
                .AnyAsync(q => q.KassenbuchEntryFk == entryId && q.Action == action, cancellationToken)
                .ConfigureAwait(false))
            return;

        dbContext.KassenbuchEntryAudits.Add(new KassenbuchEntryAudit
        {
            KassenbuchEntryFk = entryId,
            UserForeignKey = userId,
            Action = action,
            Data = JsonSerializer.Serialize(data),
            TimeStampCreated = timeStamp,
        });
    }

    private static async Task EnsureFieldReportsAsync(
        DatabaseContext dbContext,
        IMediaAssetService mediaAssetService,
        User debugUser,
        Instant now,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new FieldReportSeed(
                Guid.Parse("e46f024d-1df9-43ce-9274-a92d3a5fb241"),
                "convoy-crossing-at-first-light",
                "Convoy crossing at first light",
                "Recap of an early-morning convoy movement through contested terrain, enemy contact, and lessons identified.",
                "After Action",
                """
                The convoy crossed the open ground just after first light with dust hanging low over the route.

                Recon pushed ahead, logistics kept the column tight, and the lead element held pace until the first contact forced a short halt.

                The useful lesson was simple: clear route timing and disciplined radio calls kept the initiative ours.
                """,
                "media/field-reports/convoy-crossing-at-first-light.jpg",
                "UTF convoy moving through dusty terrain at first light",
                true,
                0,
                now.Plus(Duration.FromDays(-10))
            ),
            new FieldReportSeed(
                Guid.Parse("1d6ea560-e900-4f63-8a40-ffbdb7bb61a6"),
                "briefing-room-signals",
                "Briefing room signals",
                "Planning notes from the comms and slotting discipline that shaped a clean training insertion.",
                "Training",
                """
                The training night started before anyone stepped into the AO.

                Fireteam leads settled routes, radio nets, contingency marks, and resupply language around one shared map.

                The session stayed controlled because the brief made each handoff explicit.
                """,
                "media/field-reports/briefing-room-signals.jpg",
                "UTF members coordinating before a training operation",
                false,
                10,
                now.Plus(Duration.FromDays(-7))
            ),
            new FieldReportSeed(
                Guid.Parse("f3240d1b-8529-450b-bd35-5c9c3d6f0986"),
                "route-security-after-contact",
                "Route security after contact",
                "A short recon report on reacting to contact and locking down the route before movement resumed.",
                "Recon",
                """
                Contact came from the treeline while the route team was still confirming the next bound.

                The squad split security, marked likely movement lanes, and kept the convoy staged until the flank was quiet.

                It was a small action, but it showed why route discipline matters more than speed.
                """,
                "media/field-reports/route-security-after-contact.jpg",
                "UTF patrol establishing security after contact",
                false,
                20,
                now.Plus(Duration.FromDays(-4))
            ),
            new FieldReportSeed(
                Guid.Parse("6d75f1a2-b8d5-4423-87c6-86245c6da447"),
                "night-cache-resupply",
                "Night cache resupply",
                "Logistics notes from a wet-weather cache move where inventory discipline mattered more than speed.",
                "Logistics",
                """
                Rain turned the cache track into a slow crawl, so the logistics element kept the job small and deliberate.

                Drivers staged under red task light while the quartermaster checked each crate against the mission load plan.

                The useful pattern was keeping one voice on inventory and one voice on movement, which prevented a quiet resupply from turning into a traffic jam.
                """,
                "media/field-reports/night-cache-resupply.jpg",
                "UTF logistics team loading a wet night supply cache",
                false,
                30,
                now.Plus(Duration.FromDays(-2))
            ),
        };

        foreach (var seed in seeds)
        {
            var heroImageAsset = await EnsurePublicSeedMediaAssetAsync(
                    mediaAssetService,
                    seed.HeroImagePath,
                    ["debug", "field-report"],
                    cancellationToken
                )
                .ConfigureAwait(false);
            var report = await dbContext.FieldReports
                .FirstOrDefaultAsync(
                    q => q.PrimaryKey == seed.PrimaryKey || q.Slug == seed.Slug,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (report is null)
            {
                report = new FieldReport
                {
                    PrimaryKey       = seed.PrimaryKey,
                    TimeStampCreated = seed.TimeStampPublished.Plus(Duration.FromDays(-1)),
                    CreatedByFk      = debugUser.PrimaryKey,
                };
                dbContext.FieldReports.Add(report);
            }

            report.Slug = seed.Slug;
            report.Title = seed.Title;
            report.Excerpt = seed.Excerpt;
            report.Category = seed.Category;
            report.Markdown = seed.Markdown;
            report.HeroImageAssetFk = heroImageAsset.PrimaryKey;
            report.HeroImageAlt = seed.HeroImageAlt;
            report.IsFeatured = seed.IsFeatured;
            report.SortOrder = seed.SortOrder;
            report.IsPublished = true;
            report.TimeStampPublished = seed.TimeStampPublished;
            report.IsDeleted = false;
            if (report.TimeStampCreated == default)
                report.TimeStampCreated = seed.TimeStampPublished.Plus(Duration.FromDays(-1));
            report.TimeStampUpdated = seed.TimeStampPublished;
            report.CreatedByFk = report.CreatedByFk == default ? debugUser.PrimaryKey : report.CreatedByFk;
            report.UpdatedByFk = debugUser.PrimaryKey;

            await EnsureFieldReportAuditAsync(
                    dbContext,
                    report.PrimaryKey,
                    debugUser.PrimaryKey,
                    EFieldReportAuditAction.Created,
                    seed.TimeStampPublished.Plus(Duration.FromDays(-1)),
                    new
                    {
                        seed.Slug,
                        seed.Title,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
            await EnsureFieldReportAuditAsync(
                    dbContext,
                    report.PrimaryKey,
                    debugUser.PrimaryKey,
                    EFieldReportAuditAction.Published,
                    seed.TimeStampPublished,
                    new
                    {
                        seed.TimeStampPublished,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureFieldReportAuditAsync(
        DatabaseContext dbContext,
        Guid reportId,
        Guid userId,
        EFieldReportAuditAction action,
        Instant timeStamp,
        object data,
        CancellationToken cancellationToken)
    {
        if (await dbContext.FieldReportAudits
                .AnyAsync(
                    q => q.FieldReportFk == reportId && q.Action == action,
                    cancellationToken
                )
                .ConfigureAwait(false))
            return;

        dbContext.FieldReportAudits.Add(new FieldReportAudit
        {
            FieldReportFk    = reportId,
            UserForeignKey   = userId,
            Action           = action,
            Data             = JsonSerializer.Serialize(data),
            TimeStampCreated = timeStamp,
        });
    }

    private static string CreateDemoModPackHtml()
        => Arma3ModPackParser.ToHtml(
            DebugModPackTitle,
            new Arma3ModPackParser.TitleLinkPair[]
            {
                new("CBA_A3", "https://steamcommunity.com/sharedfiles/filedetails/?id=450814997"),
                new("ACE3", "https://steamcommunity.com/sharedfiles/filedetails/?id=463939057"),
            },
            []
        );

    private static string CreateTrainingModPackHtml()
        => Arma3ModPackParser.ToHtml(
            DebugTrainingModPackTitle,
            new Arma3ModPackParser.TitleLinkPair[]
            {
                new("CBA_A3", "https://steamcommunity.com/sharedfiles/filedetails/?id=450814997"),
                new("ACE3", "https://steamcommunity.com/sharedfiles/filedetails/?id=463939057"),
                new("Task Force Arrowhead Radio", "https://steamcommunity.com/sharedfiles/filedetails/?id=894678801"),
            },
            []
        );

    private static string CreateReconModPackHtml()
        => Arma3ModPackParser.ToHtml(
            DebugReconModPackTitle,
            new Arma3ModPackParser.TitleLinkPair[]
            {
                new("CBA_A3", "https://steamcommunity.com/sharedfiles/filedetails/?id=450814997"),
                new("ACE3", "https://steamcommunity.com/sharedfiles/filedetails/?id=463939057"),
                new("3CB Factions", "https://steamcommunity.com/sharedfiles/filedetails/?id=1673456286"),
                new("LAMBS Danger.fsm", "https://steamcommunity.com/sharedfiles/filedetails/?id=1858075458"),
            },
            []
        );

    private static byte[] GetTerrainImage(string title)
        => title switch
        {
            DebugTerrainTanoaTitle   => DebugTanoaTerrainJpeg,
            DebugTerrainLivoniaTitle => DebugLivoniaTerrainJpeg,
            DebugTerrainMaldenTitle  => DebugMaldenTerrainJpeg,
            _                        => DebugAltisTerrainJpeg,
        };

    private static async Task<MediaAssetDto> EnsureSeedMediaAssetAsync(
        IMediaAssetService mediaAssetService,
        byte[] imageBytes,
        string fileName,
        IEnumerable<string> tags,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(imageBytes, writable: false);
        return await mediaAssetService.ImportAsync(
                stream,
                fileName,
                SeedImageMimeType,
                tags,
                null,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task<MediaAssetDto> EnsurePublicSeedMediaAssetAsync(
        IMediaAssetService mediaAssetService,
        string sourcePath,
        IEnumerable<string> tags,
        CancellationToken cancellationToken)
    {
        if (!PublicSeedAssetsByPath.TryGetValue(sourcePath, out var asset))
            throw new InvalidOperationException($"Public seed asset '{sourcePath}' is not registered.");

        await using var stream = new MemoryStream(ReadSeedAsset(asset.ResourceName), writable: false);
        return await mediaAssetService.ImportAsync(
                stream,
                asset.FileName,
                asset.ContentType,
                tags,
                null,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static string CreateFileSlug(string value)
    {
        var builder = new StringBuilder();
        var previousWasSeparator = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private static byte[] ReadSeedAsset(string resourceName)
    {
        var assembly = typeof(DebugDemoDataSeeder).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(
                               $"Debug seed asset '{resourceName}' was not embedded.");
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private static readonly ClaimSeed[] ClaimSeeds =
    [
        new("Administration", "Global administrator", X39.UnitedTacticalForces.Claims.Administrative.All, "Allows every administrative action."),
        new("Administration", "Event administrator", X39.UnitedTacticalForces.Claims.Administrative.Event, "Allows event administration."),
        new("Administration", "Terrain administrator", X39.UnitedTacticalForces.Claims.Administrative.Terrain, "Allows terrain administration."),
        new("Administration", "Mod pack administrator", X39.UnitedTacticalForces.Claims.Administrative.ModPack, "Allows mod-pack administration."),
        new("Administration", "User administrator", X39.UnitedTacticalForces.Claims.Administrative.User, "Allows user administration."),
        new("Administration", "Role administrator", X39.UnitedTacticalForces.Claims.Administrative.Role, "Allows role and claim administration."),
        new("Administration", "Server administrator", X39.UnitedTacticalForces.Claims.Administrative.Server, "Allows game-server and cloud administration."),
        new("Administration", "Wiki administrator", X39.UnitedTacticalForces.Claims.Administrative.Wiki, "Allows wiki administration."),
        new("Administration", "Verein administrator", X39.UnitedTacticalForces.Claims.Administrative.Verein, "Allows Verein administration."),
        new("Creation", "Create events", X39.UnitedTacticalForces.Claims.Creation.Events, "Allows creating events."),
        new("Creation", "Create terrain", X39.UnitedTacticalForces.Claims.Creation.Terrain, "Allows creating terrain."),
        new("Creation", "Create mod packs", X39.UnitedTacticalForces.Claims.Creation.ModPack, "Allows creating mod packs."),
        new("Creation", "Create wiki pages", X39.UnitedTacticalForces.Claims.Creation.Wiki, "Allows creating wiki pages."),
        new("Creation", "Create servers", X39.UnitedTacticalForces.Claims.Creation.Server, "Allows creating game servers."),
        new("Creation", "Create roles", X39.UnitedTacticalForces.Claims.Creation.Role, "Allows creating roles."),
        new("Users", "List users", X39.UnitedTacticalForces.Claims.User.List, "Allows listing users."),
        new("Users", "View Steam IDs", X39.UnitedTacticalForces.Claims.User.ViewSteamId64, "Allows viewing Steam identifiers."),
        new("Users", "View Discord IDs", X39.UnitedTacticalForces.Claims.User.ViewDiscordId, "Allows viewing Discord identifiers."),
        new("Users", "Modify nicknames", X39.UnitedTacticalForces.Claims.User.Nickname, "Allows changing nicknames."),
        new("Users", "Modify email", X39.UnitedTacticalForces.Claims.User.EMail, "Allows changing email addresses."),
        new("Users", "Modify users", X39.UnitedTacticalForces.Claims.User.Modify, "Allows user profile changes."),
        new("Users", "Verify users", X39.UnitedTacticalForces.Claims.User.Verify, "Allows verifying users."),
        new("Users", "Ban users", X39.UnitedTacticalForces.Claims.User.Ban, "Allows banning users."),
        new("Users", "Assign roles", X39.UnitedTacticalForces.Claims.User.Roles, "Allows assigning user roles."),
        new("Wiki", "Manage wiki", X39.UnitedTacticalForces.Claims.Wiki.All, "Allows all wiki actions."),
        new("Wiki", "Modify wiki", X39.UnitedTacticalForces.Claims.Wiki.Modify, "Allows editing wiki pages."),
        new("Wiki", "Delete wiki", X39.UnitedTacticalForces.Claims.Wiki.Delete, "Allows deleting wiki pages."),
        new("Wiki", "Rename wiki", X39.UnitedTacticalForces.Claims.Wiki.Rename, "Allows renaming wiki pages."),
        new("Content", "Manage public pages", X39.UnitedTacticalForces.Claims.Content.Pages, "Allows managing public website pages."),
        new("Verein", "Manage Verein", X39.UnitedTacticalForces.Claims.Verein.All, "Allows all Verein administration actions."),
        new("Verein", "Manage Verein members", X39.UnitedTacticalForces.Claims.Verein.Members, "Allows managing formal memberships."),
        new("Verein", "Manage Verein documents", X39.UnitedTacticalForces.Claims.Verein.Documents, "Allows managing Verein documents."),
        new("Verein", "Manage Kassenbuch", X39.UnitedTacticalForces.Claims.Verein.Kassenbuch, "Allows managing Kassenbuch entries."),
        new("Resource", "Server access", X39.UnitedTacticalForces.Claims.ResourceBased.Server.All, "Resource claim for all actions on a server.", true),
        new("Resource", "View server", X39.UnitedTacticalForces.Claims.ResourceBased.Server.View, "Resource claim for viewing a server.", true),
        new("Resource", "Operate server", X39.UnitedTacticalForces.Claims.ResourceBased.Server.Operate, "Resource claim for server lifecycle operations.", true),
        new("Resource", "Modify server", X39.UnitedTacticalForces.Claims.ResourceBased.Server.Modify, "Resource claim for changing a server.", true),
        new("Resource", "Manage server access", X39.UnitedTacticalForces.Claims.ResourceBased.Server.ManageAccess, "Resource claim for managing server access.", true),
        new("Resource", "Delete server data", X39.UnitedTacticalForces.Claims.ResourceBased.Server.DeleteData, "Resource claim for deleting persisted server data.", true),
        new("Resource", "Mod-pack access", X39.UnitedTacticalForces.Claims.ResourceBased.ModPack.All, "Resource claim for all actions on a mod pack.", true),
        new("Resource", "Mod-pack modify", X39.UnitedTacticalForces.Claims.ResourceBased.ModPack.Modify, "Resource claim for mod-pack modification.", true),
        new("Resource", "Mod-pack delete", X39.UnitedTacticalForces.Claims.ResourceBased.ModPack.Delete, "Resource claim for mod-pack deletion.", true),
        new("Resource", "Terrain access", X39.UnitedTacticalForces.Claims.ResourceBased.Terrain.All, "Resource claim for all actions on terrain.", true),
        new("Resource", "Terrain modify", X39.UnitedTacticalForces.Claims.ResourceBased.Terrain.Modify, "Resource claim for terrain modification.", true),
        new("Resource", "Terrain delete", X39.UnitedTacticalForces.Claims.ResourceBased.Terrain.Delete, "Resource claim for terrain deletion.", true),
    ];

    private static readonly UserSeed[] DebugUserSeeds =
    [
        new(
            "Raven",
            Guid.Parse("54b124b0-6861-4b93-9c6b-9cf52237fb80"),
            "Raven",
            "raven.debug@utforces.local",
            76561199000000001,
            140000000000000001,
            "raven.debug",
            DebugRavenAvatarJpeg,
            true
        ),
        new(
            "Signal",
            Guid.Parse("4dc9a44b-4c3b-4ebb-a018-b7aa96fb0363"),
            "Signal",
            "signal.debug@utforces.local",
            76561199000000002,
            140000000000000002,
            "signal.debug",
            DebugSignalAvatarJpeg,
            true
        ),
        new(
            "Hammer",
            Guid.Parse("8063d513-b487-4bd7-a19c-d6fe1054e98e"),
            "Hammer",
            "hammer.debug@utforces.local",
            76561199000000003,
            140000000000000003,
            "hammer.debug",
            DebugHammerAvatarJpeg,
            true
        ),
        new(
            "Slate",
            Guid.Parse("2cabf39a-5433-4315-b482-368cb6732298"),
            "Slate",
            "slate.debug@utforces.local",
            76561199000000004,
            140000000000000004,
            "slate.debug",
            DebugSlateAvatarJpeg,
            true
        ),
        new(
            "Fox",
            Guid.Parse("8dfda7b2-63e0-40f8-a53f-3b26f70bb625"),
            "Fox",
            "fox.debug@utforces.local",
            76561199000000005,
            140000000000000005,
            "fox.debug",
            DebugFoxAvatarJpeg,
            true
        ),
        new(
            "Viper",
            Guid.Parse("83b75cb3-f36f-4c2a-8ce6-1e21926b3dc7"),
            "Viper",
            "viper.debug@utforces.local",
            76561199000000006,
            140000000000000006,
            "viper.debug",
            DebugViperAvatarJpeg,
            false
        ),
    ];

    private static readonly UserProfileSeed[] UserProfileSeeds =
    [
        new("X39", "Alex", "Keller", "+49 30 0000001", "Debugstrasse 1", "c/o UTF Vorstand", "10115", "Berlin", "Germany"),
        new("Raven", "Mara", "Stein", "+49 30 0000002", "Operationsweg 12", null, "10117", "Berlin", "Germany"),
        new("Signal", "Jonas", "Falk", "+49 30 0000003", "Funkallee 7", "Apartment 4", "10243", "Berlin", "Germany"),
        new("Hammer", "Nico", "Brandt", "+49 30 0000004", "Werkhof 5", null, "10365", "Berlin", "Germany"),
        new("Slate", "Lea", "Voss", "+49 30 0000005", "Archivplatz 9", null, "10405", "Berlin", "Germany"),
        new("Fox", "Timo", "Reed", "+49 30 0000006", "Medienring 22", null, "10557", "Berlin", "Germany"),
        new("Viper", "Kai", "Winter", "+49 30 0000007", "Reservestrasse 3", null, "10623", "Berlin", "Germany"),
    ];

    private static readonly RoleSeed[] DebugRoleSeeds =
    [
        new(
            "Debug Event Staff",
            "Debug-only event organizers with enough access to create, schedule, and inspect local operations.",
            new[]
            {
                X39.UnitedTacticalForces.Claims.Administrative.Event,
                X39.UnitedTacticalForces.Claims.Creation.Events,
                X39.UnitedTacticalForces.Claims.User.List,
            },
            new[]
            {
                "Raven",
                "Signal",
            }
        ),
        new(
            "Debug Server Operators",
            "Debug-only operators for local game-server lifecycle and cloud control-plane screens.",
            new[]
            {
                X39.UnitedTacticalForces.Claims.Administrative.Server,
                X39.UnitedTacticalForces.Claims.Creation.Server,
                X39.UnitedTacticalForces.Claims.User.List,
                X39.UnitedTacticalForces.Claims.User.ViewSteamId64,
            },
            new[]
            {
                "X39",
                "Hammer",
            }
        ),
        new(
            "Debug Media Editors",
            "Debug-only editorial users for wiki-backed field report creation and publication.",
            new[]
            {
                X39.UnitedTacticalForces.Claims.Administrative.Wiki,
                X39.UnitedTacticalForces.Claims.Creation.Wiki,
                X39.UnitedTacticalForces.Claims.Content.Pages,
                X39.UnitedTacticalForces.Claims.Wiki.Modify,
                X39.UnitedTacticalForces.Claims.Wiki.Delete,
            },
            new[]
            {
                "Signal",
                "Fox",
            }
        ),
        new(
            "Debug Arsenal Maintainers",
            "Debug-only content maintainers for mod-pack and terrain administration workflows.",
            new[]
            {
                X39.UnitedTacticalForces.Claims.Administrative.ModPack,
                X39.UnitedTacticalForces.Claims.Administrative.Terrain,
                X39.UnitedTacticalForces.Claims.Creation.ModPack,
                X39.UnitedTacticalForces.Claims.Creation.Terrain,
            },
            new[]
            {
                "Hammer",
                "Slate",
            }
        ),
        new(
            "Debug Recruit Intake",
            "Debug-only low-access role used to verify role assignment and limited-user UI states.",
            Array.Empty<string>(),
            new[]
            {
                "Viper",
            }
        ),
        new(
            "Debug Verein Board",
            "Debug-only Verein administrators for document, member, and Kassenbuch workflows.",
            new[]
            {
                X39.UnitedTacticalForces.Claims.Administrative.Verein,
                X39.UnitedTacticalForces.Claims.Verein.All,
                X39.UnitedTacticalForces.Claims.Verein.Members,
                X39.UnitedTacticalForces.Claims.Verein.Documents,
                X39.UnitedTacticalForces.Claims.Verein.Kassenbuch,
            },
            new[]
            {
                "X39",
                "Signal",
            }
        ),
    ];

    private sealed record ClaimSeed(
        string Category,
        string Title,
        string Identifier,
        string Description,
        bool IsPrefix = false,
        string? Value = null,
        string? ValueType = null);

    private sealed record UserSeed(
        string Key,
        Guid PrimaryKey,
        string Nickname,
        string? Email,
        ulong SteamId64,
        ulong DiscordId,
        string DiscordUsername,
        byte[] Avatar,
        bool IsVerified,
        bool IsBanned = false);

    private sealed record UserProfileSeed(
        string UserKey,
        string FirstName,
        string LastName,
        string PhoneNumber,
        string AddressLine1,
        string? AddressLine2,
        string PostalCode,
        string City,
        string Country);

    private sealed record RoleSeed(
        string Title,
        string Description,
        string[] ClaimIdentifiers,
        string[] UserKeys);



    private sealed record PublicSeedAsset(string ResourceName, string FileName, string ContentType);

    private sealed record EventSeed(
        Guid PrimaryKey,
        string Title,
        string Description,
        byte[] Image,
        Terrain Terrain,
        ModPackRevision ModPackRevision,
        Instant ScheduledFor,
        string OwnerUserKey,
        string HostedByUserKey,
        bool IsVisible,
        int MinimumAccepted,
        EventAttendanceSeed[] Attendances,
        EventSlotSeed[] Slots);

    private sealed record EventAttendanceSeed(string UserKey, EEventAcceptance Acceptance);

    private sealed record EventSlotSeed(
        short SlotNumber,
        string Group,
        string Title,
        EArmaSide Side,
        string? AssignedUserKey = null,
        bool IsSelfAssignable = true,
        bool IsVisible = true);


    private sealed record WikiPageSeed(
        Guid PrimaryKey,
        string Title,
        bool IsDeleted,
        Instant TimeStampCreated,
        WikiPageRevisionSeed[] Revisions,
        WikiPageAuditSeed[] Audits);

    private sealed record WikiPageRevisionSeed(
        Guid PrimaryKey,
        Instant TimeStampCreated,
        string AuthorUserKey,
        string Markdown,
        string Comment);

    private sealed record WikiPageAuditSeed(
        EWikiPageAuditAction Action,
        string UserKey,
        Instant TimeStampCreated);

    private sealed record PublicPageSeed(
        string Slug,
        bool IsDeleted,
        Instant TimeStampCreated,
        PublicPageRevisionSeed[] Revisions);

    private sealed record PublicPageRevisionSeed(
        int Version,
        string Title,
        string Summary,
        string HeroKicker,
        string HeroTitle,
        string HeroDescription,
        string HeroImagePath,
        string Markdown,
        string ContentJson,
        string Comment,
        bool IsPublished,
        Instant? TimeStampPublished,
        string AuthorUserKey);

    private sealed record VereinMembershipSeed(
        string UserKey,
        string MemberNumber,
        EVereinMembershipStatus Status,
        Instant TimeStampStarted,
        Instant? TimeStampEnded,
        string Notes);

    private sealed record VereinDocumentSeed(
        EVereinDocumentKind Kind,
        bool IsDeleted,
        Instant TimeStampCreated,
        VereinDocumentRevisionSeed[] Revisions);

    private sealed record VereinDocumentRevisionSeed(
        int Version,
        string Title,
        string Summary,
        string Markdown,
        string Comment,
        bool IsPublished,
        Instant? TimeStampPublished,
        string AuthorUserKey);

    private sealed record KassenbuchEntrySeed(
        Instant TimeStampBooked,
        EKassenbuchEntryDirection Direction,
        decimal Amount,
        string Currency,
        string Category,
        string Description,
        string Reference,
        string DocumentNumber,
        bool IsDeleted);

    private sealed record FieldReportSeed(
        Guid PrimaryKey,
        string Slug,
        string Title,
        string Excerpt,
        string Category,
        string Markdown,
        string HeroImagePath,
        string HeroImageAlt,
        bool IsFeatured,
        int SortOrder,
        Instant TimeStampPublished);
}
