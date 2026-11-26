# Phase 36: Policy Gate And Approval UX - Research

**Researched:** 2026-06-20
**Domain:** ASP.NET Core policy-gated workflow, durable approval state, Blazor WebAssembly/Fluent UI UX
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

All copied constraints in this section are [VERIFIED: .planning/phases/36-policy-gate-and-approval-ux/36-CONTEXT.md].

### Locked Decisions

## Implementation Decisions

### the agent's Discretion
All implementation choices are at the agent's discretion because discuss phase was skipped per user setting. Use ROADMAP
phase goal, success criteria, existing policy gate foundations, prior cloud workflow conventions, and codebase patterns to
guide decisions.

### Verification Boundary
- All UAT is deferred per user instruction. Do not require or claim manual authenticated browser testing, live provider
  mutation, Steam, Arma, Hetzner, provisioning, teardown, or Phase 30 UAT as passed for this phase.
- Automated verification should focus on durable gate state behavior, authorization-sensitive action boundaries,
  approval/denial transitions, expiration/mismatch blockers, repository/client contracts, and UI boundary rendering.

### the agent's Discretion

All implementation choices are at the agent's discretion because discuss phase was skipped per user setting. Use ROADMAP
phase goal, success criteria, existing policy gate foundations, prior cloud workflow conventions, and codebase patterns to
guide decisions.

### Deferred Ideas (OUT OF SCOPE)

Manual/authenticated/browser/live-provider/Steam/Arma/Hetzner/provisioning/teardown/Phase 30 UAT is deferred.
</user_constraints>

## Summary

Phase 36 should extend the existing `/servers` owner workflow by replacing the disabled `Review policy decision` placeholder with a repository-backed policy gate panel and approver controls that consume existing backend policy/approval foundations. [VERIFIED: sources/WebApp/Pages/Servers/Index.razor] The backend already evaluates gates before provisioning, provider teardown, and billable cloud lifecycle enqueue; blocked gates return `409 Conflict` and do not mutate provider resources or enqueue jobs. [VERIFIED: sources/Api/Controllers/CloudInventoryController.cs] [VERIFIED: sources/Api/Controllers/GameServersController.cs] [VERIFIED: tests/Api.Tests/CloudPolicies/CloudSpendingPolicyGateEndpointTests.cs]

The main planning risk is WebApp client completeness: generated Kiota coverage exists for game-server-scoped policy visibility but not for `cloud-spending-policies/approvals/{id}/approve` and `/deny`. [VERIFIED: sources/WebApp/UtfApi/CloudSpendingPolicies] [VERIFIED: sources/WebApp/UtfApi/GameServers/Item/CloudPolicyVisibility/CloudPolicyVisibilityRequestBuilder.cs] The plan should therefore include a generated-client refresh or a tightly scoped generated-request pattern before adding approver UX. [CITED: https://learn.microsoft.com/en-us/openapi/kiota/errors]

**Primary recommendation:** Use existing policy gate/approval services and safe repository models, add missing approval command client/facade support, then render one explicit `/servers` policy gate panel plus a scoped pending-approval detail/queue; keep all continuation buttons disabled unless decision id, approval request id, action kind, scope ids, owner ids, dry-run/session id, and expiration still match. [VERIFIED: 36-UI-SPEC.md] [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs]

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| Gate evaluation for provisioning/teardown/lifecycle | API / Backend | Database / Storage | Backend owns billable-action authorization and must block mutation before provider or lifecycle work starts. [VERIFIED: sources/Api/Services/CloudPolicies/CloudSpendingPolicyGateService.cs] |
| Durable decision and approval records | Database / Storage | API / Backend | `CloudSpendingPolicyDecision` and `CloudSpendingPolicyApprovalRequest` persist scope, actor, costs, status, expiration, and sanitized reasons. [VERIFIED: sources/Api/Data/Hosting/CloudSpendingPolicyDecision.cs] [VERIFIED: sources/Api/Data/Hosting/CloudSpendingPolicyApprovalRequest.cs] |
| Warning acknowledgement and approved continuation | API / Backend | WebApp repository | Backend validates matching decision/request scope; WebApp passes only the displayed continuation id. [VERIFIED: sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs] [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowKiotaSender.cs] |
| Approve/deny approval requests | API / Backend | WebApp repository, Browser / Client | Existing endpoints are admin-authorized and resolve durable requests; UI should expose controls only through repository-safe commands. [VERIFIED: sources/Api/Controllers/CloudSpendingPoliciesController.cs] |
| User-facing policy gate state and next action | Browser / Client | WebApp repository | Razor renders safe models and explicit next steps; it must not bind generated DTOs or raw policy JSON. [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs] |
| Redaction and forbidden-field blocking | WebApp repository | Browser / Client | Repository projection uses `CloudWorkflowRedactionGuard`; UI renders redaction-blocked copy instead of partial sensitive data. [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs] |

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| POLICY-01 | Authorized user can see allow, warning, approval-required, and denied policy outcomes as actionable workflow states. [VERIFIED: .planning/REQUIREMENTS.md] | Existing `CloudWorkflowResultState` already includes `PolicyAllow`, `PolicyWarning`, `ApprovalRequired`, and `Denied`; Phase 36 should render these as the primary selected policy panel rather than generic cloud-result text. [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowModels.cs] |
| POLICY-02 | Authorized user can acknowledge warning decisions or follow approval next steps without bypassing policy scope, expiration, or decision identity. [VERIFIED: .planning/REQUIREMENTS.md] | `IsWarningAcknowledgedAsync` and `ValidateApprovedAsync` compare decision/request scope and expiration server-side; WebApp should pass only the selected `acknowledgedDecisionId` or `approvalRequestId`. [VERIFIED: sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs] |
| POLICY-03 | Authorized approver can approve or deny provisioning and teardown requests from the cloud server workflow with durable audit context. [VERIFIED: .planning/REQUIREMENTS.md] | Approval service persists pending, approved, denied, and expired states with resolver, timestamps, sanitized reason, and audit JSON; generated WebApp client support for approve/deny appears missing and should be planned first. [VERIFIED: sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs] [VERIFIED: sources/WebApp/UtfApi/CloudSpendingPolicies] |
</phase_requirements>

## Project Constraints (from AGENTS.md)

- Keep the existing game-controller abstraction during the first milestone. [VERIFIED: AGENTS.md]
- Lifecycle, job, worker, ownership, and audit state must survive API restarts. [VERIFIED: AGENTS.md]
- Every cost-generating or hosting session must have an owner from creation until cleanup or transfer completes. [VERIFIED: AGENTS.md]
- Do not implement Hetzner provisioning in the first milestone scope and do not expose Steam/provider/worker credentials to frontend clients or unredacted logs. [VERIFIED: AGENTS.md]
- Existing Aspire, PostgreSQL, API, and Blazor WebApp local development must keep working. [VERIFIED: AGENTS.md]
- Use C# file-scoped namespaces, one public type per `.cs` file, existing feature folders, repository facades over generated Kiota clients, and generated-code boundaries for `sources/WebApp/UtfApi/**` and EF migrations. [VERIFIED: AGENTS.md]
- Use structured `ILogger<T>` logs and do not log secret values or raw configuration. [VERIFIED: AGENTS.md]
- Add focused automated tests around persistence, job transitions, authorization-sensitive actions, worker recovery, policy gates, and redaction. [VERIFIED: AGENTS.md] [VERIFIED: .planning/config.json]

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET target framework | `net9.0` | API, WebApp, tests, shared projects | Existing solution targets .NET 9 projects. [VERIFIED: sources/Api/Api.csproj] [VERIFIED: sources/WebApp/WebApp.csproj] |
| ASP.NET Core MVC/API | 9.0.4 packages | Controllers, `[Authorize]`, OpenAPI, cookie-backed API endpoints | Existing policy and inventory endpoints are ASP.NET Core controllers; Microsoft documents policy requirements/handlers and `[Authorize]` policies as the standard ASP.NET authorization model. [VERIFIED: sources/Api/Api.csproj] [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-9.0] |
| Entity Framework Core + Npgsql | 9.0.4 | Persistent decisions, approvals, dry-run, owner, and audit state | Existing persistence uses EF Core/Npgsql; Microsoft documents `SaveChanges` transactional behavior and explicit transactions for multi-step atomicity. [VERIFIED: sources/Api/Api.csproj] [CITED: https://learn.microsoft.com/en-us/ef/core/saving/transactions] |
| Blazor WebAssembly | 9.0.4 | `/servers` owner and approver workflow UI | Existing WebApp is Blazor WASM and uses Razor components. [VERIFIED: sources/WebApp/WebApp.csproj] |
| Microsoft Fluent UI Blazor | 4.11.8 | Buttons, message bars, dialogs, data grids, icons, providers | Existing UI registers Fluent UI and has `FluentDialogProvider`, `FluentTooltipProvider`, and `FluentMessageBarProvider`; Fluent UI Blazor docs describe component providers and `AddFluentUIComponents`. [VERIFIED: sources/WebApp/WebApp.csproj] [VERIFIED: sources/WebApp/Shared/MainLayout.razor] [CITED: https://github.com/microsoft/fluentui-blazor] |
| Microsoft Kiota Bundle | 1.22.2 | Generated request builders and parsable models | Existing WebApp repository facade uses generated request builders behind `IGameServerRepository`; Kiota docs define error mappings and runtime exception behavior. [VERIFIED: sources/WebApp/WebApp.csproj] [CITED: https://learn.microsoft.com/en-us/openapi/kiota/errors] |
| xUnit + EF Core Sqlite | xUnit 2.9.3, Sqlite 9.0.4 | Fast API/service/repository boundary tests | Existing `Api.Tests` uses xUnit and in-memory Sqlite patterns for policy, approval, and endpoint blockers. [VERIFIED: tests/Api.Tests/Api.Tests.csproj] [VERIFIED: tests/Api.Tests/CloudPolicies/CloudSpendingPolicyApprovalTests.cs] |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.AspNetCore.OpenApi` | 9.0.4 | OpenAPI surface used by generated client workflows | Use if approve/deny routes need regenerated Kiota builders from the current API description. [VERIFIED: sources/Api/Api.csproj] |
| `Microsoft.FluentUI.AspNetCore.Components.Icons` | 4.11.8 | `Icons.Regular.Size20/Size24` for action buttons | Use icons specified by the UI contract for review, acknowledge, refresh, approve, deny, and continuation actions. [VERIFIED: sources/WebApp/WebApp.csproj] [VERIFIED: 36-UI-SPEC.md] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Repository-safe models in `GameServerRepository` | Bind generated `UtfApi` DTOs directly in Razor | Rejected because project conventions and tests forbid generated DTO/raw HTTP binding in `/servers`. [VERIFIED: AGENTS.md] [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs] |
| Generated Kiota route builders | Raw `HttpClient` calls for approval commands | Rejected because v1.8 decisions require generated clients behind repository facades. [VERIFIED: .planning/STATE.md] |
| Existing admin approval endpoints | New global policy administration redesign | Rejected because Phase 36 consumes foundations and policy administration is out of scope. [VERIFIED: 36-CONTEXT.md] |

**Installation:** No new external packages are recommended for Phase 36. [VERIFIED: sources/WebApp/WebApp.csproj] [VERIFIED: sources/Api/Api.csproj]

## Package Legitimacy Audit

No external package install is recommended for this phase, so slopcheck and registry checks are not applicable. [VERIFIED: phase research scope] Existing package versions are read from project files rather than newly selected. [VERIFIED: sources/WebApp/WebApp.csproj] [VERIFIED: sources/Api/Api.csproj]

## Architecture Patterns

### System Architecture Diagram

```text
/servers selected server + fresh dry-run review
        |
        v
WebApp policy panel calls IGameServerRepository
        |
        v
Generated Kiota request builder or repository-local policy transport
        |
        v
API gate endpoint evaluates decision before mutation
        |
        +--> allow: return allowed/success state; later action may continue if scope still matches
        |
        +--> warn: persist decision -> return warning gate -> user sends displayed acknowledgedDecisionId
        |
        +--> approval required: persist/reuse pending request -> approver approve/deny command
        |         |
        |         v
        |   approval service stores resolver, reason, expiration, audit details
        |
        +--> denied/expired/mismatch/missing: return blocked state; no provider/lifecycle mutation
```

### Recommended Project Structure

```text
sources/
  Api/
    Controllers/CloudSpendingPoliciesController.cs       # existing approval command endpoints
    Services/CloudPolicies/                              # existing gate/evaluation/approval services
    DTO/Payloads/CloudSpendingPolicyApprovalDecisionPayload.cs
  WebApp/
    Services/GameServerRepository/
      IGameServerRepository.cs                           # add repository facade methods for review/approve/deny
      GameServerRepositoryImpl.cs                        # call generated builders or Kiota sender
      CloudWorkflow/                                     # add safe approval/gate projection models if needed
    Pages/Servers/Index.razor                            # render selected gate + approver actions
    Pages/Servers/Index.razor.css                        # reuse policy panel/detail/action/table styles
tests/
  Api.Tests/
    CloudPolicies/                                       # service/controller tests for scope, expiration, no mutation
    WebApp/                                              # source-boundary tests for Razor/repository safety
```

### Pattern 1: Gate Before Mutation

**What:** Evaluate policy in the API endpoint before provisioning, teardown, or enqueueing a billable lifecycle job; return conflict if the gate cannot proceed. [VERIFIED: sources/Api/Controllers/CloudInventoryController.cs] [VERIFIED: sources/Api/Controllers/GameServersController.cs]

**When to use:** Every Phase 36 continuation path that could lead to provider provisioning, provider teardown, or cloud lifecycle work. [VERIFIED: .planning/ROADMAP.md]

**Example:**

```csharp
// Source: sources/Api/Controllers/CloudInventoryController.cs
var gate = await cloudSpendingPolicyGateService
    .EvaluateProvisioningAsync(planId, user, cancellationToken, acknowledgedDecisionId, approvalRequestId)
    .ConfigureAwait(false);
if (!gate.CanProceed)
    return Conflict(gate.ToDto());
```

### Pattern 2: Repository-Safe Policy Projection

**What:** Parse policy conflicts and visibility through repository-layer safe models; render `CloudWorkflowModels` values in Razor. [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowPolicyGateTransport.cs] [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs]

**When to use:** Any `/servers` UI state that displays decision, approval, audit, cost, or continuation identity. [VERIFIED: 36-UI-SPEC.md]

**Example:**

```csharp
// Source: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowKiotaSender.cs
catch (CloudWorkflowPolicyGateTransport ex) when (ex.ResponseStatusCode == 409 || ex.Decision.PrimaryKey != 0)
{
    return CloudWorkflowResultMapper.FromPolicyGate(ex, actionName);
}
```

### Pattern 3: Durable Approver Decision

**What:** Approve and deny only pending requests, persist resolver, sanitized reason, resolution timestamp, expiration for approvals, and audit details. [VERIFIED: sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs]

**When to use:** The approver section in `/servers` and any repository facade method that resolves approval requests. [VERIFIED: 36-UI-SPEC.md]

**Example:**

```csharp
// Source: sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs
request.Status = ECloudSpendingPolicyApprovalStatus.Approved;
request.ResolvedByFk = approver.PrimaryKey;
request.ApprovalReason = SanitizeReason(payload.Reason);
request.TimeStampResolved = now;
request.TimeStampExpires = expires;
```

### Anti-Patterns to Avoid

- **Rendering generated DTOs or raw JSON in Razor:** `CloudSpendingPolicyDecisionDto` includes `InputSnapshotJson` and `RiskDetailsJson`; these must not be user-visible. [VERIFIED: sources/Api/DTO/CloudSpendingPolicyDecisionDto.cs] [VERIFIED: 36-UI-SPEC.md]
- **Treating denied/expired as failures:** The UI contract defines denied and expired as explicit workflow states with next action copy. [VERIFIED: 36-UI-SPEC.md]
- **Approving without server-side scope validation:** Backend already validates decision/request scope and expiration; UI should not duplicate this as the authority or bypass it with stale ids. [VERIFIED: sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs]
- **Using `/servers/cloud` as the normal owner next step:** Owner workflow must remain in `/servers`; `/servers/cloud` remains admin/fleet scope. [VERIFIED: 36-UI-SPEC.md] [VERIFIED: .planning/ROADMAP.md]

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Authorization gates | Client-side role checks as authority | ASP.NET Core `[Authorize]` plus existing claim/resource policies | Microsoft documents policies/requirements/handlers as the standard model; existing controllers already use these policies. [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-9.0] [VERIFIED: sources/Api/Controllers/CloudSpendingPoliciesController.cs] |
| Transaction semantics | Manual partial persistence across entities without need | EF Core `SaveChanges` transaction; explicit transaction only for multi-step gate evaluation | EF Core wraps a single `SaveChanges` in a transaction by default; existing evaluation service uses explicit transaction for decision idempotency. [CITED: https://learn.microsoft.com/en-us/ef/core/saving/transactions] [VERIFIED: sources/Api/Services/CloudPolicies/CloudSpendingPolicyEvaluationService.cs] |
| Kiota policy error parsing | Raw HTTP response parsing in Razor | `CloudWorkflowKiotaSender` and generated/request-builder facade | Kiota supports error mappings; project tests forbid raw HTTP/generated DTO use in `/servers`. [CITED: https://learn.microsoft.com/en-us/openapi/kiota/errors] [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs] |
| Modal confirmation and form controls | Custom JS dialogs | Fluent UI dialog/provider and Blazor event/form patterns | Existing layout registers Fluent providers and Microsoft documents Blazor validation/components. [VERIFIED: sources/WebApp/Shared/MainLayout.razor] [CITED: https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation?view=aspnetcore-9.0] |
| Redaction scanning | Ad hoc substring hiding in Razor | Existing `CloudWorkflowRedactionGuard` plus boundary tests | Repository guard already fails closed on forbidden display text. [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs] |

**Key insight:** Phase 36 is orchestration and UX over existing durable policy primitives; the plan should add missing client/facade/UI slices, not invent a new policy engine. [VERIFIED: 36-CONTEXT.md] [VERIFIED: sources/Api/Services/CloudPolicies]

## Common Pitfalls

### Pitfall 1: Approval Client Route Gap

**What goes wrong:** The UI plan assumes generated approve/deny route builders exist, but only visibility builders are present under `CloudSpendingPolicies`. [VERIFIED: sources/WebApp/UtfApi/CloudSpendingPolicies]

**Why it happens:** Prior phases generated only routes they needed; approve/deny endpoints are backend-ready but not WebApp-facade-ready. [VERIFIED: sources/Api/Controllers/CloudSpendingPoliciesController.cs] [VERIFIED: sources/WebApp/UtfApi/CloudSpendingPolicies]

**How to avoid:** Plan an early task to refresh generated Kiota artifacts or add narrowly generated request builders, then add repository methods for `ApprovePolicyRequestAsync` and `DenyPolicyRequestAsync`. [CITED: https://learn.microsoft.com/en-us/openapi/kiota/errors]

**Warning signs:** `Client.CloudSpendingPolicies.Approvals[...]` cannot compile, or Razor starts using raw `HttpClient`. [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs]

### Pitfall 2: Stale Continuation Identity

**What goes wrong:** A warning acknowledgement or approval request id from an old decision is reused after server, owner, plan, session, or action changes. [VERIFIED: 36-UI-SPEC.md]

**Why it happens:** Phase 35 keeps selected dry-run review state and Phase 34 stores pending workflow action context; Phase 36 must clear or invalidate continuation context on scope changes. [VERIFIED: .planning/phases/35-dry-run-planning-and-review/35-06-SUMMARY.md] [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs]

**How to avoid:** Store a selected policy gate scope snapshot in the WebApp state and require exact match before enabling `Acknowledge warning` or `Continue approved action`. [VERIFIED: 36-UI-SPEC.md]

**Warning signs:** Button remains enabled after changing selected server, owner, or regenerated dry-run plan. [VERIFIED: 36-UI-SPEC.md]

### Pitfall 3: UI Suggests Provider Mutation in Phase 36

**What goes wrong:** Copy or buttons imply live provisioning/teardown is available or verified in Phase 36. [VERIFIED: 36-CONTEXT.md]

**Why it happens:** The same repository actions can call provisioning/teardown endpoints in later phases; Phase 36 must present policy resolution as the current deliverable. [VERIFIED: sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs]

**How to avoid:** Keep provider action continuation explicit and scoped, with deferred-UAT copy and no live evidence claims. [VERIFIED: 36-UI-SPEC.md]

**Warning signs:** Copy says `Provision now`, `launch`, `deploy`, `payment`, `invoice`, or claims live Hetzner/Steam/Arma/browser UAT. [VERIFIED: 36-UI-SPEC.md]

### Pitfall 4: Secret-Bearing Policy Details Leak Through Visibility DTOs

**What goes wrong:** Raw policy input or risk details JSON reaches the UI. [VERIFIED: sources/Api/DTO/CloudSpendingPolicyDecisionDto.cs]

**Why it happens:** API DTOs include bounded internal JSON fields for audit/debug use; UI must use allowlisted projection models instead. [VERIFIED: sources/Api/DTO/CloudSpendingPolicyDecisionDto.cs] [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs]

**How to avoid:** Extend boundary tests with Phase 36 source markers and forbid raw DTO, `AdditionalData`, raw JSON, request/response body, and secret terms in new Razor sections. [VERIFIED: 36-UI-SPEC.md]

**Warning signs:** New Razor markup references `InputSnapshotJson`, `RiskDetailsJson`, generated `Api.Models`, or `AdditionalData`. [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs]

## Code Examples

### Policy Gate State Mapping

```csharp
// Source: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowResultMapper.cs
private static CloudWorkflowModels.CloudWorkflowResultState ToPolicyState(CloudWorkflowPolicyGateTransport transport)
{
    if (transport.IsDenied)
        return CloudWorkflowModels.CloudWorkflowResultState.Denied;
    if (transport.RequiresApproval)
        return CloudWorkflowModels.CloudWorkflowResultState.ApprovalRequired;
    if (transport.RequiresAcknowledgement)
        return CloudWorkflowModels.CloudWorkflowResultState.PolicyWarning;
    return transport.CanProceed
        ? CloudWorkflowModels.CloudWorkflowResultState.PolicyAllow
        : CloudWorkflowModels.CloudWorkflowResultState.Failure;
}
```

### Approval Scope Validation

```csharp
// Source: sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs
if (request.Status != ECloudSpendingPolicyApprovalStatus.Approved
    || request.TimeStampExpires <= now
    || !MatchesDecisionScope(request, decision)
    || request.DryRunPlanFk != dryRunPlanFk
    || request.ResourceInventoryRecordFk != resourceInventoryRecordFk)
{
    return null;
}
```

### UI Pattern For Safe Metadata Rendering

```razor
@* Source: sources/WebApp/Pages/Servers/Index.razor *@
<dl class="meta-grid cloud-result-meta">
    @foreach (var row in GetCloudWorkflowResultRows(_selectedCloudWorkflowResult))
    {
        <dt>@row.Label</dt>
        <dd>@row.Value</dd>
    }
</dl>
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| API-owned local process state | Durable control-plane records and worker/lifecycle abstractions | v1.0-v1.8 milestone direction. [VERIFIED: AGENTS.md] | Phase 36 must use persisted decision/approval state, not transient UI-only flags. [VERIFIED: sources/Api/Data/Hosting/CloudSpendingPolicyApprovalRequest.cs] |
| Raw browser HTTP bridges | Generated Kiota clients behind WebApp repository facades | Phase 34. [VERIFIED: .planning/STATE.md] | Approve/deny methods must go through repository facade and generated/request-builder boundary. [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs] |
| Disabled policy placeholder after dry-run review | Actionable gate states before billable continuation | Phase 36 goal. [VERIFIED: sources/WebApp/Pages/Servers/Index.razor] [VERIFIED: .planning/ROADMAP.md] | Planner should replace placeholder with real gate evaluation, acknowledgement, approval, denied, expired, and mismatch UI states. [VERIFIED: 36-UI-SPEC.md] |

**Deprecated/outdated:**
- Treating runtime stop/runtime cleanup as provider cost stop is out of scope and explicitly wrong for v1.8. [VERIFIED: .planning/REQUIREMENTS.md]
- Editing generated `sources/WebApp/UtfApi/**` by hand is forbidden by project conventions. [VERIFIED: AGENTS.md]

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | RESOLVED: Phase 36 uses the existing `Claims.Administrative.Server` approver authority; a narrower approval claim is out of scope unless introduced by a later product/security phase. | Open Questions (RESOLVED) | If a narrower approver role is later required, API authorization and tests belong in a follow-up phase before UI controls are re-scoped. [VERIFIED: sources/Api/Controllers/CloudSpendingPoliciesController.cs] |
| A2 | RESOLVED: Phase 36 plans repository-local Kiota `RequestInformation` sender handling for approve/deny commands when generated route builders are absent; do not hand-edit generated `sources/WebApp/UtfApi/**` files. | Open Questions (RESOLVED) | If a safe narrow Kiota generation command is recovered during execution, generated artifacts may be refreshed under the established generator protocol instead of using the repository-local sender. [VERIFIED: .planning/phases/36-policy-gate-and-approval-ux/36-03-PLAN.md] |

## Open Questions (RESOLVED)

1. **Should approver authority remain `Claims.Administrative.Server` for Phase 36?**
   - What we know: Existing approve/deny endpoints require `Claims.Administrative.Server`. [VERIFIED: sources/Api/Controllers/CloudSpendingPoliciesController.cs]
   - RESOLVED: Keep `Claims.Administrative.Server` for Phase 36 approver authority. The phase consumes existing approval foundations and does not introduce a new approval-claim model. [VERIFIED: .planning/phases/36-policy-gate-and-approval-ux/36-02-PLAN.md]
   - Follow-up boundary: A narrower approver policy can be introduced in a later product/security phase if required. [VERIFIED: .planning/ROADMAP.md]

2. **How should generated approve/deny client artifacts be produced?**
   - What we know: `CloudSpendingPolicies/Approvals` directories exist but no approve/deny request builders are present. [VERIFIED: sources/WebApp/UtfApi/CloudSpendingPolicies]
   - RESOLVED: Use a repository-local Kiota `RequestInformation` sender for approve/deny commands unless execution recovers a safe narrow generation command. Do not hand-edit generated `sources/WebApp/UtfApi/**` files. [VERIFIED: .planning/phases/36-policy-gate-and-approval-ux/36-03-PLAN.md]
   - Follow-up boundary: If generated route builders are refreshed, the executor must follow the established generator-recovery protocol and document the generated-artifact scope. [VERIFIED: AGENTS.md]

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| .NET SDK | Build/test and source generation workflows | Yes | 10.0.201 installed; projects target `net9.0`. [VERIFIED: dotnet --version] [VERIFIED: sources/Api/Api.csproj] | Use installed SDK if it supports `net9.0`; otherwise install .NET 9 SDK. |
| NuGet global packages | Existing restored packages | Yes | Global cache at `C:\Users\CodexSandboxOffline\.nuget\packages\`. [VERIFIED: dotnet nuget locals] | `dotnet restore` if cache misses. |
| Context7 CLI | Optional documentation lookup | No | `ctx7` command not found. [VERIFIED: Get-Command ctx7] | Official Microsoft docs and official GitHub docs were used. [CITED: https://learn.microsoft.com/en-us/openapi/kiota/errors] |
| Project skills directory | Project-specific skill rules | No | `.codex/skills` and `.agents/skills` absent. [VERIFIED: shell discovery] | Use AGENTS.md and GSD instructions. |
| Knowledge graph | Semantic project context | No | `.planning/graphs/graph.json` absent. [VERIFIED: shell discovery] | Use source grep and required planning docs. |

**Missing dependencies with no fallback:**
- None for research and planning. [VERIFIED: shell discovery]

**Missing dependencies with fallback:**
- Context7 CLI missing; fallback official docs were read through browser. [VERIFIED: Get-Command ctx7] [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-9.0]

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 with Microsoft.NET.Test.Sdk 17.13.0 and EF Core Sqlite 9.0.4. [VERIFIED: tests/Api.Tests/Api.Tests.csproj] |
| Config file | No standalone test config detected; tests live under `tests/Api.Tests` and parser tests under `tests/X39.BI.Config.Parsing.Tests`. [VERIFIED: shell discovery] |
| Quick run command | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudSpendingPolicyGate|FullyQualifiedName~CloudSpendingPolicyApproval|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal` |
| Full suite command | `dotnet test X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` |

### Phase Requirements -> Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| POLICY-01 | Render allow/warning/approval-required/denied as actionable `/servers` gate states. [VERIFIED: .planning/REQUIREMENTS.md] | WebApp source-boundary/unit scan | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~PolicyGateApprovalUxBoundaryTests" --verbosity minimal` | No - Wave 0 add test. |
| POLICY-02 | Acknowledge warnings and continue approvals only by matching decision/request identity and scope. [VERIFIED: .planning/REQUIREMENTS.md] | API service + repository boundary | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudSpendingPolicyApprovalTests|FullyQualifiedName~CloudWorkflowRepositoryBoundaryTests" --verbosity minimal` | Partial existing; add Phase 36 continuation UI assertions. |
| POLICY-03 | Approver approve/deny actions persist durable audit context and update UI state. [VERIFIED: .planning/REQUIREMENTS.md] | API controller/service + repository facade + source-boundary | `dotnet test tests\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CloudSpendingPolicyControllerTests|FullyQualifiedName~CloudSpendingPolicyApprovalTests|FullyQualifiedName~PolicyGateApprovalUxBoundaryTests" --verbosity minimal` | Partial existing; add repository/WebApp facade tests. |

### Sampling Rate

- **Per task commit:** Run the narrow test filter for touched area plus `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal` for Razor/repository changes. [VERIFIED: .planning/phases/35-dry-run-planning-and-review/35-06-SUMMARY.md]
- **Per wave merge:** Run policy, approval, endpoint blocker, and WebApp boundary tests. [VERIFIED: test list]
- **Phase gate:** Run `dotnet build X39.UnitedTacticalForces.slnx --no-restore --verbosity minimal` and the full `Api.Tests` policy/cloud filters; do not claim manual/browser/live provider UAT. [VERIFIED: 36-CONTEXT.md]

### Wave 0 Gaps

- [ ] `tests/Api.Tests/WebApp/PolicyGateApprovalUxBoundaryTests.cs` - source scans for `ServersPolicyGateApproval` marker, required labels, forbidden copy, no generated DTO/raw HTTP binding, and deferred UAT copy. [VERIFIED: 36-UI-SPEC.md]
- [ ] Repository facade tests for approve/deny route use and safe projection once generated/request-builder support exists. [VERIFIED: sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs]
- [ ] Controller/repository tests for approve/deny generated-client coverage, if generation changes artifacts. [VERIFIED: sources/Api/Controllers/CloudSpendingPoliciesController.cs]

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | Yes | Existing authenticated ASP.NET Core controller endpoints and current user resolution via claims/database user. [VERIFIED: sources/Api/Controllers/CloudSpendingPoliciesController.cs] |
| V3 Session Management | Yes | Existing auth/session cookie behavior remains unchanged; Phase 36 must not add client-stored authority or continuation trust. [VERIFIED: sources/WebApp/Program.cs] |
| V4 Access Control | Yes | `[Authorize]` policies and resource/admin claims guard read and approval endpoints; backend gate remains source of truth. [VERIFIED: sources/Api/Controllers/GameServersController.cs] [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-9.0] |
| V5 Input Validation | Yes | Use typed payloads, sanitized approval reasons, Blazor visible labels/validation, and server-side validation. [VERIFIED: sources/Api/DTO/Payloads/CloudSpendingPolicyApprovalDecisionPayload.cs] [CITED: https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation?view=aspnetcore-9.0] |
| V6 Cryptography | Limited | Do not add new cryptography; existing evaluation uses SHA-256 for idempotency/fingerprint hashes. [VERIFIED: sources/Api/Services/CloudPolicies/CloudSpendingPolicyEvaluationService.cs] |
| V7 Error Handling and Logging | Yes | Return sanitized failure states and do not log/show secrets or raw payloads. [VERIFIED: sources/WebApp/Services/GameServerRepository/CloudWorkflow/CloudWorkflowRedactionGuard.cs] |

### Known Threat Patterns for ASP.NET Core + Blazor Policy Gate UX

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Stale approval id replay | Elevation of Privilege | Server validates request status, expiration, decision scope, dry-run plan id, and resource id before allowing continuation. [VERIFIED: sources/Api/Services/CloudPolicies/CloudSpendingPolicyApprovalService.cs] |
| Client-side authorization bypass | Elevation of Privilege | Approve/deny endpoints stay `[Authorize(Claims.Administrative.Server)]`; UI hides controls but backend remains authority. [VERIFIED: sources/Api/Controllers/CloudSpendingPoliciesController.cs] |
| Provider mutation before gate success | Tampering | Endpoint gates run before provisioning/teardown/job enqueue and return conflict on blocked state. [VERIFIED: tests/Api.Tests/CloudPolicies/CloudSpendingPolicyGateEndpointTests.cs] |
| Secret/raw JSON exposure | Information Disclosure | Render allowlisted safe models only; block redaction failures and test forbidden field names. [VERIFIED: tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs] |
| Confusing denied/expired with technical failure | Repudiation / UX safety | Present denied/expired/mismatch/missing approval as explicit workflow states with next safe action. [VERIFIED: 36-UI-SPEC.md] |

## Sources

### Primary (HIGH confidence)

- `.planning/phases/36-policy-gate-and-approval-ux/36-CONTEXT.md` - phase boundary, deferred UAT, implementation constraints. [VERIFIED: file read]
- `.planning/phases/36-policy-gate-and-approval-ux/36-UI-SPEC.md` - UI design, copy, redaction, action, and accessibility contract. [VERIFIED: file read]
- `.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md`, `.planning/STATE.md` - requirement mapping, phase dependency, v1.8 decisions. [VERIFIED: file read]
- `sources/Api/Services/CloudPolicies/*`, `sources/Api/Controllers/CloudInventoryController.cs`, `sources/Api/Controllers/CloudSpendingPoliciesController.cs`, `sources/Api/Controllers/GameServersController.cs` - backend policy gate and approval foundations. [VERIFIED: codebase grep]
- `sources/WebApp/Services/GameServerRepository/CloudWorkflow/*`, `sources/WebApp/Pages/Servers/Index.razor`, `tests/Api.Tests/WebApp/CloudWorkflowRepositoryBoundaryTests.cs` - repository-safe result taxonomy and existing UI boundary tests. [VERIFIED: codebase grep]
- Microsoft Learn ASP.NET Core policy authorization - policy/requirement/handler guidance. [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-9.0]
- Microsoft Learn EF Core transactions - default `SaveChanges` transaction and explicit transaction guidance. [CITED: https://learn.microsoft.com/en-us/ef/core/saving/transactions]
- Microsoft Learn Kiota errors - error mapping and runtime behavior. [CITED: https://learn.microsoft.com/en-us/openapi/kiota/errors]
- Microsoft Learn Blazor forms validation - `EditForm`, validation, validator guidance. [CITED: https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation?view=aspnetcore-9.0]
- Microsoft Fluent UI Blazor GitHub README - package, providers, Blazor component usage. [CITED: https://github.com/microsoft/fluentui-blazor]

### Secondary (MEDIUM confidence)

- `.planning/phases/34-generated-client-and-repository-gate-foundation/34-RESEARCH.md` and verification artifacts - prior phase repository/policy gate decisions. [VERIFIED: file grep]
- `.planning/phases/35-dry-run-planning-and-review/35-06-SUMMARY.md` - existing `/servers` dry-run completion and test commands. [VERIFIED: file read]

### Tertiary (LOW confidence)

- None used. [VERIFIED: research log]

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - versions verified from project files; no new package recommendation. [VERIFIED: sources/*.csproj]
- Architecture: HIGH - backend services, controllers, repository models, and tests already implement the core gate behavior. [VERIFIED: codebase grep]
- Pitfalls: HIGH - risks are grounded in missing generated client artifacts, UI placeholder state, and existing boundary tests. [VERIFIED: sources/WebApp/UtfApi/CloudSpendingPolicies] [VERIFIED: sources/WebApp/Pages/Servers/Index.razor]

**Research date:** 2026-06-20
**Valid until:** 2026-07-20 for codebase-specific planning; re-check official docs and generated client artifacts if planning starts after that date. [ASSUMED]
