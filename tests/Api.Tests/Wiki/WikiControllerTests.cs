using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SecurityClaim = System.Security.Claims.Claim;
using UtfClaims = X39.UnitedTacticalForces.Claims;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Wiki;
using X39.UnitedTacticalForces.Api.DTO;
using Xunit;

namespace Api.Tests.Wiki;

public class WikiControllerTests
{
    [Fact]
    public async Task GetWikiPage_ReturnsLatestRevisionByDefaultAndAllRevisionsWhenRequested()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("wiki-author");
        var page = CreatePage("Doctrine/Signal Discipline", user.PrimaryKey);
        dbContext.Users.Add(user);
        dbContext.WikiPages.Add(page);
        await dbContext.SaveChangesAsync();
        var anonymousController = CreateController(dbContext, CreateAnonymousPrincipal());
        var controller = CreateController(
            dbContext,
            CreatePrincipal(user, (UtfClaims.Wiki.Modify, string.Empty))
        );

        var latest = await InvokeOkAsync<WikiPageDto>(
            anonymousController.GetWikiPageAsync(page.PrimaryKey)
        );
        Assert.IsType<ForbidResult>(
            await anonymousController.GetWikiPageAsync(page.PrimaryKey, allRevisions: true)
        );
        var all = await InvokeOkAsync<WikiPageDto>(
            controller.GetWikiPageAsync(page.PrimaryKey, allRevisions: true)
        );

        var latestRevision = Assert.Single(latest.Revisions ?? []);
        Assert.Equal("Updated Markdown", latestRevision.Markdown);
        Assert.Null(latestRevision.Author);
        Assert.Equal(string.Empty, latestRevision.Comment);
        Assert.Equal(2, all.Revisions?.Length);
        Assert.Equal("update", all.Revisions![0].Comment);
        Assert.Equal(
            ["Updated Markdown", "Initial Markdown"],
            all.Revisions!.Select(q => q.Markdown).ToArray()
        );
    }

    [Fact]
    public async Task GetAllWikiPageHeaders_FiltersDeletedPagesUnlessRequested()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("wiki-author");
        dbContext.Users.Add(user);
        dbContext.WikiPages.Add(CreatePage("Doctrine/Active", user.PrimaryKey));
        dbContext.WikiPages.Add(CreatePage("Doctrine/Archived", user.PrimaryKey, isDeleted: true));
        await dbContext.SaveChangesAsync();
        var anonymousController = CreateController(dbContext, CreateAnonymousPrincipal());
        var controller = CreateController(
            dbContext,
            CreatePrincipal(user, (UtfClaims.Wiki.Modify, string.Empty))
        );

        var activeOnly = Assert.IsType<WikiPageHeader[]>(
            Assert.IsType<OkObjectResult>(await anonymousController.GetAllWikiPageHeadersAsync()).Value
        );
        Assert.IsType<ForbidResult>(
            await anonymousController.GetAllWikiPageHeadersAsync(showDeleted: true)
        );
        var withDeleted = Assert.IsType<WikiPageHeader[]>(
            Assert.IsType<OkObjectResult>(
                await controller.GetAllWikiPageHeadersAsync(showDeleted: true)
            ).Value
        );

        Assert.Single(activeOnly);
        Assert.Equal("Doctrine/Active", activeOnly[0].Name);
        Assert.Equal(["Doctrine/Active", "Doctrine/Archived"], withDeleted.Select(q => q.Name).ToArray());
    }

    [Fact]
    public async Task WikiRevisionHistory_RequiresWikiPermission()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("wiki-author");
        var page = CreatePage("Doctrine/Signal Discipline", user.PrimaryKey);
        dbContext.Users.Add(user);
        dbContext.WikiPages.Add(page);
        await dbContext.SaveChangesAsync();
        var revisionId = page.Revisions!.OrderBy(q => q.TimeStampCreated).First().PrimaryKey;
        var anonymousController = CreateController(dbContext, CreateAnonymousPrincipal());
        var controller = CreateController(
            dbContext,
            CreatePrincipal(user, (UtfClaims.Wiki.Modify, string.Empty))
        );

        Assert.IsType<ForbidResult>(
            await anonymousController.GetWikiPageRevisionHeadersAsync(page.PrimaryKey)
        );
        Assert.IsType<ForbidResult>(
            await anonymousController.GetWikiPageRevisionAsync(page.PrimaryKey, revisionId)
        );

        var headers = await InvokeOkAsync<WikiPageRevisionHeader[]>(
            controller.GetWikiPageRevisionHeadersAsync(page.PrimaryKey)
        );
        var revision = await InvokeOkAsync<WikiPageRevisionDto>(
            controller.GetWikiPageRevisionAsync(page.PrimaryKey, revisionId)
        );

        Assert.Equal(2, headers.Length);
        Assert.Equal("Initial Markdown", revision.Markdown);
    }

    [Fact]
    public async Task GetWikiPage_HidesDeletedPagesFromAnonymousUsers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("wiki-author");
        var page = CreatePage("Doctrine/Archived", user.PrimaryKey, isDeleted: true);
        dbContext.Users.Add(user);
        dbContext.WikiPages.Add(page);
        await dbContext.SaveChangesAsync();
        var anonymousController = CreateController(dbContext, CreateAnonymousPrincipal());
        var controller = CreateController(
            dbContext,
            CreatePrincipal(user, (UtfClaims.Wiki.Modify, string.Empty))
        );

        Assert.IsType<NotFoundResult>(await anonymousController.GetWikiPageAsync(page.PrimaryKey));
        var archivedPage = await InvokeOkAsync<WikiPageDto>(controller.GetWikiPageAsync(page.PrimaryKey));

        Assert.True(archivedPage.IsDeleted);
    }

    [Fact]
    public async Task MutationsRequireApiIdentityAndPersistAuditRecords()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("wiki-editor");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var unauthorizedController = CreateController(
            dbContext,
            CreateNonApiPrincipal((UtfClaims.Creation.Wiki, string.Empty))
        );
        Assert.IsType<UnauthorizedResult>(
            await unauthorizedController.CreateWikiPageAsync("New page", "Markdown", "initial")
        );

        var controller = CreateController(
            dbContext,
            CreatePrincipal(
                user,
                (UtfClaims.Creation.Wiki, string.Empty),
                (UtfClaims.Wiki.Modify, string.Empty),
                (UtfClaims.Wiki.Rename, string.Empty),
                (UtfClaims.Wiki.Delete, string.Empty),
                (UtfClaims.Administrative.Wiki, string.Empty)
            )
        );
        var created = await InvokeOkAsync<WikiPageDto>(
            controller.CreateWikiPageAsync("Doctrine/Test", "Initial", "initial")
        );
        await InvokeOkAsync<WikiPageRevisionDto>(
            controller.CreateWikiPageRevisionAsync(created.PrimaryKey, "Updated", "update")
        );
        var renamed = await InvokeOkAsync<WikiPageDto>(
            controller.RenameWikiPageAsync(created.PrimaryKey, "Doctrine/Renamed")
        );
        Assert.Equal("Doctrine/Renamed", renamed.Title);
        Assert.IsType<NoContentResult>(await controller.DeleteWikiPageAsync(created.PrimaryKey));
        Assert.IsType<NoContentResult>(await controller.RestoreWikiPageAsync(created.PrimaryKey));

        var page = await dbContext.WikiPages.SingleAsync(q => q.PrimaryKey == created.PrimaryKey);
        Assert.False(page.IsDeleted);

        var auditActions = await dbContext.WikiPageAudits
            .OrderBy(q => q.TimeStampCreated)
            .Select(q => q.Action)
            .ToArrayAsync();
        Assert.Contains(EWikiPageAuditAction.Created, auditActions);
        Assert.Contains(EWikiPageAuditAction.TitleChanged, auditActions);
        Assert.Contains(EWikiPageAuditAction.Deleted, auditActions);
        Assert.Contains(EWikiPageAuditAction.Restored, auditActions);
    }

    private static async Task<T> InvokeOkAsync<T>(Task<IActionResult> actionTask)
    {
        var actionResult = await actionTask;
        return Assert.IsType<T>(Assert.IsType<OkObjectResult>(actionResult).Value);
    }

    private static async Task<T> InvokeOkAsync<T>(Task<ActionResult<WikiPageRevision>> actionTask)
    {
        var actionResult = await actionTask;
        return Assert.IsType<T>(Assert.IsType<OkObjectResult>(actionResult.Result).Value);
    }

    private static WikiController CreateController(DatabaseContext dbContext, ClaimsPrincipal user)
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

    private static ClaimsPrincipal CreateNonApiPrincipal(params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity("Test");
        foreach (var (type, value) in claims)
            identity.AddClaim(new SecurityClaim(type, value));
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateAnonymousPrincipal()
        => new(new ClaimsIdentity());

    private static WikiPage CreatePage(string title, Guid authorId, bool isDeleted = false)
    {
        var pageId = Guid.NewGuid();
        var createdAt = NodaSystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(-2));
        return new WikiPage
        {
            PrimaryKey       = pageId,
            Title            = title,
            IsDeleted        = isDeleted,
            TimeStampCreated = createdAt,
            Revisions =
            [
                new WikiPageRevision
                {
                    PrimaryKey       = Guid.NewGuid(),
                    PageForeignKey   = pageId,
                    AuthorForeignKey = authorId,
                    TimeStampCreated = createdAt,
                    Markdown         = "Initial Markdown",
                    Comment          = "initial",
                },
                new WikiPageRevision
                {
                    PrimaryKey       = Guid.NewGuid(),
                    PageForeignKey   = pageId,
                    AuthorForeignKey = authorId,
                    TimeStampCreated = createdAt.Plus(Duration.FromDays(1)),
                    Markdown         = "Updated Markdown",
                    Comment          = "update",
                },
            ],
        };
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
