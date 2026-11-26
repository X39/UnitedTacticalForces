# Phase 33: Guided Entry, Ownership, And Scope Boundaries - Research

**Researched:** 2026-06-19
**Domain:** ASP.NET Core API, Blazor WebAssembly, EF Core/PostgreSQL, cloud planning entry workflow
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
## Phase Boundary

This phase is only the guided entry, ownership selection, and planning-input boundary for Arma 3 cloud creation.

- Authorized users should begin the cloud creation path from `/servers`, not `/servers/cloud`.
- `/servers/cloud` remains the admin console for provider/fleet operations.
- Users can create a new Arma 3 game server or select an eligible existing Arma 3 server for cloud planning.
- A billing owner and active assignment must be selected before dry-run planning or provisioning can proceed.
- The flow must collect planning inputs: size, location, storage, ports, SSH/admin CIDR, modpack, and runtime basics.
- The phase must not mutate provider resources.
- The phase must not add payment, invoicing, multi-provider, or multi-session-worker behavior.
- Phase 30 remains deferred and out of scope.

## Verification Boundary

All UAT is deferred by user instruction. Treat manual, authenticated, and live-provider UAT as deferred evidence, not a blocker for planning.

Use automated build/test/static verification where possible; do not require live provider, Steam, Arma, or authenticated browser UAT for this phase.

### the agent's Discretion
All implementation choices are at the agent's discretion because discuss-phase was skipped.

### Deferred Ideas (OUT OF SCOPE)
- Authenticated, live provider, Steam, Arma, and Phase 30 UAT are deferred.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CREATE-01 | Authorized users can start the guided Arma 3 cloud server creation flow from `/servers`. | `/servers` is implemented by `sources/WebApp/Pages/Servers/Index.razor`; `/servers/cloud` is implemented by `sources/WebApp/Pages/Servers/Cloud.razor` and is the admin cloud console. [VERIFIED: codebase grep] |
| CREATE-02 | Authorized users can create a new Arma 3 game server or select an existing Arma 3 game server for cloud planning. | Accessible server listing already uses resource/admin scope in `GameServersController.GetUserAccessibleDatabaseGameServersAsync`; current server creation is admin-only and queues an install lifecycle job, so a scoped cloud-entry create path or an explicit acceptance of that side effect must be planned. [VERIFIED: codebase grep] |
| CREATE-03 | Authorized users must choose an active billing owner and assignment before cloud dry-run planning or provisioning. | `IBillingOwnershipService.RequireActiveAssignmentAsync` rejects missing/inactive/non-unique active assignments, and `Arma3CloudDryRunPlannerService` rejects selected billing account/assignment mismatches before producing a valid plan. [VERIFIED: codebase grep] |
| CREATE-04 | Authorized users can enter cloud planning inputs for size, location, storage, ports, SSH/admin CIDR, modpack, and runtime basics. | `Arma3CloudDryRunPlanRequestPayload` already carries provider size/location/image/datacenter, ports, storage, SSH exposure, and CIDR fields; modpack/runtime basics are currently represented by the selected game server and existing game-server/modpack surfaces rather than by that payload. [VERIFIED: codebase grep] |
</phase_requirements>

## Summary

Phase 33 should be planned as an owner/operator entry flow embedded in the existing `/servers` page, not as a new admin-cloud surface. [VERIFIED: CONTEXT.md] The UI should reuse the current `Servers/Index.razor` layout classes and Fluent UI components from `33-UI-SPEC.md`, while keeping `/servers/cloud` as an admin-only provider/fleet console. [VERIFIED: 33-UI-SPEC.md]

The main backend planning issue is authorization scope, not provider behavior. [VERIFIED: codebase grep] The API already has durable billing-owner entities, dry-run planning services, generated Kiota models, and tests around owner mismatch/no side effects, but the current billing account list/create endpoints and provider metadata endpoints are admin-only. [VERIFIED: codebase grep] The planner should therefore include either owner-scoped projection endpoints or carefully scoped authorization changes before the `/servers` entry flow can satisfy CREATE-02 through CREATE-04 for non-admin authorized operators. [ASSUMED]

Do not plan live provider mutation in this phase. [VERIFIED: CONTEXT.md] The existing dry-run planner persists `CloudDryRunPlan` and related local records, and the existing game-server create endpoint queues an install lifecycle job. [VERIFIED: codebase grep] If Phase 33 must stop before dry-run planning, the plan should collect and validate readiness inputs and leave actual dry-run submission for a later phase; if it creates a new `GameServer`, the plan must decide whether to reuse the existing create endpoint despite its install job side effect or introduce a cloud-planning draft/create path. [ASSUMED]

**Primary recommendation:** Implement a `/servers` guided panel backed by server-enforced eligibility, owner-assignment, and planning-input validation endpoints; use generated Kiota clients behind `IGameServerRepository`; do not call provider mutation or expose `/servers/cloud` as the owner entry path. [VERIFIED: codebase grep]

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| Guided entry surface under `/servers` | Browser / Client | API / Backend | Blazor owns the routed user workflow and visible validation; API owns the scoped data and authorization checks. [VERIFIED: codebase grep] |
| Eligible Arma 3 server selection | API / Backend | Browser / Client | The server must enforce active/resource-scoped server visibility and Arma 3 eligibility; the browser only presents the filtered list. [VERIFIED: codebase grep] |
| New Arma 3 game-server entry for cloud planning | API / Backend | Database / Storage | Creating a persisted `GameServer` and active owner assignment is server/database work; the existing create route is admin-only and queues an install lifecycle job. [VERIFIED: codebase grep] |
| Billing owner and active assignment selection | API / Backend | Database / Storage | `BillingOwnershipService` and the filtered unique database index enforce one active billing assignment per server. [VERIFIED: codebase grep] |
| Planning input capture and visible validation | Browser / Client | API / Backend | The UI must show validation immediately; API validation remains authoritative before dry-run/provision can continue. [CITED: https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation] |
| Provider metadata options | API / Backend | Database / Storage | Hetzner metadata is stored locally and current metadata endpoints are admin-only, so an owner-safe projection is needed if non-admins choose size/location/image values. [VERIFIED: codebase grep] |
| Provider resource mutation | Out of scope | - | The phase context explicitly forbids mutating provider resources. [VERIFIED: CONTEXT.md] |

## Project Constraints (from AGENTS.md)

- Keep the existing game-controller abstraction during this milestone so current Arma 3 support is migrated rather than replaced. [VERIFIED: AGENTS.md]
- Lifecycle, job, worker, ownership, and audit state must survive API restarts. [VERIFIED: AGENTS.md]
- Every cost-generating or hosting session must have an owner from creation until cleanup or transfer completes. [VERIFIED: AGENTS.md]
- Do not implement Hetzner provisioning in the first milestone. [VERIFIED: AGENTS.md]
- Steam credentials, future provider tokens, and worker credentials must not be exposed to frontend clients or unredacted logs. [VERIFIED: AGENTS.md]
- Existing local development through Aspire, PostgreSQL, API, and Blazor WebApp should keep working. [VERIFIED: AGENTS.md]
- Add focused tests around persistence, job transitions, authorization-sensitive actions, and worker recovery where the phase touches those areas. [VERIFIED: AGENTS.md]
- Use server-side EF entities only inside the API and expose DTO/payload/update/contract types to the client. [VERIFIED: AGENTS.md]
- Wrap generated Kiota request builders in WebApp repository interfaces; do not hand-edit `sources/WebApp/UtfApi/**`. [VERIFIED: AGENTS.md]
- Use file-scoped namespaces, one public type per `.cs` file, `Dto`/`Payload` suffixes, `E` enum prefix, and the existing C# formatting conventions. [VERIFIED: AGENTS.md]
- Do not log secrets or raw configuration values from secret/user appsettings, OAuth settings, database settings, provider tokens, Steam credentials, or worker credentials. [VERIFIED: AGENTS.md]

## Standard Stack

### Core

| Library / Platform | Version | Purpose | Why Standard |
|--------------------|---------|---------|--------------|
| .NET SDK / target framework | Projects target `net9.0`; local SDK probe returned `10.0.201`. | Builds the ASP.NET Core API, Blazor WebAssembly app, Aspire AppHost, and tests. | Existing project baseline; no runtime/package migration is needed for this phase. [VERIFIED: codebase grep] |
| ASP.NET Core MVC/Auth | 9.0.x package family in project files. | API controllers, route authorization, cookie-authenticated calls, OpenAPI generation. | Existing controllers and `[Authorize]` policies already gate server/cloud operations. [VERIFIED: codebase grep] |
| Entity Framework Core + Npgsql | `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4. | PostgreSQL persistence for game servers, billing assignments, cloud plans, sessions, jobs, and metadata. | Existing `ApiDbContext` already models the lifecycle/billing/cloud planning state this phase must respect. [VERIFIED: dotnet list package] |
| Blazor WebAssembly | `Microsoft.AspNetCore.Components.WebAssembly` 9.0.4. | `/servers` routed UI and client-side workflow state. | Existing WebApp is Blazor WASM and `Servers/Index.razor` is the intended entry surface. [VERIFIED: dotnet list package] |
| Microsoft Fluent UI Blazor | `Microsoft.FluentUI.AspNetCore.Components` 4.11.8. | Buttons, message bars, progress/status controls, icons. | UI-SPEC requires Fluent UI and the project already registers Fluent components. [VERIFIED: dotnet list package] |
| Microsoft Kiota Bundle | `Microsoft.Kiota.Bundle` 1.22.2. | Generated API client models and route builders used by WebApp repositories. | Generated builders already exist for billing accounts and `game-servers/{id}/cloud-dry-run-plans/arma3`. [VERIFIED: dotnet list package] |
| xUnit + Microsoft.NET.Test.Sdk | xUnit 2.9.3, Test SDK 17.13.0. | Automated API/service tests. | Existing `tests/Api.Tests` covers cloud planning, billing ownership, and no-provider-dependency behaviors. [VERIFIED: dotnet list package] |

### Supporting

| Library / Component | Version | Purpose | When to Use |
|---------------------|---------|---------|-------------|
| `X39.Util.DependencyInjection` | 2.0.1.8. | Attribute-driven service registration. | Use existing `[Scoped<...>]`/`[Singleton<...>]` patterns if adding repository/service abstractions. [VERIFIED: codebase grep] |
| `IBillingOwnershipService` | Existing API service. | Creates user billing accounts, assigns owners, reads/requires active assignments, and writes audit events. | Use for CREATE-03; do not duplicate active-owner logic. [VERIFIED: codebase grep] |
| `IArma3CloudDryRunPlannerService` | Existing API service. | Validates and persists Arma 3 cloud dry-run plans using local metadata and billing assignment checks. | Use only when the phase intentionally advances to dry-run; do not use it as a pure UI readiness check without accepting persisted plan side effects. [VERIFIED: codebase grep] |
| `ICloudAdminRepository` | Existing WebApp service. | Admin console access to provider status, metadata, sessions, plans, resources, costs, policies, reconciliation. | Keep in `/servers/cloud`; do not route owner creation through it. [VERIFIED: codebase grep] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Existing Blazor/Fluent `/servers` page | A new `/servers/create-cloud` route | A new route is possible, but UI-SPEC requires the workflow to start inside `/servers` and reuse its dense operational layout. [VERIFIED: 33-UI-SPEC.md] |
| Generated Kiota builders behind repositories | Raw `HttpClient.GetFromJsonAsync`/`PostAsJsonAsync` in `GameServerRepositoryImpl` | Raw HTTP bridges already exist in this repository, but project decisions require cloud integration through generated Kiota clients and repository facades. [VERIFIED: STATE.md] |
| Existing billing ownership service | Direct EF insert/update of `GameServerBillingAssignment` | Direct writes risk violating active-owner uniqueness, audit behavior, inactive-owner rejection, and transfer semantics. [VERIFIED: codebase grep] |
| Existing metadata cache | Live provider API calls from the owner workflow | Live provider interaction is out of scope, and provider tokens must stay server-side. [VERIFIED: CONTEXT.md] |

**Installation:**

No new external packages should be installed for this phase. [VERIFIED: codebase grep]

## Package Legitimacy Audit

No external package installation is recommended for Phase 33, so the slopcheck package legitimacy gate is not applicable. [VERIFIED: codebase grep]

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| none | - | - | - | - | - | No package changes recommended. [VERIFIED: codebase grep] |

**Packages removed due to slopcheck [SLOP] verdict:** none. [VERIFIED: codebase grep]
**Packages flagged as suspicious [SUS]:** none. [VERIFIED: codebase grep]

## Architecture Patterns

### System Architecture Diagram

```text
Authorized user
  -> /servers (Servers/Index.razor)
  -> load accessible servers via GameServersController.GetGameServersAsync
  -> choose eligible existing Arma 3 server
     OR request creation of a cloud-planning Arma 3 server
  -> API enforces server/resource/create scope
  -> API returns eligible active billing owners/assignment choices
  -> UI collects runtime/modpack + size/location/storage/ports/CIDR inputs
  -> client shows validation immediately
  -> API validates authoritative readiness against persisted server, owner assignment, and metadata
  -> UI shows ready summary and "Continue to dry run" boundary
  -> future dry-run/provisioning phase

/servers/cloud
  -> Cloud.razor admin console
  -> provider config, metadata sync, fleet/session/resource/cost operations
  -> not the normal owner creation path
```

All arrows above are conceptual data/control flow for planning; current provider metadata and billing account admin endpoints are not yet owner-scoped. [VERIFIED: codebase grep]

### Recommended Project Structure

```text
sources/
+-- Api/
|   +-- Controllers/
|   |   +-- GameServersController.cs              # existing server operations and owner assignment endpoints
|   |   +-- CloudPlanningController.cs            # existing dry-run endpoint; use only if persisted dry-run is intended
|   +-- DTO/
|   |   +-- ...                                   # add guided-entry/readiness DTOs here if needed
|   +-- DTO/Payloads/
|   |   +-- ...                                   # add guided-entry payloads here if needed
|   +-- Data/Hosting/
|   |   +-- ...                                   # existing game server, billing, cloud planning, job entities
|   +-- Services/
|       +-- ...                                   # reuse BillingOwnershipService and cloud planner services
+-- WebApp/
|   +-- Pages/Servers/
|   |   +-- Index.razor                           # owner/operator guided entry surface
|   |   +-- Index.razor.css                       # reuse existing route-scoped layout classes
|   |   +-- Cloud.razor                           # admin console; keep separate
|   +-- Services/GameServerRepository/
|       +-- IGameServerRepository.cs              # facade additions
|       +-- GameServerRepositoryImpl.cs           # call generated Kiota builders
+-- WebApp/UtfApi/
    +-- ...                                       # generated only; do not hand-edit
```

This structure follows the existing controller/DTO/repository/generated-client boundaries. [VERIFIED: AGENTS.md]

### Pattern 1: Resource-Scoped Server Eligibility

**What:** Load candidate servers through the API and enforce server visibility/authorization server-side; the UI only filters/presents what the API allows. [VERIFIED: codebase grep]

**When to use:** Use for CREATE-01 and CREATE-02 whenever a user selects an existing server or creates a server intended for cloud planning. [VERIFIED: REQUIREMENTS.md]

**Relevant existing pattern:**

```csharp
// Source: sources/Api/Controllers/GameServersController.cs
// Existing behavior: admins see all active servers; non-admins see servers matched by resource claims.
private IQueryable<GameServer> GetUserAccessibleDatabaseGameServersAsync()
{
    if (User.HasClaim(Claims.Administrative.All) || User.HasClaim(Claims.Administrative.Server))
        return apiDbContext.GameServers.Where((q) => q.IsActive);

    var userClaims = User.Claims
        .Where((q) => q.Type == Claims.ResourceBased.Server.All
                      || q.Type.StartsWith($"{Claims.ResourceBased.Server.All}:"));

    return userClaims
        .Select((claim) => long.TryParse(claim.Value, out var id) ? id : 0)
        .Aggregate(
            apiDbContext.GameServers.Where((q) => false),
            (current, id) => current.Union(apiDbContext.GameServers.Where((q) => q.PrimaryKey == id && q.IsActive))
        );
}
```

ASP.NET Core resource authorization may need imperative checks after loading a route resource because attribute authorization runs before resource loading/model binding. [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based]

### Pattern 2: Billing Owner Assignment Through the Service

**What:** Use `IBillingOwnershipService` for active owner lookup and assignment instead of manipulating assignment rows directly. [VERIFIED: codebase grep]

**When to use:** Use for CREATE-03 and for any CREATE-02 path that persists a new server before planning. [VERIFIED: REQUIREMENTS.md]

**Relevant existing behavior:**

```csharp
// Source: sources/Api/Services/BillingOwnershipService.cs
// Existing behavior: active account required, prior active assignment closed, new active assignment audited.
public async Task<GameServerBillingAssignment> AssignOwnerAsync(
    long gameServerFk,
    long billingAccountFk,
    long assignedByFk,
    string? reason,
    CancellationToken cancellationToken)
{
    var billingAccount = await apiDbContext.BillingAccounts
        .FirstOrDefaultAsync((q) => q.PrimaryKey == billingAccountFk, cancellationToken)
        .ConfigureAwait(false);
    if (billingAccount is null)
        throw new InvalidOperationException("Billing account does not exist.");
    if (!billingAccount.IsActive)
        throw new InvalidOperationException("Billing account is inactive.");

    // Existing implementation closes the previous active assignment and creates a new audited one.
}
```

The database also defines a filtered unique index for one active assignment per game server where `TimeStampUnassigned IS NULL`. [VERIFIED: codebase grep]

### Pattern 3: Generated Kiota Client Behind Repository Facades

**What:** Add WebApp methods to `IGameServerRepository`/`GameServerRepositoryImpl`, and call generated `UtfApi` builders from the implementation. [VERIFIED: AGENTS.md]

**When to use:** Use for billing account reads/creates, guided readiness endpoints, and future dry-run calls. [VERIFIED: STATE.md]

**Relevant generated routes:**

```csharp
// Source: sources/WebApp/UtfApi/GameServers/Item/CloudDryRunPlans/Arma3/Arma3RequestBuilder.cs
await Client.GameServers[gameServerId]
    .CloudDryRunPlans
    .Arma3
    .PostAsync(payload, cancellationToken: cancellationToken);

// Source: sources/WebApp/UtfApi/BillingAccounts/All/AllRequestBuilder.cs
await Client.BillingAccounts.All.GetAsync(cancellationToken: cancellationToken);
```

The generated dry-run builder posts to `/game-servers/{gameServerId}/cloud-dry-run-plans/arma3`, and the generated billing builder gets `/billing-accounts/all`. [VERIFIED: codebase grep]

### Pattern 4: Visible Form Validation With Server Authority

**What:** Put visible field-level validation in the Blazor guided panel and mirror critical validation server-side before dry-run/provision can continue. [CITED: https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation]

**When to use:** Use for CREATE-04 fields: title, Arma 3 eligibility, owner/assignment, server type, location/datacenter/image, storage, unique ports, SSH/admin CIDR, modpack, and runtime basics. [VERIFIED: 33-UI-SPEC.md]

**Relevant documentation pattern:**

```csharp
// Source: Microsoft Blazor forms validation docs
var messageStore = new ValidationMessageStore(editContext);
editContext.OnValidationRequested += (_, _) =>
{
    messageStore.Clear();
    // Add field-specific messages for cross-field checks such as duplicate ports.
};
```

`DataAnnotationsValidator` attaches validation support based on data annotations, and `ValidationMessageStore` supports custom validation messages in an `EditContext`. [CITED: https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation]

### Anti-Patterns to Avoid

- **Owner flow through `/servers/cloud`:** `/servers/cloud` is the admin provider/fleet console, not the normal owner creation path. [VERIFIED: CONTEXT.md]
- **Client-only authorization filtering:** Client checks in `MeService` are useful for display but cannot protect owner/server/provider data. [VERIFIED: codebase grep]
- **Calling dry-run as a pure validation endpoint:** The current Arma 3 dry-run service can persist plans, resources, and cost estimates, so using it changes local state. [VERIFIED: codebase grep]
- **Reusing admin-only billing/metadata endpoints for non-admin users:** Current billing account and Hetzner metadata endpoints are guarded by `Claims.Administrative.Server`. [VERIFIED: codebase grep]
- **Hand-editing generated Kiota files:** `sources/WebApp/UtfApi/**` is generated and should be wrapped, not modified. [VERIFIED: AGENTS.md]

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Server/resource authorization | Browser-only claim checks or ad-hoc route guards | Existing ASP.NET Core `[Authorize]` policies plus imperative server-side checks where route resources must be loaded | Attribute authorization cannot evaluate a loaded resource before the action loads it, so server-side resource checks must be deliberate. [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based] |
| Active billing owner enforcement | Direct EF writes to `GameServerBillingAssignment` | `IBillingOwnershipService` and the existing active-assignment index | Existing service enforces inactive-account rejection, one active assignment, and audit records. [VERIFIED: codebase grep] |
| Arma 3 cloud planning validation | Duplicated provider/port/metadata logic in the UI | Existing `Arma3CloudDryRunPlanRequestPayload` semantics and planner validation for authoritative checks | Existing tests cover owner mismatch, validation failures, and no provider HTTP dependency. [VERIFIED: codebase grep] |
| Provider metadata fetching | Browser calls or live provider calls | Server-side cached metadata projections | Provider tokens must not reach the browser, and live provider mutation/use is out of scope. [VERIFIED: AGENTS.md] |
| API client plumbing | New raw browser HTTP bridge | Generated Kiota builders wrapped by repositories | Project decisions require cloud integration through generated clients and facades. [VERIFIED: STATE.md] |
| UI component system | New component library or marketing wizard | Existing Blazor + Fluent UI + route CSS | UI-SPEC requires a dense operational panel under `/servers`. [VERIFIED: 33-UI-SPEC.md] |

**Key insight:** The hard part is preserving authorization and ownership boundaries while reusing existing cloud planning primitives; custom UI-only checks would make the flow look correct while leaving IDOR, owner enumeration, and premature side-effect risks. [VERIFIED: codebase grep]

## Common Pitfalls

### Pitfall 1: Treating Admin Cloud Console Data as Owner-Visible Data

**What goes wrong:** The `/servers` guided entry starts using `/servers/cloud` services or admin-only metadata/billing endpoints, exposing admin fleet concepts to owners. [VERIFIED: codebase grep]

**Why it happens:** `CloudAdminRepository` already has convenient provider metadata/status methods, but `HetznerProviderController.GetMetadataAsync` and `GetMetadataStatusAsync` are admin-only. [VERIFIED: codebase grep]

**How to avoid:** Add a scoped, secret-safe planning-options/readiness projection if owners need metadata choices, and keep admin controls in `Cloud.razor`. [ASSUMED]

**Warning signs:** Owner flow imports `ICloudAdminRepository`, links to `/servers/cloud`, or shows provider configuration/sync/reconciliation actions. [VERIFIED: codebase grep]

### Pitfall 2: Advancing to Dry-Run Too Early

**What goes wrong:** The Phase 33 form posts to `POST /game-servers/{id}/cloud-dry-run-plans/arma3` while the phase only intends input readiness. [VERIFIED: codebase grep]

**Why it happens:** The dry-run endpoint is close to the desired payload and already validates billing owner and provider metadata. [VERIFIED: codebase grep]

**How to avoid:** Plan a non-mutating readiness/validation endpoint or keep server-side dry-run submission behind the "Continue to dry run" boundary for a later phase. [ASSUMED]

**Warning signs:** Tests assert new `CloudDryRunPlan`, `CloudResourceInventoryRecord`, or cost estimate rows during CREATE-01 through CREATE-04. [VERIFIED: codebase grep]

### Pitfall 3: Existing Server Creation Is Not a Pure Planning Draft

**What goes wrong:** CREATE-02 is implemented by calling the current admin create endpoint, which creates a server, assigns a billing owner, and enqueues an install lifecycle job. [VERIFIED: codebase grep]

**Why it happens:** The current endpoint is the only persisted server creation path in `GameServersController`. [VERIFIED: codebase grep]

**How to avoid:** Decide explicitly whether Phase 33 permits this local install side effect or needs a separate cloud-planning create/draft path that persists the minimum server/owner state without queueing install. [ASSUMED]

**Warning signs:** A new owner/operator path calls `CreateGameServerAsync` without a test proving lifecycle jobs are intended. [VERIFIED: codebase grep]

### Pitfall 4: Owner Choice Enumeration

**What goes wrong:** A non-admin user can see or select billing accounts outside their authorized owner scope. [ASSUMED]

**Why it happens:** Current `BillingAccountsController.GetBillingAccountsAsync` and `CreateBillingAccountAsync` are admin-only; no owner-scoped list/create endpoint was found. [VERIFIED: codebase grep]

**How to avoid:** Plan explicit owner eligibility rules and API tests for account visibility, inactive accounts, mismatched assignment rejection, and no side effects. [VERIFIED: codebase grep]

**Warning signs:** UI loads all billing accounts and hides unauthorized rows client-side. [VERIFIED: codebase grep]

### Pitfall 5: CIDR and Port Validation Drift

**What goes wrong:** The UI accepts incomplete/unsafe networking inputs, or the API silently defaults admin CIDR to `0.0.0.0/0` while the design requires explicit CIDR when SSH/admin exposure is enabled. [VERIFIED: codebase grep]

**Why it happens:** Existing dry-run planner has defaults that are useful for admin dry-runs but the UI-SPEC requires stricter visible validation. [VERIFIED: 33-UI-SPEC.md]

**How to avoid:** Implement explicit client and server readiness validation for 1-65535 ports, unique port values, nullable RCON when disabled, positive storage, and required CIDR when SSH/admin exposure is enabled. [VERIFIED: 33-UI-SPEC.md]

**Warning signs:** Empty CIDR produces an apparently ready summary for SSH-enabled planning. [ASSUMED]

### Pitfall 6: Raw HTTP Bridge Creep

**What goes wrong:** New WebApp methods add more `HttpClient` JSON calls instead of using generated route builders. [VERIFIED: codebase grep]

**Why it happens:** `GameServerRepositoryImpl` already contains raw HTTP calls for several routes. [VERIFIED: codebase grep]

**How to avoid:** Use or regenerate Kiota builders and keep new calls behind repository methods. [VERIFIED: STATE.md]

**Warning signs:** New string-built route paths for cloud planning, billing accounts, or owner assignment in `GameServerRepositoryImpl`. [VERIFIED: codebase grep]

## Code Examples

### Current Dry-Run Payload Shape

```csharp
// Source: sources/Api/DTO/Payloads/Arma3CloudDryRunPlanRequestPayload.cs
public sealed class Arma3CloudDryRunPlanRequestPayload
{
    public long? GameServerFk { get; init; }
    public long SelectedBillingAccountFk { get; init; }
    public long SelectedBillingAssignmentFk { get; init; }
    public string? ServerTypeName { get; init; }
    public string? LocationName { get; init; }
    public string? DatacenterName { get; init; }
    public string? ImageName { get; init; }
    public int? GamePort { get; init; }
    public int? SteamQueryPort { get; init; }
    public int? SteamPort { get; init; }
    public int? RconPort { get; init; }
    public int? StorageGigabytes { get; init; }
    public bool ExposeSsh { get; init; } = true;
    public string? AdministrativeSourceCidr { get; init; }
}
```

This payload supports the Phase 33 provider/network/storage fields, but modpack/runtime basics must be collected from existing server/modpack state or a new readiness DTO because they are not explicit payload fields. [VERIFIED: codebase grep]

### Current `/servers` Authorization Helpers

```csharp
// Source: sources/WebApp/Pages/Servers/Index.razor and sources/WebApp/Services/MeService.cs
private bool CanViewServers => Me.HasBaseClaimOrServerAdmin(Claims.ResourceBased.Server.All);

private bool CanManageServers => Me.HasClaimOrServerAdmin();

private bool HasServerPermission(string claim, long serverId)
{
    return Me.HasResourceClaimOrServerAdmin(Claims.ResourceBased.Server.All, serverId.ToString(CultureInfo.InvariantCulture))
           || Me.HasResourceClaimOrServerAdmin(claim, serverId.ToString(CultureInfo.InvariantCulture));
}
```

`CanViewServers` is broader than `CanManageServers`, while the existing create form is currently gated by `CanManageServers`; Phase 33 needs a separate owner/operator create/plan capability rather than reusing admin-only visibility. [VERIFIED: codebase grep]

### Existing Dry-Run Authorization Gate

```csharp
// Source: sources/Api/Controllers/CloudPlanningController.cs
[HttpPost("arma3", Name = nameof(CreateArma3DryRunPlanAsync))]
[Authorize(Claims.ResourceBased.Server.Configuration)]
public async Task<ActionResult<Arma3CloudDryRunPlanDto>> CreateArma3DryRunPlanAsync(
    [FromRoute] long gameServerId,
    [FromBody] Arma3CloudDryRunPlanRequestPayload payload,
    CancellationToken cancellationToken)
```

The existing dry-run endpoint is server-resource scoped by configuration permission and validates the route/payload server id match. [VERIFIED: codebase grep]

### Client Validation Model Pattern

```csharp
// Source: https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation
private readonly ValidationMessageStore _messageStore;

private void ValidatePlanningInputs(EditContext editContext)
{
    _messageStore.Clear();

    if (PlanningModel.Ports.Where((q) => q.HasValue).Distinct().Count() != PlanningModel.Ports.Count((q) => q.HasValue))
        _messageStore.Add(() => PlanningModel.GamePort, "Ports must be unique.");

    if (PlanningModel.ExposeSsh && string.IsNullOrWhiteSpace(PlanningModel.AdministrativeSourceCidr))
        _messageStore.Add(() => PlanningModel.AdministrativeSourceCidr, "CIDR is required when SSH access is enabled.");
}
```

Use the exact local model and field names chosen during implementation; the important pattern is field-level validation with `EditContext` and `ValidationMessageStore`. [CITED: https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation]

## State of the Art

| Old / Risky Approach | Current Recommended Approach | When Changed | Impact |
|----------------------|------------------------------|--------------|--------|
| Start cloud creation from admin `/servers/cloud`. | Start owner/operator creation from `/servers`, keep `/servers/cloud` admin-only. | Locked for Phase 33. | Prevents normal creation from exposing fleet/provider admin concerns. [VERIFIED: CONTEXT.md] |
| Use raw browser HTTP calls for cloud routes. | Use generated Kiota request builders behind repository facades. | Locked in project state before Phase 33. | Keeps client integration consistent and discoverable. [VERIFIED: STATE.md] |
| Let provider action establish ownership later. | Require active billing owner/assignment before dry-run/provision continuation. | Locked by CREATE-03 and milestone billing constraint. | Prevents unattributed cost-generating sessions. [VERIFIED: REQUIREMENTS.md] |
| Fetch live provider data during owner entry. | Use cached metadata/projections and avoid provider mutation. | Locked by Phase 33 boundary. | Keeps provider credentials and live actions out of this phase. [VERIFIED: CONTEXT.md] |

**Deprecated/outdated for this phase:**

- Treating Phase 30 UAT as a blocker is out of date for this phase because the user explicitly deferred all UAT evidence. [VERIFIED: CONTEXT.md]
- Treating `/servers/cloud` as the owner creation path contradicts the Phase 33 context and UI-SPEC. [VERIFIED: 33-UI-SPEC.md]

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | A new owner-scoped billing account/assignment projection is likely needed for non-admin operators because existing billing account endpoints are admin-only. [ASSUMED] | Summary, Pitfalls | Planner could overbuild if only admins are expected to use Phase 33, or underbuild if non-admin owners must use it. |
| A2 | Phase 33 should avoid calling the existing dry-run endpoint unless persisted dry-run records are considered acceptable before the "continue" boundary. [ASSUMED] | Summary, Architecture Patterns | Planner could produce unintended local plan/resource/cost records during entry. |
| A3 | Creating a new cloud-planning server should probably avoid the existing install lifecycle job unless the product accepts that side effect. [ASSUMED] | Pitfalls | Planner could trigger local lifecycle work before cloud planning is complete. |
| A4 | Owner-safe provider option projection should expose only names/labels/capabilities needed for planning, not admin provider status or sync controls. [ASSUMED] | Architecture Patterns | Planner could leak admin/provider details into the owner flow or fail to provide enough data for CREATE-04. |

## Open Questions (RESOLVED)

1. **Which non-admin claim should authorize new Arma 3 cloud-planning server creation?**
   - What we know: existing `GetGameServerControllers` uses `Claims.Creation.Server`, but `CreateGameServerAsync` is admin-only. [VERIFIED: codebase grep]
   - Decision: use `Claims.Creation.Server` as the Phase 33 non-admin gate for starting the side-effect-light Arma 3 cloud-planning server path, with server-side tests proving users without that claim cannot create planning servers. [RESOLVED: agent discretion, narrow reuse of existing creation claim]
   - Rationale: this matches the existing controller-discovery creation gate without broadening to the admin-only `Claims.Administrative.Server` policy or inventing a new claim mid-phase. [RESOLVED]

2. **Should "create a new Arma 3 game server" enqueue installation during Phase 33?**
   - What we know: current server creation creates `GameServer`, assigns billing owner, and enqueues an `Install` lifecycle job. [VERIFIED: codebase grep]
   - Decision: Phase 33 new-server creation must avoid queuing install lifecycle jobs and must not start provider, Steam, Arma, worker, or billable operations. It may persist a local Arma 3 planning server only when ownership is assigned through `IBillingOwnershipService` and audit behavior is preserved. [RESOLVED: agent discretion, phase boundary]
   - Rationale: the phase goal requires complete planning inputs before billable action; the existing install enqueue side effect would move local lifecycle execution too early for the guided entry flow. [RESOLVED]

3. **How should owner-visible billing accounts be scoped?**
   - What we know: billing account list/create routes are admin-only, and `BillingAccount` has `OwnerKind`, `OwnerUserFk`, `CreatedByFk`, and `IsActive`. [VERIFIED: codebase grep]
   - Decision: Phase 33 should expose only active billing accounts and assignments that the current authenticated user is allowed to use for guided cloud entry. The initial owner-visible projection should include the user's own active user-owned billing accounts and any already-active assignment for the selected server; it must not expose unrelated accounts, admin fleet billing data, payment details, or customer invoicing concepts. [RESOLVED: agent discretion, least privilege]
   - Rationale: this satisfies CREATE-03 while preventing account enumeration and preserving the `/servers/cloud` admin boundary for broader billing/provider management. [RESOLVED]

4. **Should Phase 33 include server-side readiness validation separate from dry-run planning?**
   - What we know: current dry-run planning validates and persists local plan records. [VERIFIED: codebase grep]
   - Decision: Phase 33 should include a server-side non-mutating readiness validation endpoint before the UI enables continuation to dry-run planning. The endpoint validates server eligibility, active owner assignment, planning inputs, and safe cached provider option references without creating provider resources, cloud dry-run plans, sessions, inventory records, or cost estimates. [RESOLVED: agent discretion, CREATE-03/CREATE-04 enforcement]
   - Rationale: client-only validation would not enforce authorization-sensitive owner and server boundaries, while calling the existing dry-run endpoint would persist plan records before the Phase 33 readiness boundary. [RESOLVED]

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| .NET CLI | Build/test and package version checks | Yes | `10.0.201` | Use installed SDK to build `net9.0` projects. [VERIFIED: dotnet --version] |
| xUnit test infrastructure | API/service verification | Yes | xUnit 2.9.3, Test SDK 17.13.0 | Existing `tests/Api.Tests` can host focused tests. [VERIFIED: dotnet list package] |
| EF Core SQLite test provider | Persistence-oriented tests without PostgreSQL | Yes | 9.0.4 | Existing API tests use SQLite in-memory patterns. [VERIFIED: dotnet list package] |
| Context7 CLI (`ctx7`) | Library documentation lookup | No | - | Official Microsoft/OWASP/Fluent docs were used instead. [VERIFIED: shell probe] |
| Live PostgreSQL/Aspire | Manual local app run | Not required for research | - | Automated tests can use existing SQLite test patterns; live UAT is deferred. [VERIFIED: CONTEXT.md] |
| Live provider/Steam/Arma/authenticated browser | Manual/live UAT | Not required | - | User explicitly deferred these UAT categories. [VERIFIED: CONTEXT.md] |

**Missing dependencies with no fallback:**

- None for planning research and automated test planning. [VERIFIED: shell probe]

**Missing dependencies with fallback:**

- Context7 CLI was unavailable; official docs and codebase inspection were used. [VERIFIED: shell probe]

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 with Microsoft.NET.Test.Sdk 17.13.0. [VERIFIED: dotnet list package] |
| Config file | Test project files under `tests/Api.Tests/Api.Tests.csproj` and `tests/X39.BI.Config.Parsing.Tests/X39.BI.Config.Parsing.Tests.csproj`; no central test config was found. [VERIFIED: codebase grep] |
| Quick run command | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudPlanning|FullyQualifiedName~Billing|FullyQualifiedName~GameServer" --no-restore --verbosity minimal` [VERIFIED: shell probe] |
| Full suite command | `dotnet test X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` [VERIFIED: shell probe] |

### Phase Requirements -> Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| CREATE-01 | `/servers` exposes guided entry for authorized server operators and does not require `/servers/cloud`. | Build/static + optional bUnit/WebApp test if a test project is added. | `dotnet build X39.UnitedTacticalForces.slnx --no-restore` | No WebApp component test project found; Wave 0 gap if component tests are desired. [VERIFIED: codebase grep] |
| CREATE-02 | Existing eligible Arma 3 servers are scoped by authorization; new Arma 3 planning server path uses the chosen authorization rule and avoids unintended side effects. | API unit/integration tests with SQLite. | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudPlanning|FullyQualifiedName~GameServer"` | Partial; cloud planning tests exist, guided-entry create/eligibility tests need additions. [VERIFIED: codebase grep] |
| CREATE-03 | Active billing owner and selected assignment are required before dry-run/provision continuation. | API integration tests. | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Arma3Cloud"` | Partial; owner mismatch and missing active owner tests exist for dry-run, but owner-scoped selection endpoints need tests if added. [VERIFIED: codebase grep] |
| CREATE-04 | Planning input validation covers size/location/storage/unique ports/CIDR/modpack/runtime basics. | API validation tests plus testable client validation helper if extracted. | `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudPlanning"` | Partial; dry-run validation tests exist, UI-visible validation helper tests do not. [VERIFIED: codebase grep] |

### Sampling Rate

- **Per task commit:** run the targeted API test command and `dotnet build X39.UnitedTacticalForces.slnx --no-restore`. [VERIFIED: shell probe]
- **Per wave merge:** run `dotnet test X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal`. [VERIFIED: shell probe]
- **Phase gate:** full suite green plus explicit deferred-evidence notes for manual/authenticated/live-provider UAT. [VERIFIED: CONTEXT.md]

### Wave 0 Gaps

- [ ] Add API tests for any new owner-scoped billing-owner/options/readiness endpoints. [ASSUMED]
- [ ] Add tests for the CREATE-02 decision around new server creation side effects, especially whether an install lifecycle job is or is not enqueued. [ASSUMED]
- [ ] Extract complex UI validation into a testable C# helper/model if validation grows beyond straightforward inline checks. [ASSUMED]
- [ ] Do not block on authenticated browser, live provider, Steam, Arma, or Phase 30 UAT. [VERIFIED: CONTEXT.md]

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | yes | Existing ASP.NET Core authentication and `[Authorize]` gates protect API routes. [VERIFIED: codebase grep] |
| V3 Session Management | yes | Existing cookie-authenticated WebApp/API calls continue; no new session mechanism is recommended. [VERIFIED: codebase grep] |
| V4 Access Control | yes | Server-side resource/owner/provider scope checks are mandatory for server selection, owner choices, and planning readiness. [CITED: https://owasp.org/www-project-application-security-verification-standard/] |
| V5 Input Validation | yes | Validate IDs, controller type, title length, active owner/assignment, storage, ports, CIDR, metadata names, modpack, and runtime basics on server and visible client paths. [VERIFIED: 33-UI-SPEC.md] |
| V6 Cryptography | no new crypto | Do not add cryptographic code; keep provider/Steam/worker secrets server-side and redacted. [VERIFIED: AGENTS.md] |

OWASP ASVS is intended as a basis for testing web application technical security controls and secure development requirements. [CITED: https://owasp.org/www-project-application-security-verification-standard/]

### Known Threat Patterns for This Stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| IDOR on `gameServerId` or billing account ids | Elevation of privilege / Information disclosure | Enforce route-resource authorization and owner-scope server-side before returning or accepting ids. [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based] |
| Billing owner enumeration | Information disclosure | Return only active owner accounts the current user may use; never load all accounts and filter only in Blazor. [ASSUMED] |
| Client-side-only gating | Elevation of privilege | Treat `MeService` checks as display logic only and enforce API policies. [VERIFIED: codebase grep] |
| Secret disclosure through owner planning UI | Information disclosure | Expose only secret-safe metadata/projections; never display provider token references, Steam credentials, worker credentials, or headers. [VERIFIED: AGENTS.md] |
| Invalid port/CIDR/storage input | Tampering / Denial of service | Validate port range/uniqueness, positive storage, CIDR requirement, and nullable RCON consistently in UI and API. [VERIFIED: 33-UI-SPEC.md] |
| Premature provider or lifecycle side effects | Tampering / Repudiation | Keep provider mutation out of Phase 33 and audit any persisted owner/server changes. [VERIFIED: CONTEXT.md] |

## Sources

### Primary (HIGH confidence)

- `AGENTS.md` - project constraints, stack, conventions, architecture. [VERIFIED: file read]
- `.planning/STATE.md` - active project decisions and deferred UAT context. [VERIFIED: file read]
- `.planning/ROADMAP.md` - Phase 33 roadmap entry and success criteria. [VERIFIED: file read]
- `.planning/REQUIREMENTS.md` - CREATE-01 through CREATE-04 and out-of-scope items. [VERIFIED: file read]
- `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-CONTEXT.md` - phase boundary and deferred UAT instruction. [VERIFIED: file read]
- `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-UI-SPEC.md` - Blazor/Fluent UI contract and validation requirements. [VERIFIED: file read]
- `sources/Api/Data/ApiDbContext.cs` and `sources/Api/Data/Hosting/**` - persisted hosting, billing, cloud, worker, job state. [VERIFIED: codebase grep]
- `sources/Api/Controllers/GameServersController.cs`, `BillingAccountsController.cs`, `CloudPlanningController.cs`, `HetznerProviderController.cs` - API routes and authorization gates. [VERIFIED: codebase grep]
- `sources/Api/Services/BillingOwnershipService.cs` and `sources/Api/Services/CloudPlanning/Arma3CloudDryRunPlannerService.cs` - owner enforcement and dry-run behavior. [VERIFIED: codebase grep]
- `sources/WebApp/Pages/Servers/Index.razor`, `Index.razor.css`, `Cloud.razor`, `Shared/MainLayout.razor`, `Services/MeService.cs` - current Blazor server/admin surfaces and claim display helpers. [VERIFIED: codebase grep]
- `sources/WebApp/Services/GameServerRepository/**`, `sources/WebApp/UtfApi/**` - repository facade and generated client routes/models. [VERIFIED: codebase grep]
- `tests/Api.Tests/**` - existing cloud planning, billing, inventory, policy test patterns. [VERIFIED: codebase grep]
- `dotnet list X39.UnitedTacticalForces.slnx package --include-transitive` - package versions. [VERIFIED: dotnet list package]
- `dotnet test X39.UnitedTacticalForces.slnx --list-tests --verbosity minimal` - test infrastructure availability. [VERIFIED: shell probe]

### Primary Documentation (HIGH confidence)

- Microsoft Learn, Resource-based authorization in ASP.NET Core - checked resource authorization timing and imperative authorization guidance. [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based]
- Microsoft Learn, Blazor forms validation - checked `DataAnnotationsValidator`, `EditContext`, and `ValidationMessageStore` validation patterns. [CITED: https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation]
- Microsoft Learn, Policy-based authorization in ASP.NET Core - checked policy/requirements context. [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies]
- Microsoft Learn, Fluent UI Blazor integration - checked Fluent UI Blazor package/component role. [CITED: https://learn.microsoft.com/en-us/fluent-ui/web-components/integrations/blazor]
- Fluent UI Blazor official site - checked project/component availability. [CITED: https://www.fluentui-blazor.net/]
- OWASP ASVS project page - checked ASVS purpose for technical control/security requirements. [CITED: https://owasp.org/www-project-application-security-verification-standard/]

### Secondary (MEDIUM confidence)

- None used for decisions. [VERIFIED: researcher notes]

### Tertiary (LOW confidence)

- None used for recommendations. [VERIFIED: researcher notes]

## Metadata

**Confidence breakdown:**

- Standard stack: HIGH - versions and active dependencies were verified from project files and `dotnet list package`. [VERIFIED: dotnet list package]
- Architecture: HIGH - current `/servers`, `/servers/cloud`, API controllers, EF entities, services, generated clients, and tests were inspected directly. [VERIFIED: codebase grep]
- Pitfalls: HIGH for current-code side effects and admin-only endpoints; MEDIUM for recommended new owner-scoped projections because exact product authorization rules remain undecided. [VERIFIED: codebase grep]
- Security: HIGH for access-control and secret-boundary concerns; MEDIUM for exact owner-scope policy until the user/product confirms account eligibility rules. [VERIFIED: codebase grep]

**Research date:** 2026-06-19
**Valid until:** 2026-07-19 for local codebase findings, or until Phase 32/33 code changes alter the server, billing, or cloud planning surfaces. [ASSUMED]
