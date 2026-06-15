using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;
using SecurityClaim = System.Security.Claims.Claim;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Verein;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads.Verein;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using X39.UnitedTacticalForces.Common.Verein;
using Xunit;

namespace Api.Tests.Verein;

public class VereinControllerTests
{
    [Fact]
    public async Task PublishedSatzung_ReturnsLatestPublishedRevisionAndHidesDrafts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var actor = CreateUser("verein-admin");
        dbContext.Users.Add(actor);
        await dbContext.SaveChangesAsync();
        var controller = CreateVereinController(
            dbContext,
            CreatePrincipal(actor, (Claims.Verein.Documents, string.Empty))
        );

        var draft = await InvokeOkAsync<VereinDocumentDto>(
            controller.CreateDocumentAsync(
                EVereinDocumentKind.Satzung,
                CreateDocumentPayload("Draft Satzung", isPublished: false)
            )
        );

        Assert.IsType<NotFoundResult>(await new VereinController(dbContext).GetPublishedSatzungAsync());

        await InvokeOkAsync<VereinDocumentDto>(
            controller.CreateDocumentRevisionAsync(
                draft.PrimaryKey,
                CreateDocumentPayload("Published Satzung", isPublished: true)
            )
        );
        await InvokeOkAsync<VereinDocumentDto>(
            controller.CreateDocumentRevisionAsync(
                draft.PrimaryKey,
                CreateDocumentPayload("Latest Satzung", isPublished: true)
            )
        );

        var published = await InvokeOkAsync<VereinController.PublishedVereinDocumentDto>(
            new VereinController(dbContext).GetPublishedSatzungAsync()
        );

        Assert.Equal("Latest Satzung", published.Revision?.Title);
        Assert.Equal(3, published.Revision?.Version);
        AssertPublishedDocumentOmitsAdminMetadata(published);
    }

    [Fact]
    public async Task KassenbuchRead_RequiresActiveMembershipOrTreasuryAuthority()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var activeMember = CreateUser("active-member");
        var suspendedMember = CreateUser("suspended-member");
        var nonMember = CreateUser("non-member");
        var treasurer = CreateUser("treasurer");
        dbContext.Users.AddRange(activeMember, suspendedMember, nonMember, treasurer);
        dbContext.VereinMemberships.AddRange(
            CreateMembership(activeMember.PrimaryKey, EVereinMembershipStatus.Active),
            CreateMembership(suspendedMember.PrimaryKey, EVereinMembershipStatus.Suspended)
        );
        dbContext.KassenbuchEntries.Add(CreateEntry(activeMember.PrimaryKey));
        await dbContext.SaveChangesAsync();

        var activeController = CreateVereinController(dbContext, CreatePrincipal(activeMember));
        var suspendedController = CreateVereinController(dbContext, CreatePrincipal(suspendedMember));
        var nonMemberController = CreateVereinController(dbContext, CreatePrincipal(nonMember));
        var treasurerController = CreateVereinController(
            dbContext,
            CreatePrincipal(treasurer, (Claims.Verein.Kassenbuch, string.Empty))
        );

        Assert.IsType<KassenbuchPageDto>(
            Assert.IsType<OkObjectResult>(await activeController.GetKassenbuchAsync()).Value
        );
        Assert.IsType<ForbidResult>(await suspendedController.GetKassenbuchAsync());
        Assert.IsType<ForbidResult>(await nonMemberController.GetKassenbuchAsync());
        Assert.IsType<KassenbuchPageDto>(
            Assert.IsType<OkObjectResult>(await treasurerController.GetKassenbuchAsync()).Value
        );
    }

    [Fact]
    public async Task TreasurerMutations_PersistEntriesAndAuditRecords()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var treasurer = CreateUser("treasurer");
        dbContext.Users.Add(treasurer);
        await dbContext.SaveChangesAsync();
        var controller = CreateVereinController(
            dbContext,
            CreatePrincipal(treasurer, (Claims.Verein.Kassenbuch, string.Empty))
        );

        var created = await InvokeOkAsync<KassenbuchEntryDto>(
            controller.CreateKassenbuchEntryAsync(CreateKassenbuchPayload("Initial dues", 20m))
        );
        var updated = await InvokeOkAsync<KassenbuchEntryDto>(
            controller.UpdateKassenbuchEntryAsync(
                created.PrimaryKey,
                CreateKassenbuchPayload("Updated dues", 25m)
            )
        );
        var deleted = await InvokeOkAsync<KassenbuchEntryDto>(
            controller.DeleteKassenbuchEntryAsync(updated.PrimaryKey)
        );

        Assert.True(deleted.IsDeleted);
        var auditActions = await dbContext.KassenbuchEntryAudits
            .OrderBy(q => q.TimeStampCreated)
            .Select(q => q.Action)
            .ToArrayAsync();
        Assert.Contains(EKassenbuchAuditAction.Created, auditActions);
        Assert.Contains(EKassenbuchAuditAction.Updated, auditActions);
        Assert.Contains(EKassenbuchAuditAction.Deleted, auditActions);
    }

    [Fact]
    public async Task MembershipUpsert_PersistsStateAndGetMeProjection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("member");
        var actor = CreateUser("membership-admin");
        dbContext.Users.AddRange(user, actor);
        await dbContext.SaveChangesAsync();
        var vereinController = CreateVereinController(
            dbContext,
            CreatePrincipal(actor, (Claims.Verein.Members, string.Empty))
        );

        var membership = await InvokeOkAsync<VereinMembershipDto>(
            vereinController.UpsertMembershipAsync(
                user.PrimaryKey,
                new VereinMembershipUpdatePayload
                {
                    MemberNumber = "UTF-001",
                    Status       = EVereinMembershipStatus.Active,
                    Notes        = "Founding member",
                    Reason       = "Initial import",
                }
            )
        );
        var meController = CreateUsersController(dbContext, CreatePrincipal(user));
        var me = Assert.IsType<FullUserDto>(Assert.IsType<OkObjectResult>(await meController.GetMeAsync(CancellationToken.None)).Value);

        Assert.Equal("UTF-001", membership.MemberNumber);
        var exactMembership = await InvokeOkAsync<VereinMembershipDto>(
            vereinController.GetMembershipAsync(user.PrimaryKey)
        );
        Assert.Equal(user.PrimaryKey, exactMembership.UserFk);
        Assert.Equal("UTF-001", exactMembership.MemberNumber);
        Assert.True(me.IsVereinMember);
        Assert.Equal("Active", me.VereinMembershipStatus);
        Assert.Equal("UTF-001", me.VereinMemberNumber);

        await InvokeOkAsync<VereinMembershipDto>(
            vereinController.UpsertMembershipAsync(
                user.PrimaryKey,
                new VereinMembershipUpdatePayload
                {
                    MemberNumber = "UTF-001",
                    Status       = EVereinMembershipStatus.Ended,
                    Notes        = "Left Verein",
                    Reason       = "Resignation",
                }
            )
        );
        me = Assert.IsType<FullUserDto>(Assert.IsType<OkObjectResult>(await meController.GetMeAsync(CancellationToken.None)).Value);

        Assert.False(me.IsVereinMember);
        Assert.Equal("Ended", me.VereinMembershipStatus);
        Assert.Contains(
            EVereinMembershipAuditAction.Ended,
            await dbContext.VereinMembershipAudits.Select(q => q.Action).ToArrayAsync()
        );
    }

    [Fact]
    public async Task ProfileSettings_ReportsSelfServiceAccountDeletionEligibility()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var communityUser = CreateUser("community");
        var activeMember = CreateUser("active-member");
        var suspendedMember = CreateUser("suspended-member");
        var endedMember = CreateUser("ended-member");
        dbContext.Users.AddRange(communityUser, activeMember, suspendedMember, endedMember);
        dbContext.VereinMemberships.AddRange(
            CreateMembership(activeMember.PrimaryKey, EVereinMembershipStatus.Active),
            CreateMembership(suspendedMember.PrimaryKey, EVereinMembershipStatus.Suspended),
            CreateMembership(endedMember.PrimaryKey, EVereinMembershipStatus.Ended)
        );
        await dbContext.SaveChangesAsync();

        var communitySettings = await InvokeOkAsync<ProfileSettingsDto>(
            CreateUsersController(dbContext, CreatePrincipal(communityUser)).GetMeSettingsAsync(CancellationToken.None)
        );
        var activeSettings = await InvokeOkAsync<ProfileSettingsDto>(
            CreateUsersController(dbContext, CreatePrincipal(activeMember)).GetMeSettingsAsync(CancellationToken.None)
        );
        var suspendedSettings = await InvokeOkAsync<ProfileSettingsDto>(
            CreateUsersController(dbContext, CreatePrincipal(suspendedMember)).GetMeSettingsAsync(CancellationToken.None)
        );
        var endedSettings = await InvokeOkAsync<ProfileSettingsDto>(
            CreateUsersController(dbContext, CreatePrincipal(endedMember)).GetMeSettingsAsync(CancellationToken.None)
        );

        Assert.True(communitySettings.CanDeleteAccount);
        Assert.Null(communitySettings.AccountDeletionBlockedReason);
        Assert.False(activeSettings.CanDeleteAccount);
        Assert.NotNull(activeSettings.AccountDeletionBlockedReason);
        Assert.False(suspendedSettings.CanDeleteAccount);
        Assert.NotNull(suspendedSettings.AccountDeletionBlockedReason);
        Assert.True(endedSettings.CanDeleteAccount);
        Assert.Null(endedSettings.AccountDeletionBlockedReason);
    }

    [Fact]
    public async Task ProfileSettings_NonMembersCanSaveFormalApplicantFieldsAndDateOfBirth()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var applicant = CreateUser("applicant");
        dbContext.Users.Add(applicant);
        await dbContext.SaveChangesAsync();

        var updated = await InvokeOkAsync<ProfileSettingsDto>(
            CreateUsersController(dbContext, CreatePrincipal(applicant))
                .UpdateMeSettingsAsync(
                    new ProfileSettingsUpdate
                    {
                        FirstName    = " Ada ",
                        LastName     = " Lovelace ",
                        DateOfBirth  = new LocalDate(1990, 1, 2),
                        PhoneNumber  = " +49 30 123456 ",
                        AddressLine1 = " Main Street 1 ",
                        AddressLine2 = " Apartment 2 ",
                        PostalCode   = " 10115 ",
                        City         = " Berlin ",
                        Country      = " Germany ",
                    },
                    CancellationToken.None
                )
        );

        Assert.False(updated.IsVereinMember);
        Assert.Equal("Ada", updated.FirstName);
        Assert.Equal("Lovelace", updated.LastName);
        Assert.Equal(new LocalDate(1990, 1, 2), updated.DateOfBirth);
        Assert.Equal("+49 30 123456", updated.PhoneNumber);
        Assert.Equal("Main Street 1", updated.AddressLine1);
        Assert.Equal("Apartment 2", updated.AddressLine2);
        Assert.Equal("10115", updated.PostalCode);
        Assert.Equal("Berlin", updated.City);
        Assert.Equal("Germany", updated.Country);

        var persisted = await dbContext.Users.SingleAsync(q => q.PrimaryKey == applicant.PrimaryKey);
        Assert.Equal(new LocalDate(1990, 1, 2), persisted.DateOfBirth);
        Assert.Equal("Ada", persisted.FirstName);
    }

    [Fact]
    public async Task SelfDelete_AnonymizesEligibleAccounts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var communityUser = CreateUserWithPersonalData("community");
        var endedMember = CreateUserWithPersonalData("ended-member");
        dbContext.Users.AddRange(communityUser, endedMember);
        dbContext.VereinMemberships.Add(CreateMembership(endedMember.PrimaryKey, EVereinMembershipStatus.Ended));
        await dbContext.SaveChangesAsync();

        Assert.IsType<SignOutResult>(
            await CreateUsersController(dbContext, CreatePrincipal(communityUser))
                .MeDeleteAsync(null, CancellationToken.None)
        );
        Assert.IsType<SignOutResult>(
            await CreateUsersController(dbContext, CreatePrincipal(endedMember))
                .MeDeleteAsync(null, CancellationToken.None)
        );

        var deletedUsers = await dbContext.Users
            .Where(q => q.PrimaryKey == communityUser.PrimaryKey || q.PrimaryKey == endedMember.PrimaryKey)
            .ToArrayAsync();
        Assert.All(
            deletedUsers,
            user =>
            {
                Assert.True(user.IsDeleted);
                Assert.Equal("[anonymous]", user.Nickname);
                Assert.Null(user.EMail);
                Assert.Null(user.FirstName);
                Assert.Null(user.LastName);
                Assert.Null(user.DateOfBirth);
                Assert.Null(user.PhoneNumber);
                Assert.Null(user.AddressLine1);
                Assert.Null(user.AddressLine2);
                Assert.Null(user.PostalCode);
                Assert.Null(user.City);
                Assert.Null(user.Country);
                Assert.Empty(user.Avatar);
                Assert.Equal(string.Empty, user.AvatarMimeType);
                Assert.Equal(0UL, user.Steam.Id64);
                Assert.Equal(0UL, user.Discord.Id);
                Assert.Equal(string.Empty, user.Discord.Username);
            }
        );
    }

    [Fact]
    public async Task SelfDelete_BlocksActiveAndSuspendedVereinMembers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var activeMember = CreateUserWithPersonalData("active-member");
        var suspendedMember = CreateUserWithPersonalData("suspended-member");
        dbContext.Users.AddRange(activeMember, suspendedMember);
        dbContext.VereinMemberships.AddRange(
            CreateMembership(activeMember.PrimaryKey, EVereinMembershipStatus.Active),
            CreateMembership(suspendedMember.PrimaryKey, EVereinMembershipStatus.Suspended)
        );
        await dbContext.SaveChangesAsync();

        Assert.IsType<ConflictObjectResult>(
            await CreateUsersController(dbContext, CreatePrincipal(activeMember))
                .MeDeleteAsync(null, CancellationToken.None)
        );
        Assert.IsType<ConflictObjectResult>(
            await CreateUsersController(dbContext, CreatePrincipal(suspendedMember))
                .MeDeleteAsync(null, CancellationToken.None)
        );

        Assert.False(await dbContext.Users.AnyAsync(q => q.IsDeleted));
        Assert.Equal("active-member", (await dbContext.Users.SingleAsync(q => q.PrimaryKey == activeMember.PrimaryKey)).Nickname);
        Assert.Equal("suspended-member", (await dbContext.Users.SingleAsync(q => q.PrimaryKey == suspendedMember.PrimaryKey)).Nickname);
    }

    [Fact]
    public async Task MembershipRequests_CreateReadAndWithdraw_EnforcesOwnerAndOpenRequestRules()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var applicant = CreateUser("applicant");
        var otherUser = CreateUser("other-user");
        var activeMember = CreateUser("active-member");
        dbContext.Users.AddRange(applicant, otherUser, activeMember);
        dbContext.VereinMemberships.Add(CreateMembership(activeMember.PrimaryKey, EVereinMembershipStatus.Active));
        await dbContext.SaveChangesAsync();

        var anonymousController = CreateVereinController(dbContext, new ClaimsPrincipal(new ClaimsIdentity()));
        var applicantController = CreateVereinController(dbContext, CreatePrincipal(applicant));
        var otherController = CreateVereinController(dbContext, CreatePrincipal(otherUser));
        var activeMemberController = CreateVereinController(dbContext, CreatePrincipal(activeMember));

        Assert.IsType<UnauthorizedResult>(
            await anonymousController.GetMyMembershipRequestAsync()
        );
        Assert.IsType<UnauthorizedResult>(
            await anonymousController.CreateMembershipRequestAsync(CreateMembershipRequestPayload())
        );
        Assert.IsType<UnauthorizedResult>(
            await anonymousController.WithdrawMembershipRequestAsync(123)
        );

        var created = await InvokeOkAsync<VereinMembershipRequestDto>(
            applicantController.CreateMembershipRequestAsync(CreateMembershipRequestPayload())
        );
        Assert.Equal(EVereinMembershipRequestStatus.Submitted, created.Status);
        Assert.Equal(applicant.PrimaryKey, created.UserFk);

        Assert.IsType<ConflictObjectResult>(
            await applicantController.CreateMembershipRequestAsync(CreateMembershipRequestPayload())
        );
        Assert.IsType<ConflictObjectResult>(
            await activeMemberController.CreateMembershipRequestAsync(CreateMembershipRequestPayload("Active"))
        );
        Assert.IsType<NotFoundResult>(
            await otherController.GetMyMembershipRequestAsync()
        );
        Assert.IsType<ForbidResult>(
            await otherController.WithdrawMembershipRequestAsync(created.PrimaryKey)
        );

        var current = await InvokeOkAsync<VereinMembershipRequestDto>(
            applicantController.GetMyMembershipRequestAsync()
        );
        Assert.Equal(created.PrimaryKey, current.PrimaryKey);

        var withdrawn = await InvokeOkAsync<VereinMembershipRequestDto>(
            applicantController.WithdrawMembershipRequestAsync(created.PrimaryKey)
        );
        Assert.Equal(EVereinMembershipRequestStatus.Withdrawn, withdrawn.Status);
        Assert.NotNull(withdrawn.TimeStampResolved);
        Assert.IsType<BadRequestObjectResult>(
            await applicantController.WithdrawMembershipRequestAsync(created.PrimaryKey)
        );

        var secondRequest = await InvokeOkAsync<VereinMembershipRequestDto>(
            applicantController.CreateMembershipRequestAsync(CreateMembershipRequestPayload("Ada", "Byron"))
        );
        Assert.NotEqual(created.PrimaryKey, secondRequest.PrimaryKey);
        Assert.Equal(2, await dbContext.VereinMembershipRequests.CountAsync(q => q.UserFk == applicant.PrimaryKey));
    }

    [Fact]
    public async Task MembershipRequests_AdminTransitions_CreateMembershipAndKeepAuditDataConcise()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var applicant = CreateUser("applicant");
        var admin = CreateUser("membership-admin");
        var nonAdmin = CreateUser("non-admin");
        dbContext.Users.AddRange(applicant, admin, nonAdmin);
        await dbContext.SaveChangesAsync();

        var applicantController = CreateVereinController(dbContext, CreatePrincipal(applicant));
        var nonAdminController = CreateVereinController(dbContext, CreatePrincipal(nonAdmin));
        var adminController = CreateVereinController(
            dbContext,
            CreatePrincipal(admin, (Claims.Verein.Members, string.Empty))
        );
        var submitted = await InvokeOkAsync<VereinMembershipRequestDto>(
            applicantController.CreateMembershipRequestAsync(
                CreateMembershipRequestPayload(motivation: "Sensitive motivation should stay out of audit metadata")
            )
        );

        Assert.IsType<ForbidResult>(
            await nonAdminController.GetAdminMembershipRequestsAsync()
        );
        Assert.IsType<ForbidResult>(
            await nonAdminController.TransitionMembershipRequestAsync(
                    submitted.PrimaryKey,
                    CreateMembershipTransitionPayload(EVereinMembershipRequestStatus.AcceptedForContact)
                )
        );

        var listed = await InvokeOkAsync<VereinMembershipRequestDto[]>(
            adminController.GetAdminMembershipRequestsAsync(
                EVereinMembershipRequestStatus.Submitted,
                search: "applicant"
            )
        );
        Assert.Single(listed);

        var contactAccepted = await InvokeOkAsync<VereinMembershipRequestDto>(
            adminController.TransitionMembershipRequestAsync(
                submitted.PrimaryKey,
                CreateMembershipTransitionPayload(EVereinMembershipRequestStatus.AcceptedForContact, "Contact details verified")
            )
        );
        Assert.Equal(EVereinMembershipRequestStatus.AcceptedForContact, contactAccepted.Status);
        Assert.False(await dbContext.VereinMemberships.AnyAsync(q => q.UserFk == applicant.PrimaryKey));

        Assert.IsType<BadRequestObjectResult>(
            await adminController.TransitionMembershipRequestAsync(
                    submitted.PrimaryKey,
                    CreateMembershipTransitionPayload(EVereinMembershipRequestStatus.AcceptedForContact, "Duplicate contact transition")
                )
        );

        var approved = await InvokeOkAsync<VereinMembershipRequestDto>(
            adminController.TransitionMembershipRequestAsync(
                submitted.PrimaryKey,
                CreateMembershipTransitionPayload(
                    EVereinMembershipRequestStatus.Approved,
                    "Approved after contact",
                    "UTF-042",
                    "Board approved"
                )
            )
        );
        Assert.Equal(EVereinMembershipRequestStatus.Approved, approved.Status);
        Assert.NotNull(approved.TimeStampResolved);

        var membership = await dbContext.VereinMemberships.SingleAsync(q => q.UserFk == applicant.PrimaryKey);
        Assert.Equal(EVereinMembershipStatus.Active, membership.Status);
        Assert.Equal("UTF-042", membership.MemberNumber);

        var meController = CreateUsersController(dbContext, CreatePrincipal(applicant));
        var me = Assert.IsType<FullUserDto>(Assert.IsType<OkObjectResult>(await meController.GetMeAsync(CancellationToken.None)).Value);
        Assert.True(me.IsVereinMember);
        Assert.Equal("Active", me.VereinMembershipStatus);
        Assert.Equal("UTF-042", me.VereinMemberNumber);

        var requestAuditActions = await dbContext.VereinMembershipRequestAudits
            .OrderBy(q => q.TimeStampCreated)
            .Select(q => q.Action)
            .ToArrayAsync();
        Assert.Contains(EVereinMembershipRequestAuditAction.Created, requestAuditActions);
        Assert.Contains(EVereinMembershipRequestAuditAction.AcceptedForContact, requestAuditActions);
        Assert.Contains(EVereinMembershipRequestAuditAction.Approved, requestAuditActions);
        Assert.Contains(
            EVereinMembershipAuditAction.Created,
            await dbContext.VereinMembershipAudits.Select(q => q.Action).ToArrayAsync()
        );

        var requestAuditData = string.Join(
            "\n",
            await dbContext.VereinMembershipRequestAudits.Select(q => q.Data).ToArrayAsync()
        );
        Assert.DoesNotContain("Sensitive motivation", requestAuditData, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ada", requestAuditData, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lovelace", requestAuditData, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Main Street", requestAuditData, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VereinSettings_PublicDefaultsAndAdminUpdatePersistAudit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var admin = CreateUser("membership-admin");
        var nonAdmin = CreateUser("non-admin");
        dbContext.Users.AddRange(admin, nonAdmin);
        await dbContext.SaveChangesAsync();

        var publicController = CreateVereinController(dbContext, new ClaimsPrincipal(new ClaimsIdentity()));
        var defaults = await InvokeOkAsync<VereinSettingsDto>(publicController.GetSettingsAsync());
        Assert.Null(defaults.MembershipContributionMonthlyAmount);
        Assert.Null(defaults.MembershipContributionAnnualAmount);
        Assert.Equal("EUR", defaults.MembershipContributionCurrency);
        Assert.NotEmpty(defaults.MembershipContributionPublicTextDe);
        Assert.NotEmpty(defaults.MembershipContributionPublicTextEn);

        var nonAdminController = CreateVereinController(dbContext, CreatePrincipal(nonAdmin));
        Assert.IsType<ForbidResult>(await nonAdminController.GetAdminSettingsAsync());
        Assert.IsType<ForbidResult>(
            await nonAdminController.UpdateSettingsAsync(CreateVereinSettingsPayload())
        );

        var adminController = CreateVereinController(
            dbContext,
            CreatePrincipal(admin, (Claims.Verein.Members, string.Empty))
        );
        var updated = await InvokeOkAsync<VereinSettingsDto>(
            adminController.UpdateSettingsAsync(CreateVereinSettingsPayload())
        );

        Assert.Equal(15.50m, updated.MembershipContributionMonthlyAmount);
        Assert.Equal(180m, updated.MembershipContributionAnnualAmount);
        Assert.Equal("EUR", updated.MembershipContributionCurrency);
        Assert.Equal("Die Mitgliedschaft traegt die laufenden Betriebskosten.", updated.MembershipContributionPublicTextDe);
        Assert.Equal("Membership sustains ongoing operating costs.", updated.MembershipContributionPublicTextEn);

        var persisted = await dbContext.VereinSettings.SingleAsync();
        Assert.Equal(VereinSettings.DefaultIdentifier, persisted.Identifier);
        Assert.Equal(admin.PrimaryKey, persisted.UpdatedByFk);
        Assert.Equal(15.50m, persisted.MembershipContributionMonthlyAmount);
        Assert.Equal(180m, persisted.MembershipContributionAnnualAmount);

        var audit = await dbContext.VereinSettingsAudits.SingleAsync();
        Assert.Equal(EVereinSettingsAuditAction.Created, audit.Action);
        Assert.Equal(admin.PrimaryKey, audit.UserForeignKey);
        Assert.DoesNotContain("laufenden Betriebskosten", audit.Data, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ongoing operating costs", audit.Data, StringComparison.OrdinalIgnoreCase);

        var publicSettings = await InvokeOkAsync<VereinSettingsDto>(publicController.GetSettingsAsync());
        Assert.Equal(updated.MembershipContributionMonthlyAmount, publicSettings.MembershipContributionMonthlyAmount);
        Assert.Equal(updated.MembershipContributionAnnualAmount, publicSettings.MembershipContributionAnnualAmount);
        Assert.Equal(updated.MembershipContributionPublicTextEn, publicSettings.MembershipContributionPublicTextEn);
    }

    [Fact]
    public async Task VereinSettings_UpdateRequiresMonthlyAndAnnualAmountsTogether()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var admin = CreateUser("membership-admin");
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync();

        var controller = CreateVereinController(
            dbContext,
            CreatePrincipal(admin, (Claims.Verein.Members, string.Empty))
        );
        var payload = CreateVereinSettingsPayload() with
        {
            MembershipContributionAnnualAmount = null,
        };

        var result = await controller.UpdateSettingsAsync(payload);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(dbContext.VereinSettings);
    }

    private static async Task<T> InvokeOkAsync<T>(Task<IActionResult> actionTask)
    {
        var actionResult = await actionTask;
        return Assert.IsType<T>(Assert.IsType<OkObjectResult>(actionResult).Value);
    }

    private static void AssertPublishedDocumentOmitsAdminMetadata(VereinController.PublishedVereinDocumentDto document)
    {
        var json = JsonSerializer.Serialize(document);
        Assert.DoesNotContain("comment", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorFk", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("revisions", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isDeleted", json, StringComparison.OrdinalIgnoreCase);
    }

    private static VereinController CreateVereinController(DatabaseContext dbContext, ClaimsPrincipal user)
        => new(dbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user,
                },
            },
        };

    private static UsersController CreateUsersController(DatabaseContext dbContext, ClaimsPrincipal user)
        => new(NullLogger<UsersController>.Instance, dbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user,
                },
            },
        };

    private static ClaimsPrincipal CreatePrincipal(User user, params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity(Constants.AuthorizationSchemas.Api);
        identity.AddClaim(new SecurityClaim(ClaimTypes.Name, user.PrimaryKey.ToString()));
        foreach (var (type, value) in claims)
            identity.AddClaim(new SecurityClaim(type, value));
        return new ClaimsPrincipal(identity);
    }

    private static User CreateUser(string nickname)
        => new()
        {
            PrimaryKey     = Guid.NewGuid(),
            Nickname       = nickname,
            AvatarMimeType = string.Empty,
            Discord        = new DiscordUser
            {
                Username = nickname,
            },
            Steam = new SteamUser(),
        };

    private static User CreateUserWithPersonalData(string nickname)
        => new()
        {
            PrimaryKey     = Guid.NewGuid(),
            Nickname       = nickname,
            EMail          = $"{nickname}@example.test",
            FirstName      = "Ada",
            LastName       = "Lovelace",
            DateOfBirth    = new LocalDate(1990, 1, 2),
            PhoneNumber    = "+49 30 123456",
            AddressLine1   = "Main Street 1",
            AddressLine2   = "Apartment 2",
            PostalCode     = "10115",
            City           = "Berlin",
            Country        = "Germany",
            Avatar         = [1, 2, 3],
            AvatarMimeType = "image/png",
            Discord = new DiscordUser
            {
                Id       = CreateStableExternalId(nickname),
                Username = nickname,
            },
            Steam = new SteamUser
            {
                Id64 = CreateStableExternalId(nickname),
            },
        };

    private static ulong CreateStableExternalId(string value)
    {
        var result = 14695981039346656037UL;
        foreach (var character in value)
        {
            result ^= character;
            result *= 1099511628211UL;
        }

        return result;
    }

    private static VereinDocumentRevisionPayload CreateDocumentPayload(string title, bool isPublished)
        => new()
        {
            Title       = title,
            Summary     = $"Summary for {title}",
            Markdown    = $"# {title}\n\nPublished body.",
            Comment     = "test revision",
            IsPublished = isPublished,
        };

    private static VereinMembership CreateMembership(Guid userId, EVereinMembershipStatus status)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        return new VereinMembership
        {
            UserFk           = userId,
            Status           = status,
            TimeStampStarted = now.Plus(Duration.FromDays(-10)),
            TimeStampEnded   = status == EVereinMembershipStatus.Ended ? now : null,
            Notes            = string.Empty,
            TimeStampCreated = now.Plus(Duration.FromDays(-10)),
            TimeStampUpdated = now,
        };
    }

    private static KassenbuchEntry CreateEntry(Guid actorId)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        return new KassenbuchEntry
        {
            TimeStampBooked  = now,
            Direction        = EKassenbuchEntryDirection.Income,
            Amount           = 10m,
            Currency         = "EUR",
            Category         = "Dues",
            Description      = "Membership dues",
            IsDeleted        = false,
            TimeStampCreated = now,
            TimeStampUpdated = now,
            CreatedByFk      = actorId,
            UpdatedByFk      = actorId,
        };
    }

    private static KassenbuchEntryPayload CreateKassenbuchPayload(string description, decimal amount)
        => new()
        {
            TimeStampBooked = NodaSystemClock.Instance.GetCurrentInstant(),
            Direction       = EKassenbuchEntryDirection.Income,
            Amount          = amount,
            Currency        = "EUR",
            Category        = "Dues",
            Description     = description,
        };

    private static VereinSettingsPayload CreateVereinSettingsPayload()
        => new()
        {
            MembershipContributionMonthlyAmount = 15.50m,
            MembershipContributionAnnualAmount  = 180m,
            MembershipContributionCurrency     = "eur",
            MembershipContributionPublicTextDe = "Die Mitgliedschaft traegt die laufenden Betriebskosten.",
            MembershipContributionPublicTextEn = "Membership sustains ongoing operating costs.",
        };

    private static VereinMembershipRequestPayload CreateMembershipRequestPayload(
        string firstName = "Ada",
        string lastName = "Lovelace",
        string motivation = "I want to support formal Verein work.")
        => new()
        {
            FirstName                        = firstName,
            LastName                         = lastName,
            DateOfBirth                      = new LocalDate(1990, 1, 2),
            Email                            = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}@example.test",
            PhoneNumber                      = "+49 30 123456",
            AddressLine1                     = "Main Street 1",
            AddressLine2                     = "Apartment 2",
            PostalCode                       = "10115",
            City                             = "Berlin",
            Country                          = "Germany",
            Motivation                       = motivation,
            AcknowledgesMembershipSeparation = true,
            AcknowledgesDataProcessing       = true,
            AcknowledgesSatzung              = true,
        };

    private static VereinMembershipRequestTransitionPayload CreateMembershipTransitionPayload(
        EVereinMembershipRequestStatus status,
        string reason = "Reviewed by test admin",
        string? memberNumber = null,
        string? notes = null)
        => new()
        {
            Status       = status,
            Reason       = reason,
            MemberNumber = memberNumber,
            Notes        = notes,
        };

    private static async Task<DatabaseContext> CreateDbContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<SqliteApiDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new SqliteApiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private sealed class SqliteApiDbContext(DbContextOptions<SqliteApiDbContext> dbContextOptions)
        : DatabaseContext(dbContextOptions)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {

            configurationBuilder
                .Properties<Instant>()
                .HaveConversion<InstantToLongConverter>();
            configurationBuilder
                .Properties<LocalDate>()
                .HaveConversion<LocalDateToIntConverter>();
        }
    }
}
