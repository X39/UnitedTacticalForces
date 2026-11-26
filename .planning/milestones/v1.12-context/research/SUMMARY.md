# Project Research Summary

**Project:** United Tactical Forces
**Domain:** WebApp-driven Arma 3 cloud server creation, operation, teardown, and cost responsibility
**Researched:** 2026-06-19
**Confidence:** HIGH for repo-derived direction; MEDIUM for external package/version guidance

## Executive Summary

v1.8 is a productization milestone for an existing cloud game-server control plane, not a new orchestration build. United Tactical Forces already has durable lifecycle jobs, Hetzner planning/provisioning/teardown, cloud worker runtime execution, policy gates, billing attribution, reconciliation, and admin visibility. The missing product surface is a coherent WebApp path where an authorized user creates or selects one Arma 3 server, assigns an active billing owner, reviews a non-mutating dry-run plan, clears policy gates, provisions, operates runtime jobs, tears down provider resources, and reviews active/historical cost responsibility.

The recommended approach is to keep the current .NET 9, ASP.NET Core API, Blazor WebAssembly, Fluent UI, Kiota, repository facade, EF Core/PostgreSQL, and Aspire stack. Build the experience in `/servers` as a server-oriented workflow and keep `/servers/cloud` as the administrator fleet/provider console. Add only the narrow missing integration points: generated-client refresh, repository methods for dry-run/provision/teardown/policy continuation, typed policy-gate action results, and server-scoped API wrappers if non-admin server owners may provision or tear down provider resources.

The key risks are cost and security regressions caused by making complex cloud operations look too simple. The roadmap must preserve billing owner attribution from creation through cleanup, treat provider teardown as the cost-stop boundary, render policy gates as first-class states instead of generic errors, avoid leaking provider/Steam/worker secrets, and clearly label all v1.8 verification as implementation evidence. Deferred Phase 30 live Hetzner/Steam/Arma UAT remains out of scope and must not be claimed as passed.

## Key Findings

### Recommended Stack

Use the existing stack and avoid new frontend, state-management, orchestration, provider SDK, or payment dependencies. v1.8 should be implemented with Blazor components over generated Kiota clients wrapped by WebApp repository facades. The main stack task is to regenerate and verify the generated client so policy-gate DTOs and continuation query parameters are available.

**Core technologies:**
- .NET 9 / ASP.NET Core API: existing control plane, authorization, EF Core persistence, OpenAPI, and cloud service endpoints.
- Blazor WebAssembly: existing WebApp shell, auth wiring, routed pages, and server-owner/admin surfaces.
- Microsoft Fluent UI Blazor 4.11.8: existing wizard, form, grid, message, dialog, and progress components; no second UI system needed.
- Microsoft Kiota Bundle 1.22.2: generated API client pattern already used by WebApp repositories; refresh before adding workflow code.
- EF Core 9 with PostgreSQL: durable source of truth for jobs, sessions, resources, owner intervals, policy decisions, audit, and cost records.
- Aspire AppHost: keep local PostgreSQL/API/WebApp development working.
- bUnit, if UI workflow is implemented: focused component tests for validation, gate states, redaction, and cost labels.

Critical version guidance: do not make Fluent UI 4.14.2 or Kiota 2.0.0 upgrades prerequisites for v1.8 unless regeneration or compilation proves they are needed.

### Expected Features

**Must have (table stakes):**
- Server-oriented cloud entry from `/servers` for creating or selecting an Arma 3 server.
- Mandatory active billing owner and assignment selection before dry-run or provisioning.
- Cloud planning form for size, location/datacenter, image, storage, ports, SSH/admin CIDR, modpack, and runtime inputs.
- Non-mutating dry-run generation and review with planned resources, validation failures, assumptions, metadata freshness, estimates, tax/currency notes, and cost-source labels.
- Policy gate visibility for allowed, warning/acknowledgement, approval-required, and denied states.
- Explicit provisioning confirmation tied to the reviewed valid/latest plan.
- Provisioning/bootstrap/runtime status with durable job progress, worker readiness, endpoint, retryable states, and sanitized failures.
- Runtime controls for install, update, start, stop, restart, collect logs, and runtime cleanup.
- Provider teardown as the explicit cost-stop action, separate from runtime stop/cleanup and local server deletion.
- Active and historical cost responsibility with estimated, reconciled, adjusted, owner, assignment, cleanup risk, reconciliation, and audit context.
- Secret-safe UI and error handling across review, mutation, policy, runtime, cost, and audit paths.

**Should have (differentiators):**
- Cost-first creation review that makes owner responsibility and cost-start semantics explicit.
- First-class plan assumptions, recommendations, metadata age, firewall rules, and validation explanations.
- Integrated policy next steps, including approval request visibility and retry with continuation IDs.
- Cleanup-risk-first teardown UX showing remaining billable resources and retry/orphan state.
- Evidence-ready preflight summary for a later operator-approved live UAT milestone.

**Defer / non-goals:**
- Payment processing, chargeback, customer invoicing, or checkout.
- Live OPS-02/OPS-03 / Phase 30 Hetzner/Steam/Arma UAT closure.
- Multi-provider creation beyond the existing Hetzner-backed path.
- Multi-session cloud workers.
- Generic cloud dashboard as the main user UX.
- Manual SSH/provider-console workflows.
- Ownerless quick launch.
- Treating runtime stop or runtime cleanup as cost stop.
- Raw browser HTTP bridges for cloud actions.
- Full policy configuration redesign.
- General redesign of all server admin pages.
- Headless clients, resize, snapshots, backups, and templates marketplace.

### Architecture Approach

Keep the API as the durable control plane and the WebApp as a repository-backed owner/admin UI. The architecture should compose existing server-scoped cloud status, cost, policy, lifecycle, and dry-run endpoints into one `/servers` workflow, while retaining `/servers/cloud` for admin-only provider status, metadata sync, global inventory, reconciliation, policy configuration, approvals, and cleanup triage.

**Major components:**
1. `/servers` selected-server workflow: create/select Arma 3 server, choose owner, plan, review, provision, operate, tear down, and inspect cost history.
2. `/servers/cloud` admin console: fleet/provider inventory, metadata, global sessions/resources, reconciliation, policy configuration, approvals, and admin cleanup.
3. `GameServersController` or new server-scoped cloud operations controller: owner-safe dry-run details, provisioning, teardown, and policy-gated operations.
4. `CloudPlanningController`: existing server-scoped Arma 3 dry-run creation; optionally host plan-details routes.
5. `CloudInventoryController`: keep global admin provisioning/teardown/details/reconciliation endpoints.
6. `ICloudSpendingPolicyGateService`: single source for provisioning, teardown, lifecycle, and future billable-action gates.
7. `IGameServerRepository` and `ICloudAdminRepository`: generated-client wrappers for dry-run, provision, teardown, lifecycle, policy continuation, approvals, and typed action results.
8. WebApp action result model: centralize success, policy gate, bad request/not found, and transport failure handling.

### Critical Pitfalls

1. **Using the admin cloud console as the owner workflow** - avoid global admin endpoints in owner paths; use server-scoped routes and repositories.
2. **Provisioning from stale or mismatched dry-run review** - show plan id, session id, owner, assignment, metadata timestamp, freshness, estimates, assumptions, and require regeneration when invalid or stale.
3. **Losing billing owner attribution** - make owner selection a first-class step and revalidate active ownership before plan and provision.
4. **Treating policy gates as generic errors** - model 409 gate responses as actionable states with explicit acknowledgement or approval continuation.
5. **Conflating runtime cleanup, local delete, and provider teardown** - keep labels, confirmations, endpoints, and post-action status distinct; only provider teardown stops billable resources.
6. **Hiding partial provisioning/teardown** - refresh and pin session/resources/cost/reconciliation/audit after mutation, including cleanup-needed and remaining billable counts.
7. **Leaking cross-owner data or secrets** - owner pages must use scoped projections and allowlisted display fields only.
8. **Claiming v1.8 checks as live Phase 30 UAT** - label fake/dry-run/browser tests as implementation evidence unless a later approved live UAT run occurs.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Guided Entry, Ownership, And Scope Boundaries

**Rationale:** The workflow cannot safely plan or provision until the route, selected server, Arma 3 constraint, active billing owner, and authorization scope are settled.
**Delivers:** `/servers` entry point, create/select Arma 3 server path, mandatory active billing owner/assignment, initial runtime/modpack inputs, and no admin aggregate calls in owner flow.
**Addresses:** Guided creation entry, permission-aware billing owner selector, select/create Arma 3 server.
**Avoids:** Admin-console misuse, owner attribution loss, cross-owner leakage, and billable infrastructure before runtime prerequisites are visible.

### Phase 2: Generated Client And Repository Gate Foundation

**Rationale:** The UI needs stable facades before workflow screens can handle dry-run, policy gates, provisioning, lifecycle, and teardown consistently.
**Delivers:** Generated Kiota refresh verification, `CreateArma3CloudDryRunPlanAsync`, gate-aware action result model, continuation IDs, 409 policy-gate mapping, and approval repository methods.
**Uses:** Kiota, repository facades, API OpenAPI metadata, `CloudSpendingPolicyGateResultDto`.
**Implements:** Shared WebApp action result and repository integration points.
**Avoids:** Raw HTTP bridges, generated-file hand edits, generic policy failures, and duplicate error handling.

### Phase 3: Dry-Run Planning And Review

**Rationale:** Dry-run review is the safety gate before any provider mutation and should be the main user trust-building surface.
**Delivers:** Planning input form, provider/runtime recommendations, non-mutating Arma 3 dry-run generation, plan details, validation failures, assumptions, firewall/resources, metadata freshness, estimates, VAT/tax note, and regenerate-plan path.
**Addresses:** Cloud planning input form, non-mutating dry-run, pre-provisioning review, admin readiness support.
**Avoids:** Stale/mismatched plan provisioning, hidden assumptions, secret leakage through raw planning details, and idle billable infrastructure.

### Phase 4: Policy Gate And Approval UX

**Rationale:** Provisioning and other billable actions must not proceed until warnings, approvals, or denials are understood and acted on explicitly.
**Delivers:** Allowed/warn/approval-required/denied rendering, acknowledgement retry, approval request handoff, admin approval/denial actions, expired/mismatched approval behavior, and no automatic retry.
**Addresses:** Policy gate visibility, approval workflow handoff, policy next steps.
**Avoids:** Bypassable warnings, confusing failed provision attempts, and approval scope ambiguity.

### Phase 5: Provisioning, Runtime Operation, And Teardown

**Rationale:** Mutation comes after plan and policy safety. This phase wires the reviewed plan into real provider and runtime actions with clear cost boundaries.
**Delivers:** Server-scoped provision, session/resource/bootstrap/worker refresh, endpoint display, durable job status, install/update/start/stop/restart/log/runtime cleanup controls, explicit provider teardown confirmation, retryable states, and remaining billable resource display.
**Addresses:** Provision from selected plan, provisioning progress, runtime operation controls, durable job feedback, public connection details, teardown as cost-stop action.
**Avoids:** Runtime cleanup/provider teardown confusion, partial success hiding, stale mutation results, and billable resources left invisible.

### Phase 6: Cost Responsibility, History, And Audit Visibility

**Rationale:** Cost accountability is the core value of the cloud orchestration effort and must remain visible after provisioning, cleanup, transfer, and deletion.
**Delivers:** Active cost responsibility, historical owner intervals, estimated/reconciled/adjusted labels, billing assignment history, audit context, cleanup/orphan risk, reconciliation status, and post-teardown visibility.
**Addresses:** Active cost responsibility view, historical cost responsibility view, cleanup/orphan risk visibility.
**Avoids:** Payment/invoice scope creep, cross-owner cost leaks, and loss of accountability after teardown/deletion.

### Phase 7: Verification And Phase 30 Handoff

**Rationale:** The milestone must prove implementation behavior without fabricating deferred live provider/runtime evidence.
**Delivers:** API/repository/component/browser smoke coverage for auth matrix, owner attribution, policy gates, stale plans, lifecycle boundaries, partial failures, redaction, and evidence classification.
**Addresses:** Secret-safe display, durable workflow reliability, implementation evidence readiness.
**Avoids:** Secret regressions, unauthorized mutation, and false Phase 30 UAT pass claims.

### Phase Ordering Rationale

- Ownership and route scope come first because every later cloud action depends on selected server, active owner, and authorization correctness.
- Repository/gate foundation comes before UI breadth because every billable action needs the same continuation and policy result semantics.
- Dry-run review must precede provisioning because it is the non-mutating safety surface for cost, metadata, assumptions, validation, and cleanup warnings.
- Provisioning/runtime/teardown should be implemented only after policy gates are actionable in the UI.
- Cost history follows mutation flows because it must consume real session/resource/job/owner/audit transitions while preserving estimated/reconciled/adjusted distinctions.
- Verification closes the milestone with explicit evidence classification and a handoff to future Phase 30 live UAT.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 1:** Decide whether non-admin server owners may provision and tear down provider resources, and what server-scoped API wrappers are required.
- **Phase 2:** Verify OpenAPI/Kiota generation for `CloudSpendingPolicyGateResultDto`, `acknowledgedDecisionId`, and `approvalRequestId`; repair API metadata if missing.
- **Phase 3:** Confirm safe provider metadata projection for non-admin creator choices, or restrict those choices to admin-created plans.
- **Phase 4:** Validate approval request scope, expiry, retry, and admin/approver authorization semantics.
- **Phase 5:** Validate teardown and runtime cleanup action kinds so UI and policy decisions do not conflate cost boundaries.
- **Phase 7:** Define acceptable implementation evidence versus deferred Phase 30 live UAT evidence.

Phases with standard patterns (skip research-phase unless implementation reveals gaps):
- **Phase 1 UI shell:** Blazor/Fluent form and wizard composition follows existing `/events/create` and `/servers` patterns.
- **Phase 3 review UI:** Rendering DTO summaries, message bars, progress, and validation states uses established Fluent UI patterns.
- **Phase 6 cost display:** Existing selected-server and cloud admin cost models already distinguish estimated/reconciled/adjusted labels.
- **Phase 7 component tests:** bUnit component testing and existing API test patterns are documented enough for focused coverage.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Mostly repo-derived; external package guidance is MEDIUM but not central to v1.8 because upgrades are non-goals. |
| Features | HIGH | Directly grounded in `.planning/PROJECT.md`, CR-0001 scope, shipped v1.0-v1.7 foundations, and current WebApp/API surfaces. |
| Architecture | HIGH | Research identifies concrete controllers, repositories, pages, DTOs, services, and missing integration points. |
| Pitfalls | HIGH | Risks are repo-specific and tied to known cost, policy, authorization, teardown, redaction, and deferred-UAT constraints. |

**Overall confidence:** HIGH

### Gaps to Address

- **Owner mutation authority:** Planning must decide whether non-admin server owners can provision/tear down provider resources or whether admins must approve/execute those mutations.
- **Generated client completeness:** Regeneration must expose policy gate DTOs and continuation query parameters; if not, fix OpenAPI metadata before UI work.
- **Safe metadata projection:** Non-admin creators need sanitized location/server type/image recommendations, or the workflow must be admin-only for provider choices.
- **Policy gate transport shape:** Repository code needs a tested pattern for Kiota 409 responses carrying `CloudSpendingPolicyGateResultDto`.
- **Runtime input completeness:** Requirements should define which modpack/runtime fields are required before provisioning versus allowed to remain configurable after provisioning.
- **Live UAT boundary:** v1.8 verification must explicitly state that Phase 30 live Hetzner/Steam/Arma UAT remains deferred unless a separate operator-approved run occurs.

## Sources

### Primary (HIGH confidence)

- `.planning/PROJECT.md` - v1.8 goal, active requirements, constraints, decisions, shipped v1.0-v1.7 foundations, and Phase 30 deferral.
- `.planning/research/STACK.md` - stack reuse, generated-client gaps, repository facade guidance, and testing recommendations.
- `.planning/research/FEATURES.md` - table-stakes features, differentiators, requirement categories, anti-features, and deferrals.
- `.planning/research/ARCHITECTURE.md` - existing components, missing integration points, proposed data flow, authorization/redaction boundaries, and build order.
- `.planning/research/PITFALLS.md` - high-risk pitfalls, phase-level prevention strategy, verification focus, and deferred live UAT warnings.
- `docs/change-requests/CR-0001-cloud-game-server-orchestration.md` - cloud orchestration, billing attribution, frontend, security, reliability, and acceptance context cited by research.

### Secondary (MEDIUM confidence)

- NuGet `Microsoft.FluentUI.AspNetCore.Components` and Fluent UI Blazor wizard docs - confirms newer package availability and existing wizard component support.
- Microsoft Learn Kiota dependency documentation and NuGet `Microsoft.Kiota.Bundle` - confirms generated-client dependency direction.
- Microsoft Learn Blazor testing and NuGet `bunit` - supports focused component test recommendation.
- Playwright .NET docs - supports deferring browser E2E unless explicitly required.

---
*Research completed: 2026-06-19*
*Ready for roadmap: yes*
