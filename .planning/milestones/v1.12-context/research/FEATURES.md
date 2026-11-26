# Feature Research: v1.8 Cloud Server Creation Experience

**Domain:** WebApp-driven Arma 3 cloud game-server creation, review, operation, teardown, and cost responsibility
**Researched:** 2026-06-19
**Confidence:** HIGH for repo/CR-derived scope; MEDIUM for general product-shape guidance

## Summary

v1.8 should turn the shipped CR-0001 foundations into one coherent server-oriented user journey. The typical experience for this domain is not a generic cloud inventory console; it is a guided flow where an authorized user selects or creates an Arma 3 server, chooses who owns the cost, reviews a non-mutating dry-run plan, clears policy gates, provisions, operates the runtime, and tears down billable resources with clear cost-stop semantics.

The backend foundations already exist for persistent lifecycle jobs, billing ownership, Hetzner metadata and dry-run planning, real provisioning and teardown, cloud-worker runtime execution, cost/reconciliation visibility, policy gates and approvals, and estimated/reconciled/adjusted cost labels. The v1.8 feature work should therefore be mostly product composition in the WebApp and repository/client layer, with only narrow API additions if a required generated-client route is missing.

The main product gap is that current surfaces are split: `/servers` has basic server creation and server details, while `/servers/cloud` exposes admin-oriented cloud operations, plans, resources, estimates, reconciliation, and policies. v1.8 should make the cloud path feel like creating and operating one Arma 3 game server, with admin-only cloud dashboard details available as supporting evidence rather than the primary user workflow.

Do not re-scope OPS-02/OPS-03 live UAT as a product feature. v1.8 can make the path evidence-ready by showing preflight, approval, provider readiness, cleanup, and final cost-risk information, but live Hetzner/Steam/Arma proof remains a later operator-approved evidence milestone.

## User Journey

1. **Start from Servers:** User chooses "Create cloud server" or opens an existing Arma 3 server and chooses "Plan cloud hosting".
2. **Choose responsibility:** User selects an active billing owner they are allowed to use, or an admin creates/selects one before continuing.
3. **Configure Arma 3 cloud inputs:** User selects modpack/runtime inputs and cloud options: server type, location/datacenter, image, storage, game/query/Steam/RCON ports, SSH exposure, and administrative source CIDR. Recommended defaults should be available from cached provider metadata.
4. **Generate dry-run plan:** WebApp sends a non-mutating Arma 3 cloud dry-run request for the selected server and billing assignment.
5. **Review before mutation:** User sees validation failures, planned resources, firewall rules, assumptions, metadata freshness, hourly/monthly estimates, VAT/tax note, cost source labels, and selected/recommended values.
6. **Pass policy gates:** WebApp shows whether provisioning can proceed, requires acknowledgement, requires approval, or is denied. Approval-required states should route the user/admin to the existing approval workflow instead of hiding the block.
7. **Provision with explicit confirmation:** User confirms that provider resources may be created and cost responsibility starts when billable resources are created, not when the Arma runtime starts.
8. **Watch provisioning and bootstrap:** WebApp shows session state, planned/created resources, bootstrap state, worker registration, heartbeat, endpoint, retryable states, and sanitized failure reasons.
9. **Operate runtime:** Authorized owner/admin can install/update/configure/start/stop/restart/collect logs and see durable job status, progress, readiness, recent logs, and public connection details.
10. **Manage cost responsibility:** User sees current estimated cost, accumulated estimated/reconciled/adjusted cost, owner intervals, active billable resources, metadata freshness, reconciliation findings, and cleanup risk.
11. **Teardown deliberately:** User distinguishes runtime stop/cleanup from provider teardown. Provider teardown is the cost-stop action and must show warnings, cleanup result, orphan/cleanup-needed state, and retry options.
12. **Review history:** After deletion, authorized users can inspect historical ownership, audit records, lifecycle jobs, logs, cost labels, adjustments, and cleanup outcome.

## Table Stakes

| Feature | Testable User Capability | Complexity | Depends On | Notes |
|---------|--------------------------|------------|------------|-------|
| Server-oriented cloud entry point | Authorized user can start cloud creation from `/servers` without first navigating a cloud-admin inventory console. | MEDIUM | Existing `/servers` page and generated repositories | Add "Create cloud server" or "Plan cloud hosting" path that stays anchored to one game server. |
| Select or create Arma 3 server | User can create a server with Arma 3 controller/title/billing owner or select an existing Arma 3 server before planning. | MEDIUM | v1.0 game-server creation and billing owner assignment | Current create form is generic; v1.8 should constrain the cloud flow to Arma 3 and make owner selection mandatory. |
| Permission-aware billing owner selector | User can only select active billing owners they may assign; admin can see/create owner options where permitted. | MEDIUM | v1.0 billing accounts/assignments | Cost responsibility must be chosen before dry-run and preserved through provisioning/cleanup. |
| Cloud planning input form | User can enter or accept recommended size, location/datacenter, image, storage, ports, RCON, SSH exposure, admin CIDR, modpack, and runtime inputs. | HIGH | v1.1 `Arma3CloudDryRunPlanRequestPayload`, provider metadata, modpack/config services | Defaults should favor one Arma 3 server per worker and use cached metadata recommendations. |
| Non-mutating dry-run generation | User can generate a plan and see that no provider resources were created. | MEDIUM | v1.1 Arma 3 dry-run planner | The dry-run response includes `IsValid`, plan/session ids, resources, assumptions, validation failures, and estimates. |
| Pre-provisioning review | User can review planned resources, firewall rules, selected vs recommended values, validation failures, assumptions, metadata timestamp/freshness, hourly/monthly estimate, currency, and VAT/tax note before provisioning. | HIGH | v1.1 dry-run DTOs and v1.4 cost labels | This is the critical safety screen before provider mutation. |
| Policy gate visibility | User sees allow/warn/approval-required/deny state, required acknowledgement, approval request id, and reason before provisioning or teardown. | HIGH | v1.5 `CloudSpendingPolicyGateResultDto`, policy visibility | Do not let a provision button fail silently after policy denial. |
| Approval workflow handoff | User/admin can identify pending approvals, approved exceptions, denied requests, expiration, and next step from the server flow. | MEDIUM | v1.5 approval request/visibility surfaces | Full policy configuration can stay admin-only; action-specific approval state belongs in the creation flow. |
| Provision from selected plan | User can provision only a valid/latest dry-run plan and sees the resulting session/resources. | MEDIUM | v1.2 `ProvisionDryRunPlanAsync` | Confirmation copy must state billable resources may be created and ownership starts immediately. |
| Provisioning progress/status | User can monitor session state, resource states, bootstrap status, worker registration, heartbeat, endpoint, failures, and retryable actions. | MEDIUM | v1.2 session status and worker registration visibility | Current `/servers/cloud` has this; v1.8 should present it in the selected server workflow. |
| Runtime operation controls | Authorized owner/admin can install, update, start, stop, restart, collect logs, and cleanup runtime from the server page. | MEDIUM | v1.3 cloud runtime and v1.0 lifecycle jobs | Runtime cleanup must remain separate from provider teardown. |
| Durable job feedback | User can see queued/running/retry/failed/succeeded lifecycle jobs, progress percentage/message, sanitized failure reason, and recent log timestamp. | MEDIUM | v1.0/v1.3 lifecycle job projections | Required because provisioning/runtime actions are long-running. |
| Public connection details | User can see game endpoint/port details after provisioning/worker readiness without exposing credentials. | LOW | v1.2/v1.3 cloud session/runtime status | Connection info is a CR acceptance criterion. |
| Teardown as cost-stop action | User can tear down provider resources separately from stopping the game runtime, with warning that stopped runtime may still cost money. | HIGH | v1.2 teardown and v1.4 cost-stop semantics | This must be more prominent than runtime stop/cleanup. |
| Cleanup/orphan risk visibility | User/admin can see cleanup-needed billable resources, orphan indicators, reconciliation findings, and retry options. | MEDIUM | v1.4 reconciliation and v1.2 cleanup state | Missing this creates unexpected-cost risk. |
| Active cost responsibility view | Server owner can see current estimated cost, active billable resources, owner, assignment, source label, metadata freshness, and confidence. | MEDIUM | v1.4 cost visibility, v1.0 owner assignment | Must be visible while resources exist. |
| Historical cost responsibility view | Authorized user can see accumulated estimated, provider-allocated reconciled, adjusted cost, owner intervals, billing records, and audit context after teardown. | MEDIUM | v1.6 billing reconciliation and adjustments | Labels must distinguish estimated/reconciled/adjusted. |
| Admin provider readiness guard | Admin sees provider config, metadata freshness, reconciliation age, policy risk inputs, and pending approvals before encouraging users to provision. | MEDIUM | v1.1 provider status/metadata, v1.4 reconciliation, v1.5 policy | Keep as admin-supporting readiness, not the main user workflow. |
| Secret-safe display | No provider tokens, Steam credentials, worker credentials, bearer/session secrets, or raw bootstrap secrets appear in UI, logs, errors, DTOs, or audit summaries. | HIGH | v1.0-v1.7 redaction foundations | Test with failure and denied-policy paths, not only happy path. |

## Differentiators

| Feature | Testable User Capability | Complexity | Why It Matters |
|---------|--------------------------|------------|----------------|
| Cost-first creation review | User must acknowledge who pays, when cost starts, and the estimated/reconciled/adjusted model before provisioning. | MEDIUM | Most game-server panels hide infrastructure accounting; UTF's CR requires visible, auditable owner attribution. |
| Plan assumptions as first-class UI | User sees recommendation reasons, metadata age, validation failures, firewall rules, and assumptions instead of only a price total. | MEDIUM | Builds trust in on-demand Arma 3 provisioning where modpacks, ports, and provider availability can fail late. |
| Integrated policy gate next steps | User sees "can proceed", "acknowledge", "approval required", or "denied" with the next action in context. | HIGH | Prevents confusing failed provision attempts and makes admin approval operationally clear. |
| Server-owner cost history after deletion | Owner can review historical attribution and adjusted cost labels after resources are gone. | MEDIUM | Supports community accountability without implementing payment processing. |
| Cleanup-risk-first teardown UX | User sees whether teardown actually stopped billable resource cost, plus retry/orphan indicators. | MEDIUM | Directly addresses the largest cloud hosting risk: resources that continue billing after runtime is stopped or cleanup fails. |
| Evidence-ready preflight summary | Admin can capture provider readiness, metadata freshness, policy result, selected plan, owner, and cleanup warnings before later live UAT. | LOW | Helps future OPS-02/OPS-03 evidence without marking those live checks complete in v1.8. |

## Anti-Features/Deferrals

| Anti-Feature / Deferral | Why Avoid In v1.8 | Do Instead |
|-------------------------|-------------------|------------|
| Payment processing, chargeback, or customer invoicing | Explicitly outside CR-0001/v1.8; adds legal/accounting scope. | Show internal attribution, estimates, reconciled values, adjustments, and owner history. |
| Live OPS-02/OPS-03 closure as a product feature | v1.7 documents this as operator-gated live evidence requiring credentials, budget, provider approval, cleanup window, and authenticated sessions. | Make the UI path evidence-ready; leave live proof to a later approved UAT milestone. |
| Multi-provider creation | Hetzner is the shipped provider and first cloud target. | Use provider labels and abstractions, but build only the Hetzner-backed Arma 3 path. |
| Multi-session cloud workers | One server per worker is the accepted simplification for billing, firewalling, ports, ownership, and cleanup. | Keep one Arma 3 server per cloud worker in v1.8. |
| Generic cloud dashboard as the main UX | Users need to create/operate a game server, not inspect all provider inventory tabs. | Keep `/servers/cloud` admin support; make `/servers` the primary creation/owner path. |
| Manual SSH/provider-console workflow | Violates the CR goal of frontend-driven creation and auditable lifecycle jobs. | Surface provider readiness and retry states through WebApp controls backed by durable jobs. |
| Ownerless "quick launch" | Breaks the billing invariant that every cost-generating resource has an owner from creation to cleanup/transfer. | Disable dry-run/provision until an active owner and assignment are selected. |
| Treating runtime stop as cost stop | Hetzner resources can remain billable after the game process stops. | Label stop, runtime cleanup, and provider teardown as distinct actions; provider teardown is the cost-stop action. |
| Raw browser HTTP bridge for new cloud calls | Existing milestone direction replaced temporary raw calls with generated Kiota clients and repositories. | Extend generated clients/repository facades if routes are missing. |
| Full policy configuration redesign | v1.5 already shipped policy configuration/visibility; v1.8 is about creation/operation path. | Show effective policy, gate result, approvals, and next steps in context. |
| General UX redesign of all server admin pages | Too broad and not necessary for CR acceptance. | Restructure only the cloud creation/review/operate/cost path. |
| Headless clients, resize, snapshots, backups, templates marketplace | Useful later but not needed for one Arma 3 cloud server creation flow. | Defer until the first WebApp creation path and cost/cleanup responsibilities are proven. |

## Requirement Categories

### 1. Guided Creation Entry

- User can open a cloud creation flow from the server list.
- User can create a new Arma 3 server or select an existing Arma 3 server.
- User cannot continue without a title, supported controller, active billing owner, and valid billing assignment.
- WebApp uses repository/generated client patterns, not raw browser HTTP calls.

### 2. Planning Inputs And Recommendations

- User can supply all fields supported by `Arma3CloudDryRunPlanRequestPayload`.
- User can accept provider-recommended server type, location, datacenter, and image where omitted.
- User can select modpack/runtime configuration needed by cloud worker snapshots.
- UI validates obvious port/CIDR/storage input errors before submit and still displays server-side validation failures.

### 3. Dry-Run Review And Policy Gate

- User can generate a dry-run plan without provider mutation.
- Review screen shows `IsValid`, plan id, session id, planned resource state, selected/recommended values, firewall rules, assumptions, validation failures, metadata timestamp, hourly/monthly estimate, currency, tax note, and cost estimate source/freshness.
- Provision action is disabled or routed to acknowledgement/approval/denial states based on policy gate output.
- Admin/user can find the approval request and required next step from the same flow.

### 4. Provisioning And Bootstrap Operation

- User can provision the selected valid dry-run plan after explicit confirmation.
- WebApp shows provision result, session state, resources, bootstrap pending/consumed/expired state, worker registration, worker heartbeat, endpoint, cleanup flags, and retryable actions.
- Failures display sanitized reasons and never expose provider, Steam, worker, bearer, or bootstrap secret material.

### 5. Runtime Controls

- Authorized user can install, update, start, stop, restart, collect logs, and cleanup runtime for the cloud server.
- UI shows lifecycle job queue/running/retry/failure/success states, progress, current job, latest failed job, recent log timestamp, available actions, and runtime readiness.
- Runtime cleanup is visibly different from provider teardown and does not claim cost has stopped.

### 6. Cost Responsibility And History

- Active server view shows current estimated cost, active billable resource count, owner, billing assignment, source label, metadata freshness, confidence, reconciliation status, cleanup risk, and policy status.
- Historical view shows estimated, provider-allocated reconciled, and adjusted accumulated costs with owner intervals and billing/audit context.
- Cost display remains available after server teardown/deletion for users with access.

### 7. Teardown, Cleanup, And Recovery

- User can tear down provider resources only after an explicit cost-stop/destructive-action confirmation.
- UI shows that teardown, not runtime stop, is the billable-resource cleanup boundary.
- User/admin can see cleanup-needed resources, orphan candidates, reconciliation findings, retryable cleanup actions, and final deleted state.

### 8. Admin Readiness And Evidence Support

- Admin can confirm provider status, metadata freshness, reconciliation age, policy thresholds, pending approvals, automatic action attention, and cleanup risk before approving creation.
- Admin can run metadata sync/reconciliation from the existing cloud operations surface.
- v1.8 may produce evidence-friendly screen states, but must not mark live provider/runtime UAT as passed.

## Sources/Evidence

- `.planning/PROJECT.md` - v1.8 goal, active requirements, shipped v1.0-v1.7 foundations, constraints, decisions, and explicit OPS-02/OPS-03 deferral.
- `docs/change-requests/CR-0001-cloud-game-server-orchestration.md` - desired frontend end-state, billing/cost attribution rules, Arma 3 requirements, security/reliability requirements, and overall acceptance criteria.
- `sources/WebApp/Pages/Servers/Index.razor` - current server list, generic server creation, selected-server details, lifecycle controls, cloud runtime/cost/policy visibility.
- `sources/WebApp/Pages/Servers/Cloud.razor` - current admin cloud operations dashboard for provider status, metadata, sessions, plans, resources, estimates, reconciliation, policies, provision, and teardown.
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` - server-scoped repository capabilities for billing owners, lifecycle jobs, cloud session/runtime/cost/policy visibility, cost snapshot refresh, lifecycle operations, and runtime cleanup.
- `sources/WebApp/Services/CloudAdminRepository/ICloudAdminRepository.cs` - admin cloud repository capabilities for provider/metadata status, dry-run plan details, provisioning, teardown, cost visibility, policy visibility, reconciliation, and billing matches.
- `sources/Api/DTO/Payloads/Arma3CloudDryRunPlanRequestPayload.cs` - supported Arma 3 cloud planning inputs.
- `sources/Api/DTO/Arma3CloudDryRunPlanDto.cs` - dry-run output fields for validity, selected/recommended provider options, firewall, assumptions, validation failures, estimates, metadata, and persisted plan/session ids.
- `sources/Api/DTO/CloudSpendingPolicyGateResultDto.cs` - policy gate output for proceed/acknowledge/approval/deny behavior.
- `sources/Api/DTO/CloudCostVisibilityDto.cs` - active and historical cost visibility, reconciliation, billing reconciliation, source labels, active billable resources, cleanup risk, and projection timestamp.
- `sources/Api/DTO/CloudSessionStatusDto.cs` and `sources/Api/DTO/CloudRuntimeStatusDto.cs` - shipped session/bootstrap/worker/provision/teardown/runtime/job status projections that should drive the v1.8 owner workflow.
