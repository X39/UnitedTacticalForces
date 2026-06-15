using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.FileProviders;
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
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.Media;
using Xunit;

namespace Api.Tests.FieldReports;

public class MediaAssetsControllerTests
{
    [Fact]
    public async Task UploadListTagAndUsageFilters_ReusesDuplicateContentAndProjectsKnownUsage()
    {
        var storageRoot = CreateTemporaryStorageRoot();
        try
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var dbContext = await CreateDbContextAsync(connection);
            var user = CreateUser("gallery-admin");
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var controller = CreateController(
                dbContext,
                storageRoot,
                CreatePrincipal(user, (Claims.Administrative.Wiki, string.Empty))
            );

            var firstUpload = await InvokeOkAsync<MediaAssetDto>(
                controller.UploadMediaAssetAsync(
                    CreateFormFile("briefing.png", "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
                    "field-report; briefing"
                )
            );
            var duplicateUpload = await InvokeOkAsync<MediaAssetDto>(
                controller.UploadMediaAssetAsync(
                    CreateFormFile("briefing-copy.png", "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
                    "duplicate"
                )
            );
            var invalidUpload = await controller.UploadMediaAssetAsync(
                CreateFormFile("corrupt.webp", "image/webp", new byte[10 * 1024]),
                null
            );

            Assert.Equal(firstUpload.PrimaryKey, duplicateUpload.PrimaryKey);
            Assert.IsType<BadRequestObjectResult>(invalidUpload);
            Assert.StartsWith("media-assets/", firstUpload.ImagePath, StringComparison.Ordinal);
            Assert.DoesNotContain(storageRoot, firstUpload.ImagePath, StringComparison.OrdinalIgnoreCase);
            Assert.Single(Directory.GetFiles(Path.Combine(storageRoot, "App_Data", "media-assets"), "*.png"));
            Assert.Contains("duplicate", duplicateUpload.Tags);
            Assert.Contains("field-report", duplicateUpload.Tags);

            var now = NodaSystemClock.Instance.GetCurrentInstant();
            dbContext.FieldReports.Add(new FieldReport
            {
                PrimaryKey         = Guid.NewGuid(),
                Slug               = "briefing-report",
                Title              = "Briefing report",
                Excerpt            = "Reusable media report.",
                Category           = "After Action",
                Markdown           = "## Briefing report",
                HeroImageAssetFk   = firstUpload.PrimaryKey,
                HeroImageAlt       = "Briefing",
                SortOrder          = 10,
                IsPublished        = true,
                TimeStampPublished = now,
                TimeStampCreated   = now,
                TimeStampUpdated   = now,
                CreatedByFk        = user.PrimaryKey,
                UpdatedByFk        = user.PrimaryKey,
            });
            dbContext.PublicPages.Add(new PublicPage
            {
                Slug             = "gdpr",
                TimeStampCreated = now,
                TimeStampUpdated = now,
                CreatedByFk      = user.PrimaryKey,
                UpdatedByFk      = user.PrimaryKey,
                Revisions =
                [
                    new PublicPageRevision
                    {
                        Version              = 1,
                        Title                = "Privacy",
                        Summary              = "Privacy summary",
                        HeroKicker           = "Privacy",
                        HeroTitle            = "Privacy",
                        HeroDescription      = "Privacy description",
                        HeroImageAssetFk     = firstUpload.PrimaryKey,
                        Markdown             = "## Privacy",
                        Comment              = "Initial",
                        IsPublished          = true,
                        TimeStampPublished   = now,
                        TimeStampCreated     = now,
                        AuthorFk             = user.PrimaryKey,
                    },
                ],
            });
            await dbContext.SaveChangesAsync();

            var byTag = await InvokeOkAsync<MediaAssetDto[]>(
                controller.GetMediaAssetsAsync(tags: ["field-report"])
            );
            Assert.Single(byTag);

            var excludingTag = await InvokeOkAsync<MediaAssetDto[]>(
                controller.GetMediaAssetsAsync(excludeTags: ["field-report"])
            );
            Assert.Empty(excludingTag);

            var used = Assert.Single(
                await InvokeOkAsync<MediaAssetDto[]>(controller.GetMediaAssetsAsync(usageStatus: "used"))
            );
            Assert.Contains(used.Usages, usage => usage.Domain == "field-report" && usage.DisplayName == "Briefing report");
            Assert.Contains(used.Usages, usage => usage.Domain == "public-page" && usage.DisplayName == "Privacy v1");

            Assert.Empty(await InvokeOkAsync<MediaAssetDto[]>(controller.GetMediaAssetsAsync(usageStatus: "unused")));
            Assert.Single(
                await InvokeOkAsync<MediaAssetDto[]>(controller.GetMediaAssetsAsync(usageDomains: ["field-report"]))
            );
            Assert.Single(
                await InvokeOkAsync<MediaAssetDto[]>(controller.GetMediaAssetsAsync(usageDomains: ["public-page"]))
            );
            Assert.Empty(
                await InvokeOkAsync<MediaAssetDto[]>(controller.GetMediaAssetsAsync(excludeUsageDomains: ["field-report"]))
            );
            Assert.Empty(
                await InvokeOkAsync<MediaAssetDto[]>(controller.GetMediaAssetsAsync(excludeUsageStatus: "used"))
            );

            var retagged = await InvokeOkAsync<MediaAssetDto>(
                controller.ReplaceMediaAssetTagsAsync(
                    firstUpload.PrimaryKey,
                    new MediaAssetTagsPayload
                    {
                        Tags = ["gallery", "reuse"],
                    }
                )
            );
            Assert.Equal(["gallery", "reuse"], retagged.Tags);

            var readResult = Assert.IsType<PhysicalFileResult>(
                await controller.GetMediaAssetContentAsync(firstUpload.PrimaryKey)
            );
            Assert.Equal("image/png", readResult.ContentType);
            Assert.Contains(Path.Combine("App_Data", "media-assets"), readResult.FileName);
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

    private static MediaAssetsController CreateController(
        DatabaseContext dbContext,
        string storageRoot,
        ClaimsPrincipal user)
        => new(new MediaAssetService(dbContext, new TestWebHostEnvironment(storageRoot)))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user,
                },
            },
        };

    private static IFormFile CreateFormFile(
        string fileName,
        string contentType,
        byte[] content)
        => new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName)
        {
            Headers     = new HeaderDictionary(),
            ContentType = contentType,
        };

    private static string CreateTemporaryStorageRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"utf-media-assets-{Guid.NewGuid():N}");
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
