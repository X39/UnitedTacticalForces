using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;

namespace Api.Tests;

public sealed class NodaTimeSqliteApiDbContext(DbContextOptions<NodaTimeSqliteApiDbContext> dbContextOptions)
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
