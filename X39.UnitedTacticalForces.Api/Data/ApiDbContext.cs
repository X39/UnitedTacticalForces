using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Core;
using X39.UnitedTacticalForces.Api.Data.Eventing;

namespace X39.UnitedTacticalForces.Api.Data;

public class ApiDbContext : DbContext
{
    public ApiDbContext(DbContextOptions<ApiDbContext> dbContextOptions) : base(dbContextOptions)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableDetailedErrors();
        optionsBuilder.EnableSensitiveDataLogging();
        base.OnConfiguring(optionsBuilder);
    }

    public DbSet<Event> Events { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Privilege> Privileges { get; set; } = null!;
    public DbSet<Terrain> Terrains { get; set; } = null!;
    public DbSet<ModPack> ModPacks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Privilege>().HasData(Privilege.StaticData());
        base.OnModelCreating(modelBuilder);
    }
}