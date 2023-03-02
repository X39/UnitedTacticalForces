using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
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
    public DbSet<UserModPackMeta> UserModPackMetas { get; set; } = null!;
    public DbSet<UserEventMeta> UserEventMetas { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Terrain> Terrains { get; set; } = null!;
    public DbSet<ModPack> ModPacks { get; set; } = null!;
    public DbSet<EventSlot> EventSlots { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(Role.StaticData());
        base.OnModelCreating(modelBuilder);
    }
}