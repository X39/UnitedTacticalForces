using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SecurityClaim = System.Security.Claims.Claim;
using X39.UnitedTacticalForces;
using X39.UnitedTacticalForces.Api;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Authority.Extern;
using X39.UnitedTacticalForces.Database.Media;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.Media;
using Xunit;

namespace Api.Tests.FieldReports;

public class FieldReportsControllerTests
{
    [Fact]
    public async Task Controller_CanBeActivatedByDependencyInjection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var storageRoot = CreateTemporaryStorageRoot();
        try
        {
            var webHostEnvironment = new TestWebHostEnvironment(storageRoot);
            var services = new ServiceCollection()
                .AddSingleton<DatabaseContext>(dbContext)
                .AddSingleton<IWebHostEnvironment>(webHostEnvironment)
                .AddSingleton<IMediaAssetService>(new MediaAssetService(dbContext, webHostEnvironment))
                .BuildServiceProvider();

            var controller = ActivatorUtilities.CreateInstance<FieldReportsController>(services);

            Assert.NotNull(controller);
        }
        finally
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task PublicEndpoints_ReturnOnlyPublishedNonDeletedReports()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("report-owner");
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        dbContext.Users.Add(user);
        dbContext.FieldReports.AddRange(
            CreateReport("published-report", "Published report", user.PrimaryKey, now.Plus(Duration.FromDays(-2)), isPublished: true),
            CreateReport("draft-report", "Draft report", user.PrimaryKey, now.Plus(Duration.FromDays(-1)), isPublished: false),
            CreateReport("deleted-report", "Deleted report", user.PrimaryKey, now.Plus(Duration.FromDays(-3)), isPublished: true, isDeleted: true),
            CreateReport("future-report", "Future report", user.PrimaryKey, now.Plus(Duration.FromDays(7)), isPublished: true)
        );
        await dbContext.SaveChangesAsync();

        var controller = new FieldReportsController(dbContext);

        var listResult = await controller.GetPublishedFieldReportsAsync();
        var reports = Assert.IsType<FieldReportSummaryDto[]>(Assert.IsType<OkObjectResult>(listResult).Value);
        var report = Assert.Single(reports);
        Assert.Equal("published-report", report.Slug);

        var detailResult = await controller.GetPublishedFieldReportAsync("published-report");
        Assert.Equal("Published report", Assert.IsType<FieldReportDto>(Assert.IsType<OkObjectResult>(detailResult).Value).Title);
        Assert.IsType<NotFoundResult>(await controller.GetPublishedFieldReportAsync("draft-report"));
        Assert.IsType<NotFoundResult>(await controller.GetPublishedFieldReportAsync("deleted-report"));
        Assert.IsType<NotFoundResult>(await controller.GetPublishedFieldReportAsync("future-report"));
    }

    [Fact]
    public async Task AdminLifecycle_PersistsStateAndAuditRecords()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("report-admin");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, CreatePrincipal(user, (Claims.Administrative.Wiki, string.Empty)));

        var createDto = await InvokeOkAsync<CommandCenterFieldReportDto>(
            controller.CreateFieldReportAsync(CreatePayload("First report", isPublished: false))
        );

        Assert.False(createDto.IsPublished);
        Assert.False(createDto.IsDeleted);
        Assert.Equal("first-report", createDto.Slug);

        var updateDto = await InvokeOkAsync<CommandCenterFieldReportDto>(
            controller.UpdateFieldReportAsync(
                createDto.PrimaryKey,
                CreatePayload("Updated report", slug: "Updated Report!", isPublished: true)
            )
        );
        Assert.True(updateDto.IsPublished);
        Assert.Equal("updated-report", updateDto.Slug);
        Assert.NotNull(updateDto.TimeStampPublished);

        var unpublishDto = await InvokeOkAsync<CommandCenterFieldReportDto>(
            controller.UnpublishFieldReportAsync(createDto.PrimaryKey)
        );
        Assert.False(unpublishDto.IsPublished);
        Assert.Null(unpublishDto.TimeStampPublished);

        var deleteDto = await InvokeOkAsync<CommandCenterFieldReportDto>(
            controller.DeleteFieldReportAsync(createDto.PrimaryKey)
        );
        Assert.True(deleteDto.IsDeleted);

        var restoreDto = await InvokeOkAsync<CommandCenterFieldReportDto>(
            controller.RestoreFieldReportAsync(createDto.PrimaryKey)
        );
        Assert.False(restoreDto.IsDeleted);

        var auditActions = await dbContext.FieldReportAudits
            .OrderBy(audit => audit.TimeStampCreated)
            .Select(audit => audit.Action)
            .ToArrayAsync();
        Assert.Contains(EFieldReportAuditAction.Created, auditActions);
        Assert.Contains(EFieldReportAuditAction.Updated, auditActions);
        Assert.Contains(EFieldReportAuditAction.Published, auditActions);
        Assert.Contains(EFieldReportAuditAction.Unpublished, auditActions);
        Assert.Contains(EFieldReportAuditAction.Deleted, auditActions);
        Assert.Contains(EFieldReportAuditAction.Restored, auditActions);
    }

    [Fact]
    public async Task AdminCreate_PublishedReportIsReadableFromPublicEndpoints()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("report-admin");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, CreatePrincipal(user, (Claims.Administrative.Wiki, string.Empty)));

        var createDto = await InvokeOkAsync<CommandCenterFieldReportDto>(
            controller.CreateFieldReportAsync(CreatePayload("Created public report", isPublished: true))
        );

        var listResult = await controller.GetPublishedFieldReportsAsync();
        var reports = Assert.IsType<FieldReportSummaryDto[]>(Assert.IsType<OkObjectResult>(listResult).Value);
        var report = Assert.Single(reports);
        Assert.Equal(createDto.Slug, report.Slug);
        Assert.Equal("Created public report", report.Title);

        var detailResult = await controller.GetPublishedFieldReportAsync(createDto.Slug);
        var detail = Assert.IsType<FieldReportDto>(Assert.IsType<OkObjectResult>(detailResult).Value);
        Assert.Equal(createDto.PrimaryKey, detail.PrimaryKey);
        Assert.Equal("## Created public report\n\nReport body.", detail.Markdown);
    }

    [Fact]
    public async Task AdminRead_ReturnsEditableReportForAuthorizedUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("report-editor");
        var report = CreateReport(
            "editable-draft",
            "Editable draft",
            user.PrimaryKey,
            NodaSystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(-1)),
            isPublished: false
        );
        dbContext.Users.Add(user);
        dbContext.FieldReports.Add(report);
        dbContext.FieldReportAudits.Add(new FieldReportAudit
        {
            FieldReportFk    = report.PrimaryKey,
            UserForeignKey   = user.PrimaryKey,
            Action           = EFieldReportAuditAction.Created,
            Data             = "{}",
            TimeStampCreated = NodaSystemClock.Instance.GetCurrentInstant(),
        });
        await dbContext.SaveChangesAsync();
        var controller = CreateController(dbContext, CreatePrincipal(user, (Claims.Administrative.Wiki, string.Empty)));

        var dto = await InvokeOkAsync<CommandCenterFieldReportDto>(
            controller.GetCommandCenterFieldReportAsync(report.PrimaryKey)
        );

        Assert.Equal(report.PrimaryKey, dto.PrimaryKey);
        Assert.Equal("editable-draft", dto.Slug);
        Assert.Equal("Editable draft", dto.Title);
        Assert.False(dto.IsPublished);
        Assert.Equal(1, dto.AuditRecordCount);
    }

    [Fact]
    public async Task AdminRead_RejectsMissingAndUnauthorizedReports()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("report-editor");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, CreatePrincipal(user, (Claims.Administrative.Wiki, string.Empty)));
        Assert.IsType<NotFoundResult>(await controller.GetCommandCenterFieldReportAsync(Guid.NewGuid()));

        var unauthorizedController = CreateController(dbContext, CreatePrincipal(user));
        Assert.IsType<ForbidResult>(await unauthorizedController.GetCommandCenterFieldReportAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AdminCreate_NormalizesSlugRejectsDuplicatesAndRequiresApiIdentity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var user = CreateUser("report-admin");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var unauthorizedController = CreateController(
            dbContext,
            CreateNonApiPrincipal((Claims.Administrative.Wiki, string.Empty))
        );
        Assert.IsType<UnauthorizedResult>(
            await unauthorizedController.CreateFieldReportAsync(CreatePayload("Unauthenticated report"))
        );

        var controller = CreateController(dbContext, CreatePrincipal(user, (Claims.Administrative.Wiki, string.Empty)));
        var createDto = await InvokeOkAsync<CommandCenterFieldReportDto>(
            controller.CreateFieldReportAsync(CreatePayload("Slug Test!!"))
        );
        Assert.Equal("slug-test", createDto.Slug);

        Assert.IsType<ConflictResult>(
            await controller.CreateFieldReportAsync(CreatePayload("Duplicate slug", slug: "slug test"))
        );
    }

    [Fact]
    public async Task UploadHeroImage_StoresFileAndReturnsReadableRoute()
    {
        var storageRoot = CreateTemporaryStorageRoot();
        try
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var dbContext = await CreateDbContextAsync(connection);
            var user = CreateUser("report-admin");
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            var controller = CreateController(
                dbContext,
                CreatePrincipal(user, (Claims.Administrative.Wiki, string.Empty)),
                storageRoot
            );

            var uploadDto = await InvokeOkAsync<FieldReportImageUploadDto>(
                controller.UploadHeroImageAsync(
                    CreateFormFile("field-image.png", "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
                )
            );

            Assert.Equal("image/png", uploadDto.ContentType);
            Assert.Equal(8, uploadDto.Size);
            Assert.StartsWith("media-assets/", uploadDto.ImagePath, StringComparison.Ordinal);
            Assert.EndsWith("/content", uploadDto.ImagePath, StringComparison.Ordinal);

            var mediaAsset = Assert.Single(
                await dbContext.MediaAssets
                    .Include(q => q.Tags)
                    .ToArrayAsync()
            );
            Assert.Equal(uploadDto.ImagePath, IMediaAssetService.CreateImagePath(mediaAsset.PrimaryKey));
            Assert.Contains(mediaAsset.Tags ?? [], tag => tag.Value == "field-report");

            var storedPath = Path.Combine(storageRoot, "App_Data", "media-assets", mediaAsset.StorageFileName);
            Assert.True(File.Exists(storedPath));

            var mediaController = new MediaAssetsController(new MediaAssetService(dbContext, new TestWebHostEnvironment(storageRoot)))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };
            var readResult = Assert.IsType<PhysicalFileResult>(
                await mediaController.GetMediaAssetContentAsync(mediaAsset.PrimaryKey)
            );
            Assert.Equal("image/png", readResult.ContentType);
            Assert.Equal(Path.GetFullPath(storedPath), readResult.FileName);

            var createDto = await InvokeOkAsync<CommandCenterFieldReportDto>(
                controller.CreateFieldReportAsync(CreatePayload("Uploaded image report", heroImageAssetFk: uploadDto.ImageAssetFk))
            );
            Assert.Equal(uploadDto.ImagePath, createDto.HeroImagePath);
        }
        finally
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task UploadHeroImage_RejectsUnsupportedTypeAndOversizedFile()
    {
        var storageRoot = CreateTemporaryStorageRoot();
        try
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var dbContext = await CreateDbContextAsync(connection);
            var user = CreateUser("report-admin");
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            var controller = CreateController(
                dbContext,
                CreatePrincipal(user, (Claims.Administrative.Wiki, string.Empty)),
                storageRoot
            );

            Assert.IsType<BadRequestObjectResult>(
                await controller.UploadHeroImageAsync(CreateFormFile("field-image.svg", "image/svg+xml", [0x3C, 0x73]))
            );
            Assert.IsType<BadRequestObjectResult>(
                await controller.UploadHeroImageAsync(
                    CreateFormFile("field-image.png", "image/png", [0x89], length: 10L * 1024L * 1024L + 1)
                )
            );
            Assert.IsType<NotFoundResult>(controller.GetHeroImage("../outside.png"));
        }
        finally
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    private static async Task<T> InvokeOkAsync<T>(Task<IActionResult> actionTask)
    {
        var actionResult = await actionTask;
        return Assert.IsType<T>(Assert.IsType<OkObjectResult>(actionResult).Value);
    }

    private static FieldReportsController CreateController(
        DatabaseContext dbContext,
        ClaimsPrincipal user,
        string? storageRoot = null)
    {
        var webHostEnvironment = storageRoot is null ? null : new TestWebHostEnvironment(storageRoot);
        var mediaAssetService = webHostEnvironment is null ? null : new MediaAssetService(dbContext, webHostEnvironment);
        return new FieldReportsController(dbContext, mediaAssetService, webHostEnvironment)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user,
                },
            },
        };
    }

    private static IFormFile CreateFormFile(
        string fileName,
        string contentType,
        byte[] content,
        long? length = null)
        => new FormFile(new MemoryStream(content), 0, length ?? content.Length, "file", fileName)
        {
            Headers     = new HeaderDictionary(),
            ContentType = contentType,
        };

    private static string CreateTemporaryStorageRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"utf-field-report-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

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

    private static FieldReportUpsertPayload CreatePayload(
        string title,
        string? slug = null,
        bool isPublished = false,
        Guid? heroImageAssetFk = null)
        => new()
        {
            Slug             = slug,
            Title            = title,
            Excerpt          = $"Excerpt for {title}",
            Category         = "After Action",
            Markdown         = $"## {title}\n\nReport body.",
            HeroImageAssetFk = heroImageAssetFk,
            HeroImageAlt     = title,
            SortOrder        = 10,
            IsPublished      = isPublished,
        };

    private static FieldReport CreateReport(
        string slug,
        string title,
        Guid userId,
        Instant publishedAt,
        bool isPublished,
        bool isDeleted = false)
        => new()
        {
            PrimaryKey         = Guid.NewGuid(),
            Slug               = slug,
            Title              = title,
            Excerpt            = $"Excerpt for {title}",
            Category           = "After Action",
            Markdown           = $"## {title}",
            HeroImageAlt       = title,
            IsFeatured         = slug == "published-report",
            SortOrder          = 10,
            IsPublished        = isPublished,
            IsDeleted          = isDeleted,
            TimeStampPublished = isPublished ? publishedAt : null,
            TimeStampCreated   = publishedAt.Plus(Duration.FromDays(-1)),
            TimeStampUpdated   = publishedAt.Plus(Duration.FromDays(-1)),
            CreatedByFk        = userId,
            UpdatedByFk        = userId,
        };

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

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Api.Tests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = "Development";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
