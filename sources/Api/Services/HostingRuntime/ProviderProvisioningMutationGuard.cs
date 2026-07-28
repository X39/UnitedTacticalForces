using System.Data;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

/// <summary>
/// Uses the same durable server lock as lifecycle mutation requests so Terminate and each provider-create request
/// have a single database-defined order, including when multiple API instances are running.
/// </summary>
[Singleton<ProviderProvisioningMutationGuard, IProviderProvisioningMutationGuard>]
internal sealed class ProviderProvisioningMutationGuard(
    IDbContextFactory<DatabaseContext> dbContextFactory) : IProviderProvisioningMutationGuard
{
    public async Task<ProviderProvisioningMutationGuardResult<T>> ExecuteCreateAsync<T>(
        Guid serverId,
        Guid deploymentId,
        Func<CancellationToken, Task<T>> mutation,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var isPostgreSql = dbContext.Database.ProviderName?.Contains(
            "Npgsql",
            StringComparison.OrdinalIgnoreCase) == true;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
                isPostgreSql ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable,
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            bool serverExists;
            if (isPostgreSql)
            {
                await dbContext.GameServers
                    .FromSqlInterpolated(
                        $"""SELECT * FROM "GameServers" WHERE "PrimaryKey" = {serverId} FOR UPDATE""")
                    .LoadAsync(cancellationToken)
                    .ConfigureAwait(false);
                serverExists = dbContext.GameServers.Local.Any(q => q.PrimaryKey == serverId);
            }
            else
            {
                // SQLite has no row-level FOR UPDATE. A no-op write obtains its serializable writer lock before the
                // barrier read and provides the same ordering for development and test databases.
                serverExists = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"""UPDATE "GameServers" SET "TimeStampUpdated" = "TimeStampUpdated" WHERE "PrimaryKey" = {serverId}""",
                        cancellationToken)
                    .ConfigureAwait(false) == 1;
            }

            if (!serverExists)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new ProviderProvisioningMutationGuardResult<T>(
                    false,
                    default,
                    "The durable server no longer exists.");
            }

            var provisioningBlocked = await dbContext.GameServerDeployments
                .AsNoTracking()
                .Where(q => q.PrimaryKey == deploymentId && q.GameServerFk == serverId)
                .Select(q => (bool?)q.IsProvisioningBlocked)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (provisioningBlocked is not false)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new ProviderProvisioningMutationGuardResult<T>(
                    false,
                    default,
                    provisioningBlocked.HasValue
                        ? "Provisioning is blocked by durable stop or termination state."
                        : "The durable deployment no longer exists.");
            }

            // Keep the durable server lock through the transport call. Terminate either commits its barrier first,
            // preventing this mutation, or waits and records this already-issued request for reconciliation.
            var value = await mutation(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ProviderProvisioningMutationGuardResult<T>(true, value, null);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
