# Phase 17: Runtime Cost Snapshot Foundation - Pattern Map

**Mapped:** 2026-06-17
**Files analyzed:** 13 likely new/modified files or concerns
**Analogs found:** 13 / 13

## File Classification

| New/Modified File Or Concern | Role | Data Flow | Closest Analog | Match Quality |
|------------------------------|------|-----------|----------------|---------------|
| `sources/Api/Data/Hosting/CloudCostSnapshot.cs` | model | CRUD | `sources/Api/Data/Hosting/CloudCostEstimate.cs` | exact |
| `sources/Api/Data/Hosting/CloudCostSnapshotContribution.cs` | model | CRUD | `sources/Api/Data/Hosting/CloudResourceInventoryRecord.cs` | exact |
| `sources/Api/Data/Hosting/Enums/ECloudCostSnapshotSource.cs` or contract enum | model | transform | `sources/Contract/GameServer/ECloudEstimateFreshness.cs` | role-match |
| `sources/Api/Data/ApiDbContext.cs` | config | CRUD | `sources/Api/Data/ApiDbContext.cs` | exact |
| `sources/Api/Data/Migrations/*RuntimeCostSnapshots*.cs` | migration | CRUD | existing `sources/Api/Data/Migrations/**` generated EF migrations | generated analog |
| `sources/Api/Services/CloudCosts/ICloudRuntimeCostSnapshotService.cs` | service | request-response | `sources/Api/Services/CloudPlanning/ICloudCostEstimateService.cs` | exact |
| `sources/Api/Services/CloudCosts/CloudRuntimeCostSnapshotService.cs` | service | batch/CRUD | `sources/Api/Services/CloudPlanning/CloudCostEstimateService.cs` | exact |
| `sources/Api/Services/CloudCosts/CloudCostIntervalCalculator.cs` | utility | transform | `sources/Api/Services/CloudTeardown/CloudTeardownService.cs` billable predicate + assignment interval entities | role-match |
| Runtime billable resource predicate | utility | transform | `sources/Api/Services/CloudTeardown/CloudTeardownService.cs` | exact |
| Cached provider price/freshness resolution | service | transform | `sources/Api/Services/CloudPlanning/CloudCostEstimateService.cs` | exact |
| Audit redaction and append | service | event-driven | `sources/Api/Services/GameServerLifecycle/HostingAuditService.cs` | exact |
| `tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs` | test | CRUD/request-response | `tests/Api.Tests/CloudPlanning/Arma3CloudEstimateTests.cs` | exact |
| API test SQLite `DateTimeOffset` setup | test | config | `tests/Api.Tests/GameServerLifecycle/BillingOwnershipAndAuditTests.cs` | exact |

## Pattern Assignments

### `sources/Api/Data/Hosting/CloudCostSnapshot.cs` (model, CRUD)

**Analog:** `sources/Api/Data/Hosting/CloudCostEstimate.cs`

**Imports and namespace pattern** (lines 1-7):

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Contract.GameServer;

namespace X39.UnitedTacticalForces.Api.Data.Hosting;
```

**Index and FK shape** (lines 12-17, 22-73):

```csharp
[Index(nameof(DryRunPlanFk))]
[Index(nameof(GameServerFk))]
[Index(nameof(BillingAccountFk))]
[Index(nameof(BillingAssignmentFk))]
[Index(nameof(ProviderIdentifier), nameof(Freshness))]
public class CloudCostEstimate
{
    [Key]
    public long PrimaryKey { get; set; }

    [MaxLength(128)]
    public string ProviderIdentifier { get; set; } = HetznerProviderConfiguration.HetznerProviderIdentifier;

    [ForeignKey(nameof(GameServerFk))]
    public GameServer? GameServer { get; set; }
    public long GameServerFk { get; set; }

    [ForeignKey(nameof(BillingAccountFk))]
    public BillingAccount? BillingAccount { get; set; }
    public long BillingAccountFk { get; set; }

    [ForeignKey(nameof(BillingAssignmentFk))]
    public GameServerBillingAssignment? BillingAssignment { get; set; }
    public long BillingAssignmentFk { get; set; }
}
```

**Cost metadata fields to copy/adapt** (lines 121-207):

```csharp
public decimal ServerHourlyPrice { get; set; }
public decimal ServerMonthlyPrice { get; set; }
public decimal EstimatedHourlyCost { get; set; }
public decimal EstimatedMonthlyCost { get; set; }

[MaxLength(16)]
public string Currency { get; set; } = string.Empty;

public DateTimeOffset? MetadataSourceTimestamp { get; set; }
public ECloudEstimateFreshness Freshness { get; set; }
public decimal ConfidenceScore { get; set; }

[MaxLength(8192)]
public string? AssumptionsJson { get; set; }

public DateTimeOffset TimeStampCreated { get; set; }
public DateTimeOffset TimeStampUpdated { get; set; }
```

Apply this to snapshot-level fields: `SessionFk`, `SnapshotTimestamp`, interval start/end if snapshot rows are interval-level, source, currency, total cost/rate, metadata freshness, confidence, and bounded `AssumptionsJson`.

### `sources/Api/Data/Hosting/CloudCostSnapshotContribution.cs` (model, CRUD)

**Analog:** `sources/Api/Data/Hosting/CloudResourceInventoryRecord.cs`

**Resource/session/owner pattern** (lines 12-18, 23-96):

```csharp
[Index(nameof(SessionFk))]
[Index(nameof(ProviderIdentifier), nameof(ProviderResourceId))]
[Index(nameof(Kind), nameof(State))]
[Index(nameof(GameServerFk))]
[Index(nameof(BillingAccountFk))]
[Index(nameof(BillingAssignmentFk))]
public class CloudResourceInventoryRecord
{
    [Key]
    public long PrimaryKey { get; set; }

    [ForeignKey(nameof(SessionFk))]
    public CloudGameServerSession? Session { get; set; }
    public long SessionFk { get; set; }

    [MaxLength(128)]
    public string ProviderIdentifier { get; set; } = HetznerProviderConfiguration.HetznerProviderIdentifier;

    public ECloudResourceKind Kind { get; set; }

    [ForeignKey(nameof(BillingAccountFk))]
    public BillingAccount? BillingAccount { get; set; }
    public long BillingAccountFk { get; set; }

    [ForeignKey(nameof(BillingAssignmentFk))]
    public GameServerBillingAssignment? BillingAssignment { get; set; }
    public long BillingAssignmentFk { get; set; }
}
```

**Provider deletion timestamp pattern** (lines 139-174):

```csharp
[MaxLength(8192)]
public string? DetailsJson { get; set; }

public DateTimeOffset TimeStampCreated { get; set; }
public DateTimeOffset TimeStampUpdated { get; set; }

/// <summary>
/// Timestamp when provider deletion stopped cost attribution for this resource.
/// </summary>
public DateTimeOffset? TimeStampDeleted { get; set; }
```

Contribution rows should carry `SnapshotFk`, `ResourceInventoryRecordFk`, `ECloudResourceKind`, `BillingAccountFk`, `BillingAssignmentFk`, `IntervalStart`, `IntervalEnd`, rate, currency, estimated cost, and source/freshness if planner chooses per-row metadata. Add a deterministic unique index such as `(SnapshotFk, ResourceInventoryRecordFk, BillingAssignmentFk, IntervalStart, IntervalEnd, Source)`.

### `sources/Api/Data/ApiDbContext.cs` (config, CRUD)

**Analog:** `sources/Api/Data/ApiDbContext.cs`

**DbSet placement** (lines 57-79):

```csharp
#region Hosting

public DbSet<ConfigurationEntry> ConfigurationEntries { get; set; } = null!;
public DbSet<BillingAccount> BillingAccounts { get; set; } = null!;
public DbSet<CloudCostEstimate> CloudCostEstimates { get; set; } = null!;
public DbSet<CloudDryRunPlan> CloudDryRunPlans { get; set; } = null!;
public DbSet<CloudGameServerSession> CloudGameServerSessions { get; set; } = null!;
public DbSet<CloudResourceInventoryRecord> CloudResourceInventoryRecords { get; set; } = null!;
public DbSet<GameServerBillingAssignment> GameServerBillingAssignments { get; set; } = null!;
public DbSet<HostingAuditRecord> HostingAuditRecords { get; set; } = null!;

#endregion
```

**Restrict FK pattern** (lines 244-272):

```csharp
modelBuilder.Entity<CloudCostEstimate>()
    .HasOne(q => q.GameServer)
    .WithMany()
    .HasForeignKey(q => q.GameServerFk)
    .OnDelete(DeleteBehavior.Restrict);

modelBuilder.Entity<CloudCostEstimate>()
    .HasOne(q => q.BillingAccount)
    .WithMany()
    .HasForeignKey(q => q.BillingAccountFk)
    .OnDelete(DeleteBehavior.Restrict);

modelBuilder.Entity<CloudCostEstimate>()
    .HasOne(q => q.BillingAssignment)
    .WithMany()
    .HasForeignKey(q => q.BillingAssignmentFk)
    .OnDelete(DeleteBehavior.Restrict);
```

Add `DbSet<CloudCostSnapshot>` and `DbSet<CloudCostSnapshotContribution>` in the Hosting region. Configure relationships to session, resource inventory, game server, billing account, and billing assignment with `DeleteBehavior.Restrict`.

### `sources/Api/Services/CloudCosts/ICloudRuntimeCostSnapshotService.cs` (service, request-response)

**Analog:** `sources/Api/Services/CloudPlanning/ICloudCostEstimateService.cs`

**Interface shape** (lines 1-23, 25-40):

```csharp
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.CloudPlanning;

public interface ICloudCostEstimateService
{
    Task<CloudCostEstimate> CreateEstimateAsync(
        long dryRunPlanId,
        int storageGigabytes,
        User actor,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CloudCostEstimate>> GetEstimatesAsync(CancellationToken cancellationToken);

    Task<CloudCostEstimate?> GetEstimateAsync(
        long estimateId,
        CancellationToken cancellationToken);
}
```

Runtime cost interface should expose explicit refresh/create methods, session aggregate reads, and latest snapshot reads. Keep actor-aware methods if audit records are appended.

### `sources/Api/Services/CloudCosts/CloudRuntimeCostSnapshotService.cs` (service, batch/CRUD)

**Analog:** `sources/Api/Services/CloudPlanning/CloudCostEstimateService.cs`

**Imports, DI registration, constructor pattern** (lines 1-23):

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.Services.CloudProviders.Hetzner;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Contract.GameServer;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.CloudPlanning;

[Scoped<CloudCostEstimateService, ICloudCostEstimateService>]
internal class CloudCostEstimateService(
    ApiDbContext apiDbContext,
    IConfiguration configuration,
    IBillingOwnershipService billingOwnershipService,
    IHetznerMetadataSyncService hetznerMetadataSyncService,
    IHostingAuditService hostingAuditService)
    : ICloudCostEstimateService
```

Use `[Scoped<CloudRuntimeCostSnapshotService, ICloudRuntimeCostSnapshotService>]`. Do not inject live Hetzner clients for Phase 17; use `ApiDbContext`, `IConfiguration`, `IHetznerMetadataSyncService`, `IHostingAuditService`, and optionally `IBillingOwnershipService`.

**Validation, cached metadata, config assumptions** (lines 39-91):

```csharp
ArgumentNullException.ThrowIfNull(actor);

var serverType = await apiDbContext.HetznerServerTypes
    .SingleOrDefaultAsync(q => q.Name == session.ServerTypeName, cancellationToken)
    .ConfigureAwait(false);
if (serverType is null)
    throw new InvalidOperationException($"Cached server type '{session.ServerTypeName}' does not exist.");

var monthlyEstimateHours = ResolvePositiveInt(
    Constants.Configuration.Hetzner.Arma3.MonthlyEstimateHours,
    DefaultMonthlyEstimateHours);
var storageMonthlyPrice = ResolveNonNegativeDecimal(Constants.Configuration.Hetzner.Arma3.VolumeMonthlyPrice);
var ipMonthlyPrice = ResolveNonNegativeDecimal(Constants.Configuration.Hetzner.Arma3.IpMonthlyPrice);
var now = DateTimeOffset.UtcNow;
var freshness = GetFreshness(dryRunPlan.MetadataSourceTimestamp, now);
```

**Transactional write + audit pattern** (lines 132-160):

```csharp
await using var transaction = await apiDbContext.Database
    .BeginTransactionAsync(cancellationToken)
    .ConfigureAwait(false);

apiDbContext.CloudCostEstimates.Add(estimate);
await apiDbContext
    .SaveChangesAsync(cancellationToken)
    .ConfigureAwait(false);

await hostingAuditService
    .AppendAsync(
        new HostingAuditRecord
        {
            Kind                = EHostingAuditEventKind.CloudCostEstimateCreated,
            TimeStampCreated    = now,
            ActorFk             = actor.PrimaryKey,
            GameServerFk        = dryRunPlan.GameServerFk,
            BillingAccountFk    = dryRunPlan.BillingAccountFk,
            BillingAssignmentFk = dryRunPlan.BillingAssignmentFk,
            Summary             = "Cloud cost estimate created.",
            Details             = $"Cloud cost estimate {estimate.PrimaryKey} created for dry-run plan {dryRunPlan.PrimaryKey}.",
        },
        cancellationToken
    )
    .ConfigureAwait(false);

await transaction
    .CommitAsync(cancellationToken)
    .ConfigureAwait(false);
```

For idempotent refresh, load existing deterministic snapshot/contribution rows inside the transaction, replace or update them, save, append a sanitized audit record, then commit.

**Freshness and bounded assumptions** (lines 194-226):

```csharp
private ECloudEstimateFreshness GetFreshness(
    DateTimeOffset? metadataSourceTimestamp,
    DateTimeOffset now)
{
    if (metadataSourceTimestamp is null)
        return ECloudEstimateFreshness.Unknown;
    return now - metadataSourceTimestamp.Value > hetznerMetadataSyncService.GetStaleThreshold()
        ? ECloudEstimateFreshness.Stale
        : ECloudEstimateFreshness.Fresh;
}

private static string SerializeAssumptions(...)
    => JsonSerializer.Serialize(
        new
        {
            monthlyEstimateHours,
            storageGigabytes,
            noProviderMutation = true,
        },
        SerializerOptions
    );
```

### `sources/Api/Services/CloudCosts/CloudCostIntervalCalculator.cs` (utility, transform)

**Analogs:** `sources/Api/Data/Hosting/GameServerBillingAssignment.cs`, `sources/Api/Data/Hosting/CloudResourceInventoryRecord.cs`, `sources/Api/Services/CloudTeardown/CloudTeardownService.cs`

**Assignment interval fields** (lines 64-72):

```csharp
public DateTimeOffset TimeStampAssigned { get; set; }
public DateTimeOffset? TimeStampUnassigned { get; set; }
```

**Resource lifetime fields** (`CloudResourceInventoryRecord.cs` lines 156-174):

```csharp
public DateTimeOffset TimeStampCreated { get; set; }
public DateTimeOffset TimeStampUpdated { get; set; }

/// <summary>
/// Timestamp when provider deletion stopped cost attribution for this resource.
/// </summary>
public DateTimeOffset? TimeStampDeleted { get; set; }
```

**Billable resource predicate** (`CloudTeardownService.cs` lines 468-474):

```csharp
private static int CountRemainingBillableProviderResources(IEnumerable<CloudResourceInventoryRecord> resources)
    => resources.Count(q => IsBillableProviderResource(q.Kind) && HasProviderResource(q));

private static bool IsBillableProviderResource(ECloudResourceKind kind)
    => kind is ECloudResourceKind.Server
        or ECloudResourceKind.IpAddress
        or ECloudResourceKind.Volume;
```

Calculator should intersect `[resource.TimeStampCreated, resource.TimeStampDeleted ?? snapshotTimestamp]` with `[assignment.TimeStampAssigned, assignment.TimeStampUnassigned ?? snapshotTimestamp]`. Ignore `GameServer.Status`, worker status, cleanup job status, VM power state, and Arma process state.

### Audit Redaction (service, event-driven)

**Analogs:** `sources/Api/Services/GameServerLifecycle/HostingAuditService.cs`, `sources/Api/Services/GameServerLifecycle/LifecycleFailureReasonSanitizer.cs`

**Audit append sanitization** (`HostingAuditService.cs` lines 14-23, 42-48):

```csharp
public async Task<HostingAuditRecord> AppendAsync(
    HostingAuditRecord auditRecord,
    CancellationToken cancellationToken)
{
    Prepare(auditRecord);
    apiDbContext.HostingAuditRecords.Add(auditRecord);
    await apiDbContext
        .SaveChangesAsync(cancellationToken)
        .ConfigureAwait(false);
    return auditRecord;
}

private static void Prepare(HostingAuditRecord auditRecord)
{
    if (auditRecord.TimeStampCreated == default)
        auditRecord.TimeStampCreated = DateTimeOffset.UtcNow;
    auditRecord.Summary = LifecycleFailureReasonSanitizer.SanitizeSummary(auditRecord.Summary);
    auditRecord.Details = LifecycleFailureReasonSanitizer.SanitizeDetails(auditRecord.Details);
}
```

**Secret detector** (`LifecycleFailureReasonSanitizer.cs` lines 56-63):

```csharp
private static bool MayContainSecret(string message)
    => message.Contains("password", StringComparison.OrdinalIgnoreCase)
       || message.Contains("secret", StringComparison.OrdinalIgnoreCase)
       || message.Contains("token", StringComparison.OrdinalIgnoreCase)
       || message.Contains("authorization", StringComparison.OrdinalIgnoreCase)
       || message.Contains("api key", StringComparison.OrdinalIgnoreCase)
       || message.Contains("apikey", StringComparison.OrdinalIgnoreCase)
       || message.Contains("connection string", StringComparison.OrdinalIgnoreCase);
```

Snapshot assumptions and audit details must avoid provider tokens, bootstrap credentials, Steam credentials, bearer headers, raw provider failures, and unbounded JSON.

### `tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs` (test, CRUD/request-response)

**Analog:** `tests/Api.Tests/CloudPlanning/Arma3CloudEstimateTests.cs`

**SQLite relational setup** (lines 25-31):

```csharp
[Fact]
public async Task CreatePlanAsync_PersistsCostEstimateFromCachedMetadataAndConfiguredAssumptions()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var context = await CreateReadyContextAsync(connection);
```

**Persisted cost assertions** (lines 55-89):

```csharp
context.DbContext.ChangeTracker.Clear();
var estimate = await context.DbContext.CloudCostEstimates.SingleAsync();
Assert.Equal(result.CostEstimateFk, estimate.PrimaryKey);
Assert.Equal(context.GameServer.PrimaryKey, estimate.GameServerFk);
Assert.Equal(context.BillingAccount.PrimaryKey, estimate.BillingAccountFk);
Assert.Equal(context.Assignment.PrimaryKey, estimate.BillingAssignmentFk);
Assert.Equal(0.011m, estimate.ServerHourlyPrice);
Assert.Equal("EUR", estimate.Currency);
Assert.Equal(ECloudEstimateFreshness.Fresh, estimate.Freshness);
Assert.Equal(1m, estimate.ConfidenceScore);
Assert.Contains("storageGigabytes", estimate.AssumptionsJson, StringComparison.Ordinal);
Assert.Contains("noProviderMutation", estimate.AssumptionsJson, StringComparison.Ordinal);

var audit = await context.DbContext.HostingAuditRecords
    .SingleAsync(q => q.Kind == EHostingAuditEventKind.CloudCostEstimateCreated);
Assert.Equal(context.GameServer.PrimaryKey, audit.GameServerFk);
Assert.Equal(context.BillingAccount.PrimaryKey, audit.BillingAccountFk);
Assert.Equal(context.Assignment.PrimaryKey, audit.BillingAssignmentFk);
```

Adapt this for snapshot service tests covering accumulated total, owner interval attribution, deletion timestamp cost stop, assumption/freshness/confidence visibility, and repeated refresh idempotency.

**SQLite `DateTimeOffset` conversion** (`Arma3CloudEstimateTests.cs` lines 416-424):

```csharp
private sealed class SqliteApiDbContext(DbContextOptions<SqliteApiDbContext> dbContextOptions)
    : ApiDbContext(dbContextOptions)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();
    }
}
```

**No live provider sync in tests** (`Arma3CloudEstimateTests.cs` lines 427-450):

```csharp
private sealed class TestMetadataSyncService(
    ApiDbContext dbContext,
    TimeSpan staleThreshold)
    : IHetznerMetadataSyncService
{
    public Task<HetznerMetadataSyncState> SyncAsync(
        User actor,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Estimate tests must not synchronize provider metadata.");

    public async Task<HetznerMetadataSyncState> GetStatusAsync(CancellationToken cancellationToken)
        => await dbContext.HetznerMetadataSyncStates
               .SingleOrDefaultAsync(
                   q => q.ProviderIdentifier == HetznerMetadataSyncState.HetznerProviderIdentifier,
                   cancellationToken
               )
               .ConfigureAwait(false)
           ?? new HetznerMetadataSyncState
           {
               ProviderIdentifier = HetznerMetadataSyncState.HetznerProviderIdentifier,
               Status             = EHetznerMetadataSyncStatus.NeverSynced,
           };
}
```

**Billing interval preservation assertion analog** (`BillingOwnershipAndAuditTests.cs` lines 62-78):

```csharp
dbContext.ChangeTracker.Clear();
var assignments = await dbContext.GameServerBillingAssignments
    .OrderBy(q => q.PrimaryKey)
    .ToListAsync();
Assert.Equal(2, assignments.Count);
Assert.NotNull(assignments[0].TimeStampUnassigned);
Assert.Null(assignments[1].TimeStampUnassigned);

var transferAudit = await dbContext.HostingAuditRecords
    .SingleAsync(q => q.Kind == EHostingAuditEventKind.BillingOwnerTransferred);
Assert.Equal(firstAccount.PrimaryKey, transferAudit.PreviousBillingAccountFk);
Assert.Equal(secondAccount.PrimaryKey, transferAudit.NewBillingAccountFk);
Assert.Equal(secondAssignment.PrimaryKey, transferAudit.BillingAssignmentFk);
```

Use this pattern to assert old snapshot/contribution rows remain attributed to the first assignment after a later owner transfer.

## Shared Patterns

### EF Hosting Entity Conventions

**Source:** `sources/Api/Data/Hosting/CloudCostEstimate.cs`, `sources/Api/Data/Hosting/CloudResourceInventoryRecord.cs`

Apply to all new persisted hosting rows:

```csharp
[Key]
public long PrimaryKey { get; set; }

[MaxLength(128)]
public string ProviderIdentifier { get; set; } = HetznerProviderConfiguration.HetznerProviderIdentifier;

[ForeignKey(nameof(BillingAssignmentFk))]
public GameServerBillingAssignment? BillingAssignment { get; set; }
public long BillingAssignmentFk { get; set; }

[MaxLength(8192)]
public string? AssumptionsJson { get; set; }
```

### Provider Metadata And Price Source

**Source:** `sources/Api/Data/Hosting/HetznerServerType.cs` lines 73-104

```csharp
public decimal HourlyPrice { get; set; }
public decimal MonthlyPrice { get; set; }

[MaxLength(16)]
public string Currency { get; set; } = string.Empty;

[MaxLength(512)]
public string? VatNote { get; set; }

public DateTimeOffset SourceTimestamp { get; set; }
```

Runtime cost snapshots should use cached `HetznerServerType` rows and configured IP/volume assumptions. Do not add live provider billing calls in this phase.

### Transaction Boundaries

**Source:** `sources/Api/Services/CloudInventory/CloudInventoryService.cs` lines 59-86 and 209-235

```csharp
await using var transaction = await apiDbContext.Database
    .BeginTransactionAsync(cancellationToken)
    .ConfigureAwait(false);

apiDbContext.CloudGameServerSessions.Add(session);
await apiDbContext
    .SaveChangesAsync(cancellationToken)
    .ConfigureAwait(false);
```

Use one EF transaction for snapshot row changes plus audit append.

### Secret-Safe Normalization

**Source:** `sources/Api/Services/CloudInventory/CloudInventoryService.cs` lines 531-543

```csharp
private static string? NormalizeSummary(string? value)
    => string.IsNullOrWhiteSpace(value)
        ? null
        : LifecycleFailureReasonSanitizer.SanitizeSummary(value.Trim());

private static string NormalizeTransitionReason(
    string? reason,
    ECloudResourceState previousState,
    ECloudResourceState state)
{
    if (string.IsNullOrWhiteSpace(reason))
        return $"State changed from {previousState} to {state}.";
    return LifecycleFailureReasonSanitizer.SanitizeSummary(reason.Trim());
}
```

Apply before persisting any user/provider text into assumptions, details, state reasons, or audit messages.

### Test Project Dependencies

**Source:** `tests/Api.Tests/Api.Tests.csproj` lines 1-25

```xml
<TargetFramework>net9.0</TargetFramework>
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.4" />
<PackageReference Include="xunit" Version="2.9.3" />
<ProjectReference Include="..\..\sources\Api\Api.csproj" />
```

No new test package is needed for Phase 17.

## No Analog Found

None. All likely Phase 17 file roles have close local analogs.

## Metadata

**Analog search scope:** `sources/Api/Data/Hosting`, `sources/Api/Data/ApiDbContext.cs`, `sources/Api/Services/CloudPlanning`, `sources/Api/Services/CloudInventory`, `sources/Api/Services/CloudTeardown`, `sources/Api/Services/GameServerLifecycle`, `sources/Contract/GameServer`, `tests/Api.Tests`
**Files scanned:** 20+ via `rg` and targeted reads
**Pattern extraction date:** 2026-06-17
