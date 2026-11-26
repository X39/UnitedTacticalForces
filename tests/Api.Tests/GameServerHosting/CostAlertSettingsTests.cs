using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Api.Services.Notifications;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// CST-04 and CST-06 proof that a cost owner's alert settings are read and saved through the real
/// <see cref="CostAlertSettingsController" />: server-side validation without partial writes, optimistic
/// concurrency, authorization per claim class, channel availability and recording of the saving user's language.
/// The tests run against in-memory SQLite with the real authorization service and recipient resolver.
/// </summary>
public sealed class CostAlertSettingsTests
{
    /// <summary>
    /// An owner that never saved reads as revision zero with no amounts, both channels on and the configured limits.
    /// </summary>
    /// <returns>A task completing after the empty state was verified.</returns>
    [Fact]
    public async Task OwnerWithoutRow_ReadsEmptyDefaults_AndCreatesNoRow()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var dto = Assert.IsType<CostAlertSettingsDto>(Assert.IsType<OkObjectResult>(
            await CreateController(store, OwnerPrincipal(seed))
                .GetAlertSettingsAsync(seed.CostOwner.PrimaryKey, CancellationToken.None)).Value);

        Assert.Equal(seed.CostOwner.PrimaryKey, dto.CostOwnerId);
        Assert.Equal("Hosting operator", dto.CostOwnerName);
        Assert.Equal(0, dto.Revision);
        Assert.Empty(dto.Thresholds);
        Assert.True(dto.DiscordEnabled);
        Assert.True(dto.EmailEnabled);
        Assert.Equal("EUR", dto.Currency);
        Assert.Equal(10, dto.MaxThresholds);
        Assert.Equal(10000m, dto.MaxThresholdAmount);
        Assert.Equal(ECostAlertAudience.Self, dto.Audience);
        Assert.Equal("en", dto.MessageLanguage);
        Assert.Equal(0, await store.DbContext.CostAlertSettings.CountAsync());
    }

    /// <summary>
    /// A Verein owner reads the group audience and no message language.
    /// </summary>
    /// <returns>A task completing after the group state was verified.</returns>
    [Fact]
    public async Task VereinOwner_ReadsGroupAudienceWithoutLanguage()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var verein = AddGroupOwner(store, ECostOwnerKind.Verein, "Verein");
        await store.DbContext.SaveChangesAsync();

        var dto = Assert.IsType<CostAlertSettingsDto>(Assert.IsType<OkObjectResult>(
            await CreateController(store, AdminPrincipal(seed.User))
                .GetAlertSettingsAsync(verein.PrimaryKey, CancellationToken.None)).Value);

        Assert.Equal(ECostAlertAudience.Group, dto.Audience);
        Assert.Null(dto.MessageLanguage);
        Assert.Equal("Verein", dto.CostOwnerName);
    }

    /// <summary>
    /// A save sorts the amounts, increments the revision, records the caller's language and reads back unchanged.
    /// </summary>
    /// <returns>A task completing after the round trip was verified.</returns>
    [Fact]
    public async Task Save_SortsAmounts_IncrementsRevision_RecordsLanguage()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var controller = CreateController(store, OwnerPrincipal(seed));

        var saved = Assert.IsType<CostAlertSettingsDto>(Assert.IsType<OkObjectResult>(
            await controller.UpdateAlertSettingsAsync(
                seed.CostOwner.PrimaryKey,
                new CostAlertSettingsUpdate
                {
                    Revision = 0,
                    Thresholds = [25m, 5m, 10.5m],
                    DiscordEnabled = true,
                    EmailEnabled = false,
                    Language = "de-DE",
                },
                CancellationToken.None)).Value);
        var read = Assert.IsType<CostAlertSettingsDto>(Assert.IsType<OkObjectResult>(
            await controller.GetAlertSettingsAsync(seed.CostOwner.PrimaryKey, CancellationToken.None)).Value);

        Assert.Equal(1, saved.Revision);
        Assert.Equal([5m, 10.5m, 25m], saved.Thresholds);
        Assert.Equal(saved, read with { Thresholds = saved.Thresholds });
        Assert.Equal([5m, 10.5m, 25m], read.Thresholds);
        Assert.Equal("de", read.MessageLanguage);
        Assert.False(read.EmailEnabled);
        Assert.Equal("de", (await ReloadUserAsync(store, seed.User.PrimaryKey)).Locale);
    }

    /// <summary>
    /// A later save replaces the stored amounts, keeps those that remain, and increments the revision once more.
    /// </summary>
    /// <returns>A task completing after the replacement was verified.</returns>
    [Fact]
    public async Task SecondSave_ReplacesAmountsAndFlags()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        await HostingTestData.SetCostAlertThresholdsAsync(
            store.DbContext, seed.CostOwner.PrimaryKey, true, true, 5m, 10m);

        var saved = Assert.IsType<CostAlertSettingsDto>(Assert.IsType<OkObjectResult>(
            await CreateController(store, OwnerPrincipal(seed)).UpdateAlertSettingsAsync(
                seed.CostOwner.PrimaryKey,
                new CostAlertSettingsUpdate
                {
                    Revision = 1,
                    Thresholds = [10m, 20m],
                    DiscordEnabled = false,
                    EmailEnabled = true,
                },
                CancellationToken.None)).Value);

        Assert.Equal(2, saved.Revision);
        Assert.Equal([10m, 20m], saved.Thresholds);
        Assert.False(saved.DiscordEnabled);
        Assert.Equal([10m, 20m], await StoredAmountsAsync(store, seed.CostOwner.PrimaryKey));
        Assert.Null((await ReloadUserAsync(store, seed.User.PrimaryKey)).Locale);
    }

    /// <summary>
    /// A save based on a stale revision, including a first save when a row already exists, is a conflict that
    /// changes nothing, not even the recorded language.
    /// </summary>
    /// <param name="staleRevision">The revision the rejected save is based on.</param>
    /// <returns>A task completing after the conflict was verified.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task StaleRevision_ReturnsConflictAndChangesNothing(int staleRevision)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        await HostingTestData.SetCostAlertThresholdsAsync(store.DbContext, seed.CostOwner.PrimaryKey, true, true, 5m);

        var result = await CreateController(store, OwnerPrincipal(seed)).UpdateAlertSettingsAsync(
            seed.CostOwner.PrimaryKey,
            new CostAlertSettingsUpdate
            {
                Revision = staleRevision,
                Thresholds = [99m],
                DiscordEnabled = false,
                EmailEnabled = false,
                Language = "de",
            },
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal([5m], await StoredAmountsAsync(store, seed.CostOwner.PrimaryKey));
        var setting = await store.DbContext.CostAlertSettings.AsNoTracking().SingleAsync();
        Assert.Equal(1, setting.Revision);
        Assert.True(setting.DiscordEnabled);
        Assert.Null((await ReloadUserAsync(store, seed.User.PrimaryKey)).Locale);
    }

    /// <summary>
    /// Invalid amount lists are rejected with 400 and leave the stored setting and the language untouched.
    /// </summary>
    /// <param name="amounts">The rejected amounts.</param>
    /// <returns>A task completing after the rejection was verified.</returns>
    [Theory]
    [MemberData(nameof(InvalidAmounts))]
    public async Task InvalidAmounts_ReturnBadRequestAndChangeNothing(decimal[] amounts)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        await HostingTestData.SetCostAlertThresholdsAsync(store.DbContext, seed.CostOwner.PrimaryKey, true, true, 5m);

        var result = await CreateController(store, OwnerPrincipal(seed)).UpdateAlertSettingsAsync(
            seed.CostOwner.PrimaryKey,
            new CostAlertSettingsUpdate
            {
                Revision = 1,
                Thresholds = amounts,
                DiscordEnabled = false,
                EmailEnabled = false,
                Language = "de",
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal([5m], await StoredAmountsAsync(store, seed.CostOwner.PrimaryKey));
        Assert.Equal(1, (await store.DbContext.CostAlertSettings.AsNoTracking().SingleAsync()).Revision);
        Assert.Null((await ReloadUserAsync(store, seed.User.PrimaryKey)).Locale);
    }

    /// <summary>
    /// Gets the rejected amount lists: too many, zero, negative, above the limit, too precise and duplicated.
    /// </summary>
    /// <value>One argument array per rejected list.</value>
    public static TheoryData<decimal[]> InvalidAmounts => new()
    {
        Enumerable.Range(1, 11).Select(q => (decimal) q).ToArray(),
        new[] { 0m },
        new[] { -1m },
        new[] { 10000.01m },
        new[] { 1.234m },
        new[] { 5m, 5.00m },
    };

    /// <summary>
    /// An unsupported language is rejected and nothing is stored.
    /// </summary>
    /// <returns>A task completing after the rejection was verified.</returns>
    [Fact]
    public async Task UnsupportedLanguage_ReturnsBadRequest()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);

        var result = await CreateController(store, OwnerPrincipal(seed)).UpdateAlertSettingsAsync(
            seed.CostOwner.PrimaryKey,
            new CostAlertSettingsUpdate { Revision = 0, Thresholds = [5m], Language = "fr" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await store.DbContext.CostAlertSettings.CountAsync());
    }

    /// <summary>
    /// The boundary amounts of exactly the maximum, the smallest cent and ten amounts are accepted, and an empty
    /// list switches alerts off.
    /// </summary>
    /// <returns>A task completing after the boundaries were verified.</returns>
    [Fact]
    public async Task BoundaryAmounts_AreAccepted_AndEmptyListSwitchesAlertsOff()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var controller = CreateController(store, OwnerPrincipal(seed));

        var boundary = Assert.IsType<CostAlertSettingsDto>(Assert.IsType<OkObjectResult>(
            await controller.UpdateAlertSettingsAsync(
                seed.CostOwner.PrimaryKey,
                new CostAlertSettingsUpdate { Revision = 0, Thresholds = [10000m, 0.01m] },
                CancellationToken.None)).Value);
        var ten = Assert.IsType<CostAlertSettingsDto>(Assert.IsType<OkObjectResult>(
            await controller.UpdateAlertSettingsAsync(
                seed.CostOwner.PrimaryKey,
                new CostAlertSettingsUpdate
                {
                    Revision = 1,
                    Thresholds = Enumerable.Range(1, 10).Select(q => (decimal) q).ToArray(),
                },
                CancellationToken.None)).Value);
        var empty = Assert.IsType<CostAlertSettingsDto>(Assert.IsType<OkObjectResult>(
            await controller.UpdateAlertSettingsAsync(
                seed.CostOwner.PrimaryKey,
                new CostAlertSettingsUpdate { Revision = 2, Thresholds = [] },
                CancellationToken.None)).Value);

        Assert.Equal([0.01m, 10000m], boundary.Thresholds);
        Assert.Equal(10, ten.Thresholds.Count);
        Assert.Empty(empty.Thresholds);
        Assert.Equal(3, empty.Revision);
    }

    /// <summary>
    /// The owner's user, a hosting administrator and a co:use holder may save and read CanManage as true.
    /// </summary>
    /// <param name="kind">Which kind of manager saves.</param>
    /// <returns>A task completing after the save was verified.</returns>
    [Theory]
    [InlineData("owner")]
    [InlineData("administrator")]
    [InlineData("use")]
    public async Task Managers_MaySave(string kind)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var other = AddUser(store, "other");
        await store.DbContext.SaveChangesAsync();
        var principal = kind switch
        {
            "owner"         => OwnerPrincipal(seed),
            "administrator" => AdminPrincipal(other),
            _ => HostingTestData.Principal(
                other, (Claims.ResourceBased.CostOwner.Use, seed.CostOwner.PrimaryKey.ToString())),
        };

        var result = await CreateController(store, principal).UpdateAlertSettingsAsync(
            seed.CostOwner.PrimaryKey,
            new CostAlertSettingsUpdate { Revision = 0, Thresholds = [5m] },
            CancellationToken.None);

        var dto = Assert.IsType<CostAlertSettingsDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(dto.CanManage);
        Assert.Equal([5m], dto.Thresholds);
    }

    /// <summary>
    /// A co:view holder reads with CanManage false and cannot save.
    /// </summary>
    /// <returns>A task completing after the read-only access was verified.</returns>
    [Fact]
    public async Task ViewHolder_ReadsWithoutManage_AndCannotSave()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var viewer = AddUser(store, "viewer");
        await store.DbContext.SaveChangesAsync();
        var controller = CreateController(
            store,
            HostingTestData.Principal(viewer, (Claims.ResourceBased.CostOwner.View, seed.CostOwner.PrimaryKey.ToString())));

        var read = Assert.IsType<CostAlertSettingsDto>(Assert.IsType<OkObjectResult>(
            await controller.GetAlertSettingsAsync(seed.CostOwner.PrimaryKey, CancellationToken.None)).Value);
        var save = await controller.UpdateAlertSettingsAsync(
            seed.CostOwner.PrimaryKey,
            new CostAlertSettingsUpdate { Revision = 0, Thresholds = [5m] },
            CancellationToken.None);

        Assert.False(read.CanManage);
        Assert.IsType<ForbidResult>(save);
        Assert.Equal(0, await store.DbContext.CostAlertSettings.CountAsync());
    }

    /// <summary>
    /// An unrelated user may neither read nor save, and an unknown owner is not found.
    /// </summary>
    /// <returns>A task completing after the denials were verified.</returns>
    [Fact]
    public async Task UnrelatedUser_IsForbidden_AndUnknownOwnerIsNotFound()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var stranger = AddUser(store, "stranger");
        await store.DbContext.SaveChangesAsync();
        var controller = CreateController(store, HostingTestData.Principal(stranger));

        var read = await controller.GetAlertSettingsAsync(seed.CostOwner.PrimaryKey, CancellationToken.None);
        var save = await controller.UpdateAlertSettingsAsync(
            seed.CostOwner.PrimaryKey,
            new CostAlertSettingsUpdate { Revision = 0, Thresholds = [5m] },
            CancellationToken.None);
        var unknown = await CreateController(store, AdminPrincipal(seed.User))
            .GetAlertSettingsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<ForbidResult>(read);
        Assert.IsType<ForbidResult>(save);
        Assert.IsType<NotFoundObjectResult>(unknown);
        Assert.Equal(0, await store.DbContext.CostAlertSettings.CountAsync());
    }

    /// <summary>
    /// The caller's personal owner is returned, and 204 when the caller has none.
    /// </summary>
    /// <returns>A task completing after both outcomes were verified.</returns>
    [Fact]
    public async Task Mine_ReturnsPersonalOwner_Or204()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var ownerless = AddUser(store, "ownerless");
        await store.DbContext.SaveChangesAsync();

        var mine = await CreateController(store, OwnerPrincipal(seed))
            .GetMyAlertSettingsAsync(CancellationToken.None);
        var none = await CreateController(store, HostingTestData.Principal(ownerless))
            .GetMyAlertSettingsAsync(CancellationToken.None);

        var dto = Assert.IsType<CostAlertSettingsDto>(Assert.IsType<OkObjectResult>(mine).Value);
        Assert.Equal(seed.CostOwner.PrimaryKey, dto.CostOwnerId);
        Assert.IsType<NoContentResult>(none);
    }

    /// <summary>
    /// Channel availability follows the installation switches and the recipient's linked account and address.
    /// </summary>
    /// <param name="discordEnabled">The value of <c>Discord:Enabled</c>, or <see langword="null"/> when unset.</param>
    /// <param name="emailConfigured">Whether the email sender is configured.</param>
    /// <param name="linked">Whether the owner's user has a Discord account and an email address.</param>
    /// <param name="expectedDiscord">The expected Discord availability.</param>
    /// <param name="expectedEmail">The expected email availability.</param>
    /// <returns>A task completing after the availability was verified.</returns>
    [Theory]
    [InlineData("false", true, true, ECostAlertChannelAvailability.ChannelUnavailable, ECostAlertChannelAvailability.Deliverable)]
    [InlineData("true", false, true, ECostAlertChannelAvailability.Deliverable, ECostAlertChannelAvailability.ChannelUnavailable)]
    [InlineData(null, true, true, ECostAlertChannelAvailability.Deliverable, ECostAlertChannelAvailability.Deliverable)]
    [InlineData("true", true, false, ECostAlertChannelAvailability.NotLinked, ECostAlertChannelAvailability.NoAddress)]
    public async Task ChannelAvailability_FollowsConfigurationAndRecipient(
        string? discordEnabled,
        bool emailConfigured,
        bool linked,
        ECostAlertChannelAvailability expectedDiscord,
        ECostAlertChannelAvailability expectedEmail)
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        if (linked)
        {
            seed.User.Discord.Id = 42;
            seed.User.EMail = "owner@example.org";
            await store.DbContext.SaveChangesAsync();
        }

        var dto = Assert.IsType<CostAlertSettingsDto>(Assert.IsType<OkObjectResult>(
            await CreateController(store, OwnerPrincipal(seed), discordEnabled, emailConfigured)
                .GetAlertSettingsAsync(seed.CostOwner.PrimaryKey, CancellationToken.None)).Value);

        Assert.Equal(expectedDiscord, dto.Discord);
        Assert.Equal(expectedEmail, dto.Email);
    }

    /// <summary>
    /// Creates the real controller on the store with a fake email sender and an in-memory configuration.
    /// </summary>
    /// <param name="store">The test store the services read from.</param>
    /// <param name="principal">The caller of every request.</param>
    /// <param name="discordEnabled">The value of <c>Discord:Enabled</c>, or <see langword="null"/> when unset.</param>
    /// <param name="emailConfigured">Whether the fake email sender reports being configured.</param>
    /// <returns>The controller with an HTTP context for the principal.</returns>
    private static CostAlertSettingsController CreateController(
        HostingTestStore store,
        ClaimsPrincipal principal,
        string? discordEnabled = null,
        bool emailConfigured = true)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                discordEnabled is null
                    ? []
                    : new Dictionary<string, string?> { [Constants.Configuration.Discord.Enabled] = discordEnabled })
            .Build();
        var service = new CostAlertSettingsService(
            store.DbContext,
            new HostingAuthorizationService(store.DbContext),
            new CostAlertRecipientResolver(store.DbContext),
            new FakeEmailSender(emailConfigured),
            configuration,
            Options.Create(new CostAlertOptions()),
            NullLogger<CostAlertSettingsService>.Instance);
        return new CostAlertSettingsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };
    }

    /// <summary>
    /// Creates the principal of the seeded owner's user.
    /// </summary>
    /// <param name="seed">The seeded server and owner.</param>
    /// <returns>A principal without any claim.</returns>
    private static ClaimsPrincipal OwnerPrincipal(HostingTestSeed seed) => HostingTestData.Principal(seed.User);

    /// <summary>
    /// Creates the principal of a hosting administrator.
    /// </summary>
    /// <param name="user">The user the principal is created for.</param>
    /// <returns>A principal with the empty hosting administrative claim.</returns>
    private static ClaimsPrincipal AdminPrincipal(User user)
        => HostingTestData.Principal(user, (Claims.Administrative.Hosting, string.Empty));

    /// <summary>
    /// Adds an unsaved group cost owner.
    /// </summary>
    /// <param name="store">The store the owner is added to.</param>
    /// <param name="kind">The kind of the owner.</param>
    /// <param name="displayName">The display name.</param>
    /// <returns>The added owner.</returns>
    private static CostOwner AddGroupOwner(HostingTestStore store, ECostOwnerKind kind, string displayName)
    {
        var owner = new CostOwner
        {
            PrimaryKey       = Guid.NewGuid(),
            Kind             = kind,
            DisplayName      = displayName,
            Currency         = "EUR",
            TimeStampCreated = Instant.FromUnixTimeSeconds(1_700_000_000),
            TimeStampUpdated = Instant.FromUnixTimeSeconds(1_700_000_000),
        };
        store.DbContext.CostOwners.Add(owner);
        return owner;
    }

    /// <summary>
    /// Adds an unsaved active user without a cost owner.
    /// </summary>
    /// <param name="store">The store the user is added to.</param>
    /// <param name="nickname">The nickname of the user.</param>
    /// <returns>The added user.</returns>
    private static User AddUser(HostingTestStore store, string nickname)
    {
        var user = new User
        {
            PrimaryKey     = Guid.NewGuid(),
            Nickname       = nickname,
            AvatarMimeType = string.Empty,
            Claims         = [],
            Roles          = [],
            Discord        = new DiscordUser { Username = nickname },
            Steam          = new SteamUser(),
        };
        store.DbContext.Users.Add(user);
        return user;
    }

    /// <summary>
    /// Reads a user from the database without using the change tracker.
    /// </summary>
    /// <param name="store">The store to read from.</param>
    /// <param name="userId">The user to read.</param>
    /// <returns>A task resolving to the stored user.</returns>
    private static Task<User> ReloadUserAsync(HostingTestStore store, Guid userId)
        => store.DbContext.Users.AsNoTracking().SingleAsync(q => q.PrimaryKey == userId);

    /// <summary>
    /// Reads the stored amounts of one owner in ascending order.
    /// </summary>
    /// <param name="store">The store to read from.</param>
    /// <param name="costOwnerId">The owner whose amounts are read.</param>
    /// <returns>A task resolving to the stored amounts.</returns>
    private static async Task<decimal[]> StoredAmountsAsync(HostingTestStore store, Guid costOwnerId)
        => (await store.DbContext.CostAlertThresholds
                .AsNoTracking()
                .Where(q => q.Setting!.CostOwnerFk == costOwnerId)
                .Select(q => q.Amount)
                .ToArrayAsync())
            .Order()
            .ToArray();

    /// <summary>
    /// An email sender whose configured state is fixed by the test and which never delivers.
    /// </summary>
    /// <param name="isConfigured">Whether the sender reports being configured.</param>
    private sealed class FakeEmailSender(bool isConfigured) : IEmailSender
    {
        /// <inheritdoc />
        public bool IsConfigured { get; } = isConfigured;

        /// <inheritdoc />
        public string CreateMessageId(Guid costAlertId, Guid recipientUserId)
            => $"{costAlertId:N}.{recipientUserId:N}@test.invalid";

        /// <inheritdoc />
        public Task<DeliveryAttemptResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
            => Task.FromResult(DeliveryAttemptResult.Sent());
    }
}
