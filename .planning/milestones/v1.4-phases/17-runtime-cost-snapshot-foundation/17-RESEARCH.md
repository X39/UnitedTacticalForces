# Phase 17: Runtime Cost Snapshot Foundation - Research

**Researched:** 2026-06-17
**Domain:** ASP.NET Core API, EF Core persistence, Hetzner cost attribution
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
No explicit locked implementation decisions were captured. [VERIFIED: .planning/phases/17-runtime-cost-snapshot-foundation/17-CONTEXT.md]

### The agent's Discretion
All implementation choices are at the agent's discretion - discuss phase was skipped per user setting. Use ROADMAP phase goal, success criteria, and codebase conventions to guide decisions. [VERIFIED: .planning/phases/17-runtime-cost-snapshot-foundation/17-CONTEXT.md]

### Deferred Ideas (OUT OF SCOPE)
None - discuss phase skipped. [VERIFIED: .planning/phases/17-runtime-cost-snapshot-foundation/17-CONTEXT.md]
</user_constraints>

## Summary

Phase 17 should add durable runtime cost snapshots in the API data layer, not in the WebApp or worker runtime. The repository already has the required building blocks: `CloudGameServerSession`, `CloudResourceInventoryRecord`, `GameServerBillingAssignment`, `BillingAccount`, `HetznerServerType`, `HetznerMetadataSyncState`, `HostingAuditRecord`, and scoped service registration through `X39.Util.DependencyInjection`. [VERIFIED: codebase grep]

Use a new persisted `CloudCostSnapshot` entity plus contribution rows or bounded JSON contribution detail. Prefer contribution rows if the planner expects resource-level inspection, because COST-12 requires billable resource contribution visibility. [VERIFIED: .planning/REQUIREMENTS.md] The calculation service should compute cost from provider-resource existence intervals, cached pricing and assumptions, and overlapping billing-assignment intervals. [VERIFIED: .planning/ROADMAP.md]

Hetzner billing rules support the roadmap boundary: cloud servers are billed until deletion, including when powered off, Floating IPs are billed fractionally by month, and Primary IPs are billed separately while they exist. [CITED: https://docs.hetzner.com/cloud/billing/faq/] Existing teardown code already treats only server, IP address, and volume inventory records as remaining billable provider resources, and marks `TimeStampDeleted` only after provider deletion succeeds. [VERIFIED: sources/Api/Services/CloudTeardown/CloudTeardownService.cs]

**Primary recommendation:** Implement a scoped `ICloudRuntimeCostSnapshotService` that creates idempotent snapshot rows per `(session, resource, owner assignment, interval end/source)` using cached Hetzner prices, resource `TimeStampCreated` to `TimeStampDeleted ?? snapshot timestamp`, and assignment interval overlap. [VERIFIED: codebase grep]

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| Runtime cost snapshot persistence | Database / Storage | API / Backend | Snapshot records must survive API restarts and are EF Core entities/migrations in `sources/Api/Data/Hosting`. [VERIFIED: .planning/REQUIREMENTS.md] |
| Runtime cost calculation | API / Backend | Database / Storage | Calculation is business logic over persisted inventory, cached pricing, and owner intervals. [VERIFIED: sources/Api/Data/Hosting] |
| Billing attribution by owner interval | API / Backend | Database / Storage | `GameServerBillingAssignment` already stores assigned/unassigned timestamps and billing account FK. [VERIFIED: sources/Api/Data/Hosting/GameServerBillingAssignment.cs] |
| Provider cost-stop boundary | Database / Storage | API / Backend | `CloudResourceInventoryRecord.TimeStampDeleted` is the durable provider deletion timestamp. [VERIFIED: sources/Api/Data/Hosting/CloudResourceInventoryRecord.cs] |
| Audit and redaction | API / Backend | Database / Storage | `HostingAuditService` sanitizes summaries/details before writing audit records. [VERIFIED: sources/Api/Services/GameServerLifecycle/HostingAuditService.cs] |
| Future UI/API visibility | API / Backend | Browser / Client | Phase 17 should persist and calculate; Phase 19 owns DTO/client/UI surfacing. [VERIFIED: .planning/ROADMAP.md] |

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| COST-09 | User can see accumulated historical cost for allowed cloud sessions. | Store snapshots with session/resource/owner interval fields and expose service methods that aggregate by session without rewriting prior owner rows. [VERIFIED: .planning/REQUIREMENTS.md] |
| COST-10 | Cost snapshots preserve billing account and billing assignment attribution at the time the cost interval is recorded. | Use `BillingAccountFk` and `BillingAssignmentFk` on snapshot/contribution rows, following existing dry-run estimate and inventory patterns. [VERIFIED: sources/Api/Data/Hosting/CloudCostEstimate.cs] |
| COST-11 | Provider deletion, not runtime stop or powered-off state, is the stop boundary. | Use provider inventory `TimeStampDeleted`; ignore `GameServer.Status`, worker heartbeat, cleanup job state, and Arma process status for cost-stop logic. [VERIFIED: sources/Api/Services/CloudTeardown/CloudTeardownService.cs] |
| COST-12 | Admin can inspect assumptions, freshness, currency, confidence, and contribution without secrets. | Reuse `Currency`, `MetadataSourceTimestamp`, freshness/confidence enums, and sanitized assumption JSON; add per-resource contribution detail. [VERIFIED: sources/Api/Data/Hosting/CloudCostEstimate.cs] |
| RELY-14 | Snapshot/reconciliation state survives restarts and refreshes idempotently. | Persist snapshots in EF Core with deterministic unique keys and transactions; EF Core supports transactional `SaveChanges` and manual transactions. [CITED: https://learn.microsoft.com/en-us/ef/core/saving/transactions] |
</phase_requirements>

## Project Constraints (from AGENTS.md)

- Keep the existing game-controller abstraction during this milestone; runtime cost work must not replace Arma controller behavior. [VERIFIED: AGENTS.md]
- Lifecycle, job, worker, ownership, and audit state must survive API restarts. [VERIFIED: AGENTS.md]
- Every cost-generating or hosting session must have an owner from creation until cleanup or transfer completes. [VERIFIED: AGENTS.md]
- Do not implement Hetzner provisioning in this phase. [VERIFIED: AGENTS.md]
- Steam credentials, provider tokens, worker credentials, and secret-bearing values must not reach frontend clients or unredacted logs. [VERIFIED: AGENTS.md]
- Existing Aspire, PostgreSQL, API, and Blazor WebApp local development should continue to work. [VERIFIED: AGENTS.md]
- Add focused tests for persistence, cost transitions, authorization-sensitive behavior, and worker recovery boundaries. [VERIFIED: AGENTS.md]
- Use one public type per `.cs` file, file-scoped namespaces, PascalCase public members, `_` private fields, and existing feature folder patterns. [VERIFIED: AGENTS.md]
- Treat `sources/WebApp/UtfApi/**` and `sources/Api/Data/Migrations/**` as generated code. [VERIFIED: AGENTS.md]
- Use structured logging and never log secrets or raw secret configuration values. [VERIFIED: AGENTS.md]

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET / ASP.NET Core | `net9.0`, packages `9.0.4` | API host, controllers, DI, auth, OpenAPI | Active API project targets `net9.0`; cloud inventory endpoints are ASP.NET Core controllers. [VERIFIED: sources/Api/Api.csproj] |
| Entity Framework Core | `9.0.4` | Entities, relationships, migrations, PostgreSQL persistence | Existing hosting state uses EF entities, DbSets, indexes, migrations, and Npgsql. [VERIFIED: sources/Api/Api.csproj] |
| Npgsql EF Core provider | `9.0.4` | PostgreSQL database provider | API config uses `UseNpgsql` for `ConnectionStrings:database`. [VERIFIED: AGENTS.md] |
| xUnit | `2.9.3` | Focused API service tests | `tests/Api.Tests` already covers cloud planning, provisioning, teardown, ownership, and redaction. [VERIFIED: tests/Api.Tests/Api.Tests.csproj] |
| Microsoft.Data.Sqlite / EF Core Sqlite | `9.0.4` | Fast relational service tests | Existing API tests use in-memory SQLite with DateTimeOffset conversion. [VERIFIED: tests/Api.Tests/GameServerLifecycle/BillingOwnershipAndAuditTests.cs] |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `X39.Util.DependencyInjection` | `2.0.1.8` | Attribute-driven service registration | Register new scoped runtime cost service with `[Scoped<Impl, Interface>]`. [VERIFIED: sources/Api/Api.csproj] |
| `System.Text.Json` | shared framework | Bounded assumption/contribution JSON | Use for safe, bounded details matching inventory and estimate services. [VERIFIED: sources/Api/Services/CloudPlanning/CloudCostEstimateService.cs] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| New persisted snapshot entity | Calculated-only query projection | Fails RELY-14 restart-safe durable snapshot requirement. [VERIFIED: .planning/REQUIREMENTS.md] |
| Resource contribution table | JSON-only contribution detail | JSON is simpler but weaker for future filtering, owner/resource aggregation, and admin inspection. [ASSUMED] |
| Hosted background refresh in Phase 17 | Explicit service method called by existing endpoints/tests | Background scheduling adds lifecycle complexity; Phase 17 only requires foundation and idempotent refresh semantics, not a timer. [VERIFIED: .planning/ROADMAP.md] |

**Installation:** No new external packages are required. [VERIFIED: codebase grep]

**Version verification:** `dotnet --version` returned `10.0.201`, `dotnet ef --version` returned `10.0.5`, and project package references remain EF Core/API `9.0.4`. [VERIFIED: local command]

## Package Legitimacy Audit

No new packages should be installed for this phase. [VERIFIED: codebase grep] Package legitimacy gate is not applicable because the standard stack uses existing project dependencies. [VERIFIED: sources/Api/Api.csproj]

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| None new | NuGet | N/A | N/A | N/A | N/A | Approved: no install |

**Packages removed due to slopcheck [SLOP] verdict:** none. [VERIFIED: no new package recommendation]
**Packages flagged as suspicious [SUS]:** none. [VERIFIED: no new package recommendation]

## Architecture Patterns

### System Architecture Diagram

```text
Cloud session + inventory rows
        |
        v
Load billable resources (Server, IpAddress, Volume)
        |
        v
For each resource, build provider lifetime interval:
TimeStampCreated -> TimeStampDeleted or snapshot timestamp
        |
        v
Load billing assignments for same game server
        |
        v
Intersect resource lifetime with assignment intervals
        |
        v
Resolve rate source:
server type cached hourly/monthly, configured IP/volume assumptions, metadata freshness
        |
        v
Create/refresh deterministic snapshot + contribution rows in EF transaction
        |
        v
Append secret-safe hosting audit record
        |
        v
Historical service reads aggregate immutable intervals by session and owner
```

### Recommended Project Structure

```text
sources/Api/
├── Data/Hosting/
│   ├── CloudCostSnapshot.cs              # persisted interval-level snapshot
│   ├── CloudCostSnapshotContribution.cs  # resource-level contribution rows
│   └── Enums/                            # snapshot source/confidence/rate enums if API-only
├── Services/CloudCosts/
│   ├── ICloudRuntimeCostSnapshotService.cs
│   ├── CloudRuntimeCostSnapshotService.cs
│   └── CloudCostIntervalCalculator.cs
└── DTO/                                  # only if Phase 17 needs internal/admin DTOs

sources/Contract/GameServer/
└── ECloudCostSource.cs                   # only shared if future WebApp/Kiota needs enum in Phase 19

tests/Api.Tests/CloudCosts/
└── CloudRuntimeCostSnapshotServiceTests.cs
```

### Pattern 1: EF Entity With Indexed Snapshot Keys

**What:** Add hosting entities with explicit FKs, `DateTimeOffset` interval fields, decimal money fields, bounded JSON, and `[Index]` attributes. [VERIFIED: sources/Api/Data/Hosting/CloudCostEstimate.cs]

**When to use:** Use for durable cost snapshots and contribution rows because current hosting persistence follows entity-per-file plus DbSet registration. [VERIFIED: sources/Api/Data/ApiDbContext.cs]

**Example:**

```csharp
// Source: existing CloudCostEstimate/CloudResourceInventoryRecord pattern
[Index(nameof(SessionFk), nameof(TimeStampSnapshot))]
[Index(nameof(SessionFk), nameof(ResourceInventoryRecordFk), nameof(BillingAssignmentFk), nameof(IntervalStart), nameof(IntervalEnd), IsUnique = true)]
public class CloudCostSnapshotContribution
{
    [Key]
    public long PrimaryKey { get; set; }
    public long SessionFk { get; set; }
    public long ResourceInventoryRecordFk { get; set; }
    public long BillingAccountFk { get; set; }
    public long BillingAssignmentFk { get; set; }
    public ECloudResourceKind ResourceKind { get; set; }
    public DateTimeOffset IntervalStart { get; set; }
    public DateTimeOffset IntervalEnd { get; set; }
    public decimal RateAmount { get; set; }
    public decimal EstimatedCost { get; set; }
    [MaxLength(16)]
    public string Currency { get; set; } = string.Empty;
}
```

### Pattern 2: Scoped Service, No Provider Client

**What:** Runtime cost calculation should read local metadata and inventory only; do not inject `IHetznerCloudClient`, `HttpClient`, or token resolvers. [VERIFIED: tests/Api.Tests/CloudPlanning/Arma3CloudEstimateTests.cs]

**When to use:** Use for COST-09 through COST-12 because this phase estimates from cached pricing and persisted provider lifetime, not live provider reconciliation. [VERIFIED: .planning/ROADMAP.md]

**Example:**

```csharp
// Source: existing CloudCostEstimateService constructor pattern
[Scoped<CloudRuntimeCostSnapshotService, ICloudRuntimeCostSnapshotService>]
internal class CloudRuntimeCostSnapshotService(
    ApiDbContext apiDbContext,
    IConfiguration configuration,
    IHetznerMetadataSyncService hetznerMetadataSyncService,
    IHostingAuditService hostingAuditService)
    : ICloudRuntimeCostSnapshotService
{
}
```

### Pattern 3: Interval Intersection For Historical Owners

**What:** Split each resource lifetime by overlapping `GameServerBillingAssignment` intervals and preserve the assignment/account FK on each contribution. [VERIFIED: sources/Api/Data/Hosting/GameServerBillingAssignment.cs]

**When to use:** Use when calculating accumulated cost so old intervals remain attributed to the owner active during that interval. [VERIFIED: .planning/ROADMAP.md]

**Example:**

```csharp
// Source: interval fields in GameServerBillingAssignment and CloudResourceInventoryRecord
var effectiveEnd = resource.TimeStampDeleted ?? snapshotTimestamp;
var overlapStart = resource.TimeStampCreated > assignment.TimeStampAssigned
    ? resource.TimeStampCreated
    : assignment.TimeStampAssigned;
var assignmentEnd = assignment.TimeStampUnassigned ?? snapshotTimestamp;
var overlapEnd = effectiveEnd < assignmentEnd ? effectiveEnd : assignmentEnd;
if (overlapStart >= overlapEnd)
    return null;
```

### Anti-Patterns to Avoid

- **Using `GameServer.Status`, worker status, or Arma process status as a cost boundary:** Hetzner bills existing cloud servers even when off, and teardown marks cost stop only on provider deletion. [CITED: https://docs.hetzner.com/cloud/billing/faq/]
- **Mutating old snapshots when ownership changes:** Historical cost must remain on the assignment interval that incurred it. [VERIFIED: .planning/ROADMAP.md]
- **Calling Hetzner during snapshot calculation:** Phase 17 uses cached pricing and provider-resource lifetime; live reconciliation is Phase 18. [VERIFIED: .planning/ROADMAP.md]
- **Storing provider token references, bootstrap secrets, or raw failure payloads in snapshot details:** Project security constraints forbid exposing secret material in snapshots, DTOs, audits, or logs. [VERIFIED: AGENTS.md]
- **Creating duplicate snapshots on refresh:** RELY-14 requires recompute/refresh idempotency. [VERIFIED: .planning/REQUIREMENTS.md]

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Durable persistence | Ad hoc JSON files or static in-memory accumulators | EF Core entities, DbSets, migrations | Persistence across API restarts is required. [VERIFIED: .planning/REQUIREMENTS.md] |
| Atomic snapshot + audit writes | Manual partial writes | EF Core transaction around snapshots and audit append | EF transactions keep multi-write operations atomic. [CITED: https://learn.microsoft.com/en-us/ef/core/saving/transactions] |
| Indexing/idempotency | In-code duplicate scans only | EF Core unique/composite indexes | EF supports indexes, unique indexes, and filtered indexes where needed. [CITED: https://learn.microsoft.com/en-us/ef/core/modeling/indexes] |
| Secret redaction | New regex sanitizer | `LifecycleFailureReasonSanitizer` through `HostingAuditService` | Existing services already sanitize audit summary/details. [VERIFIED: sources/Api/Services/GameServerLifecycle/HostingAuditService.cs] |
| Live price fetching | New provider billing client | Cached `HetznerServerType` and config assumptions | Phase goal says cached pricing; provider reconciliation is later. [VERIFIED: .planning/ROADMAP.md] |

**Key insight:** Runtime cost is an interval-accounting problem over durable provider resource lifetimes and durable ownership intervals, not a runtime-state problem. [VERIFIED: .planning/ROADMAP.md]

## Common Pitfalls

### Pitfall 1: Treating Powered-Off Or Stopped Runtime As Free

**What goes wrong:** Cost stops when Arma stops, worker cleanup completes, or the VM is powered off. [VERIFIED: .planning/ROADMAP.md]
**Why it happens:** Runtime state and provider billing state are easy to conflate. [ASSUMED]
**How to avoid:** Use `CloudResourceInventoryRecord.TimeStampDeleted` as the only stop boundary for billable provider resources. [VERIFIED: sources/Api/Services/CloudTeardown/CloudTeardownService.cs]
**Warning signs:** Cost tests read `GameServer.Status`, worker heartbeat status, job status, or lifecycle process status. [VERIFIED: codebase grep]

### Pitfall 2: Moving Historical Cost To Current Owner

**What goes wrong:** Recalculating all accumulated cost using the latest active billing assignment violates COST-10. [VERIFIED: .planning/REQUIREMENTS.md]
**Why it happens:** Existing session/resource rows snapshot creation owner, but ownership can transfer later. [VERIFIED: sources/Api/Data/Hosting/GameServerBillingAssignment.cs]
**How to avoid:** Intersect every billable resource lifetime with every assignment interval for the same game server. [VERIFIED: sources/Api/Data/Hosting/GameServerBillingAssignment.cs]
**Warning signs:** Aggregates group only by current `CloudGameServerSession.BillingAccountFk`. [VERIFIED: sources/Api/Data/Hosting/CloudGameServerSession.cs]

### Pitfall 3: Duplicate Refresh Rows

**What goes wrong:** API restarts or repeated refreshes create duplicate contributions for the same interval. [VERIFIED: .planning/REQUIREMENTS.md]
**Why it happens:** Snapshot generation often appends without deterministic keys. [ASSUMED]
**How to avoid:** Use a deterministic uniqueness key such as `(SessionFk, ResourceInventoryRecordFk, BillingAssignmentFk, IntervalStart, IntervalEnd, Source)` and upsert/replace inside a transaction. [CITED: https://learn.microsoft.com/en-us/ef/core/modeling/indexes]
**Warning signs:** Tests call refresh twice and totals double. [VERIFIED: .planning/REQUIREMENTS.md]

### Pitfall 4: Hiding Stale Metadata

**What goes wrong:** A snapshot looks precise while using stale or missing cached pricing. [VERIFIED: .planning/REQUIREMENTS.md]
**Why it happens:** Current dry-run service already tolerates stale/unknown freshness with confidence values. [VERIFIED: sources/Api/Services/CloudPlanning/CloudCostEstimateService.cs]
**How to avoid:** Persist freshness, metadata source timestamp, confidence score, source, and assumption JSON on every snapshot. [VERIFIED: sources/Api/Data/Hosting/CloudCostEstimate.cs]
**Warning signs:** Snapshot rows have cost and currency but no freshness/confidence fields. [VERIFIED: .planning/ROADMAP.md]

### Pitfall 5: Billing Resource Kind Drift

**What goes wrong:** Firewalls or session marker rows are counted as billable runtime resources despite success criteria naming server, IP, and volume only. [VERIFIED: .planning/ROADMAP.md]
**Why it happens:** Dry-run inventory includes session, server, IP, firewall, and volume rows. [VERIFIED: sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs]
**How to avoid:** Centralize `IsBillableRuntimeResource(kind)` as server, IP address, volume and test firewall/session exclusion. [VERIFIED: sources/Api/Services/CloudTeardown/CloudTeardownService.cs]
**Warning signs:** Runtime snapshot totals include `ECloudResourceKind.Firewall`. [VERIFIED: sources/Contract/GameServer/ECloudResourceKind.cs]

## Code Examples

### Billable Resource Predicate

```csharp
// Source: sources/Api/Services/CloudTeardown/CloudTeardownService.cs
private static bool IsBillableRuntimeResource(ECloudResourceKind kind)
    => kind is ECloudResourceKind.Server
        or ECloudResourceKind.IpAddress
        or ECloudResourceKind.Volume;
```

### Transactional Snapshot Refresh

```csharp
// Source: EF Core transaction docs and existing CloudInventoryService transaction pattern
await using var transaction = await apiDbContext.Database
    .BeginTransactionAsync(cancellationToken)
    .ConfigureAwait(false);

// Load session, resources, assignments, prices.
// Delete/replace or upsert deterministic contribution rows.
// Append one sanitized audit record.

await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
```

### Freshness And Confidence Pattern

```csharp
// Source: sources/Api/Services/CloudPlanning/CloudCostEstimateService.cs
private ECloudEstimateFreshness GetFreshness(DateTimeOffset? metadataSourceTimestamp, DateTimeOffset now)
{
    if (metadataSourceTimestamp is null)
        return ECloudEstimateFreshness.Unknown;
    return now - metadataSourceTimestamp.Value > hetznerMetadataSyncService.GetStaleThreshold()
        ? ECloudEstimateFreshness.Stale
        : ECloudEstimateFreshness.Fresh;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Dry-run-only `CloudCostEstimate` tied to a plan | Runtime `CloudCostSnapshot` tied to existing provider resources and owner intervals | Phase 17 | Enables accumulated historical costs for provisioned sessions. [VERIFIED: .planning/ROADMAP.md] |
| Session/resource creation owner snapshot only | Interval split across `GameServerBillingAssignment` history | Phase 17 | Prevents old cost from moving to the current owner. [VERIFIED: .planning/ROADMAP.md] |
| Cleanup status as operational risk | Provider deletion timestamp as billing stop | Phase 17 | Runtime stop and cleanup state no longer imply cost stop. [CITED: https://docs.hetzner.com/cloud/billing/faq/] |

**Deprecated/outdated:**
- Treating `CloudCostEstimate` as runtime accumulated cost is insufficient because it is immutable dry-run estimate data linked to `DryRunPlanFk`, not provider lifetime intervals. [VERIFIED: sources/Api/Data/Hosting/CloudCostEstimate.cs]
- Treating `ECloudResourceState.CleanupNeeded` as a cost stop is incorrect; teardown leaves failed resources without `TimeStampDeleted`. [VERIFIED: sources/Api/Services/CloudTeardown/CloudTeardownService.cs]

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Resource contribution rows are preferable to JSON-only contribution detail. | Standard Stack / Alternatives | Planner may choose a simpler JSON implementation that still satisfies Phase 17 but weakens Phase 19 filtering. |
| A2 | Runtime cost calculation does not need a background timer in Phase 17. | Standard Stack / Alternatives | Planner may need to add a hosted refresh if product expectations require automatic periodic snapshots now. |
| A3 | Duplicate refresh bugs are likely when deterministic keys are absent. | Common Pitfalls | Planner might under-prioritize unique index/idempotency tests. |

## Open Questions (RESOLVED)

1. **Should Phase 17 expose new read endpoints or keep this API-internal until Phase 19?**
   - What we know: Phase 19 owns cost visibility DTOs, generated clients, and WebApp surfacing. [VERIFIED: .planning/ROADMAP.md]
   - Resolution: Keep Phase 17 API-internal/service-only. Implement durable storage, calculation services, audit-safe metadata, and focused tests in Phase 17; defer public visibility DTOs, generated clients, WebApp surfaces, and owner/admin read endpoints to Phase 19 unless a minimal internal test seam is required. [RESOLVED: plans 17-01, 17-02, 17-03]

2. **Should Primary IP be modeled separately from existing `IpAddress` kind?**
   - What we know: Hetzner docs distinguish Floating IPs and Primary IPs, while the repository currently has `ECloudResourceKind.IpAddress`. [CITED: https://docs.hetzner.com/cloud/billing/faq/]
   - Resolution: Treat `ECloudResourceKind.IpAddress` as the current billable floating/public IP category for Phase 17. Store source assumptions and resource-kind metadata on snapshots/contributions so Phase 18 and Phase 20 can refine provider labels or split primary/floating IP semantics later without rewriting historical attribution. [RESOLVED: plans 17-01, 17-02, 17-03]

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| .NET SDK | Build/test/migrations | yes | `10.0.201` | Install .NET 9 SDK if SDK roll-forward causes issues. [VERIFIED: local command] |
| dotnet-ef | EF migration generation | yes | `10.0.5` | Use project-local compatible EF tool or `dotnet tool restore` if mismatch causes migration noise. [VERIFIED: local command] |
| PostgreSQL | Production/local Aspire persistence | not probed | N/A | Existing API tests use SQLite for service-level coverage. [VERIFIED: tests/Api.Tests/Api.Tests.csproj] |
| Hetzner token | Not required in Phase 17 calculation | not required | N/A | Use cached metadata and seeded test rows. [VERIFIED: .planning/ROADMAP.md] |

**Missing dependencies with no fallback:** none for research/planning. [VERIFIED: local command]

**Missing dependencies with fallback:** PostgreSQL was not probed; service tests can use SQLite, while migration verification still needs EF tooling. [VERIFIED: tests/Api.Tests/Api.Tests.csproj]

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit `2.9.3` with `Microsoft.NET.Test.Sdk` `17.13.0`. [VERIFIED: tests/Api.Tests/Api.Tests.csproj] |
| Config file | none detected for API tests; tests use csproj package references. [VERIFIED: tests/Api.Tests/Api.Tests.csproj] |
| Quick run command | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudCosts` |
| Full suite command | `dotnet test X39.UnitedTacticalForces.slnx` |

### Phase Requirements - Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| COST-09 | Session accumulated historical cost sums snapshot contributions. | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests` | no - Wave 0 |
| COST-10 | Cost rows preserve billing account and assignment active for each interval. | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests` | no - Wave 0 |
| COST-11 | Deleted timestamp stops cost; runtime stop/powered-off/cleanup state does not. | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests` | no - Wave 0 |
| COST-12 | Snapshots expose assumptions, freshness, currency, confidence, and resource contribution without secrets. | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests` | no - Wave 0 |
| RELY-14 | Refresh is idempotent and restart-safe. | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests` | no - Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudRuntimeCostSnapshotServiceTests` [VERIFIED: tests/Api.Tests]
- **Per wave merge:** `dotnet test tests/Api.Tests/Api.Tests.csproj` [VERIFIED: tests/Api.Tests/Api.Tests.csproj]
- **Phase gate:** `dotnet build X39.UnitedTacticalForces.slnx` and `dotnet test X39.UnitedTacticalForces.slnx` before verification. [VERIFIED: AGENTS.md]

### Wave 0 Gaps

- [ ] `tests/Api.Tests/CloudCosts/CloudRuntimeCostSnapshotServiceTests.cs` - covers COST-09, COST-10, COST-11, COST-12, RELY-14. [VERIFIED: tests/Api.Tests]
- [ ] New EF migration for cost snapshots and contributions, generated under `sources/Api/Data/Migrations`. [CITED: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/]
- [ ] Optional test helper extraction for repeated SQLite `DateTimeOffsetToBinaryConverter` setup. [VERIFIED: existing test files]

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | no new auth | Reuse `[Authorize]` and existing claims if endpoints are added. [VERIFIED: sources/Api/Controllers/CloudInventoryController.cs] |
| V3 Session Management | no | No browser/session behavior in Phase 17. [VERIFIED: .planning/ROADMAP.md] |
| V4 Access Control | yes if endpoints are added | Use existing server admin/server-scope authorization patterns; do not expose owner history broadly. [VERIFIED: sources/Api/Controllers/CloudInventoryController.cs] |
| V5 Input Validation | yes | Validate interval inputs, enum switches, bounded JSON, and max lengths in entities/services. [VERIFIED: sources/Api/Services/CloudInventory/CloudInventoryService.cs] |
| V6 Cryptography | no new crypto | Do not handle provider tokens; do not store secrets. [VERIFIED: AGENTS.md] |

OWASP ASVS provides secure development and verification requirements for web applications; use it as the security verification baseline. [CITED: https://owasp.org/www-project-application-security-verification-standard/]

### Known Threat Patterns for ASP.NET Core / EF Core Cost Snapshots

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Secret leakage in assumptions/audits | Information Disclosure | Reuse `LifecycleFailureReasonSanitizer`; never store token, password, authorization header, bootstrap credential, or raw provider payload. [VERIFIED: sources/Api/Services/GameServerLifecycle/HostingAuditService.cs] |
| Unauthorized cost history access | Information Disclosure | Keep Phase 17 API-internal or admin-only; Phase 19 must add owner-scoped authorization. [VERIFIED: .planning/ROADMAP.md] |
| Duplicate refresh totals | Tampering | Unique deterministic snapshot keys plus idempotency tests. [VERIFIED: .planning/REQUIREMENTS.md] |
| SQL injection | Tampering | Use EF LINQ and parameterized provider behavior, not raw SQL. [VERIFIED: codebase grep] |
| Misleading cost certainty | Repudiation | Persist source, assumptions, metadata freshness, and confidence with every snapshot. [VERIFIED: .planning/REQUIREMENTS.md] |

## Sources

### Primary (HIGH confidence)

- `AGENTS.md` - project constraints, stack, conventions, architecture. [VERIFIED: local file]
- `.planning/phases/17-runtime-cost-snapshot-foundation/17-CONTEXT.md` - phase discretion and context boundary. [VERIFIED: local file]
- `.planning/REQUIREMENTS.md` - COST-09, COST-10, COST-11, COST-12, RELY-14. [VERIFIED: local file]
- `.planning/ROADMAP.md` - Phase 17 goal and success criteria. [VERIFIED: local file]
- `sources/Api/Data/Hosting/*` - current hosting entities and metadata cache. [VERIFIED: codebase grep]
- `sources/Api/Services/CloudPlanning/CloudCostEstimateService.cs` - existing cost estimate freshness/source pattern. [VERIFIED: codebase grep]
- `sources/Api/Services/CloudTeardown/CloudTeardownService.cs` - provider deletion and billable resource pattern. [VERIFIED: codebase grep]
- Hetzner Cloud Billing FAQ - server, powered-off, Floating IP, Primary IP billing behavior. [CITED: https://docs.hetzner.com/cloud/billing/faq/]
- Microsoft EF Core transactions docs - transaction behavior for multi-write operations. [CITED: https://learn.microsoft.com/en-us/ef/core/saving/transactions]
- Microsoft EF Core indexes docs - composite, unique, filtered indexes. [CITED: https://learn.microsoft.com/en-us/ef/core/modeling/indexes]
- Microsoft EF Core migrations docs - migration creation and model snapshots. [CITED: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/]

### Secondary (MEDIUM confidence)

- Microsoft ASP.NET Core hosted services docs - hosted service lifecycle, scoped service caveats if a timer is later added. [CITED: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services]
- OWASP ASVS project page - security verification baseline. [CITED: https://owasp.org/www-project-application-security-verification-standard/]

### Tertiary (LOW confidence)

- None. [VERIFIED: source list]

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - uses existing project packages and verified local csproj references. [VERIFIED: local command]
- Architecture: HIGH - follows existing API/Data/Services boundaries and roadmap phase ownership. [VERIFIED: codebase grep]
- Pitfalls: HIGH - key billing boundary is verified by Hetzner docs and existing teardown tests. [CITED: https://docs.hetzner.com/cloud/billing/faq/]

**Research date:** 2026-06-17
**Valid until:** 2026-07-17 for codebase architecture; 2026-06-24 for Hetzner pricing/billing assumptions because provider billing rules can change. [ASSUMED]
