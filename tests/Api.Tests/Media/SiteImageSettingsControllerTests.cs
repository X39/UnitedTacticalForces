using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using X39.UnitedTacticalForces.Api.Controllers;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Media;
using X39.UnitedTacticalForces.Api.DTO;
using Xunit;

namespace Api.Tests.Media;

public class SiteImageSettingsControllerTests
{
    [Fact]
    public async Task PublicSettings_AllowsAnonymousUsers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var assetId = Guid.NewGuid();
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        dbContext.MediaAssets.Add(MediaAssetTestData.Create(assetId));
        dbContext.SiteImageSettings.Add(new SiteImageSetting
        {
            PrimaryKey       = "home-hero",
            Title            = "Home hero",
            Description      = "Homepage hero image",
            Group            = "home",
            SortOrder        = 10,
            ImageAssetFk     = assetId,
            TimeStampCreated = now,
            TimeStampUpdated = now,
        });
        await dbContext.SaveChangesAsync();
        var controller = new SiteImageSettingsController(dbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()),
                },
            },
        };

        var result = await controller.GetPublicSiteImageSettingsAsync();

        var settings = Assert.IsType<SiteImageSettingDto[]>(Assert.IsType<OkObjectResult>(result).Value);
        var setting = Assert.Single(settings);
        Assert.Equal("home-hero", setting.Key);
        Assert.Equal(assetId, setting.ImageAssetFk);
    }

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
