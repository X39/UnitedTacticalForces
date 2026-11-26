# Phase 21: Spending Policy Model And Evaluation Boundary - Pattern Map

**Mapped:** 2026-06-18
**Files analyzed:** 17 likely new/modified files
**Analogs found:** 17 / 17

Assumption: `21-CONTEXT.md`, `21-RESEARCH.md`, and `21-VALIDATION.md` define bounded areas rather than an exact file manifest. This map uses the likely files implied by COST-07, COST-19, RELY-17, and the research recommended structure.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `sources/Api/Data/Hosting/CloudSpendingPolicy.cs` | model | CRUD | `sources/Api/Data/Hosting/CloudCostSnapshot.cs` | role-match |
| `sources/Api/Data/Hosting/CloudSpendingPolicyDecision.cs` | model | request-response/audit | `sources/Api/Data/Hosting/CloudProviderReconciliationRun.cs` | exact |
| `sources/Api/Data/ApiDbContext.cs` | config | CRUD | existing Hosting mappings in `sources/Api/Data/ApiDbContext.cs` | exact |
| `sources/Api/Data/Migrations/*CloudSpendingPolicies*.cs` | migration | CRUD | `sources/Api/Data/Migrations/20260618003000_CloudProviderReconciliation.cs` | exact |
| `sources/Contract/GameServer/ECloudSpendingPolicyDecision.cs` | model | transform | `sources/Contract/GameServer/ECloudEstimateFreshness.cs` | role-match |
| `sources/Contract/GameServer/ECloudSpendingPolicyScope.cs` | model | transform | `sources/Contract/GameServer/ECloudResourceState.cs` | role-match |
| `sources/Contract/GameServer/ECloudSpendingPolicyAction.cs` | model | transform | `sources/Contract/GameServer/ECloudReconciliationFindingKind.cs` | role-match |
| `sources/Contract/GameServer/ECloudBillableActionKind.cs` | model | transform | `sources/Contract/GameServer/ECloudResourceKind.cs` | role-match |
| `sources/Api/DTO/CloudSpendingPolicyDto.cs` | DTO | transform | `sources/Api/DTO/CloudRuntimeCostSummaryDto.cs` | role-match |
| `sources/Api/DTO/CloudSpendingPolicyDecisionDto.cs` | DTO | transform | `sources/Api/DTO/CloudProviderReconciliationRunDto.cs` | exact |
| `sources/Api/DTO/Payloads/CloudSpendingPolicyEvaluationPayload.cs` | DTO/payload | request-response | `sources/Api/DTO/Payloads/CloudDryRunPlanCreationPayload.cs` | role-match |
| `sources/Api/Services/CloudPolicies/ICloudSpendingPolicyEvaluationService.cs` | service | request-response | `sources/Api/Services/CloudCosts/ICloudRuntimeCostSnapshotService.cs` | exact |
| `sources/Api/Services/CloudPolicies/CloudSpendingPolicyEvaluationService.cs` | service | request-response/audit | `sources/Api/Services/CloudCosts/CloudRuntimeCostSnapshotService.cs` | exact |
| `sources/Api/Transform.cs` | utility | transform | existing methods in `sources/Api/Transform.cs` | exact |
| `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyConfigurationTests.cs` | test | CRUD | `tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs` | role-match |
| `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyEvaluationTests.cs` | test | request-response/audit | `tests/Api.Tests/CloudReconciliation/CloudProviderReconciliationServiceTests.cs` | exact |
| `tests/Api.Tests/Api.Tests.csproj` | config | batch | existing API test project | exact |

## Pattern Assignments

### `sources/Api/Data/Hosting/CloudSpendingPolicy.cs` (model, CRUD)

**Analog:** `sources/Api/Data/Hosting/CloudCostSnapshot.cs`

**Imports and entity attributes** (lines 1-16):
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Contract.GameServer;

namespace X39.UnitedTacticalForces.Api.Data.Hosting;

[Index(nameof(SessionFk), nameof(Source), IsUnique = true)]
[Index(nameof(GameServerFk))]
[Index(nameof(ProviderIdentifier), nameof(Source))]
[Index(nameof(RefreshedByFk))]
public class CloudCostSnapshot
```

**Property conventions** (lines 21-28, 76-83, 112-123):
```csharp
[Key]
public long PrimaryKey { get; set; }

[MaxLength(128)]
public string ProviderIdentifier { get; set; } = HetznerProviderConfiguration.HetznerProviderIdentifier;

[MaxLength(16)]
public string Currency { get; set; } = string.Empty;

public decimal TotalEstimatedCost { get; set; }

[MaxLength(8192)]
public string? AssumptionsJson { get; set; }

public DateTimeOffset TimeStampCreated { get; set; }
public DateTimeOffset TimeStampUpdated { get; set; }
```

Copy this shape for threshold decimals, owner/global scope, effective dates, active flag, audit actor FKs, bounded JSON, and created/updated timestamps.

### `sources/Api/Data/Hosting/CloudSpendingPolicyDecision.cs` (model, request-response/audit)

**Analog:** `sources/Api/Data/Hosting/CloudProviderReconciliationRun.cs`

**Durable run/audit state** (lines 12-15, 20-27, 43-53, 93-126):
```csharp
[Index(nameof(ProviderIdentifier), nameof(Status))]
[Index(nameof(RequestedByFk))]
[Index(nameof(TimeStampStarted))]
public class CloudProviderReconciliationRun
{
    [Key]
    public long PrimaryKey { get; set; }

    [MaxLength(128)]
    public string ProviderIdentifier { get; set; } = HetznerProviderConfiguration.HetznerProviderIdentifier;

    public ECloudReconciliationRunStatus Status { get; set; }
    public DateTimeOffset TimeStampStarted { get; set; }
    public DateTimeOffset? TimeStampCompleted { get; set; }

    public int UnknownProviderResourceCount { get; set; }
    public int CleanupNeededBillableCount { get; set; }

    [MaxLength(128)]
    public string? FailureCategory { get; set; }

    [MaxLength(2048)]
    public string? SanitizedFailureSummary { get; set; }

    [MaxLength(8192)]
    public string? SanitizedFailureDetails { get; set; }
}
```

Use the same bounded failure fields for failed policy evaluations. Add a unique `IdempotencyKey` index in `ApiDbContext`, because decision retry safety is a phase requirement.

### `sources/Api/Data/ApiDbContext.cs` (config, CRUD)

**Analog:** existing Hosting DbSets and relationships in `sources/Api/Data/ApiDbContext.cs`

**DbSet placement** (lines 57-83):
```csharp
#region Hosting

public DbSet<ConfigurationEntry> ConfigurationEntries { get; set; } = null!;
public DbSet<BillingAccount> BillingAccounts { get; set; } = null!;
public DbSet<CloudCostEstimate> CloudCostEstimates { get; set; } = null!;
public DbSet<CloudCostSnapshot> CloudCostSnapshots { get; set; } = null!;
public DbSet<CloudProviderReconciliationRun> CloudProviderReconciliationRuns { get; set; } = null!;
public DbSet<CloudProviderReconciliationFinding> CloudProviderReconciliationFindings { get; set; } = null!;
public DbSet<CloudResourceInventoryRecord> CloudResourceInventoryRecords { get; set; } = null!;
```

**Unique and restricted relationship patterns** (lines 278-316, 401-435):
```csharp
modelBuilder.Entity<CloudCostSnapshot>()
    .HasIndex(q => new
    {
        q.SessionFk,
        q.Source,
    })
    .HasDatabaseName("IX_CloudCostSnapshots_SessionFk_Source")
    .IsUnique();

modelBuilder.Entity<CloudProviderReconciliationFinding>()
    .HasOne(q => q.Run)
    .WithMany(q => q.Findings)
    .HasForeignKey(q => q.RunFk)
    .OnDelete(DeleteBehavior.Restrict);

modelBuilder.Entity<CloudProviderReconciliationFinding>()
    .HasOne(q => q.BillingAccount)
    .WithMany()
    .HasForeignKey(q => q.BillingAccountFk)
    .OnDelete(DeleteBehavior.Restrict);
```

Add policy/decision DbSets in Hosting, unique indexes for active/effective policy resolution and decision idempotency, and explicit `DeleteBehavior.Restrict` for history-preserving FKs.

### `sources/Api/Data/Migrations/*CloudSpendingPolicies*.cs` (migration, CRUD)

**Analog:** `sources/Api/Data/Migrations/20260618003000_CloudProviderReconciliation.cs`

**Generated migration shape** (lines 13-58, 60-101):
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(
        name: "CloudProviderReconciliationRuns",
        columns: table => new
        {
            PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            ProviderIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            RequestedByFk = table.Column<Guid>(type: "uuid", nullable: true),
            Status = table.Column<int>(type: "integer", nullable: false),
            TimeStampStarted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            SanitizedFailureSummary = table.Column<string>(
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true),
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_CloudProviderReconciliationRuns", x => x.PrimaryKey);
            table.ForeignKey(
                name: "FK_CloudReconRuns_Users_RequestedByFk",
                column: x => x.RequestedByFk,
                principalTable: "Users",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Restrict);
        });
}
```

Treat migrations as generated output. Use EF tooling after model/context edits; do not hand-style source from migrations.

### `sources/Contract/GameServer/ECloudSpendingPolicyDecision.cs` and related enums (model, transform)

**Analog:** `sources/Contract/GameServer/ECloudEstimateFreshness.cs`

**Enum style** (lines 1-22):
```csharp
namespace X39.UnitedTacticalForces.Contract.GameServer;

/// <summary>
/// Describes metadata freshness for a persisted cloud cost estimate.
/// </summary>
public enum ECloudEstimateFreshness
{
    Fresh = 0,
    Stale = 1,
    Unknown = 2,
}
```

**Risk enum source to consume** (`ECloudReconciliationFindingKind.cs`, lines 6-26):
```csharp
public enum ECloudReconciliationFindingKind
{
    UnknownProviderResource = 0,
    MissingProviderResource = 1,
    StateMismatch = 2,
    CleanupNeededBillableResource = 3,
}
```

Create one public enum per file, `E` prefix, explicit integer values, XML summary comments. Policy decisions should include `Allow`, `Warn`, `ApprovalRequired`, and `Deny`.

### `sources/Api/DTO/CloudSpendingPolicyDto.cs` and `CloudSpendingPolicyDecisionDto.cs` (DTO, transform)

**Analog:** `sources/Api/DTO/CloudRuntimeCostSummaryDto.cs`

**DTO record style** (lines 1-8, 38-83):
```csharp
using X39.UnitedTacticalForces.Contract.GameServer;

namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Describes the latest persisted runtime cost summary for one cloud session.
/// </summary>
public record CloudRuntimeCostSummaryDto
{
    public string Currency { get; init; } = string.Empty;
    public decimal AccumulatedEstimatedCost { get; init; }
    public int BillableResourceCount { get; init; }
    public ECloudEstimateFreshness Freshness { get; init; }
    public string? AssumptionsJson { get; init; }
    public IReadOnlyList<CloudRuntimeCostOwnerIntervalDto> OwnerIntervals { get; init; } = [];
}
```

DTOs should expose persisted policy/decision fields and sanitized snapshots only. Do not expose provider tokens, raw exception details, or secret-bearing configuration.

### `sources/Api/DTO/Payloads/CloudSpendingPolicyEvaluationPayload.cs` (DTO/payload, request-response)

**Analog:** `sources/Api/DTO/Payloads/CloudDryRunPlanCreationPayload.cs`

**Payload style** (lines 1-7, 11-21, 53-66):
```csharp
namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Payload used to persist a dry-run cloud inventory plan.
/// </summary>
public record CloudDryRunPlanCreationPayload
{
    public long GameServerFk { get; init; }
    public string? ProviderIdentifier { get; init; }
    public string? DisplayName { get; init; }
    public string? ValidationDetailsJson { get; init; }
    public IReadOnlyList<CloudPlannedResourcePayload> PlannedResources { get; init; } = [];
}
```

Evaluation payload should carry proposed action kind, idempotency key, owner/session/server IDs, projected additional cost, currency, and bounded metadata JSON.

### `sources/Api/Services/CloudPolicies/ICloudSpendingPolicyEvaluationService.cs` (service, request-response)

**Analog:** `sources/Api/Services/CloudCosts/ICloudRuntimeCostSnapshotService.cs`

**Interface contract style** (lines 1-10, 23-41):
```csharp
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;

namespace X39.UnitedTacticalForces.Api.Services.CloudCosts;

/// <summary>
/// Refreshes and reads persisted cloud runtime cost snapshots.
/// </summary>
public interface ICloudRuntimeCostSnapshotService
{
    Task<CloudCostSnapshot?> GetLatestSessionSnapshotAsync(
        long sessionId,
        CancellationToken cancellationToken);

    Task<CloudRuntimeCostSummary?> GetSessionCostSummaryAsync(
        long sessionId,
        CancellationToken cancellationToken);
}
```

Expose a provider-mutation-free method such as `EvaluateAsync(payload, actor, cancellationToken)` returning a persisted `CloudSpendingPolicyDecision`.

### `sources/Api/Services/CloudPolicies/CloudSpendingPolicyEvaluationService.cs` (service, request-response/audit)

**Analog:** `sources/Api/Services/CloudCosts/CloudRuntimeCostSnapshotService.cs`

**Imports, DI, JSON options** (lines 1-27):
```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Hosting;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Contract.GameServer;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.CloudCosts;

[Scoped<CloudRuntimeCostSnapshotService, ICloudRuntimeCostSnapshotService>]
internal class CloudRuntimeCostSnapshotService(
    ApiDbContext apiDbContext,
    IConfiguration configuration,
    IHetznerMetadataSyncService hetznerMetadataSyncService,
    IHostingAuditService hostingAuditService)
```

**Durable transaction/upsert pattern** (lines 79-123, 138-164):
```csharp
await using var transaction = await apiDbContext.Database
    .BeginTransactionAsync(cancellationToken)
    .ConfigureAwait(false);
var snapshot = await apiDbContext.CloudCostSnapshots
    .Include(q => q.Contributions)
    .SingleOrDefaultAsync(
        q => q.SessionFk == session.PrimaryKey && q.Source == ECloudCostSnapshotSource.Estimated,
        cancellationToken
    )
    .ConfigureAwait(false);
if (snapshot is null)
{
    snapshot = new CloudCostSnapshot
    {
        TimeStampCreated = snapshotTimestamp,
    };
    apiDbContext.CloudCostSnapshots.Add(snapshot);
}

await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
```

**Cost and freshness inputs** (lines 174-195, 252-264, 374-392):
```csharp
var snapshot = await apiDbContext.CloudCostSnapshots
    .Include(q => q.Contributions)
    .Where(q => q.SessionFk == sessionId && q.Source == ECloudCostSnapshotSource.Estimated)
    .OrderByDescending(q => q.TimeStampSnapshot)
    .ThenByDescending(q => q.PrimaryKey)
    .FirstOrDefaultAsync(cancellationToken)
    .ConfigureAwait(false);

return new CloudRuntimeCostSummary(
    snapshot.SessionFk,
    snapshot.PrimaryKey,
    snapshot.TimeStampSnapshot,
    snapshot.Currency,
    snapshot.TotalEstimatedCost,
    snapshot.TotalBillableHours,
    snapshot.BillableResourceCount,
    snapshot.Freshness,
    snapshot.ConfidenceScore,
    ownerIntervals,
    resourceContributions
);

private ECloudEstimateFreshness GetFreshness(DateTimeOffset? metadataSourceTimestamp, DateTimeOffset now)
{
    if (metadataSourceTimestamp is null)
        return ECloudEstimateFreshness.Unknown;
    return now - metadataSourceTimestamp.Value > hetznerMetadataSyncService.GetStaleThreshold()
        ? ECloudEstimateFreshness.Stale
        : ECloudEstimateFreshness.Fresh;
}
```

Policy evaluation should load effective policy, latest cost snapshot/summary, latest reconciliation run/findings, and persist the decision in one transaction. It must not inject `IHetznerCloudClient`, `HttpClient`, provisioning, or teardown services.

### `sources/Api/Transform.cs` (utility, transform)

**Analog:** existing methods in `sources/Api/Transform.cs`

**Projection style** (lines 223-293):
```csharp
public static CloudProviderReconciliationRunDto ToDto(this CloudProviderReconciliationRun self)
    => new()
    {
        PrimaryKey                   = self.PrimaryKey,
        ProviderIdentifier           = self.ProviderIdentifier,
        RequestedByFk                = self.RequestedByFk,
        Status                       = self.Status,
        UnknownProviderResourceCount = self.UnknownProviderResourceCount,
        CleanupNeededBillableCount   = self.CleanupNeededBillableCount,
        FailureCategory              = self.FailureCategory,
        SanitizedFailureSummary      = self.SanitizedFailureSummary,
        SanitizedFailureDetails      = self.SanitizedFailureDetails,
        TimeStampCreated             = self.TimeStampCreated,
        TimeStampUpdated             = self.TimeStampUpdated,
    };

public static CloudRuntimeCostSummaryDto ToDto(
    this CloudRuntimeCostSummary self,
    CloudCostSnapshot snapshot)
    => new()
    {
        SessionFk                = self.SessionFk,
        SnapshotFk               = self.SnapshotFk,
        AccumulatedEstimatedCost = self.TotalEstimatedCost,
        Freshness                = self.Freshness,
        AssumptionsJson          = snapshot.AssumptionsJson,
    };
```

Add `ToDto` methods for policy and decision records. Keep mapping explicit and flat; use arrays for nested lists.

## Shared Patterns

### Sanitized Persistence

**Source:** `sources/Api/Services/GameServerLifecycle/LifecycleFailureReasonSanitizer.cs`
**Apply to:** policy decision errors, input snapshot details, audit details

```csharp
public static string Sanitize(Exception exception)
{
    var message = SanitizeMessage(exception.Message);
    if (string.IsNullOrWhiteSpace(message))
        message = exception.GetType().Name;
    return Truncate($"{exception.GetType().Name}: {message}", MaxFailureReasonLength);
}

public static string? SanitizeDetails(string? details)
{
    if (string.IsNullOrWhiteSpace(details))
        return null;
    return Truncate(SanitizeMessage(details), MaxFailureReasonLength);
}

private static bool MayContainSecret(string message)
    => message.Contains("password", StringComparison.OrdinalIgnoreCase)
       || message.Contains("secret", StringComparison.OrdinalIgnoreCase)
       || message.Contains("token", StringComparison.OrdinalIgnoreCase)
       || message.Contains("authorization", StringComparison.OrdinalIgnoreCase)
       || message.Contains("api key", StringComparison.OrdinalIgnoreCase);
```

### Reconciliation Risk Inputs

**Source:** `sources/Api/Services/CloudReconciliation/CloudProviderReconciliationService.cs`
**Apply to:** evaluation service risk classification

```csharp
private static bool IsActiveProviderInventory(CloudResourceInventoryRecord resource)
    => !string.IsNullOrWhiteSpace(resource.ProviderResourceId)
       && resource.State != ECloudResourceState.Deleted
       && resource.TimeStampDeleted is null;

private static bool IsOrphanFinding(CloudProviderReconciliationFinding finding)
    => finding.InventoryRecordFk is null
       || finding.BillingAccountFk is null
       || finding.BillingAssignmentFk is null;

private static int CountFindings(
    IEnumerable<CloudProviderReconciliationFinding> findings,
    ECloudReconciliationFindingKind kind)
    => findings.Count(q => q.Kind == kind);
```

### JSON Snapshot Details

**Source:** `sources/Api/Services/CloudReconciliation/CloudProviderReconciliationService.cs`
**Apply to:** decision input snapshots and risk details

```csharp
private const int MaxDetailsJsonLength = 8192;

private static readonly JsonSerializerOptions SerializerOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
};

private static string SerializeDetails<T>(T value)
    => Truncate(JsonSerializer.Serialize(value, SerializerOptions), MaxDetailsJsonLength);
```

### Provider-Mutation-Free Tests

**Source:** `tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs`
**Apply to:** `CloudSpendingPolicyEvaluationTests`

```csharp
[Fact]
public void CloudRuntimeCostSnapshotService_DoesNotDependOnProviderClientOrHttpClient()
{
    var constructorParameterTypes = typeof(CloudRuntimeCostSnapshotService)
        .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .SelectMany(q => q.GetParameters())
        .Select(q => q.ParameterType)
        .ToArray();
    Assert.DoesNotContain(typeof(IHetznerCloudClient), constructorParameterTypes);
    Assert.DoesNotContain(typeof(IHttpClientFactory), constructorParameterTypes);
    Assert.DoesNotContain(typeof(HttpClient), constructorParameterTypes);
}
```

### SQLite API Test Harness

**Source:** `tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs`
**Apply to:** all Phase 21 API service tests

```csharp
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var context = await CreateReadyContextAsync(connection);

private static async Task<ApiDbContext> CreateDbContextAsync(SqliteConnection connection)
{
    var options = new DbContextOptionsBuilder<SqliteApiDbContext>()
        .UseSqlite(connection)
        .Options;
    var dbContext = new SqliteApiDbContext(options);
    await dbContext.Database.EnsureCreatedAsync();
    return dbContext;
}

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

### Sensitive Data Assertions

**Source:** `tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs`
**Apply to:** policy decision snapshot/error tests

```csharp
private static void AssertNoSensitiveMaterial(string? value)
{
    Assert.NotNull(value);
    foreach (var sensitiveWord in SensitiveWords)
    {
        Assert.DoesNotContain(sensitiveWord, value, StringComparison.OrdinalIgnoreCase);
    }
}
```

## Test Pattern Assignments

### `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyConfigurationTests.cs`

Use `CloudRuntimeCostSnapshotServiceTests` for SQLite setup and seeded `User`, `BillingAccount`, `GameServerBillingAssignment`, and `CloudGameServerSession` helpers. Cover invalid thresholds, owner/global scope, effective date selection, inactive/expired policy fallback, and DbContext index/relationship behavior.

### `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyEvaluationTests.cs`

Use `CloudProviderReconciliationServiceTests` for persisted counts/findings/audits and provider-mutation assertions.

**Risk assertion analog** (`CloudProviderReconciliationServiceTests.cs`, lines 37-58, 71-90):
```csharp
Assert.Equal(ECloudReconciliationRunStatus.Completed, result.Run.Status);
Assert.Equal(1, result.Run.UnknownProviderResourceCount);
Assert.Equal(1, result.Run.CleanupNeededBillableCount);
Assert.Empty(client.MutationCalls);

var unknown = Assert.Single(findings, q => q.Kind == ECloudReconciliationFindingKind.UnknownProviderResource);
Assert.Equal(ECloudReconciliationFindingSeverity.Critical, unknown.Severity);
Assert.Null(unknown.BillingAccountFk);

var cleanup = Assert.Single(findings, q => q.Kind == ECloudReconciliationFindingKind.CleanupNeededBillableResource);
Assert.Equal(ECloudResourceState.CleanupNeeded, cleanup.ExpectedInventoryState);
Assert.Equal(context.BillingAccount.PrimaryKey, cleanup.BillingAccountFk);
```

Cover threshold math, stale metadata, stale reconciliation, cleanup-needed resources, unknown billable resources, sanitized failures, idempotent retry, and reload with a new service/context instance.

## No Analog Found

None. Every likely Phase 21 file has a close analog in the existing Hosting, cloud-cost, reconciliation, DTO/transform, or API test slices.

## Metadata

**Analog search scope:** `sources/Api/Data/Hosting`, `sources/Api/Data/ApiDbContext.cs`, `sources/Api/Services/CloudCosts`, `sources/Api/Services/CloudReconciliation`, `sources/Api/Services/GameServerLifecycle`, `sources/Api/DTO`, `sources/Contract/GameServer`, `tests/Api.Tests`.
**Files scanned:** 60+ paths by `rg --files`/`rg`.
**Pattern extraction date:** 2026-06-18
