using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SecurityClaim = System.Security.Claims.Claim;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Content;
using X39.UnitedTacticalForces.Database.Media;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads.PublicPages;
using X39.UnitedTacticalForces.Common.PublicPages;
using Xunit;

namespace Api.Tests.PublicPages;

public class PublicPagesControllerTests
{
    private static readonly Guid DefaultHeroImageAssetId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task PublishedPage_ReturnsLatestPublishedRevisionAndHidesDrafts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var admin = CreateUser("content-admin");
        dbContext.Users.Add(admin);
        AddDefaultHeroImage(dbContext);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(
            dbContext,
            CreatePrincipal(admin, (Claims.Content.Pages, string.Empty))
        );

        await InvokeOkAsync<PublicPageDto>(
            controller.CreatePageRevisionAsync("gdpr", CreatePayload("Draft GDPR", isPublished: false))
        );
        Assert.IsType<NotFoundResult>(await new PublicPagesController(dbContext).GetPublishedPageAsync("gdpr"));

        await InvokeOkAsync<PublicPageDto>(
            controller.CreatePageRevisionAsync("gdpr", CreatePayload("Published GDPR", isPublished: true))
        );
        await InvokeOkAsync<PublicPageDto>(
            controller.CreatePageRevisionAsync("gdpr", CreatePayload("Latest GDPR", isPublished: true))
        );

        var published = await InvokeOkAsync<PublicPagesController.PublishedPublicPageDto>(
            new PublicPagesController(dbContext).GetPublishedPageAsync("gdpr")
        );

        Assert.Equal("Latest GDPR", published.Revision?.Title);
        Assert.Equal(3, published.Revision?.Version);
        AssertPublicPageOmitsAdminMetadata(published);
    }

    [Fact]
    public async Task AdminEndpoints_RequireContentPagesClaimOrGlobalAdministration()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("user");
        var admin = CreateUser("global-admin");
        dbContext.Users.AddRange(user, admin);
        AddDefaultHeroImage(dbContext);
        await dbContext.SaveChangesAsync();

        var unauthorizedController = CreateController(dbContext, CreatePrincipal(user));
        var adminController = CreateController(
            dbContext,
            CreatePrincipal(admin, (Claims.Administrative.All, string.Empty))
        );

        Assert.IsType<ForbidResult>(await unauthorizedController.GetAdminPagesAsync());
        Assert.IsType<ForbidResult>(
            await unauthorizedController.CreatePageRevisionAsync("gdpr", CreatePayload("Blocked"))
        );

        var created = await InvokeOkAsync<PublicPageDto>(
            adminController.CreatePageRevisionAsync("gdpr", CreatePayload("Allowed"))
        );
        Assert.Equal("gdpr", created.Slug);
    }

    [Fact]
    public async Task CreatingRevisions_IncrementsVersionAndWritesAudit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var admin = CreateUser("content-admin");
        dbContext.Users.Add(admin);
        AddDefaultHeroImage(dbContext);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(
            dbContext,
            CreatePrincipal(admin, (Claims.Content.Pages, string.Empty))
        );

        var first = await InvokeOkAsync<PublicPageDto>(
            controller.CreatePageRevisionAsync("gdpr", CreatePayload("First", isPublished: false))
        );
        var second = await InvokeOkAsync<PublicPageDto>(
            controller.CreatePageRevisionAsync("gdpr", CreatePayload("Second", isPublished: false))
        );

        Assert.Equal(1, first.Revision?.Version);
        Assert.Equal(2, second.Revision?.Version);
        Assert.Equal([2, 1], second.Revisions.Select(q => q.Version).ToArray());

        var auditActions = await dbContext.PublicPageAudits
            .OrderBy(q => q.TimeStampCreated)
            .Select(q => q.Action)
            .ToArrayAsync();
        Assert.Contains(EPublicPageAuditAction.Created, auditActions);
        Assert.Contains(EPublicPageAuditAction.RevisionCreated, auditActions);
    }

    [Fact]
    public async Task PublishAndUnpublish_UpdateVisibilityAndWriteAudit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var admin = CreateUser("content-admin");
        dbContext.Users.Add(admin);
        AddDefaultHeroImage(dbContext);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(
            dbContext,
            CreatePrincipal(admin, (Claims.Content.Pages, string.Empty))
        );

        var draft = await InvokeOkAsync<PublicPageDto>(
            controller.CreatePageRevisionAsync("gdpr", CreatePayload("Publishable GDPR", isPublished: false))
        );
        var revisionId = draft.Revision!.PrimaryKey;

        var published = await InvokeOkAsync<PublicPageDto>(
            controller.PublishPageRevisionAsync(revisionId)
        );
        Assert.True(published.Revision?.IsPublished);
        Assert.NotNull(published.Revision?.TimeStampPublished);

        var publicPage = await InvokeOkAsync<PublicPagesController.PublishedPublicPageDto>(
            new PublicPagesController(dbContext).GetPublishedPageAsync("gdpr")
        );
        Assert.Equal("Publishable GDPR", publicPage.Revision?.Title);

        var unpublished = await InvokeOkAsync<PublicPageDto>(
            controller.UnpublishPageRevisionAsync(revisionId)
        );
        Assert.False(unpublished.Revision?.IsPublished);
        Assert.Null(unpublished.Revision?.TimeStampPublished);
        Assert.IsType<NotFoundResult>(await new PublicPagesController(dbContext).GetPublishedPageAsync("gdpr"));

        var auditActions = await dbContext.PublicPageAudits
            .OrderBy(q => q.TimeStampCreated)
            .Select(q => q.Action)
            .ToArrayAsync();
        Assert.Contains(EPublicPageAuditAction.Published, auditActions);
        Assert.Contains(EPublicPageAuditAction.Unpublished, auditActions);
    }

    [Fact]
    public async Task CreatingGdprRevision_CanonicalizesStructuredContent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var admin = CreateUser("content-admin");
        dbContext.Users.Add(admin);
        AddDefaultHeroImage(dbContext);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(
            dbContext,
            CreatePrincipal(admin, (Claims.Content.Pages, string.Empty))
        );

        var created = await InvokeOkAsync<PublicPageDto>(
            controller.CreatePageRevisionAsync(
                "gdpr",
                CreatePayload("Structured GDPR") with
                {
                    ContentJson = """
                                  {
                                    "intro": {
                                      "title": null,
                                      "description": null
                                    },
                                    "openingCards": [
                                      {
                                        "title": null,
                                        "paragraphs": [
                                          null,
                                          "Kept paragraph"
                                        ]
                                      }
                                    ]
                                  }
                                  """,
                }
            )
        );

        var contentJson = created.Revision!.ContentJson;
        Assert.DoesNotContain("null", contentJson, StringComparison.OrdinalIgnoreCase);
        Assert.True(GdprPageContentJson.TryDeserialize(contentJson, out var content));
        Assert.Equal(string.Empty, content.Intro.Title);
        Assert.Equal(string.Empty, content.Intro.Description);
        Assert.Equal(string.Empty, content.OpeningCards[0].Title);
        Assert.Equal(string.Empty, content.OpeningCards[0].Paragraphs[0]);
        Assert.Equal("Kept paragraph", content.OpeningCards[0].Paragraphs[1]);
    }

    [Fact]
    public async Task CreatingGdprRevision_RejectsInvalidStructuredContent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var admin = CreateUser("content-admin");
        dbContext.Users.Add(admin);
        AddDefaultHeroImage(dbContext);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(
            dbContext,
            CreatePrincipal(admin, (Claims.Content.Pages, string.Empty))
        );

        var result = await controller.CreatePageRevisionAsync(
            "gdpr",
            CreatePayload("Invalid GDPR") with
            {
                ContentJson = "{ invalid json",
            }
        );

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid structured page content.", badRequest.Value);
    }

    [Fact]
    public async Task CreatingRevision_RejectsUnknownHeroImageAsset()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var admin = CreateUser("content-admin");
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(
            dbContext,
            CreatePrincipal(admin, (Claims.Content.Pages, string.Empty))
        );

        var result = await controller.CreatePageRevisionAsync(
            "gdpr",
            CreatePayload("Unsafe Hero") with
            {
                HeroImageAssetFk = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            }
        );

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Hero image asset does not exist.", badRequest.Value);
    }

    private static async Task<T> InvokeOkAsync<T>(Task<IActionResult> actionTask)
    {
        var actionResult = await actionTask;
        return Assert.IsType<T>(Assert.IsType<OkObjectResult>(actionResult).Value);
    }

    private static void AssertPublicPageOmitsAdminMetadata(PublicPagesController.PublishedPublicPageDto page)
    {
        var json = JsonSerializer.Serialize(page);
        Assert.DoesNotContain("comment", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorFk", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("revisions", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isDeleted", json, StringComparison.OrdinalIgnoreCase);
    }

    private static PublicPagesController CreateController(DatabaseContext dbContext, ClaimsPrincipal user)
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
            Discord = new DiscordUser
            {
                Username = nickname,
            },
            Steam = new SteamUser(),
        };

    private static PublicPageRevisionPayload CreatePayload(string title, bool isPublished = true)
        => new()
        {
            Title           = title,
            Summary         = $"Summary for {title}",
            HeroKicker      = "Privacy",
            HeroTitle       = title,
            HeroDescription = "Hero description",
            HeroImageAssetFk = DefaultHeroImageAssetId,
            Markdown        = $"# {title}\n\nPublished body.",
            ContentJson     = CreateGdprContentJson(title),
            Comment         = "test revision",
            IsPublished     = isPublished,
        };

    private static void AddDefaultHeroImage(DatabaseContext dbContext)
        => dbContext.MediaAssets.Add(new MediaAsset
        {
            PrimaryKey       = DefaultHeroImageAssetId,
            OriginalFileName = "gdpr-privacy-hero.png",
            ContentType      = "image/png",
            Size             = 128,
            StorageFileName  = "gdpr-privacy-hero.png",
            Sha256Hash       = new string('1', 64),
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
            TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant(),
        });

    private static string CreateGdprContentJson(string title)
        => GdprPageContentJson.Serialize(new GdprPageContent
        {
            Intro = new GdprIntroSection
            {
                Title       = title,
                Description = $"Structured body for {title}.",
            },
        });

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
