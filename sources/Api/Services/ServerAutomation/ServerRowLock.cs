using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;

namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Takes the durable server-row lock that every lifecycle mutation of a server serializes on.
/// </summary>
internal static class ServerRowLock
{
    /// <summary>
    /// Tells whether the context talks to PostgreSQL.
    /// </summary>
    /// <param name="dbContext">The database context to inspect.</param>
    /// <returns><see langword="true" /> for the Npgsql provider.</returns>
    public static bool IsPostgreSql(DatabaseContext dbContext)
        => dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Locks the server row until the current transaction ends.
    /// </summary>
    /// <param name="dbContext">The database context whose transaction holds the lock.</param>
    /// <param name="serverId">The server to lock.</param>
    /// <param name="cancellationToken">A token that cancels the statement.</param>
    /// <returns>A task completing once the lock is held until the transaction ends.</returns>
    public static async Task AcquireAsync(
        DatabaseContext dbContext,
        Guid serverId,
        CancellationToken cancellationToken)
    {
        if (IsPostgreSql(dbContext))
        {
            await dbContext.GameServers
                .FromSqlInterpolated($"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {serverId} FOR UPDATE""")
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // SQLite has no row-level FOR UPDATE; this no-op write takes its serializable writer lock instead.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "GameServers" SET "TimeStampUpdated" = "TimeStampUpdated" WHERE "PrimaryKey" = {serverId}""",
                cancellationToken)
            .ConfigureAwait(false);
    }
}
