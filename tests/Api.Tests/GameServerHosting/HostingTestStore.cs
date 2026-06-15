using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces;

namespace Api.Tests.GameServerHosting;

internal sealed class HostingTestStore : IAsyncDisposable
{
    private HostingTestStore(SqliteConnection connection, NodaTimeSqliteApiDbContext dbContext)
    {
        Connection = connection;
        DbContext = dbContext;
    }

    public SqliteConnection Connection { get; }

    public NodaTimeSqliteApiDbContext DbContext { get; }

    public static async Task<HostingTestStore> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new NodaTimeSqliteApiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new HostingTestStore(connection, dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await Connection.DisposeAsync();
    }
}