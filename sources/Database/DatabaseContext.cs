using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Content;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Database.Eventing;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Database.Media;
using X39.UnitedTacticalForces.Database.Verein;
using UtfClaims = X39.UnitedTacticalForces.Claims;

namespace X39.UnitedTacticalForces.Database;

public class DatabaseContext : DbContext
{
    /// <inheritdoc />
    public DatabaseContext(DbContextOptions<DatabaseContext> dbContextOptions) : base(dbContextOptions)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseContext"/> class for derived context types.
    /// </summary>
    /// <param name="dbContextOptions">The context options.</param>
    protected DatabaseContext(DbContextOptions dbContextOptions) : base(dbContextOptions)
    {
    }

    #region Authority

    public DbSet<Claim> Claims { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserEventMeta> UserEventMetas { get; set; } = null!;
    public DbSet<UserModPackMeta> UserModPackMetas { get; set; } = null!;

    #endregion

    #region Core

    public DbSet<ModPackDefinition> ModPackDefinitions { get; set; } = null!;
    public DbSet<ModPackRevision> ModPackRevisions { get; set; } = null!;
    public DbSet<Terrain> Terrains { get; set; } = null!;

    #endregion

    #region Eventing

    public DbSet<Event> Events { get; set; } = null!;
    public DbSet<EventSlot> EventSlots { get; set; } = null!;

    #endregion

    #region Hosting

    public DbSet<GameServer> GameServers { get; set; } = null!;
    public DbSet<CostOwner> CostOwners { get; set; } = null!;
    public DbSet<GameServerCostOwnerAssignment> GameServerCostOwnerAssignments { get; set; } = null!;
    public DbSet<GameServerDeployment> GameServerDeployments { get; set; } = null!;
    public DbSet<DeploymentResource> DeploymentResources { get; set; } = null!;
    public DbSet<LifecycleWorker> LifecycleWorkers { get; set; } = null!;
    public DbSet<LifecycleJob> LifecycleJobs { get; set; } = null!;
    public DbSet<LifecycleJobAttempt> LifecycleJobAttempts { get; set; } = null!;
    public DbSet<WorkerCommand> WorkerCommands { get; set; } = null!;
    public DbSet<PreservationSelection> PreservationSelections { get; set; } = null!;
    public DbSet<HostingAuditRecord> HostingAuditRecords { get; set; } = null!;
    public DbSet<HostingCostLedgerEntry> HostingCostLedgerEntries { get; set; } = null!;
    public DbSet<HostingFinanceOperation> HostingFinanceOperations { get; set; } = null!;
    public DbSet<HostingFinanceCorrectionAllocation> HostingFinanceCorrectionAllocations { get; set; } = null!;
    public DbSet<DanglingProviderResourceIncident> DanglingProviderResourceIncidents { get; set; } = null!;

    #endregion

    #region Wiki

    public DbSet<Wiki.WikiPage> WikiPages { get; set; } = null!;
    public DbSet<Wiki.WikiPageRevision> WikiPageRevisions { get; set; } = null!;
    public DbSet<Wiki.WikiPageAudit> WikiPageAudits { get; set; } = null!;

    #endregion

    #region Content

    public DbSet<PublicPage> PublicPages { get; set; } = null!;
    public DbSet<PublicPageRevision> PublicPageRevisions { get; set; } = null!;
    public DbSet<PublicPageAudit> PublicPageAudits { get; set; } = null!;

    #endregion

    #region Media

    public DbSet<FieldReport> FieldReports { get; set; } = null!;
    public DbSet<FieldReportAudit> FieldReportAudits { get; set; } = null!;
    /// <summary>
    /// Reusable uploaded image assets.
    /// </summary>
    public DbSet<MediaAsset> MediaAssets { get; set; } = null!;
    /// <summary>
    /// Tags assigned to reusable uploaded image assets.
    /// </summary>
    public DbSet<MediaAssetTag> MediaAssetTags { get; set; } = null!;
    /// <summary>
    /// Public-site image slots backed by reusable media assets.
    /// </summary>
    public DbSet<SiteImageSetting> SiteImageSettings { get; set; } = null!;

    #endregion

    #region Verein

    public DbSet<VereinMembership> VereinMemberships { get; set; } = null!;
    public DbSet<VereinMembershipAudit> VereinMembershipAudits { get; set; } = null!;
    public DbSet<VereinMembershipRequest> VereinMembershipRequests { get; set; } = null!;
    public DbSet<VereinMembershipRequestAudit> VereinMembershipRequestAudits { get; set; } = null!;
    public DbSet<VereinSettings> VereinSettings { get; set; } = null!;
    public DbSet<VereinSettingsAudit> VereinSettingsAudits { get; set; } = null!;
    public DbSet<VereinDocument> VereinDocuments { get; set; } = null!;
    public DbSet<VereinDocumentRevision> VereinDocumentRevisions { get; set; } = null!;
    public DbSet<VereinDocumentAudit> VereinDocumentAudits { get; set; } = null!;
    public DbSet<KassenbuchEntry> KassenbuchEntries { get; set; } = null!;
    public DbSet<KassenbuchEntryAudit> KassenbuchEntryAudits { get; set; } = null!;

    #endregion

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Claim>()
            .HasIndex(q => new
            {
                q.Identifier,
                q.Value,
                q.ValueType,
            })
            .HasDatabaseName(Claim.UniqueClaimValueIndexName)
            .IsUnique()
            .HasFilter(Claim.UniqueClaimValueIndexFilter);

        modelBuilder.Entity<Claim>()
            .HasIndex(q => new
            {
                q.Identifier,
                q.Value,
            })
            .HasDatabaseName(Claim.UniqueClaimNullValueTypeIndexName)
            .IsUnique()
            .HasFilter(Claim.UniqueClaimNullValueTypeIndexFilter);

        modelBuilder.Entity<Claim>()
            .HasIndex(q => new
            {
                q.Identifier,
                q.ValueType,
            })
            .HasDatabaseName(Claim.UniqueClaimNullValueIndexName)
            .IsUnique()
            .HasFilter(Claim.UniqueClaimNullValueIndexFilter);

        modelBuilder.Entity<Claim>()
            .HasIndex(q => q.Identifier)
            .HasDatabaseName(Claim.UniqueClaimNullValueAndValueTypeIndexName)
            .IsUnique()
            .HasFilter(Claim.UniqueClaimNullValueAndValueTypeIndexFilter);

        ConfigureClaimCatalog(modelBuilder);

        modelBuilder.Entity<User>()
            .OwnsOne(q => q.Steam, steam =>
            {
                steam.HasIndex(q => q.Id64)
                    .HasDatabaseName(User.ActiveSteamId64IndexName)
                    .IsUnique()
                    .HasFilter(User.ActiveSteamId64IndexFilter);
            });

        modelBuilder.Entity<VereinMembershipRequest>()
            .HasIndex(q => q.UserFk)
            .HasDatabaseName("IX_VereinMembershipRequests_UserFk_Open")
            .IsUnique()
            .HasFilter("\"Status\" IN (0, 1)");

        ConfigureHosting(modelBuilder);

    }

    private static void ConfigureClaimCatalog(ModelBuilder modelBuilder)
    {
        // Administrative claims predate model-managed data, while finance claims are already seeded by the API.
        // Keep this catalog to the previously missing claims so deployment cannot collide with those existing rows.
        modelBuilder.Entity<Claim>().HasData(
            CreateClaim(-11, "Creation", "Create events", UtfClaims.Creation.Events, "Allows creating events."),
            CreateClaim(-12, "Creation", "Create terrain", UtfClaims.Creation.Terrain, "Allows creating terrain."),
            CreateClaim(-13, "Creation", "Create mod packs", UtfClaims.Creation.ModPack, "Allows creating mod packs."),
            CreateClaim(-14, "Creation", "Create wiki pages", UtfClaims.Creation.Wiki, "Allows creating wiki pages."),
            CreateClaim(-15, "Creation", "Create roles", UtfClaims.Creation.Role, "Allows creating roles."),
            CreateClaim(-16, "Creation", "Create servers", UtfClaims.Creation.Server, "Allows creating game servers."),
            CreateClaim(-17, "Users", "Manage users", UtfClaims.User.All, "Allows all user-management actions."),
            CreateClaim(-18, "Users", "List users", UtfClaims.User.List, "Allows listing users."),
            CreateClaim(-19, "Users", "View Steam IDs", UtfClaims.User.ViewSteamId64, "Allows viewing Steam identifiers."),
            CreateClaim(-20, "Users", "Modify nicknames", UtfClaims.User.Nickname, "Allows changing nicknames."),
            CreateClaim(-21, "Users", "Modify email", UtfClaims.User.EMail, "Allows changing email addresses."),
            CreateClaim(-22, "Users", "Modify users", UtfClaims.User.Modify, "Allows user profile changes."),
            CreateClaim(-23, "Users", "View Discord IDs", UtfClaims.User.ViewDiscordId, "Allows viewing Discord identifiers."),
            CreateClaim(-24, "Users", "Verify users", UtfClaims.User.Verify, "Allows verifying users."),
            CreateClaim(-25, "Users", "Ban users", UtfClaims.User.Ban, "Allows banning users."),
            CreateClaim(-26, "Users", "Assign roles", UtfClaims.User.Roles, "Allows assigning user roles."),
            CreateClaim(-27, "Wiki", "Manage wiki", UtfClaims.Wiki.All, "Allows all wiki actions."),
            CreateClaim(-28, "Wiki", "Modify wiki", UtfClaims.Wiki.Modify, "Allows editing wiki pages."),
            CreateClaim(-29, "Wiki", "Delete wiki", UtfClaims.Wiki.Delete, "Allows deleting wiki pages."),
            CreateClaim(-30, "Wiki", "Rename wiki", UtfClaims.Wiki.Rename, "Allows renaming wiki pages."),
            CreateClaim(-31, "Content", "Manage public pages", UtfClaims.Content.Pages, "Allows managing public website pages."),
            CreateClaim(-32, "Verein", "Manage Verein", UtfClaims.Verein.All, "Allows all Verein administration actions."),
            CreateClaim(-33, "Verein", "Manage Verein members", UtfClaims.Verein.Members, "Allows managing formal memberships."),
            CreateClaim(-34, "Verein", "Manage Verein documents", UtfClaims.Verein.Documents, "Allows managing Verein documents."),
            CreateClaim(-35, "Verein", "Manage Kassenbuch", UtfClaims.Verein.Kassenbuch, "Allows managing Kassenbuch entries."),
            CreateClaim(-44, "Resource", "Server access", UtfClaims.ResourceBased.Server.All, "Resource claim for all actions on a server.", true),
            CreateClaim(-45, "Resource", "View server", UtfClaims.ResourceBased.Server.View, "Resource claim for viewing a server.", true),
            CreateClaim(-46, "Resource", "Operate server", UtfClaims.ResourceBased.Server.Operate, "Resource claim for server lifecycle operations.", true),
            CreateClaim(-47, "Resource", "Modify server", UtfClaims.ResourceBased.Server.Modify, "Resource claim for changing a server.", true),
            CreateClaim(-48, "Resource", "Manage server access", UtfClaims.ResourceBased.Server.ManageAccess, "Resource claim for managing server access.", true),
            CreateClaim(-49, "Resource", "Delete server data", UtfClaims.ResourceBased.Server.DeleteData, "Resource claim for deleting persisted server data.", true),
            CreateClaim(-50, "Resource", "Use cost owner", UtfClaims.ResourceBased.CostOwner.Use, "Resource claim for charging a cost owner.", true),
            CreateClaim(-51, "Resource", "View cost owner", UtfClaims.ResourceBased.CostOwner.View, "Resource claim for viewing a cost owner.", true),
            CreateClaim(-52, "Resource", "Mod-pack access", UtfClaims.ResourceBased.ModPack.All, "Resource claim for all actions on a mod pack.", true),
            CreateClaim(-53, "Resource", "Modify mod pack", UtfClaims.ResourceBased.ModPack.Modify, "Resource claim for mod-pack modification.", true),
            CreateClaim(-54, "Resource", "Delete mod pack", UtfClaims.ResourceBased.ModPack.Delete, "Resource claim for mod-pack deletion.", true),
            CreateClaim(-55, "Resource", "Terrain access", UtfClaims.ResourceBased.Terrain.All, "Resource claim for all actions on terrain.", true),
            CreateClaim(-56, "Resource", "Modify terrain", UtfClaims.ResourceBased.Terrain.Modify, "Resource claim for terrain modification.", true),
            CreateClaim(-57, "Resource", "Delete terrain", UtfClaims.ResourceBased.Terrain.Delete, "Resource claim for terrain deletion.", true));
    }

    private static Claim CreateClaim(
        long primaryKey,
        string category,
        string title,
        string identifier,
        string description,
        bool isPrefix = false)
        => new()
        {
            PrimaryKey = primaryKey,
            Category = category,
            Title = title,
            Identifier = identifier,
            Description = description,
            IsPrefix = isPrefix,
        };

    private static void ConfigureHosting(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameServer>(entity =>
        {
            entity.HasOne(q => q.CreatedBy)
                .WithMany()
                .HasForeignKey(q => q.CreatedByFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.CurrentCostOwner)
                .WithMany(q => q.CurrentServers)
                .HasForeignKey(q => q.CurrentCostOwnerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_GameServers_GracefulStopPolicy",
                    "\"GracefulStopAttempts\" > 0 AND \"GracefulStopTimeoutSeconds\" > 0");
            });
        });

        modelBuilder.Entity<CostOwner>(entity =>
        {
            entity.HasOne(q => q.User)
                .WithMany()
                .HasForeignKey(q => q.UserFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(q => q.UserFk)
                .HasDatabaseName(CostOwner.UniqueUserIndexName)
                .IsUnique()
                .HasFilter(CostOwner.UniqueUserIndexFilter);
            entity.HasIndex(q => q.Kind)
                .HasDatabaseName(CostOwner.UniqueVereinIndexName)
                .IsUnique()
                .HasFilter(CostOwner.UniqueVereinIndexFilter);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_CostOwners_KindUser",
                "(\"Kind\" = 0 AND \"UserFk\" IS NOT NULL) OR (\"Kind\" <> 0 AND \"UserFk\" IS NULL)"));
        });

        modelBuilder.Entity<GameServerCostOwnerAssignment>(entity =>
        {
            entity.HasOne(q => q.GameServer)
                .WithMany(q => q.CostOwnerAssignments)
                .HasForeignKey(q => q.GameServerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.CostOwner)
                .WithMany(q => q.ServerAssignments)
                .HasForeignKey(q => q.CostOwnerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(q => q.GameServerFk)
                .HasDatabaseName(GameServerCostOwnerAssignment.ActiveServerIndexName)
                .IsUnique()
                .HasFilter(GameServerCostOwnerAssignment.ActiveServerIndexFilter);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_GameServerCostOwnerAssignments_Interval",
                "\"TimeStampUnassigned\" IS NULL OR \"TimeStampUnassigned\" >= \"TimeStampAssigned\""));
        });

        modelBuilder.Entity<GameServerDeployment>(entity =>
        {
            entity.HasOne(q => q.GameServer)
                .WithMany(q => q.Deployments)
                .HasForeignKey(q => q.GameServerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.CostOwner)
                .WithMany(q => q.Deployments)
                .HasForeignKey(q => q.CostOwnerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(q => q.GameServerFk)
                .HasDatabaseName(GameServerDeployment.ActiveServerIndexName)
                .IsUnique()
                .HasFilter(GameServerDeployment.ActiveServerIndexFilter);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_GameServerDeployments_CostReservation",
                "(\"CostReservationStartsAt\" IS NULL AND \"CostReservationExpiresAt\" IS NULL "
                + "AND \"ReservedProviderCostEur\" IS NULL) OR "
                + "(\"CostReservationStartsAt\" IS NOT NULL AND \"CostReservationExpiresAt\" IS NOT NULL "
                + "AND \"ReservedProviderCostEur\" > 0 "
                + "AND \"CostReservationExpiresAt\" > \"CostReservationStartsAt\")"));
        });

        modelBuilder.Entity<DeploymentResource>(entity =>
        {
            entity.HasOne(q => q.Deployment)
                .WithMany(q => q.Resources)
                .HasForeignKey(q => q.DeploymentFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(q => new
                {
                    q.ProviderIdentifier,
                    q.ProvisioningIntentIdentifier,
                });
            entity.HasIndex(q => new
                {
                    q.ProviderIdentifier,
                    q.Kind,
                    q.ProviderResourceIdentifier,
                })
                .HasDatabaseName(DeploymentResource.UniqueProviderResourceIndexName)
                .IsUnique()
                .HasFilter(DeploymentResource.UniqueProviderResourceIndexFilter);
        });

        modelBuilder.Entity<LifecycleWorker>(entity =>
        {
            entity.HasOne(q => q.Deployment)
                .WithMany(q => q.Workers)
                .HasForeignKey(q => q.DeploymentFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(q => q.DeploymentFk)
                .HasDatabaseName(LifecycleWorker.ActiveDeploymentIndexName)
                .IsUnique()
                .HasFilter(LifecycleWorker.ActiveDeploymentIndexFilter);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_LifecycleWorkers_BootstrapCredential",
                    "\"BootstrapCredentialHash\" IS NULL OR \"BootstrapCredentialExpiresAt\" IS NOT NULL");
                table.HasCheckConstraint(
                    "CK_LifecycleWorkers_BootstrapConsumption",
                    "\"BootstrapCredentialConsumedAt\" IS NULL OR \"BootstrapCredentialExpiresAt\" IS NOT NULL");
                table.HasCheckConstraint(
                    "CK_LifecycleWorkers_WorkerCredential",
                    "\"WorkerCredentialHash\" IS NULL OR (\"WorkerCredentialIssuedAt\" IS NOT NULL "
                    + "AND \"WorkerCredentialExpiresAt\" IS NOT NULL "
                    + "AND \"WorkerCredentialActivatedAt\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_LifecycleWorkers_PendingWorkerCredential",
                    "\"PendingWorkerCredentialHash\" IS NULL OR (\"PendingWorkerCredentialIssuedAt\" IS NOT NULL "
                    + "AND \"PendingWorkerCredentialExpiresAt\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_LifecycleWorkers_WorkerCredentialRevocation",
                    "\"WorkerCredentialRevokedAt\" IS NULL OR \"WorkerCredentialIssuedAt\" IS NOT NULL");
            });
        });

        modelBuilder.Entity<LifecycleJob>(entity =>
        {
            entity.HasOne(q => q.GameServer)
                .WithMany(q => q.LifecycleJobs)
                .HasForeignKey(q => q.GameServerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.Deployment)
                .WithMany(q => q.LifecycleJobs)
                .HasForeignKey(q => q.DeploymentFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.RequestedBy)
                .WithMany()
                .HasForeignKey(q => q.RequestedByFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(q => q.DeploymentFk)
                .HasDatabaseName(LifecycleJob.ActiveDeploymentIndexName)
                .IsUnique()
                .HasFilter(LifecycleJob.ActiveDeploymentIndexFilter);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_LifecycleJobs_Progress", "\"Progress\" BETWEEN 0 AND 100");
                table.HasCheckConstraint(
                    "CK_LifecycleJobs_Attempts",
                    "\"Attempt\" >= 0 AND \"MaxAttempts\" > 0 AND \"Attempt\" <= \"MaxAttempts\"");
            });
        });

        modelBuilder.Entity<LifecycleJobAttempt>(entity =>
        {
            entity.HasOne(q => q.LifecycleJob)
                .WithMany(q => q.Attempts)
                .HasForeignKey(q => q.LifecycleJobFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.Worker)
                .WithMany(q => q.JobAttempts)
                .HasForeignKey(q => q.WorkerFk)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkerCommand>(entity =>
        {
            entity.HasOne(q => q.LifecycleJob)
                .WithMany(q => q.WorkerCommands)
                .HasForeignKey(q => q.LifecycleJobFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.Deployment)
                .WithMany(q => q.WorkerCommands)
                .HasForeignKey(q => q.DeploymentFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.Worker)
                .WithMany(q => q.Commands)
                .HasForeignKey(q => q.WorkerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(q => q.DeploymentFk)
                .HasDatabaseName(WorkerCommand.ActiveDeploymentIndexName)
                .IsUnique()
                .HasFilter(WorkerCommand.ActiveDeploymentIndexFilter);
        });

        modelBuilder.Entity<PreservationSelection>(entity =>
        {
            entity.HasOne(q => q.GameServer)
                .WithMany(q => q.PreservationSelections)
                .HasForeignKey(q => q.GameServerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.CreatedBy)
                .WithMany()
                .HasForeignKey(q => q.CreatedByFk)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HostingAuditRecord>(entity =>
        {
            entity.HasOne(q => q.GameServer)
                .WithMany(q => q.AuditRecords)
                .HasForeignKey(q => q.GameServerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.Deployment)
                .WithMany(q => q.AuditRecords)
                .HasForeignKey(q => q.DeploymentFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.LifecycleJob)
                .WithMany(q => q.AuditRecords)
                .HasForeignKey(q => q.LifecycleJobFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.Worker)
                .WithMany(q => q.AuditRecords)
                .HasForeignKey(q => q.WorkerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.WorkerCommand)
                .WithMany(q => q.AuditRecords)
                .HasForeignKey(q => q.WorkerCommandFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.CostOwner)
                .WithMany(q => q.AuditRecords)
                .HasForeignKey(q => q.CostOwnerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.Actor)
                .WithMany()
                .HasForeignKey(q => q.ActorFk)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HostingCostLedgerEntry>(entity =>
        {
            entity.HasOne(q => q.CostOwner)
                .WithMany(q => q.HostingCostLedgerEntries)
                .HasForeignKey(q => q.CostOwnerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.Deployment)
                .WithMany(q => q.CostLedgerEntries)
                .HasForeignKey(q => q.DeploymentFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.RecordedBy)
                .WithMany()
                .HasForeignKey(q => q.RecordedByFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.FinanceOperation)
                .WithMany(q => q.Entries)
                .HasForeignKey(q => q.FinanceOperationFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.ReversedEntry)
                .WithMany(q => q.ReversalEntries)
                .HasForeignKey(q => q.ReversedEntryFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(q => q.RecordIdentifier)
                .HasDatabaseName("IX_HostingCostLedgerEntries_RecordIdentifier_Unique")
                .IsUnique()
                .HasFilter("\"RecordIdentifier\" IS NOT NULL");
            entity.HasIndex(q => q.FinanceOperationFk);
            entity.HasIndex(q => q.ReversedEntryFk)
                .HasDatabaseName("IX_HostingCostLedgerEntries_ReversedEntryFk_Unique")
                .IsUnique()
                .HasFilter("\"ReversedEntryFk\" IS NOT NULL");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_HostingCostLedgerEntries_Shape",
                    "(\"Kind\" = 0 AND \"Amount\" < 0 AND \"DeploymentFk\" IS NOT NULL "
                    + "AND \"EstimatedHourlyRate\" > 0 AND \"ProviderIdentifier\" IS NOT NULL "
                    + "AND \"SliceStartsAt\" IS NOT NULL AND \"SliceEndsAt\" IS NOT NULL "
                    + "AND \"RecordedByFk\" IS NULL AND \"RecordIdentifier\" IS NULL "
                    + "AND \"PreviousAllowance\" IS NULL AND \"NewAllowance\" IS NULL "
                    + "AND \"FinanceOperationFk\" IS NULL AND \"ReversedEntryFk\" IS NULL) OR "
                    + "(\"Kind\" = 1 AND \"Amount\" > 0 AND \"DeploymentFk\" IS NULL "
                    + "AND \"EstimatedHourlyRate\" IS NULL AND \"ProviderIdentifier\" IS NULL "
                    + "AND \"SliceStartsAt\" IS NULL AND \"SliceEndsAt\" IS NULL "
                    + "AND \"RecordedByFk\" IS NOT NULL AND \"RecordIdentifier\" IS NOT NULL "
                    + "AND \"ExternalReference\" IS NOT NULL "
                    + "AND \"PreviousAllowance\" IS NULL AND \"NewAllowance\" IS NULL "
                    + "AND \"FinanceOperationFk\" IS NULL AND \"ReversedEntryFk\" IS NULL) OR "
                    + "(\"Kind\" = 2 AND \"Amount\" = 0 AND \"DeploymentFk\" IS NULL "
                    + "AND \"EstimatedHourlyRate\" IS NULL AND \"ProviderIdentifier\" IS NULL "
                    + "AND \"SliceStartsAt\" IS NULL AND \"SliceEndsAt\" IS NULL "
                    + "AND \"RecordedByFk\" IS NOT NULL AND \"RecordIdentifier\" IS NOT NULL "
                    + "AND \"Reason\" IS NOT NULL "
                    + "AND (\"PreviousAllowance\" IS NOT NULL OR \"NewAllowance\" IS NOT NULL) "
                    + "AND \"FinanceOperationFk\" IS NULL AND \"ReversedEntryFk\" IS NULL) OR "
                    + "(\"Kind\" = 3 AND \"Amount\" < 0 AND \"DeploymentFk\" IS NULL "
                    + "AND \"EstimatedHourlyRate\" IS NULL AND \"ProviderIdentifier\" IS NULL "
                    + "AND \"SliceStartsAt\" IS NULL AND \"SliceEndsAt\" IS NULL "
                    + "AND \"RecordedByFk\" IS NOT NULL AND \"RecordIdentifier\" IS NOT NULL "
                    + "AND \"ExternalReference\" IS NOT NULL AND \"Reason\" IS NOT NULL "
                    + "AND \"PreviousAllowance\" IS NULL AND \"NewAllowance\" IS NULL "
                    + "AND \"FinanceOperationFk\" IS NULL AND \"ReversedEntryFk\" IS NULL) OR "
                    + "(\"Kind\" IN (4, 5) AND \"DeploymentFk\" IS NOT NULL "
                    + "AND ((\"Kind\" = 4 AND \"Amount\" > 0) OR (\"Kind\" = 5 AND \"Amount\" < 0)) "
                    + "AND \"EstimatedHourlyRate\" IS NULL AND \"ProviderIdentifier\" IS NULL "
                    + "AND \"SliceStartsAt\" IS NULL AND \"SliceEndsAt\" IS NULL "
                    + "AND \"RecordedByFk\" IS NOT NULL AND \"RecordIdentifier\" IS NULL "
                    + "AND \"ExternalReference\" IS NULL AND \"Reason\" IS NOT NULL "
                    + "AND \"PreviousAllowance\" IS NULL AND \"NewAllowance\" IS NULL "
                    + "AND \"FinanceOperationFk\" IS NOT NULL AND \"ReversedEntryFk\" IS NULL) OR "
                    + "(\"Kind\" = 6 AND \"Amount\" <> 0 "
                    + "AND \"EstimatedHourlyRate\" IS NULL AND \"ProviderIdentifier\" IS NULL "
                    + "AND \"SliceStartsAt\" IS NULL AND \"SliceEndsAt\" IS NULL "
                    + "AND \"RecordedByFk\" IS NOT NULL AND \"RecordIdentifier\" IS NULL "
                    + "AND \"ExternalReference\" IS NULL AND \"Reason\" IS NOT NULL "
                    + "AND \"PreviousAllowance\" IS NULL AND \"NewAllowance\" IS NULL "
                    + "AND \"FinanceOperationFk\" IS NOT NULL AND \"ReversedEntryFk\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_HostingCostLedgerEntries_Slice",
                    "\"SliceStartsAt\" IS NULL OR \"SliceEndsAt\" > \"SliceStartsAt\"");
                table.HasCheckConstraint("CK_HostingCostLedgerEntries_Currency", "length(\"Currency\") = 3");
            });
        });

        modelBuilder.Entity<HostingFinanceOperation>(entity =>
        {
            entity.HasOne(q => q.Deployment)
                .WithMany()
                .HasForeignKey(q => q.DeploymentFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.SourceCostOwner)
                .WithMany()
                .HasForeignKey(q => q.SourceCostOwnerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.DestinationCostOwner)
                .WithMany()
                .HasForeignKey(q => q.DestinationCostOwnerFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.TargetLedgerEntry)
                .WithMany()
                .HasForeignKey(q => q.TargetLedgerEntryFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.TargetFinanceOperation)
                .WithMany(q => q.ReversalOperations)
                .HasForeignKey(q => q.TargetFinanceOperationFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.RecordedBy)
                .WithMany()
                .HasForeignKey(q => q.RecordedByFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(q => q.TargetLedgerEntryFk)
                .HasDatabaseName("IX_HostingFinanceOperations_TargetLedgerEntryFk_Unique")
                .IsUnique()
                .HasFilter("\"TargetLedgerEntryFk\" IS NOT NULL");
            entity.HasIndex(q => q.TargetFinanceOperationFk)
                .HasDatabaseName("IX_HostingFinanceOperations_TargetFinanceOperationFk_Unique")
                .IsUnique()
                .HasFilter("\"TargetFinanceOperationFk\" IS NOT NULL");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_HostingFinanceOperations_Shape",
                    "(\"Kind\" = 0 AND \"DeploymentFk\" IS NOT NULL "
                    + "AND \"SourceCostOwnerFk\" IS NOT NULL AND \"DestinationCostOwnerFk\" IS NOT NULL "
                    + "AND \"SourceCostOwnerFk\" <> \"DestinationCostOwnerFk\" "
                    + "AND \"TargetLedgerEntryFk\" IS NULL AND \"TargetFinanceOperationFk\" IS NULL "
                    + "AND \"Amount\" > 0 AND \"Reason\" IS NOT NULL) OR "
                    + "(\"Kind\" = 1 AND \"SourceCostOwnerFk\" IS NULL AND \"DestinationCostOwnerFk\" IS NULL "
                    + "AND ((\"TargetLedgerEntryFk\" IS NOT NULL AND \"TargetFinanceOperationFk\" IS NULL) "
                    + "OR (\"TargetLedgerEntryFk\" IS NULL AND \"TargetFinanceOperationFk\" IS NOT NULL)) "
                    + "AND \"Amount\" > 0 AND \"Reason\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_HostingFinanceOperations_TargetNotSelf",
                    "\"TargetFinanceOperationFk\" IS NULL OR \"TargetFinanceOperationFk\" <> \"PrimaryKey\"");
                table.HasCheckConstraint("CK_HostingFinanceOperations_Currency", "length(\"Currency\") = 3");
            });
        });

        modelBuilder.Entity<HostingFinanceCorrectionAllocation>(entity =>
        {
            entity.HasOne(q => q.FinanceOperation)
                .WithMany(q => q.AffectedLedgerEntries)
                .HasForeignKey(q => q.FinanceOperationFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(q => q.AffectedLedgerEntry)
                .WithMany(q => q.CorrectionAllocations)
                .HasForeignKey(q => q.AffectedLedgerEntryFk)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_HostingFinanceCorrectionAllocations_Amount",
                "\"Amount\" > 0"));
        });

    }
}
