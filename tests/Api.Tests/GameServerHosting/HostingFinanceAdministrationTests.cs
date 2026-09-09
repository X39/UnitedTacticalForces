using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace Api.Tests.GameServerHosting;

public sealed class HostingFinanceAdministrationTests
{
    [Fact]
    public async Task Create_AllKindsEnforcesIdentityRulesAndWritesAudit()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var actor = new User { PrimaryKey = Guid.NewGuid(), Nickname = "Treasurer" };
        var represented = new User { PrimaryKey = Guid.NewGuid(), Nickname = "Current nickname" };
        store.DbContext.Users.AddRange(actor, represented);
        await store.DbContext.SaveChangesAsync();
        var controller = CreateController(store, actor);

        var userResult = Assert.IsType<CreatedAtRouteResult>(await controller.CreateAsync(
            new CostOwnerCreationPayload { Kind = ECostOwnerKind.User, UserId = represented.PrimaryKey },
            default));
        Assert.Equal(nameof(CostOwnersController.GetAsync), userResult.RouteName);
        var userOwner = Assert.IsType<HostingCostOwnerDto>(userResult.Value);
        Assert.Equal("Current nickname", userOwner.DisplayName);
        Assert.IsType<ConflictObjectResult>(await controller.CreateAsync(
            new CostOwnerCreationPayload { Kind = ECostOwnerKind.User, UserId = represented.PrimaryKey },
            default));

        Assert.IsType<CreatedAtRouteResult>(await controller.CreateAsync(
            new CostOwnerCreationPayload { Kind = ECostOwnerKind.Verein }, default));
        Assert.IsType<ConflictObjectResult>(await controller.CreateAsync(
            new CostOwnerCreationPayload { Kind = ECostOwnerKind.Verein }, default));
        var organizationResult = Assert.IsType<CreatedAtRouteResult>(await controller.CreateAsync(
            new CostOwnerCreationPayload
            {
                Kind = ECostOwnerKind.ExternalOrganization,
                DisplayName = "  Partner Unit  ",
            },
            default));
        Assert.Equal("Partner Unit", Assert.IsType<HostingCostOwnerDto>(organizationResult.Value).DisplayName);
        Assert.Equal(3, await store.DbContext.HostingAuditRecords
            .CountAsync(q => q.Kind == EHostingAuditEventKind.CostOwnerCreated));
    }

    [Fact]
    public async Task RenameAndFreeze_RespectAccountKindAndRemainAuditable()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext);
        var organization = new CostOwner
        {
            PrimaryKey = Guid.NewGuid(),
            Kind = ECostOwnerKind.ExternalOrganization,
            DisplayName = "Old name",
            Currency = "EUR",
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant(),
        };
        store.DbContext.CostOwners.Add(organization);
        await store.DbContext.SaveChangesAsync();
        var controller = CreateController(store, seed.User);

        Assert.IsType<BadRequestObjectResult>(await controller.RenameAsync(
            seed.CostOwner.PrimaryKey,
            new CostOwnerDisplayNameUpdatePayload { DisplayName = "Not permitted" },
            default));
        var renamed = Assert.IsType<HostingCostOwnerDto>(Assert.IsType<OkObjectResult>(await controller.RenameAsync(
            organization.PrimaryKey,
            new CostOwnerDisplayNameUpdatePayload { DisplayName = "New name" },
            default)).Value);
        Assert.Equal("New name", renamed.DisplayName);
        var frozen = Assert.IsType<HostingCostOwnerDto>(Assert.IsType<OkObjectResult>(await controller.SetFrozenAsync(
            organization.PrimaryKey,
            new CostOwnerFreezeUpdatePayload { IsFrozen = true },
            default)).Value);
        Assert.True(frozen.IsFrozen);
        Assert.Contains(await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.CostOwnerRenamed);
        Assert.Contains(await store.DbContext.HostingAuditRecords.ToArrayAsync(),
            q => q.Kind == EHostingAuditEventKind.CostOwnerFrozen);
    }

    [Fact]
    public async Task Protocol_EnrichesProviderCostAndComputesReversibilityAcrossPages()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        seed.User.Nickname = "Renamed member";
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var finance = new HostingFinanceService(store.DbContext, new HostingAuditWriter());
        var payment = await finance.RecordBankTransferPaymentAsync(
            seed.CostOwner.PrimaryKey,
            Guid.NewGuid(),
            10m,
            "EUR",
            now - Duration.FromHours(2),
            "BANK-42",
            "Opening credit",
            seed.User.PrimaryKey);
        await finance.ReverseLedgerEntryAsync(
            seed.CostOwner.PrimaryKey,
            payment.PrimaryKey,
            Guid.NewGuid(),
            now,
            "Duplicate payment",
            seed.User.PrimaryKey);
        store.DbContext.HostingCostLedgerEntries.Add(
            new HostingCostLedgerEntry
            {
                PrimaryKey = Guid.NewGuid(),
                CostOwnerFk = seed.CostOwner.PrimaryKey,
                DeploymentFk = seed.Deployment!.PrimaryKey,
                Kind = EHostingLedgerEntryKind.ProviderCostSlice,
                Amount = -1m,
                BalanceAfter = 9m,
                Currency = "EUR",
                ProviderIdentifier = "hetzner",
                EstimatedHourlyRate = 0.5m,
                SliceStartsAt = now - Duration.FromHours(1),
                SliceEndsAt = now,
                TimeStampEffective = now - Duration.FromHours(1),
                TimeStampCreated = now - Duration.FromHours(1),
            });
        await store.DbContext.SaveChangesAsync();
        var controller = CreateController(store, seed.User);

        var result = Assert.IsType<HostingFinanceProtocolPageDto>(Assert.IsType<OkObjectResult>(
            await controller.GetProtocolAsync(
                seed.CostOwner.PrimaryKey,
                "hetzner",
                null,
                null,
                null,
                0,
                50,
                default)).Value);
        var providerCost = Assert.Single(result.Entries);
        Assert.Equal("Renamed member", providerCost.CostOwnerDisplayName);
        Assert.Equal(seed.Server.Title, providerCost.ServerTitle);
        Assert.Equal("hetzner", providerCost.ProviderIdentifier);

        var paymentResult = Assert.IsType<HostingFinanceProtocolPageDto>(Assert.IsType<OkObjectResult>(
            await controller.GetProtocolAsync(
                seed.CostOwner.PrimaryKey,
                "BANK-42",
                null,
                null,
                null,
                0,
                1,
                default)).Value);
        Assert.False(Assert.Single(paymentResult.Entries).IsReversible);
    }

    private static CostOwnersController CreateController(HostingTestStore store, User actor)
    {
        var controller = new CostOwnersController(
            store.DbContext,
            null!,
            null!,
            null!,
            new HostingAuditWriter());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = HostingTestData.Principal(actor, (Claims.Finance.All, string.Empty)),
            },
        };
        return controller;
    }
}
