using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Core;
using X39.UnitedTacticalForces.Api.Data.Eventing;
using X39.UnitedTacticalForces.Api.Data.Hosting;

namespace X39.UnitedTacticalForces.Api.Data;

public class ApiDbContext : DbContext
{
    /// <inheritdoc />
    public ApiDbContext(DbContextOptions<ApiDbContext> dbContextOptions) : base(dbContextOptions)
    {
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableDetailedErrors();
        optionsBuilder.EnableSensitiveDataLogging();
        base.OnConfiguring(optionsBuilder);
    }

    #region Authority

    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserEventMeta> UserEventMetas { get; set; } = null!;
    public DbSet<UserModPackMeta> UserModPackMetas { get; set; } = null!;

    #endregion

    #region Core

    public DbSet<ModPack> ModPacks { get; set; } = null!;
    public DbSet<Terrain> Terrains { get; set; } = null!;

    #endregion

    #region Eventing

    public DbSet<Event> Events { get; set; } = null!;
    public DbSet<EventSlot> EventSlots { get; set; } = null!;

    #endregion

    #region Hosting

    public DbSet<ConfigurationEntry> ConfigurationEntries { get; set; } = null!;
    public DbSet<GameServer> GameServers { get; set; } = null!;
    public DbSet<LifetimeEvent> LifetimeEvents { get; set; } = null!;
    public DbSet<GameServerLog> GameServerLogs { get; set; } = null!;

    #endregion

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(Role.StaticData());
        base.OnModelCreating(modelBuilder);
    }
}