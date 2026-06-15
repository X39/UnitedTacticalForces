using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Services.Media;
using X39.UnitedTacticalForces.Database;
using Xunit;

namespace Api.Tests.Data;

public class DebugDemoDataSeederTests
{
    private static readonly Guid DefaultDebugUserId = Guid.Parse("b6aa28a3-a740-4e0c-b5dc-157c04881872");

    [Fact]
    public async Task SeedAsync_CreatesExpectedDemoDataAndIsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        using var storage = new TestStorageDirectory();
        var mediaAssetService = new MediaAssetService(dbContext, new TestWebHostEnvironment(storage.Path));

        await DebugDemoDataSeeder.SeedAsync(dbContext, mediaAssetService);

        Assert.True(await dbContext.Users.AnyAsync(q => q.PrimaryKey == DefaultDebugUserId));
        Assert.Equal(7, await dbContext.Users.CountAsync());
        Assert.Equal(4, await dbContext.Terrains.CountAsync());
        Assert.Equal(3, await dbContext.Events.CountAsync());
        Assert.NotEmpty(await dbContext.FieldReports.ToArrayAsync());

        var countsAfterFirstSeed = await ReadCountsAsync(dbContext);
        await DebugDemoDataSeeder.SeedAsync(dbContext, mediaAssetService);

        Assert.Equal(countsAfterFirstSeed, await ReadCountsAsync(dbContext));
    }

    private static async Task<DatabaseContext> CreateDbContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new NodaTimeSqliteApiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private static async Task<(int Users, int Claims, int Roles, int MediaAssets, int Events, int FieldReports)>
        ReadCountsAsync(DatabaseContext dbContext)
        => (
            await dbContext.Users.CountAsync(),
            await dbContext.Claims.CountAsync(),
            await dbContext.Roles.CountAsync(),
            await dbContext.MediaAssets.CountAsync(),
            await dbContext.Events.CountAsync(),
            await dbContext.FieldReports.CountAsync()
        );

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Api.Tests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = "Development";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestStorageDirectory : IDisposable
    {
        public TestStorageDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "utf-debug-seeding-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            var fullPath = System.IO.Path.GetFullPath(Path);
            var expectedRoot = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "utf-debug-seeding-tests"));
            if (fullPath.StartsWith(expectedRoot + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && Directory.Exists(fullPath))
                Directory.Delete(fullPath, recursive: true);
        }
    }
}
