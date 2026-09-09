using System.Security.Claims;
using Api.Tests.GameServerHosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Database.Core;
using Xunit;
using Event = X39.UnitedTacticalForces.Database.Eventing.Event;

namespace Api.Tests.ModPacks;

/// <summary>Verifies persistent evidence for generated mod-pack format deliveries.</summary>
public sealed class ModPackFormatDeliveryTests
{
    /// <summary>Verifies authenticated management delivery evidence.</summary>
    [Fact]
    public async Task ManagementDownload_RecordsAuthenticatedUser()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seeded = await SeedAsync(store, false);
        var controller = CreateController(store, Principal(seeded.UserId));

        var result = await controller.DownloadAsync(seeded.RevisionId, "arma3-launcher-html", default);

        Assert.IsType<FileContentResult>(result);
        var delivery = await store.DbContext.ModPackFormatDeliveries.SingleAsync();
        Assert.Equal(seeded.RevisionId, delivery.ModPackRevisionFk);
        Assert.Equal("arma3-launcher-html", delivery.FormatIdentifier);
        Assert.Equal(seeded.UserId, delivery.UserFk);
        Assert.Null(delivery.EventFk);
    }

    /// <summary>Verifies an authenticated Event owner can download a private Event format.</summary>
    [Fact]
    public async Task PrivateEventOwnerDownload_RecordsEventAndUser()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seeded = await SeedAsync(store, false);
        var controller = CreateController(store, Principal(seeded.UserId));

        var result = await controller.DownloadEventFormatAsync(seeded.EventId, "arma3-launcher-html", default);

        Assert.IsType<FileContentResult>(result);
        var delivery = await store.DbContext.ModPackFormatDeliveries.SingleAsync();
        Assert.Equal(seeded.EventId, delivery.EventFk);
        Assert.Equal(seeded.UserId, delivery.UserFk);
    }

    /// <summary>Verifies anonymous delivery evidence for a public Event.</summary>
    [Fact]
    public async Task PublicEventAnonymousDownload_RecordsEventWithoutUser()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seeded = await SeedAsync(store, true);
        var controller = CreateController(store, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.DownloadEventFormatAsync(seeded.EventId, "arma3-launcher-html", default);

        Assert.IsType<FileContentResult>(result);
        var delivery = await store.DbContext.ModPackFormatDeliveries.SingleAsync();
        Assert.Equal(seeded.EventId, delivery.EventFk);
        Assert.Null(delivery.UserFk);
    }

    /// <summary>Verifies a forbidden private Event request creates no evidence.</summary>
    [Fact]
    public async Task PrivateEventUnauthorizedDownload_RecordsNoEvidence()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seeded = await SeedAsync(store, false);
        var controller = CreateController(store, Principal(Guid.NewGuid()));

        var result = await controller.DownloadEventFormatAsync(seeded.EventId, "arma3-launcher-html", default);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(await store.DbContext.ModPackFormatDeliveries.ToArrayAsync());
    }

    /// <summary>Verifies an unsupported format creates no evidence.</summary>
    [Fact]
    public async Task UnsupportedFormat_RecordsNoEvidence()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seeded = await SeedAsync(store, false);
        var controller = CreateController(store, Principal(seeded.UserId));

        var result = await controller.DownloadAsync(seeded.RevisionId, "unsupported", default);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(await store.DbContext.ModPackFormatDeliveries.ToArrayAsync());
    }

    private static ModPackFormatsController CreateController(HostingTestStore store, ClaimsPrincipal principal)
        => new(store.DbContext, [new Arma3LauncherHtmlFormatHandler()], [])
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
        };

    private static ClaimsPrincipal Principal(Guid userId)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, userId.ToString())],
            Constants.AuthorizationSchemas.Api));

    private static async Task<(Guid UserId, long RevisionId, Guid EventId)> SeedAsync(
        HostingTestStore store,
        bool isVisible)
    {
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var definition = new ModPackDefinition
        {
            GameIdentifier = "arma3",
            Title = "Operations",
            OwnerFk = seed.User.PrimaryKey,
            IsActive = true,
            TimeStampCreated = now,
        };
        var revision = new ModPackRevision
        {
            Definition = definition,
            UpdatedByFk = seed.User.PrimaryKey,
            IsActive = true,
            TimeStampCreated = now,
        };
        var eventEntity = new Event
        {
            PrimaryKey = Guid.NewGuid(),
            Title = "Operation",
            Description = string.Empty,
            OwnerFk = seed.User.PrimaryKey,
            HostedByFk = seed.User.PrimaryKey,
            ModPackRevision = revision,
            IsVisible = isVisible,
            ScheduledForOriginal = now,
            ScheduledFor = now,
            TimeStampCreated = now,
        };
        store.DbContext.AddRange(definition, revision, eventEntity);
        await store.DbContext.SaveChangesAsync();
        return (seed.User.PrimaryKey, revision.PrimaryKey, eventEntity.PrimaryKey);
    }
}
