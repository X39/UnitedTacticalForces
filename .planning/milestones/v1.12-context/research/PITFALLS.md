# Pitfalls Research: v1.8 Cloud Server Creation Experience

**Domain:** Guided WebApp workflow for Arma 3 cloud server creation on existing control-plane/worker foundations
**Researched:** 2026-06-19
**Confidence:** HIGH for repo-specific risks, MEDIUM for phase numbering until roadmap is finalized

## Summary

v1.8 should not invent new cloud orchestration semantics. The backend already has durable dry-run plans, owner snapshots, policy gates, provider provisioning, cloud worker bootstrap, runtime execution, cost visibility, reconciliation, teardown, and audit records. The risk is that a new guided WebApp flow makes those paths feel simple by hiding the exact safeguards that make them safe.

The roadmap should treat the creation experience as a server-oriented orchestration shell around existing APIs: create/select server, confirm active billing owner, generate an Arma 3 dry-run plan, review estimates/freshness/assumptions/policy, then provision only after any warning acknowledgement or approval is explicit. Runtime cleanup, local server deletion, and provider teardown must stay visibly distinct because only provider teardown stops Hetzner billable resources.

Implementation verification for v1.8 should prove UI, authorization, policy, redaction, and persistence behavior with fakes/dry-run data. It must not claim deferred Phase 30 live UAT. Phase 30 remains a separate operator-approved live Hetzner/Steam/Arma evidence milestone with final provider-side billable count `0`.

## High-Risk Pitfalls

### 1. Reusing the admin cloud console as the user creation flow

**What goes wrong:** Authorized server owners are forced through `/servers/cloud`, or the new wizard calls admin-only cloud inventory endpoints. That either blocks valid non-admin owners or encourages over-broad claims to make the UI work.

**Why this repo is exposed:** `CloudInventoryController` dry-run, provisioning, teardown, status, resources, estimates, reconciliation, and policy aggregate endpoints are admin-gated. The resource-scoped Arma 3 planner is under `game-servers/{gameServerId}/cloud-dry-run-plans/arma3` and uses `Claims.ResourceBased.Server.Configuration`.

**Prevention:** Build v1.8 around server-scoped repository methods and pages. Use admin aggregate views only for administrators. For owner workflows, consume resource-scoped game-server cloud status, cost, policy, and planning endpoints, and add missing generated client/repository methods instead of using `CloudAdminRepository`.

**Likely roadmap phase:** Phase 33 - creation entrypoint, server selection, billing owner selection, and generated client/repository scope.

**Testable checks:** Owner with server config/start claims can create/review a dry-run plan for their server but cannot list global cloud inventory. Admin can still use `/servers/cloud`.

### 2. Provisioning from stale or mismatched dry-run review

**What goes wrong:** A user reviews one estimate but provisions after provider metadata, active billing owner, or selected server state changes. The backend will reject many cases, but the UX may present this as a random failure or encourage retry without a new review.

**Why this repo is exposed:** Provisioning validates a valid unused plan, active owner match, provider readiness, and metadata timestamp match. Estimates carry `Freshness`, confidence, metadata timestamps, and assumptions, but the current admin action button simply calls provision and catches a generic exception.

**Prevention:** Make the review screen the source of truth. Show plan id, session id, owner account, billing assignment, metadata timestamp, freshness, hourly/monthly estimate, confidence, VAT/tax note, planned resources, assumptions, validation failures, and a "regenerate plan" action. Disable provision when the latest plan is stale, invalid, superseded, owner-mismatched, or missing estimate data.

**Likely roadmap phase:** Phase 34 - Arma 3 dry-run planner review and pre-provisioning confirmation.

**Testable checks:** Stale metadata, changed billing owner, invalid plan, and superseded plan all block the final provision button before provider mutation and require a regenerated plan.

### 3. Losing billing owner attribution during create/select/plan handoff

**What goes wrong:** A server is created under one billing account, the cloud plan uses another, or ownership changes between plan and provision without a visible decision. Cost history then becomes technically persisted but confusing or wrong to the user.

**Why this repo is exposed:** `CreateGameServerAsync` assigns an initial active owner and queues install. Arma 3 dry-run payloads require selected billing account and assignment. Provisioning rejects a plan whose owner snapshot no longer matches the active owner.

**Prevention:** Treat owner selection as a first-class step. The wizard should display both billing account and assignment id, explain that this owner is responsible until cleanup/transfer, and refresh/revalidate the active owner before plan creation and before provisioning. Ownership transfer should be out of the wizard unless explicitly planned.

**Likely roadmap phase:** Phase 33, with revalidation repeated in Phase 34.

**Testable checks:** Plan creation fails or is disabled when no active owner exists, when the selected assignment is not active for the server, or when the owner changes after review.

### 4. Treating policy gates as generic errors or bypassable warnings

**What goes wrong:** A 409 from provisioning, teardown, or lifecycle job enqueue is shown as "failed", or the UI retries automatically without explicit acknowledgement/approval. Users cannot request approval, approvers cannot understand scope, and warnings become invisible.

**Why this repo is exposed:** The API returns `CloudSpendingPolicyGateResultDto` with `RequiresAcknowledgement`, `RequiresApproval`, `IsDenied`, `Decision`, and optional `ApprovalRequest`. Current WebApp repository methods for provisioning and teardown do not model acknowledged decision or approval request query parameters.

**Prevention:** Add gate-aware client/repository methods for provision, lifecycle, and teardown actions. The UI must render four states: allowed, warning requiring explicit acknowledgement, approval required with request id/status, and denied with reason. Retrying with `acknowledgedDecisionId` or `approvalRequestId` should happen only from explicit user action.

**Likely roadmap phase:** Phase 34 for preflight display; Phase 35 for mutation actions.

**Testable checks:** Warning returns a review step, approval required creates/displays a durable request, denied never enables mutation, and an approved unexpired request allows exactly the matching scope.

### 5. Conflating runtime cleanup, local server delete, and provider teardown

**What goes wrong:** UI text or button placement implies "Cleanup" or local `Delete` stops costs. Users may leave Hetzner servers, IPs, or volumes running because the game process or local server row looks gone.

**Why this repo is exposed:** `GameServersController` queues lifecycle jobs, including cloud runtime cleanup through the worker path. `CloudTeardownService` deletes provider resources and marks "Provider resource deleted. Cost attribution stopped." Existing UI already warns that provider teardown stops billable resources while runtime cleanup does not.

**Prevention:** Keep three visibly separate actions: runtime cleanup, local server deactivate/delete, and provider teardown. Provider teardown must show remaining billable resource count and cleanup failure state. Local server deletion should be blocked or strongly warned when an active cloud session exists.

**Likely roadmap phase:** Phase 35 - provision/runtime/teardown action flow; Phase 36 - cost and cleanup history.

**Testable checks:** Runtime cleanup success leaves provider cost warnings visible; provider teardown success clears billable resources; partial teardown keeps cleanup-needed state and owner attribution visible.

### 6. Hiding partial provisioning or partial teardown after a "success" toast

**What goes wrong:** A flow reports provisioning or teardown as done while some provider resources remain cleanup-needed, orphan-candidate, or reconciliation-risky. This is a direct cost exposure path.

**Why this repo is exposed:** Provisioning marks created resources and sessions `CleanupNeeded` on partial failure. Teardown returns `CleanupFailed` and `RemainingBillableResourceCount`. Reconciliation exposes findings and orphan indicators.

**Prevention:** Mutation results must refresh and pin the resulting session status, resources, cost visibility, reconciliation findings, and audit trail. Success copy should distinguish "provisioned", "cleanup still required", "teardown completed", and "remaining billable resources".

**Likely roadmap phase:** Phase 35, reinforced in Phase 36.

**Testable checks:** Simulated partial provision and partial teardown show persistent cleanup-needed state, owner, audit record, and remaining billable count without hiding the session.

### 7. Leaking cross-owner cloud, cost, reconciliation, or audit details

**What goes wrong:** A server owner sees global sessions, unrelated billing accounts, provider resource ids, reconciliation findings, imported billing context, or audit records outside their server scope.

**Why this repo is exposed:** Admin cloud visibility intentionally aggregates all sessions/resources/costs. Server-scoped endpoints exist for cloud status, runtime status, cost visibility, and policy visibility with resource-based authorization.

**Prevention:** Owner pages must use server-scoped projections only. Admin-only data grids, resource inventory lists, reconciliation runs, and global policy aggregates stay on `/servers/cloud`. Any new cost-responsibility view must project only owned/authorized sessions and avoid raw provider or import rows unless the actor is admin.

**Likely roadmap phase:** Phase 33 for route/repository design; Phase 36 for cost history views.

**Testable checks:** Non-admin server owner cannot fetch or render another server's cloud status/cost/policy/audit. Admin-only inventory and reconciliation routes remain inaccessible.

### 8. Exposing secrets or sensitive operational data through review details

**What goes wrong:** Planned operations JSON, failure text, bootstrap user data, worker credential material, Steam credentials, Hetzner token references, bearer/session tokens, or raw provider diagnostics leak into Blazor DTOs, toasts, logs, or copied support output.

**Why this repo is exposed:** Provisioning creates bootstrap credentials and user data; the service redacts bootstrap credential text on one failure path and uses failure sanitizers. The v1.8 UI will be tempted to show validation/planning JSON and provider details to make the review feel transparent.

**Prevention:** Only render allowlisted fields in the wizard. Treat `ValidationDetailsJson`, `PlannedOperationsJson`, failure summaries, audit details, and assumptions as bounded and already-sanitized but still not automatically safe for broad owner display. Add redaction regression tests around UI models and exception handling.

**Likely roadmap phase:** Phase 34 for review rendering; Phase 37 for verification hardening.

**Testable checks:** Fixture strings containing Hetzner tokens, Steam credentials, worker bootstrap credentials, bearer tokens, and session ids are replaced or absent in DTOs, toasts, audit projections, and rendered markup.

### 9. Creating billable infrastructure before runtime prerequisites are actionable

**What goes wrong:** The wizard provisions a VM but does not collect or verify enough Arma 3 runtime context for a useful server: modpack, ports, storage, mission/config inputs, RCON exposure decision, admin source CIDR, or expected install/runtime prerequisites. The result is a billable idle worker.

**Why this repo is exposed:** The Arma 3 dry-run payload covers provider shape, ports, storage, SSH exposure, and administrative CIDR. v1.8 also needs modpack/runtime choices, but current server creation is a minimal controller/title/owner form.

**Prevention:** Put runtime readiness before mutation. The wizard should require or explicitly defer modpack/runtime inputs, show what remains configurable after provisioning, and warn when provisioning will create costs before the server can be started.

**Likely roadmap phase:** Phase 33 for input collection; Phase 34 for dry-run validation and assumptions.

**Testable checks:** Missing required runtime inputs produce validation failures or explicit assumptions; provision remains disabled unless the user has reviewed the cost of an idle provisioned worker.

### 10. Claiming v1.8 implementation checks as live Phase 30 UAT

**What goes wrong:** Browser tests, fake provider tests, or dry-run walkthroughs are recorded as satisfying OPS-02/OPS-04/EVID live requirements. That would fabricate evidence and obscure the real operator gate.

**Why this repo is exposed:** v1.7 audit records Phase 30 as deferred, missing live provisioning/runtime/cleanup evidence and final provider-side billable count `0`. v1.8 intentionally comes before live UAT closure.

**Prevention:** v1.8 verification artifacts must label evidence as implementation verification unless a separately approved Phase 30 run occurs. No live provider mutation should happen during v1.8 verification without explicit operator approval, budget, credentials, cleanup window, and final billable count capture.

**Likely roadmap phase:** Phase 37 - verification, evidence classification, and Phase 30 handoff.

**Testable checks:** Verification docs contain an explicit "not Phase 30 live UAT" statement, and automated tests use fakes/dry-run data unless a Phase 30 operator evidence file is filled.

## Phase-Level Prevention Strategy

| Roadmap phase | Primary goal | Pitfalls prevented | Required safeguards |
|---------------|--------------|--------------------|---------------------|
| Phase 33 - Guided entry and ownership | Create/select Arma 3 server, billing owner, assignment, modpack/runtime basics, and route/repository boundaries. | 1, 3, 7, 9 | Server-scoped repository methods, active owner revalidation, no admin aggregate calls in owner flow, initial runtime input validation. |
| Phase 34 - Dry-run review and policy preflight | Generate Arma 3 dry-run plan and show cost, metadata, resources, assumptions, validation, and policy gate state before mutation. | 2, 4, 8, 9 | Freshness gates, plan/estimate snapshot display, allowlisted details, policy conflict modeling, regenerate-plan path. |
| Phase 35 - Provisioning, runtime, and teardown actions | Execute provision/lifecycle/teardown through explicit confirmation, warning acknowledgement, approval, and refresh flows. | 4, 5, 6 | No auto-retry, distinct runtime cleanup vs provider teardown actions, mutation result refresh, remaining billable resource visibility. |
| Phase 36 - Cost responsibility and audit visibility | Surface active/historical owner cost responsibility with estimated/reconciled/adjusted labels and cleanup/reconciliation risk. | 5, 6, 7 | Owner-scoped projections, source labels, audit context, cleanup-needed/orphan visibility, no payment/invoice language. |
| Phase 37 - Verification and UAT handoff | Prove the product path without fabricating live provider/runtime UAT. | 8, 10 plus regression coverage for 1-7 | Auth matrix tests, fake provider partial-failure tests, redaction tests, browser workflow tests, explicit Phase 30 deferred classification. |

## Verification Focus

- **Authorization matrix:** Admin, server owner with `Server.Configuration`, server owner with start/stop only, unrelated authenticated user, and anonymous user across wizard, dry-run, provision, runtime, teardown, cost, policy, and audit routes.
- **Owner attribution:** Plan and provision use the active billing assignment; owner changes between review and provision block mutation and force regeneration.
- **Policy gates:** Warning acknowledgement, approval required, denied, expired approval, mismatched approval scope, and automatic-action attention are rendered distinctly and tested through repository methods.
- **Freshness and metadata:** Stale provider metadata, stale plan timestamp, missing estimate, invalid plan, and superseded plan disable provision and explain the required next step.
- **Lifecycle boundary:** Runtime cleanup, local server delete, and provider teardown have different labels, endpoints, confirmations, and post-action status.
- **Partial failure:** Fake provider creates server then fails IP/volume/firewall; fake teardown fails one billable resource; UI keeps cleanup-needed state and remaining cost risk visible.
- **Redaction:** Secrets and raw credential-like values are absent from rendered review details, toasts, exception messages, audit projections, and logs.
- **Evidence classification:** v1.8 verification docs distinguish fake/dry-run/browser implementation evidence from deferred Phase 30 live UAT.

## Sources/Evidence

- `.planning/PROJECT.md` - v1.8 scope, active requirements, constraints, and explicit Phase 30 deferral.
- `.planning/STATE.md` - current milestone planning state, blocker history, and deferred live UAT conditions.
- `docs/change-requests/CR-0001-cloud-game-server-orchestration.md` - frontend, billing, security, reliability, and acceptance criteria.
- `.planning/milestones/v1.7-MILESTONE-AUDIT.md` - OPS-02/OPS-04/EVID live UAT gaps and Phase 30 missing evidence.
- `sources/Api/Controllers/CloudInventoryController.cs:47`, `:87`, `:141` - admin-only dry-run inventory, provisioning, and teardown endpoints.
- `sources/Api/Controllers/CloudPlanningController.cs:16`, `:37` - resource-scoped Arma 3 dry-run planning endpoint and authorization.
- `sources/Api/Controllers/GameServersController.cs:174`, `:199`, `:222`, `:246`, `:272`, `:411`, `:1416`, `:1743` - server-scoped cloud visibility/cost/policy, owner assignment, server creation, and lifecycle policy gate integration.
- `sources/Api/Services/CloudPolicies/CloudSpendingPolicyGateService.cs:24`, `:70`, `:104`, `:276`, `:318` - provisioning, teardown, lifecycle policy evaluation and gate states.
- `sources/Api/DTO/CloudSpendingPolicyGateResultDto.cs:13`, `:23` - gate DTO fields that v1.8 must render instead of treating 409 as a generic error.
- `sources/Api/Services/CloudProvisioning/CloudProvisioningService.cs:113`, `:197`, `:221`, `:238`, `:450`, `:676` - valid unused plan, active-owner, metadata, provider readiness, cleanup-needed, and bootstrap redaction safeguards.
- `sources/Api/Services/CloudTeardown/CloudTeardownService.cs:107`, `:325`, `:416`, `:422`, `:423`, `:468` - provider teardown as cost-stop boundary and remaining billable resource reporting.
- `sources/WebApp/Pages/Servers/Index.razor:333`, `:774`, `:824` - existing server view, lifecycle queue flow, and current minimal server creation form.
- `sources/WebApp/Pages/Servers/Cloud.razor:660`, `:1214`, `:1240` - existing admin warning and generic provision/teardown action handling.
- `sources/Api/DTO/Payloads/Arma3CloudDryRunPlanRequestPayload.cs` and `sources/Api/DTO/Arma3CloudDryRunPlanDto.cs` - planner inputs and review outputs v1.8 should surface.
