using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using X39.UnitedTacticalForces.Database;
using Xunit;

namespace X39.UnitedTacticalForces.Api.Tests.GameServerHosting;

public class CorrectCloudGameServerHostingMigrationTests
{
    [Fact]
    public void UpgradeScriptGuardsLegacyRuntimeAndUnbackedFinanceBeforeDroppingCoordinates()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseNpgsql(
                "Host=localhost;Database=script-only;Username=script;Password=script",
                npgsqlOptions => npgsqlOptions.UseNodaTime())
            .Options;
        using var dbContext = new DatabaseContext(options);

        var sql = dbContext.GetService<IMigrator>().GenerateScript(
            "20260717193551_AddDurableGameServerHosting",
            "20260718101752_PersistCloudProviderResourceObservations");

        var runtimeGuard = sql.IndexOf(
            "Cloud hosting migration refused: unresolved legacy hosting state exists.",
            StringComparison.Ordinal);
        var financeGuard = sql.IndexOf(
            "Cloud hosting migration refused: existing finance projections have no append-only opening ledger.",
            StringComparison.Ordinal);
        var firstDestructiveChange = sql.IndexOf(
            "DROP INDEX \"IX_GameServers_AllocatedLocalPort_Unique\"",
            StringComparison.Ordinal);

        Assert.True(runtimeGuard >= 0);
        Assert.True(financeGuard > runtimeGuard);
        Assert.True(firstDestructiveChange > financeGuard);
        Assert.Contains("\"TimeStampResolved\" IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("\"Balance\" <> 0 OR \"Allowance\" IS NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("ADD \"ProviderState\" character varying(128)", sql, StringComparison.Ordinal);
        Assert.Contains("ADD \"PublicAddress\" character varying(256)", sql, StringComparison.Ordinal);
    }
}
