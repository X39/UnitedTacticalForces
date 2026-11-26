# Phase 21: Spending Policy Model And Evaluation Boundary - Research

**Researched:** 2026-06-18 [VERIFIED: system date]
**Domain:** ASP.NET Core 9 API, EF Core/Npgsql persistence, cloud cost policy evaluation, restart-safe audit logging [VERIFIED: AGENTS.md; VERIFIED: codebase grep]
**Confidence:** HIGH [VERIFIED: codebase grep; CITED: Microsoft Learn]

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
No locked implementation decisions were provided in `21-CONTEXT.md`. [VERIFIED: 21-CONTEXT.md]

### the agent's Discretion
All implementation choices are at the agent's discretion because discuss phase was skipped per user setting. Use the roadmap phase goal, success criteria, requirements COST-07, COST-19, and RELY-17, and codebase conventions to guide decisions.

### Deferred Ideas (OUT OF SCOPE)
None.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| COST-07 | Administrator can define spending limits, approval thresholds, warning thresholds, and automatic shutdown policies. [VERIFIED: .planning/REQUIREMENTS.md] | Add persisted `SpendingPolicy` entities with owner/global scope, thresholds, automatic action settings, effective dates, and audit metadata under `sources/Api/Data/Hosting`. [VERIFIED: codebase grep] |
| COST-19 | Policy evaluation treats stale cost metadata, stale reconciliation data, cleanup-needed resources, and unknown billable provider resources as risk inputs instead of silently ignoring them. [VERIFIED: .planning/REQUIREMENTS.md] | Reuse `ECloudEstimateFreshness`, `CloudProviderReconciliationRun`, `CloudProviderReconciliationFinding`, and `CloudResourceInventoryRecord.State` as evaluation inputs. [VERIFIED: codebase grep] |
| RELY-17 | Policy configuration, evaluation decisions, approvals, denials, automatic-action attempts, and errors survive API restarts. [VERIFIED: .planning/REQUIREMENTS.md] | Persist policy decisions and sanitized failure snapshots in EF entities with unique idempotency indexes. [VERIFIED: codebase grep; CITED: https://learn.microsoft.com/en-us/ef/core/modeling/indexes] |
</phase_requirements>

## Summary

Phase 21 should be implemented as an API/backend persistence and evaluation boundary, not as a frontend or provider-client feature. [VERIFIED: ROADMAP.md] The existing v1.4 model already persists current cost inputs (`CloudCostSnapshot`, `CloudCostSnapshotContribution`, `CloudCostEstimate`) and reconciliation risk inputs (`CloudProviderReconciliationRun`, `CloudProviderReconciliationFinding`, `CloudResourceInventoryRecord`). [VERIFIED: codebase grep]

Use the existing Hosting bounded context for policy entities, DTOs, transforms, and services. [VERIFIED: AGENTS.md; VERIFIED: codebase grep] The policy evaluator should accept a proposed billable action input, load the effective owner/global policy, read persisted cost/reconciliation state, compute a decision, and persist a decision record before returning. [VERIFIED: ROADMAP.md; ASSUMED]

**Primary recommendation:** Create a provider-mutation-free `ICloudSpendingPolicyEvaluationService` that persists a durable `CloudSpendingPolicyDecision` with bounded input/error JSON and an idempotency key per proposed action. [VERIFIED: ROADMAP.md; VERIFIED: codebase grep; ASSUMED]

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| Spending policy configuration model | API / Backend | Database / Storage | Policy configuration is durable business state and belongs in EF/Core API persistence. [VERIFIED: AGENTS.md; VERIFIED: codebase grep] |
| Effective policy selection | API / Backend | Database / Storage | Selection depends on owner/global scope, effective dates, active flags, and persisted billing owner records. [VERIFIED: codebase grep; ASSUMED] |
| Threshold and risk evaluation | API / Backend | Database / Storage | Evaluation consumes persisted cost snapshots and reconciliation findings and must not mutate provider resources. [VERIFIED: ROADMAP.md; VERIFIED: codebase grep] |
| Decision/error persistence | Database / Storage | API / Backend | RELY-17 requires restart-safe decision/error records. [VERIFIED: .planning/REQUIREMENTS.md] |
| Policy DTOs | API / Backend | Browser / Client later | Phase 21 needs DTOs for API contract shape, while Phase 24 owns generated WebApp client visibility. [VERIFIED: ROADMAP.md] |
| Automatic shutdown execution | API / Backend later | Worker later | Phase 21 stores automatic-action settings only; Phase 23 executes actions. [VERIFIED: ROADMAP.md] |

## Project Constraints (from AGENTS.md)

- Keep the existing game-controller abstraction during the first milestone and migrate current support rather than replacing it. [VERIFIED: AGENTS.md]
- Lifecycle, job, worker, ownership, and audit state must survive API restarts. [VERIFIED: AGENTS.md]
- Every cost-generating or hosting session must have an owner from creation until cleanup or transfer completes. [VERIFIED: AGENTS.md]
- Do not implement Hetzner provisioning in this milestone phase. [VERIFIED: AGENTS.md; VERIFIED: ROADMAP.md]
- Steam credentials, provider tokens, and worker credentials must not be exposed to frontend clients or unredacted logs. [VERIFIED: AGENTS.md]
- Existing Aspire, PostgreSQL, API, and Blazor WebApp local development should keep working. [VERIFIED: AGENTS.md]
- Add focused tests around persistence, job transitions, authorization-sensitive actions, and worker recovery where relevant. [VERIFIED: AGENTS.md]
- Use one public type per `.cs` file, file-scoped namespaces, PascalCase public members, private fields with `_`, DTO suffixes, `Payload`/`Update` suffixes, and `E` prefixes for contract enums. [VERIFIED: AGENTS.md]
- Treat `sources/WebApp/UtfApi/**` and `sources/Api/Data/Migrations/**` as generated style references only. [VERIFIED: AGENTS.md]
- Use `sources/` for active code and do not place active code under `source/`. [VERIFIED: AGENTS.md]
- Do not log secrets or raw config values from secret/user config or OAuth/database settings. [VERIFIED: AGENTS.md]

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK/runtime | net9.0 projects; SDKs 9.0.100, 9.0.200, and 9.0.308 installed [VERIFIED: dotnet --list-sdks] | Build and run API/test projects. | All active projects target `net9.0`. [VERIFIED: dotnet list package; VERIFIED: AGENTS.md] |
| ASP.NET Core | 9.0.4 package family in API/WebApp [VERIFIED: dotnet list package] | API controllers, auth, OpenAPI, DI, logging. | Existing API uses ASP.NET Core controllers and `[Authorize]` policies. [VERIFIED: codebase grep] |
| EF Core + Npgsql | `Microsoft.EntityFrameworkCore.Tools` 9.0.4 and `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4 [VERIFIED: dotnet list package] | PostgreSQL persistence and migrations. | Existing `ApiDbContext` and migrations use EF Core; Microsoft documents migrations as the standard way to evolve schemas while preserving data. [VERIFIED: codebase grep; CITED: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/] |
| X39.Util.DependencyInjection | 2.0.1.8 [VERIFIED: dotnet list package] | Attribute-driven DI registration. | Existing API services use `[Scoped<Implementation, Interface>]`. [VERIFIED: codebase grep] |
| xUnit + SQLite provider | xUnit 2.9.3, `Microsoft.EntityFrameworkCore.Sqlite` 9.0.4 [VERIFIED: dotnet list package] | Fast API service tests with relational EF behavior. | Existing API tests use SQLite in-memory; Microsoft discourages EF's non-relational in-memory provider and recommends considering SQLite for relational behavior. [VERIFIED: codebase grep; CITED: https://learn.microsoft.com/en-us/ef/core/testing/] |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Text.Json` | Built-in .NET [VERIFIED: codebase grep] | Bounded client-safe input snapshot and assumption JSON. | Existing cost/reconciliation services serialize safe JSON snapshots with camelCase options. [VERIFIED: codebase grep] |
| `ILogger<T>` | Built-in ASP.NET Core [VERIFIED: codebase grep] | Structured operational logs. | Use structured templates and sanitized fields only; Microsoft documents ASP.NET Core logging providers and configuration. [VERIFIED: AGENTS.md; CITED: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-10.0] |
| `LifecycleFailureReasonSanitizer` | Internal helper [VERIFIED: codebase grep] | Secret redaction for persisted summaries/details. | Reuse for policy decision failures instead of adding a second sanitizer. [VERIFIED: codebase grep] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Existing EF entities and services | A generic rules engine package | Not recommended; Phase 21 thresholds/risk inputs are simple domain logic and no new package is needed. [ASSUMED] |
| Bounded JSON string snapshots | PostgreSQL `jsonb` mapping | Existing code uses bounded `string?` JSON columns for assumptions/details, so string JSON keeps the phase consistent. [VERIFIED: codebase grep] |
| SQLite service tests | PostgreSQL integration tests | PostgreSQL is closer to production, but existing API tests already use SQLite in-memory and can run without local Postgres. [VERIFIED: codebase grep; CITED: https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database] |

**Installation:**

```bash
# No new external packages are recommended for Phase 21. [VERIFIED: dotnet list package; ASSUMED]
```

**Version verification:** Existing package versions were verified with `dotnet list X39.UnitedTacticalForces.slnx package` on 2026-06-18. [VERIFIED: dotnet list package]

## Package Legitimacy Audit

No new package installation is recommended for this phase, so the package legitimacy gate is not applicable. [VERIFIED: dotnet list package; ASSUMED]

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| none | — | — | — | — | not run | No new package install. [ASSUMED] |

**Packages removed due to slopcheck [SLOP] verdict:** none. [ASSUMED]
**Packages flagged as suspicious [SUS]:** none. [ASSUMED]

## Architecture Patterns

### System Architecture Diagram

```text
Billable action proposal
        |
        v
CloudSpendingPolicyEvaluationService
        |
        +--> Load billing owner / assignment -------------------+
        |                                                       |
        +--> Load effective owner policy, fallback global policy |
        |                                                       |
        +--> Load latest cost snapshot / estimate inputs         |
        |                                                       |
        +--> Load latest reconciliation run and findings         |
        |                                                       |
        v                                                       |
Normalize input snapshot and risk flags                         |
        |                                                       |
        v                                                       |
Decision branch: deny > approval-required > warn > allow         |
        |                                                       |
        v                                                       |
Persist CloudSpendingPolicyDecision + sanitized error/details <--+
        |
        v
Return DTO to caller; no provider resource mutation
```

### Recommended Project Structure

```text
sources/
├── Api/
│   ├── Data/Hosting/
│   │   ├── CloudSpendingPolicy.cs
│   │   └── CloudSpendingPolicyDecision.cs
│   ├── DTO/
│   │   ├── CloudSpendingPolicyDto.cs
│   │   ├── CloudSpendingPolicyDecisionDto.cs
│   │   └── Payloads/CloudSpendingPolicyEvaluationPayload.cs
│   ├── Services/CloudPolicies/
│   │   ├── ICloudSpendingPolicyEvaluationService.cs
│   │   └── CloudSpendingPolicyEvaluationService.cs
│   └── Transform.cs
├── Contract/GameServer/
│   ├── ECloudSpendingPolicyDecision.cs
│   ├── ECloudSpendingPolicyScope.cs
│   ├── ECloudSpendingPolicyAction.cs
│   └── ECloudBillableActionKind.cs
└── Api/Data/Migrations/
    └── [generated migration files]
```

### Pattern 1: Effective Policy Selection

**What:** Choose the owner-specific active policy whose effective interval contains `now`; if none exists, fall back to the active global policy. [ASSUMED]

**When to use:** Every evaluation and later every policy visibility projection should resolve a single effective policy for a billing account. [ASSUMED]

**Example:**

```csharp
// Source: existing ApiDbContext + EF query style [VERIFIED: codebase grep]
var ownerPolicy = await apiDbContext.CloudSpendingPolicies
    .AsNoTracking()
    .Where(q => q.Scope == ECloudSpendingPolicyScope.BillingAccount
                && q.BillingAccountFk == billingAccountId
                && q.IsActive
                && q.TimeStampEffectiveFrom <= now
                && (q.TimeStampEffectiveTo == null || now < q.TimeStampEffectiveTo))
    .OrderByDescending(q => q.TimeStampEffectiveFrom)
    .ThenByDescending(q => q.PrimaryKey)
    .FirstOrDefaultAsync(cancellationToken);
```

### Pattern 2: Decision Persistence With Idempotency

**What:** Store one decision per idempotency key and proposed action snapshot; return the existing decision when a caller retries after API restart. [ASSUMED]

**When to use:** Evaluation may be retried by API endpoints or future durable jobs and must not create duplicate decision history for the same proposed action. [VERIFIED: .planning/REQUIREMENTS.md; ASSUMED]

**Example:**

```csharp
// Source: existing unique idempotency index pattern on CloudCostSnapshotContribution [VERIFIED: codebase grep]
modelBuilder.Entity<CloudSpendingPolicyDecision>()
    .HasIndex(q => q.IdempotencyKey)
    .HasDatabaseName("IX_CloudSpendingPolicyDecisions_IdempotencyKey")
    .IsUnique();
```

### Pattern 3: Provider-Mutation-Free Evaluation

**What:** The evaluator must depend on `ApiDbContext`, cost visibility/snapshot services, and sanitizer/audit helpers; it must not depend on `IHetznerCloudClient`, `HttpClient`, teardown, or provisioning services. [VERIFIED: ROADMAP.md; VERIFIED: codebase grep]

**When to use:** Phase 21 evaluation only classifies proposed actions and persists decisions. [VERIFIED: ROADMAP.md]

**Example:**

```csharp
// Source: existing no-provider-client test pattern in CloudRuntimeCostSnapshotServiceTests [VERIFIED: codebase grep]
var parameterTypes = typeof(CloudSpendingPolicyEvaluationService)
    .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
    .SelectMany(q => q.GetParameters())
    .Select(q => q.ParameterType)
    .ToArray();

Assert.DoesNotContain(typeof(IHetznerCloudClient), parameterTypes);
Assert.DoesNotContain(typeof(HttpClient), parameterTypes);
```

### Anti-Patterns to Avoid

- **Mutating provider resources during evaluation:** Phase 21 must classify only; provisioning/teardown gates and automatic actions are later phases. [VERIFIED: ROADMAP.md]
- **Ignoring stale/unknown data:** `ECloudEstimateFreshness.Stale`, `ECloudEstimateFreshness.Unknown`, stale reconciliation timestamps, unknown provider resources, and cleanup-needed billable resources must influence the decision. [VERIFIED: .planning/REQUIREMENTS.md; VERIFIED: codebase grep]
- **Persisting raw exception/provider text:** Existing reconciliation and lifecycle paths sanitize and truncate failure details; policy errors should do the same. [VERIFIED: codebase grep]
- **Returning EF entities from API endpoints:** Existing architecture uses DTOs/transforms for client-facing shapes. [VERIFIED: AGENTS.md; VERIFIED: codebase grep]
- **Building a separate billing ledger:** v1.4 cost snapshots and owner intervals already provide estimated accumulated/current cost inputs. [VERIFIED: codebase grep]

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Schema evolution | Manual SQL files only | EF Core migrations | EF migrations compare model changes with a model snapshot and generate tracked migration files. [CITED: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/] |
| Unique/retry safety | In-memory retry cache | EF unique indexes | EF supports unique indexes; duplicate inserts fail instead of creating duplicate persisted decisions. [CITED: https://learn.microsoft.com/en-us/ef/core/modeling/indexes] |
| Secret redaction | New regex-only sanitizer | `LifecycleFailureReasonSanitizer` | Existing code already sanitizes token/password/secret-like text for persisted details. [VERIFIED: codebase grep] |
| Cost accumulation | New cost calculator | `CloudRuntimeCostSnapshotService` / persisted snapshots | v1.4 already persists accumulated estimated cost and owner interval contributions. [VERIFIED: codebase grep] |
| Provider risk discovery | Live provider calls inside evaluator | Latest reconciliation run/findings | Reconciliation is already a read-only provider boundary and persists unknown/cleanup risk. [VERIFIED: codebase grep] |

**Key insight:** The evaluator should be deterministic over persisted inputs and proposed-action data; live cloud reads and resource mutations belong outside this phase. [VERIFIED: ROADMAP.md; VERIFIED: codebase grep; ASSUMED]

## Common Pitfalls

### Pitfall 1: Threshold Math Uses Only Projected Cost

**What goes wrong:** A proposed action is allowed because only the new projected cost is compared to the limit. [ASSUMED]
**Why it happens:** Current accumulated cost and projected additional cost are modeled separately. [VERIFIED: codebase grep]
**How to avoid:** Compare `currentEstimatedCost + projectedActionCost` against warning, approval, and deny limits. [ASSUMED]
**Warning signs:** Tests pass for zero current cost but fail when an owner already has active billable sessions. [ASSUMED]

### Pitfall 2: Owner Policy Does Not Fall Back Correctly

**What goes wrong:** Owners without a specific policy either bypass policy or get denied despite a global policy. [ASSUMED]
**Why it happens:** Scope/effective-date resolution is not centralized. [ASSUMED]
**How to avoid:** Put effective policy selection in the service and test owner-specific, global fallback, inactive, and expired policies. [ASSUMED]
**Warning signs:** Multiple endpoints duplicate policy selection LINQ. [ASSUMED]

### Pitfall 3: Risk Inputs Are Flattened Into One Boolean

**What goes wrong:** Unknown provider resources and cleanup-needed billable resources produce the same opaque decision reason. [ASSUMED]
**Why it happens:** Reconciliation visibility already computes aggregate flags, but policy decisions need explainable trigger details. [VERIFIED: codebase grep; ASSUMED]
**How to avoid:** Persist separate counts/flags for stale metadata, stale reconciliation, cleanup-needed billable resources, unknown provider resources, orphan findings, and missing snapshots. [VERIFIED: .planning/REQUIREMENTS.md; ASSUMED]
**Warning signs:** Decision DTO has `HasRisk` but no reason codes/counts. [ASSUMED]

### Pitfall 4: Sanitized Failure Details Are Not Tested

**What goes wrong:** Policy decisions persist raw exception text with token/password/authorization data. [ASSUMED]
**Why it happens:** Error paths are less frequently tested than success decisions. [ASSUMED]
**How to avoid:** Reuse `LifecycleFailureReasonSanitizer` and add tests with secret-like exception messages. [VERIFIED: codebase grep]
**Warning signs:** Policy failure tests assert only status and not persisted text content. [ASSUMED]

### Pitfall 5: Required Relationships Cascade-Delete Historical Decisions

**What goes wrong:** Deleting or changing related entities removes historical policy decisions. [ASSUMED]
**Why it happens:** EF required relationships use cascade deletes by default. [CITED: https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete]
**How to avoid:** Use nullable historical foreign keys where appropriate and explicit `DeleteBehavior.Restrict` for billing/session/policy relationships. [VERIFIED: codebase grep; ASSUMED]
**Warning signs:** New `OnModelCreating` relationships omit `OnDelete(DeleteBehavior.Restrict)`. [VERIFIED: codebase grep; ASSUMED]

## Code Examples

### Policy Validation Shape

```csharp
// Source: existing entity validation style via ArgumentOutOfRangeException/ArgumentException [VERIFIED: AGENTS.md; VERIFIED: codebase grep]
if (policy.MonthlySpendingLimit < 0m)
    throw new ArgumentOutOfRangeException(nameof(policy.MonthlySpendingLimit), "Spending limit must not be negative.");
if (policy.WarningThreshold > policy.ApprovalThreshold)
    throw new ArgumentException("Warning threshold must be less than or equal to approval threshold.");
if (policy.ApprovalThreshold > policy.MonthlySpendingLimit)
    throw new ArgumentException("Approval threshold must be less than or equal to spending limit.");
```

### Decision Precedence

```csharp
// Source: phase requirements require allow/warn/approval-required/deny classifications [VERIFIED: ROADMAP.md]
var projectedTotal = input.CurrentEstimatedCost + input.ProjectedAdditionalCost;
var decision = projectedTotal > policy.MonthlySpendingLimit || input.HasUnknownBillableProviderResources
    ? ECloudSpendingPolicyDecision.Deny
    : projectedTotal >= policy.ApprovalThreshold || input.HasStaleReconciliation
        ? ECloudSpendingPolicyDecision.ApprovalRequired
        : projectedTotal >= policy.WarningThreshold || input.HasStaleMetadata
            ? ECloudSpendingPolicyDecision.Warn
            : ECloudSpendingPolicyDecision.Allow;
```

### Sanitized Decision Error Persistence

```csharp
// Source: LifecycleFailureReasonSanitizer pattern [VERIFIED: codebase grep]
decision.Status = ECloudSpendingPolicyEvaluationStatus.Failed;
decision.SanitizedFailureSummary = LifecycleFailureReasonSanitizer.SanitizeSummary(exception.Message);
decision.SanitizedFailureDetails = LifecycleFailureReasonSanitizer.SanitizeDetails(exception.ToString());
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| API process-local game server state | Durable lifecycle, worker, ownership, audit, inventory, cost, and reconciliation records | v1.0-v1.4 milestones [VERIFIED: .planning/STATE.md] | Phase 21 should continue durable EF persistence and avoid in-memory policy state. [VERIFIED: .planning/STATE.md; ASSUMED] |
| Dry-run-only cost estimates | Runtime cost snapshots plus reconciliation visibility | v1.4 [VERIFIED: .planning/STATE.md] | Policy should evaluate current accumulated cost and reconciliation risk, not only plan estimates. [VERIFIED: ROADMAP.md] |
| Treating stale metadata as display-only | Treat stale/unknown metadata and reconciliation as policy risk inputs | v1.5 requirements [VERIFIED: .planning/REQUIREMENTS.md] | Phase 21 must store risk trigger details in each persisted decision. [VERIFIED: .planning/REQUIREMENTS.md; ASSUMED] |

**Deprecated/outdated:**
- Using only current provider state for policy evaluation is out of scope for Phase 21; use persisted v1.4 snapshots and reconciliation risk. [VERIFIED: ROADMAP.md]
- Using EF's non-relational in-memory provider for these tests is discouraged by Microsoft and not used by the current API tests. [CITED: https://learn.microsoft.com/en-us/ef/core/testing/; VERIFIED: codebase grep]

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Decision idempotency should be keyed by a caller/proposed-action idempotency key. | Summary, Architecture Patterns | Planner may need a different key if Phase 22 chooses a different request lifecycle. |
| A2 | Unknown billable provider resources should deny by default, while stale reconciliation should require approval by default. | Code Examples | User may prefer warn/approval behavior instead of deny for early rollout. |
| A3 | No new package is needed for rule evaluation. | Standard Stack, Package Legitimacy Audit | A future configurable expression/rules feature might require a vetted package, but Phase 21 criteria do not require it. |

## Open Questions (RESOLVED)

1. **What are the initial default policy thresholds?** [RESOLVED]
   - What we know: COST-07 requires storing spending limits, warning thresholds, approval thresholds, and automatic shutdown policies. [VERIFIED: .planning/REQUIREMENTS.md]
   - What's unclear: The roadmap does not specify concrete default amounts or whether global policy seeding is required. [VERIFIED: ROADMAP.md]
   - Recommendation: Plan schema support and tests with explicit seeded test policies; avoid production default enforcement unless the user supplies amounts. [ASSUMED]
   - Resolution: Phase 21 must not seed or silently enable production default thresholds. It must provide durable schema, API/use-case validation, and tests using explicit fixture policies. A missing effective policy evaluates to an explicit allow/no-policy decision record unless a later phase configures enforcement. [RESOLVED: agent discretion because workflow.skip_discuss=true]

2. **Should owner-specific policy override all global risk behavior or only thresholds?** [RESOLVED]
   - What we know: Phase 21 requires owner/global scope. [VERIFIED: ROADMAP.md]
   - What's unclear: The roadmap does not define inheritance/merge semantics. [VERIFIED: ROADMAP.md]
   - Recommendation: Use one effective policy at a time: owner-specific active policy wins, otherwise global policy. [ASSUMED]
   - Resolution: Phase 21 uses exactly one effective policy at evaluation time. The active owner/billing-account policy wins wholesale over the active global policy; fields are not merged. Tests must prove deterministic precedence. [RESOLVED: agent discretion because workflow.skip_discuss=true]

3. **What exact action kinds belong in Phase 21?** [RESOLVED]
   - What we know: Phase 22 will gate provisioning, start, restart, update, teardown-sensitive actions, and future resize extension points. [VERIFIED: ROADMAP.md]
   - What's unclear: Phase 21 may only need enum/model support for those future action kinds. [ASSUMED]
   - Recommendation: Define `ECloudBillableActionKind` broadly now, but only expose the evaluation service boundary without wiring gates. [ASSUMED]
   - Resolution: Phase 21 defines broad action-kind and automatic-action enums plus an administrator-facing API/use-case boundary for policy configuration and dry evaluation. It does not wire provisioning/runtime gates, approval workflow execution, or automatic shutdown execution; those are owned by phases 22 and 23. [RESOLVED: ROADMAP.md phase boundaries]

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| .NET SDK | Build/test/API projects | yes [VERIFIED: dotnet --list-sdks] | 9.0.100, 9.0.200, 9.0.308 installed; active `dotnet --version` is 10.0.201 [VERIFIED: dotnet --version] | Use installed 9.0 SDK via `global.json` if version drift becomes a problem. [ASSUMED] |
| EF Core CLI | Migration generation/listing | yes [VERIFIED: dotnet ef --version] | 10.0.5 [VERIFIED: dotnet ef --version] | Use local/project tool version aligned to EF 9 if CLI 10 causes scaffolding drift. [ASSUMED] |
| PostgreSQL CLI (`psql`/`pg_isready`) | Direct DB probing | no [VERIFIED: shell probe] | — | Use Aspire-managed Postgres or EF migration scaffolding without direct psql. [VERIFIED: AGENTS.md; ASSUMED] |
| Docker | Aspire/Postgres development support | partial [VERIFIED: docker info] | Client 29.5.3 responded; daemon status not fully verified. [VERIFIED: docker info] | Use existing SQLite tests for service-level verification when Postgres is unavailable. [VERIFIED: codebase grep; ASSUMED] |
| `ctx7` | Documentation lookup fallback | no [VERIFIED: shell probe] | — | Official Microsoft/OWASP docs were fetched through web search. [VERIFIED: web search] |

**Missing dependencies with no fallback:**
- None for research and service-level planning. [ASSUMED]

**Missing dependencies with fallback:**
- PostgreSQL CLI tools are missing; existing API tests use SQLite in-memory and Aspire can provide local Postgres for manual development. [VERIFIED: shell probe; VERIFIED: codebase grep; VERIFIED: AGENTS.md]

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 with Microsoft.NET.Test.Sdk 17.13.0 [VERIFIED: dotnet list package] |
| Config file | `tests/Api.Tests/Api.Tests.csproj` [VERIFIED: codebase grep] |
| Quick run command | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicy` [ASSUMED] |
| Full suite command | `dotnet test X39.UnitedTacticalForces.slnx` [VERIFIED: AGENTS.md] |

### Phase Requirements -> Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| COST-07 | Policy validation rejects invalid limits/threshold ordering and stores owner/global/effective settings. [VERIFIED: .planning/REQUIREMENTS.md; ASSUMED] | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicyConfigurationTests` | no; Wave 0 [VERIFIED: rg] |
| COST-19 | Evaluation treats stale metadata, stale reconciliation, cleanup-needed billable resources, and unknown billable resources as risk triggers. [VERIFIED: .planning/REQUIREMENTS.md] | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicyEvaluationTests` | no; Wave 0 [VERIFIED: rg] |
| RELY-17 | Decision records, errors, and input snapshots survive a new service instance and retry idempotently. [VERIFIED: .planning/REQUIREMENTS.md] | unit/service | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicyEvaluationTests` | no; Wave 0 [VERIFIED: rg] |

### Sampling Rate

- **Per task commit:** `dotnet test tests/Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CloudSpendingPolicy` [ASSUMED]
- **Per wave merge:** `dotnet test tests/Api.Tests/Api.Tests.csproj` after existing compile drift is repaired. [VERIFIED: dotnet test --list-tests; ASSUMED]
- **Phase gate:** `dotnet build X39.UnitedTacticalForces.slnx` plus targeted policy tests green; full `dotnet test X39.UnitedTacticalForces.slnx` currently has pre-existing API test compile errors. [VERIFIED: dotnet test --list-tests]

### Wave 0 Gaps

- [ ] `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyConfigurationTests.cs` covers COST-07 policy validation/effective selection. [ASSUMED]
- [ ] `tests/Api.Tests/CloudPolicies/CloudSpendingPolicyEvaluationTests.cs` covers COST-19 threshold/risk handling and RELY-17 idempotent persistence. [ASSUMED]
- [ ] Existing API test compile drift must be fixed or isolated before using `dotnet test tests/Api.Tests/Api.Tests.csproj` as a gate: current errors include stale `CloudInventoryController` constructor calls, missing `Constants` imports, and `ECloudReconciliationRunStatus.Succeeded` references. [VERIFIED: dotnet test --list-tests]

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | yes [VERIFIED: config.json; VERIFIED: codebase grep] | Reuse existing `[Authorize]`/cookie auth for any Phase 21 admin endpoints; do not expose unauthenticated policy operations. [VERIFIED: codebase grep] |
| V3 Session Management | limited [VERIFIED: config.json; VERIFIED: codebase grep] | No session changes are planned; preserve existing cookie/session behavior. [VERIFIED: codebase grep; ASSUMED] |
| V4 Access Control | yes [VERIFIED: config.json; VERIFIED: codebase grep] | Use existing administrative server claims for policy administration and avoid browser-only enforcement. [VERIFIED: codebase grep; CITED: https://owasp.org/www-project-secure-coding-practices-quick-reference-guide/stable-en/02-checklist/05-checklist] |
| V5 Input Validation | yes [VERIFIED: config.json] | Validate thresholds, dates, action settings, and scope IDs server-side with allow-listed enums and range checks. [CITED: https://owasp.org/www-project-secure-coding-practices-quick-reference-guide/stable-en/02-checklist/05-checklist; ASSUMED] |
| V6 Cryptography | no new cryptography [VERIFIED: ROADMAP.md] | Do not add crypto; keep provider tokens and worker credentials outside DTOs/logs. [VERIFIED: AGENTS.md] |
| V7 Error Handling and Logging | yes [VERIFIED: config.json; VERIFIED: codebase grep] | Persist sanitized, bounded failure summaries/details and use structured logs without secrets. [VERIFIED: codebase grep; CITED: https://learn.microsoft.com/en-us/dotnet/core/extensions/data-redaction] |
| V11 Business Logic | yes [VERIFIED: .planning/REQUIREMENTS.md] | Test threshold precedence, risk precedence, effective policy selection, and idempotency. [ASSUMED] |

### Known Threat Patterns for ASP.NET Core + EF Policy Evaluation

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Direct endpoint bypass of policy gates in later phases | Elevation of Privilege | Keep evaluation service server-side and require future billable endpoints to call it. [VERIFIED: ROADMAP.md; ASSUMED] |
| Threshold tampering through invalid payload values | Tampering | Server-side validation for non-negative values, threshold ordering, active/effective dates, and enum allow lists. [ASSUMED; CITED: https://owasp.org/www-project-secure-coding-practices-quick-reference-guide/stable-en/02-checklist/05-checklist] |
| Secret leakage in persisted errors/input snapshots | Information Disclosure | Reuse `LifecycleFailureReasonSanitizer`; cap JSON/text lengths; never include token/config values. [VERIFIED: AGENTS.md; VERIFIED: codebase grep] |
| Duplicate policy decisions after retry/restart | Repudiation / Integrity | Unique idempotency index and return existing decision on duplicate proposal. [CITED: https://learn.microsoft.com/en-us/ef/core/modeling/indexes; ASSUMED] |
| SQL injection in policy queries | Tampering | Use EF LINQ and parameterized provider queries; do not build raw SQL from policy input. [VERIFIED: codebase grep; ASSUMED] |

## Sources

### Primary (HIGH confidence)

- Project `AGENTS.md` - stack, conventions, constraints, generated-code rules, security rules. [VERIFIED: AGENTS.md]
- `.planning/phases/21-spending-policy-model-and-evaluation-boundary/21-CONTEXT.md` - phase boundary and discretion. [VERIFIED: 21-CONTEXT.md]
- `.planning/REQUIREMENTS.md` - COST-07, COST-19, RELY-17. [VERIFIED: .planning/REQUIREMENTS.md]
- `.planning/ROADMAP.md` - Phase 21 success criteria and phase sequencing. [VERIFIED: .planning/ROADMAP.md]
- `sources/Api/Data/ApiDbContext.cs`, `sources/Api/Data/Hosting/*.cs`, `sources/Api/Services/CloudCosts/*.cs`, `sources/Api/Services/CloudReconciliation/*.cs`, `sources/Api/Services/GameServerLifecycle/*.cs`. [VERIFIED: codebase grep]
- Microsoft Learn EF Core indexes - unique/composite/filter index guidance. [CITED: https://learn.microsoft.com/en-us/ef/core/modeling/indexes]
- Microsoft Learn EF Core migrations - schema evolution and model snapshot guidance. [CITED: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/]
- Microsoft Learn EF Core cascade delete - required relationships cascade by default. [CITED: https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete]
- Microsoft Learn EF Core testing - testing strategy and SQLite/in-memory cautions. [CITED: https://learn.microsoft.com/en-us/ef/core/testing/]
- Microsoft Learn data redaction - redaction purpose and package guidance. [CITED: https://learn.microsoft.com/en-us/dotnet/core/extensions/data-redaction]
- OWASP ASVS project and OWASP secure coding checklist - security verification and server-side input validation/access control. [CITED: https://owasp.org/www-project-application-security-verification-standard/; CITED: https://owasp.org/www-project-secure-coding-practices-quick-reference-guide/stable-en/02-checklist/05-checklist]

### Secondary (MEDIUM confidence)

- `dotnet list X39.UnitedTacticalForces.slnx package` - resolved local package versions. [VERIFIED: dotnet list package]
- `dotnet ef migrations list --project sources/Api/Api.csproj --startup-project sources/Api/Api.csproj --no-build` - existing migration sequence; database connection unavailable for applied/pending status. [VERIFIED: dotnet ef migrations list]

### Tertiary (LOW confidence)

- Assumed decision defaults and idempotency key shape; these need planner/user confirmation if they become externally visible behavior. [ASSUMED]

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - package versions and project structure were verified locally, and no new packages are recommended. [VERIFIED: dotnet list package; VERIFIED: codebase grep]
- Architecture: HIGH - the phase maps cleanly to existing Hosting persistence, cost snapshots, reconciliation findings, and sanitized audit patterns. [VERIFIED: codebase grep; VERIFIED: ROADMAP.md]
- Pitfalls: MEDIUM - most pitfalls are derived from existing code paths and requirements, but exact policy precedence defaults remain assumed. [VERIFIED: codebase grep; ASSUMED]

**Research date:** 2026-06-18 [VERIFIED: system date]
**Valid until:** 2026-07-18 for project-code findings; re-check package/tool versions if planning occurs after that date. [ASSUMED]
