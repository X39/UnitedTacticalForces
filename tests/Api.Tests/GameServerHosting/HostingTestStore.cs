using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using X39.UnitedTacticalForces;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Owns an in-memory SQLite connection and a hosting database context created on it for one test.
/// </summary>
internal sealed class HostingTestStore : IAsyncDisposable
{
    private HostingTestStore(SqliteConnection connection, NodaTimeSqliteApiDbContext dbContext)
    {
        Connection = connection;
        DbContext = dbContext;
    }

    /// <summary>
    /// Gets the open in-memory SQLite connection that keeps the database alive.
    /// </summary>
    /// <value>The connection shared by every context created for this store.</value>
    public SqliteConnection Connection { get; }

    /// <summary>
    /// Gets the database context created on <see cref="Connection"/> with the schema already ensured.
    /// </summary>
    /// <value>The test database context.</value>
    public NodaTimeSqliteApiDbContext DbContext { get; }

    /// <summary>
    /// Creates a store with a fresh in-memory database and an ensured schema.
    /// </summary>
    /// <param name="interceptors">
    /// Optional EF Core interceptors added to the context options, for tests that observe saves.
    /// </param>
    /// <returns>The created store; dispose it to release the context and the connection.</returns>
    public static async Task<HostingTestStore> CreateAsync(IInterceptor[]? interceptors = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var optionsBuilder = new DbContextOptionsBuilder<NodaTimeSqliteApiDbContext>()
            .UseSqlite(connection);
        if (interceptors is not null)
            optionsBuilder.AddInterceptors(interceptors);
        var dbContext = new NodaTimeSqliteApiDbContext(optionsBuilder.Options);
        await dbContext.Database.EnsureCreatedAsync();
        return new HostingTestStore(connection, dbContext);
    }

    /// <summary>
    /// Disposes the database context and closes the in-memory connection.
    /// </summary>
    /// <returns>A task completing once both resources were released.</returns>
    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await Connection.DisposeAsync();
    }
}