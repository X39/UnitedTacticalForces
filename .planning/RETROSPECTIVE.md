# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

## Milestone: v1.0 - Local Worker Foundation

**Shipped:** 2026-06-15
**Phases:** 4 | **Plans:** 13 | **Tasks:** 41

### What Was Built

- Durable lifecycle job storage, DTOs, enqueue services, and lifecycle command endpoints.
- Local worker registry, heartbeat, leases, retry/recovery transitions, and executor integration with the existing Arma 3 controller abstraction.
- Billing accounts, active assignment enforcement, historical assignment intervals, lifecycle job attribution snapshots, and hosting audit records.
- Secret-safe lifecycle failure/audit sanitization plus API tests for ownership, queue transitions, recovery, and redaction.
- WebApp server operations visibility for lifecycle jobs, active owners, logs, worker health, stuck jobs, billing history, and audit records.

### What Worked

- Keeping the existing game-controller abstraction reduced migration risk and preserved current local Arma 3 behavior.
- Modeling lifecycle jobs and worker claims as database transitions made restart recovery testable.
- Billing attribution landed before cloud provisioning, avoiding provider-cost behavior without an owner.
- Phase summaries captured enough decisions and verification evidence to support milestone audit quickly.

### What Was Inefficient

- Kiota regeneration was blocked by the local API/PostgreSQL stack, so Phase 04 used a temporary raw HTTP repository bridge.
- Live authenticated WebApp verification could not run without Aspire PostgreSQL and representative data.
- Some validation metadata was missing or partial even though implementation verification passed.

### Patterns Established

- Lifecycle commands enqueue jobs and return `LifecycleJobDto` instead of inferred server snapshots.
- Workers claim jobs through persisted leases and conditional database updates.
- Billing assignment is required before managed lifecycle jobs and is snapshotted onto queued work.
- Audit and failure text should pass through a sanitizer before persistence or frontend projection.
- Admin recovery views can derive stuck-job state from persisted lifecycle job status, retry, and lease fields.

### Key Lessons

1. Durable ownership and audit state should come before cloud provisioning because it lowers operational and cost risk.
2. Generated-client work needs a reliable local OpenAPI source as part of frontend planning, not as an afterthought.
3. Browser UAT for authenticated admin data should be planned with seeded data and an available database stack.
4. Validation metadata should be generated during each phase if milestone audits will depend on it.

### Cost Observations

- Model mix: not measured.
- Sessions: not measured.
- Notable: Path-limited commits were needed because unrelated local changes existed in `.planning/config.json` and `docs/`.

---

## Milestone: v1.1 - Hetzner Provider Dry-Run & Resource Inventory

**Shipped:** 2026-06-16
**Phases:** 4 | **Plans:** 12 | **Tasks:** 36

### What Was Built

- Secret-safe Hetzner provider configuration with admin endpoints, durable verification state, and redaction tests.
- Read-only Hetzner Cloud client plus typed metadata cache for server types, locations, datacenters, images, firewall capabilities, freshness, and pricing-relevant fields.
- Durable planned cloud sessions, planned resource inventory records, dry-run plans, state transitions, owner snapshots, and audit records without provider mutation calls.
- Local-only Arma 3 dry-run planner with owner enforcement, metadata validation, firewall/storage assumptions, validation failures, and cost estimate snapshots.
- Resource-scoped Arma 3 dry-run API plus admin reads for plans, resources, estimates, provider status, and metadata freshness.
- WebApp `/servers/cloud` admin surface for provider, metadata, plans, resources, estimates, and detail panels.

### What Worked

- Keeping v1.1 dry-run only let provider metadata, inventory, estimates, and admin visibility land without cost exposure.
- Reflection and fake HTTP tests made the no-mutating-provider-operation contract explicit and cheap to verify.
- Snapshotting billing owner, assignment, metadata, and estimate assumptions preserved auditability even when future metadata changes.
- Adding `*-VERIFICATION.md` artifacts closed the audit contract cleanly and made requirement traceability easier to inspect.

### What Was Inefficient

- Phase 08 summaries originally lacked `requirements-completed` frontmatter, causing the milestone audit to classify requirements as orphaned from verification evidence.
- Kiota regeneration remained blocked, so the cloud admin WebApp used another temporary raw HTTP repository.
- Authenticated live-data browser UAT still needs an Aspire/API session with representative server-admin data.
- The milestone CLI used UTC date output, requiring manual local-date normalization during closeout.

### Patterns Established

- Provider credentials are persisted as token references only; resolved token values remain server-side configuration.
- Hetzner provider access is exposed through read-only Verify/Get methods until a later provisioning milestone explicitly adds mutations.
- Metadata sync replaces cache rows transactionally and preserves the last successful cache on failure.
- Planned resources use nullable provider ids and explicit states so dry-run inventory never implies real cloud allocation.
- Estimates snapshot metadata and assumptions at creation time rather than recomputing against mutable provider cache rows.

### Key Lessons

1. Verification artifacts need to be generated during each phase, not reconstructed at milestone close.
2. Dry-run planning is a useful forcing function for provider, inventory, cost, and admin contracts before real provisioning.
3. Generated-client regeneration should be planned as a first-class dependency for WebApp-facing API expansion.
4. Live browser UAT needs representative authenticated data and local infrastructure availability, otherwise it becomes predictable closeout debt.

### Cost Observations

- Model mix: not measured.
- Sessions: not measured.
- Notable: v1.1 still required path-limited commits because unrelated local `.planning/config.json` and `docs/` changes were present.

---

## Milestone: v1.2 - Real Hetzner Provisioning & Cloud Worker Bootstrap

**Shipped:** 2026-06-17
**Phases:** 6 | **Plans:** 16 | **Tasks:** 13

### What Was Built

- Explicit Hetzner create/delete provider boundaries for servers, firewalls, floating IPs, volumes, and detach/delete operations.
- Durable provisioning and teardown services that persist provider ids, endpoint data, billing owner snapshots, cleanup-needed state, deletion timestamps, and sanitized audit records.
- Hash-only cloud worker bootstrap credentials, one-time credential consumption, cloud-init user data, and provisioning integration without exposing provider or worker secrets.
- Cloud worker registration, worker credential hashing, heartbeat/capability reporting, local/cloud worker distinction, and restart-safe persisted cloud worker state.
- Admin and server-owner cloud status projections for sessions, resources, bootstrap credentials, worker heartbeat, cleanup/orphan risk, endpoint data, and retryable actions.
- Regenerated Kiota WebApp client paths for cloud admin and selected-server cloud status, replacing the temporary raw cloud HTTP bridge.
- Package advisory and warning disposition work that removed high/moderate advisories and named nullable warning groups while documenting accepted XML documentation warning debt.

### What Worked

- Keeping provider mutations behind explicit method names made the transition from dry-run to real provisioning easier to audit.
- Persisting ownership snapshots and cleanup-needed state before cloud runtime work kept cost attribution intact during partial failures.
- Hash-only credential storage and DTO redaction tests gave the worker bootstrap path a clear security boundary.
- Using generated Kiota clients after live OpenAPI capture closed a known WebApp integration debt without broad UI rewrites.
- Recording OPS-02 as an accepted future UAT gate avoided fabricating evidence that requires operator credentials and billable provider approval.

### What Was Inefficient

- Milestone audit and closeout had to revisit artifact metadata because Phase 12.2 summaries used `affects` rather than `requirements_completed`.
- Live/authenticated UAT could not be completed autonomously, so the session needed explicit operator-gate handling.
- Build/AppHost file locks recurred during verification and required process cleanup before final build passes.
- Some remediation phases still lack uniform Nyquist `VALIDATION.md` files even though their verification evidence is complete.

### Patterns Established

- Provider resource creation is explicit, owner-attributed, and preflighted before any mutating Hetzner call.
- Deletion, not power-off, is the cloud cost-stop boundary.
- Cloud worker credentials are one-time or hash-only after issuance; raw material is returned only through the intended bootstrap/registration channel.
- Admin and owner visibility should project combined cloud state from persisted records rather than reading provider state directly.
- Operator-gated live UAT should be tracked as future work when it needs credentials, real provider approval, and cleanup evidence.

### Key Lessons

1. Live provider UAT must be planned as an operator-run activity with token reference, budget, cleanup window, and final provider-side zero-billable-resource evidence.
2. Summary frontmatter needs to follow the GSD extraction contract or milestone audits require manual cross-checks.
3. Bootstrap and registration can ship before cloud runtime if the API stores enough durable worker/session state for later placement.
4. Generated-client regeneration is safer after live OpenAPI capture, but should include compatibility planning for renamed models and verb corrections.

### Cost Observations

- Model mix: not measured.
- Sessions: not measured.
- Notable: The milestone intentionally avoided autonomous billable Hetzner resource creation.

---

## Milestone: v1.3 - Cloud Arma 3 Runtime Execution

**Shipped:** 2026-06-17
**Phases:** 4 | **Plans:** 12 | **Tasks:** 0 reported by archive tooling

### What Was Built

- Worker-facing lifecycle job claiming, bounded execution snapshots, cloud-session/game-server claim scoping, and credential-authenticated progress/log/success/retry/failure reporting.
- Standalone cloud worker runtime with bootstrap config loading, local credential storage, registration/resume, heartbeat, claim polling, and Arma 3 job execution.
- Worker-side SteamCMD and DepotDownloader validation/command construction, Arma 3 server/basic/BattlEye configuration rendering, workshop snapshot handling, and lowercased mod content preparation.
- Native Arma 3 process start, stop, restart, observation, persisted runtime state, pid/log offset tracking, restart recovery, and sanitized runtime log upload.
- Server-scoped runtime status and cleanup APIs plus WebApp selected-server visibility for readiness, endpoint, heartbeat, active job, progress, recent log, sanitized failure, retry state, and cleanup actions.
- Worker-local runtime cleanup that stops owned processes, uploads remaining logs, deletes only guarded runtime files, and leaves provider teardown/cost-stop state untouched.

### What Worked

- Carrying the v1.2 worker/session/billing state into worker claim snapshots kept cloud runtime execution scoped without giving workers database access.
- Keeping `sources/CloudWorker` independent of the API project preserved a clean agent/control-plane boundary.
- Fake command/process runner abstractions made Steam tool and native process behavior testable without real credentials or installed game files.
- Reusing lifecycle jobs, worker reports, game-server logs, lifetime events, and update streams kept runtime visibility consistent with the local-worker model.
- The final Phase 16 review caught repeated cleanup visibility after successful cleanup before milestone close.

### What Was Inefficient

- No v1.3 phase produced `VALIDATION.md`, so milestone audit had to rely on verification files plus summary frontmatter.
- Summary frontmatter uses `requirements` instead of the workflow's expected `requirements_completed`, requiring manual requirement cross-check during audit.
- Live Steam/Arma, authenticated browser, and live Hetzner UAT remain impossible to complete autonomously without operator credentials and cleanup approval.

### Patterns Established

- Cloud workers claim durable jobs through API-projected execution snapshots, not direct database access.
- Worker-side runtime cleanup is a local filesystem/process operation and must not be treated as provider teardown or cost stop.
- Runtime action visibility should be computed server-side from persisted state, active jobs, worker heartbeat, and cleanup history.
- Steam/provider/worker credential material stays out of DTOs, logs, audits, job errors, and generated clients.

### Key Lessons

1. A bounded worker snapshot is enough to execute Arma 3 runtime work while preserving the API as the control plane.
2. Runtime cleanup and provider teardown must remain separate concepts because only provider deletion stops Hetzner billing.
3. Live provider and Steam UAT should be planned as a dedicated operator-run milestone with credentials, budget, cleanup, and evidence capture.
4. Verification metadata needs to match the workflow extraction contract, or audits become manual even when implementation coverage is strong.

### Cost Observations

- Model mix: not measured.
- Sessions: not measured.
- Notable: v1.3 did not autonomously create billable Hetzner resources or run Steam tool operations.

---

## Milestone: v1.5 - Spending Policy And Automated Shutdown

**Shipped:** 2026-06-18
**Phases:** 4 | **Plans:** 13 | **Tasks:** tracked in phase summaries

### What Was Built

- Durable spending-policy configuration, effective lookup, dry evaluation, persisted owner-attributed decisions, and sanitized risk/evidence snapshots.
- Billable provisioning, lifecycle, teardown, and future resize gate integration with visible allow/warn/approval-required/deny outcomes.
- Durable approval requests, approval/denial records, scoped exception checks, expiration handling, audit trails, and authorization-sensitive APIs.
- Idempotent automatic policy action attempts that enqueue lifecycle stop/cleanup or explicit provider teardown jobs only when configured and safe.
- Admin and server-owner policy visibility for effective policy, thresholds, pending approvals, denials, exceptions, automatic actions, and next-step guidance.
- Focused verification for persistence, threshold math, stale-data risk, approval expiry, authorization, automatic-action enqueueing, idempotency, visibility, and redaction.

### What Worked

- Building policy after v1.4 cost snapshots and reconciliation findings kept threshold enforcement grounded in existing owner/cost/risk data.
- Keeping policy evaluation dry and persisted made retries, approval gates, and visibility deterministic across API restarts.
- Explicit separation between runtime stop, runtime cleanup, and provider teardown preserved the billing cost-stop boundary.
- Server-side visibility projection kept policy state, authorization checks, and redaction in the API instead of adding a new raw WebApp HTTP path.

### What Was Inefficient

- Kiota regeneration was attempted but blocked by an unavailable local OpenAPI endpoint, so generated-pattern request builders and models were added manually.
- The broad `CloudSpendingPolicy` test filter still includes three known legacy evaluator failures outside the Phase 24 visibility scope.
- v1.5 Nyquist metadata is partial even though each phase has passed functional verification evidence.
- Authenticated browser, live Hetzner, and live Steam/Arma UAT still require operator credentials, budget, provider approval, and cleanup evidence.

### Patterns Established

- Spending policy decisions snapshot owner, cost, reconciliation risk, action scope, and sanitized inputs before any billable mutation proceeds.
- Warning acknowledgement and approval exceptions are durable, scoped, expiring controls rather than transient request flags.
- Automatic policy actions write durable attempts first, then enqueue lifecycle/provider jobs through the established job boundaries.
- Admin and owner policy views should be composed server-side from policy, cost, reconciliation, approval, and automatic-action records.

### Key Lessons

1. Cost policy is safer when it is layered after running-cost and reconciliation visibility instead of trying to infer resource truth from provider calls during enforcement.
2. Automatic shutdown features need explicit action semantics because runtime stop, worker-local cleanup, and provider teardown have different billing and audit meanings.
3. Visibility requirements should be implemented with the same generated-client/repository conventions as the rest of the WebApp, even when regeneration is blocked.
4. Operator-gated live UAT should remain a non-claim until credentials, budget, provider approval, runtime data, and cleanup evidence exist.

### Cost Observations

- Model mix: not measured.
- Sessions: not measured.
- Notable: v1.5 avoided autonomous live provider/runtime actions and used internal estimated costs plus reconciliation findings for guardrails, not invoice-grade billing.

---

## Milestone: v1.6 - Invoice Reconciliation And Cost Adjustments

**Shipped:** 2026-06-18
**Phases:** 4 | **Plans:** 12 | **Tasks:** tracked in phase summaries

### What Was Built

- Durable billing import batches, normalized Hetzner line items, duplicate detection, bounded diagnostics, admin-only API boundaries, and redaction coverage.
- Provider billing match runs and review records that connect imported rows to cloud inventory, sessions, billing assignments, cost snapshots, owner intervals, and discrepancy evidence.
- Immutable adjustment overlays with actor, status, reason, owner/resource/session scope, audit history, and preserved source import/match evidence.
- Reconciled and adjusted cost projections for admin and server-owner visibility without mutating raw imports, match records, or estimated snapshots.
- Generated billing import, adjustment, and match Kiota client surfaces plus repository-facade match operations.

### What Worked

- Building reconciliation after ownership, cost snapshots, provider findings, and spending policy kept provider-derived billing evidence tied to existing durable context.
- Keeping adjustments as immutable overlays preserved auditability and avoided rewriting raw provider evidence or estimates.
- Repository-contained generated-client use closed VIS-21 without adding a page-level raw HTTP bridge.
- Focused CloudBilling and cost visibility tests provided a tight verification slice despite known broad-suite failures outside v1.6 scope.

### What Was Inefficient

- The first Kiota refresh attempt through Aspire did not expose the API OpenAPI endpoint; a direct API plus temporary PostgreSQL startup was needed.
- Full Kiota generation introduced unrelated older type-name churn, so the committed refresh had to be scoped to billing surfaces.
- No v1.6 phase produced `VALIDATION.md`, so milestone audit recorded Nyquist metadata debt even though functional verification passed.
- Authenticated browser, live provider billing, and live Steam/Arma UAT remain operator-gated.

### Patterns Established

- Billing imports, match records, adjustments, and cost projections keep raw evidence immutable and apply corrections as read-side overlays.
- Provider-derived billing rows should reconcile against historical billing assignment intervals, not only current owners.
- Generated-client refreshes can be scoped when full regeneration would create unrelated churn, but the scope and rationale should be documented.
- Server-owner cost visibility must filter reconciled provider evidence by the game-server scope the user is allowed to inspect.

### Key Lessons

1. Invoice/export reconciliation should be layered after durable cost ownership and policy controls, not before them.
2. Adjustment workflows need source evidence, owner interval history, and audit records in the same projection to be reviewable.
3. Kiota regeneration should have a documented local OpenAPI recipe using disabled external integrations and temporary database setup.
4. Missing validation metadata is still debt even when focused functional verification is strong.

### Cost Observations

- Model mix: not measured.
- Sessions: not measured.
- Notable: v1.6 did not perform live provider billing/UAT; it used persisted import fixtures and focused automated verification.

---

## Milestone: v1.7 - Operational UAT And Evidence Hardening

**Shipped:** 2026-06-19
**Phases:** 3 completed plus 1 deferred | **Plans:** 9 completed

### What Was Built

- Current v1.7 live UAT evidence artifact covering approval, authenticated access, provider metadata, dry-run/cost/policy/reconciliation readiness, live execution, runtime, cleanup, redaction, and final classification.
- Runbook link from `docs/operations/hetzner-live-uat.md` to the v1.7 evidence artifact.
- Explicit operator-skipped UAT disposition for Phase 29 approval/auth/provider readiness checks without claiming OPS-03 passed.
- Validation metadata backfill for v1.6 phases 25-28, v1.3 phases 13-16, and v1.0 phases 02-04.
- Scoped no-UAT-run reliability verification showing no unauthorized provider mutation, no fabricated UAT fix, and no product source expansion.

### What Worked

- The evidence artifact made the approval, auth, provider, billing-owner, dry-run, cost, policy, reconciliation, teardown, and redaction prerequisites explicit before any live provider mutation.
- The milestone preserved truth by recording skipped/deferred UAT as non-passing evidence instead of turning operator gaps into a false pass.
- Validation backfill improved cross-milestone auditability without rewriting already-shipped requirement truth.
- The final artifact scanner was cleared only after UAT state was resolved as skipped/deferred, keeping milestone close mechanics honest.

### What Was Inefficient

- Phase 30 stayed in the roadmap as a planned live execution phase but could not run without operator approval and credentials, forcing a forced archive path.
- GSD's milestone CLI has a force guard internally but the public command parser does not expose `--force`, so completion needed a direct library call.
- Strict per-phase `VALIDATION.md` remains partial for Phase 29 and absent for Phase 32; Phase 31 uses an index artifact instead.
- Live provider/runtime UAT is still deferred, so this milestone closed operational scaffolding and metadata rather than the end-to-end live evidence loop.

### Patterns Established

- Live provider mutation remains blocked unless `.planning/v1.7-LIVE-UAT-EVIDENCE.md` reaches `ready-for-live-execution` with safe operator evidence.
- Skipped UAT should be terminal in workflow state but non-passing in requirement truth.
- Backfilled validation metadata should classify partial/deferred live evidence explicitly instead of changing shipped facts.

### Key Lessons

1. Operator-gated UAT should be modeled as its own approved execution window with credentials, budget, representative sessions, and cleanup evidence ready before a phase starts.
2. A skipped UAT decision needs to clear workflow waiting state while still leaving affected requirements unchecked.
3. Milestone tools need a first-class partial/deferred close path for accepted live-operator gaps.
4. Validation metadata backfill is useful, but generating strict `VALIDATION.md` during each phase is still cleaner.

### Cost Observations

- Model mix: not measured.
- Sessions: not measured.
- Notable: v1.7 avoided provider cost exposure because no live Hetzner or Steam/Arma mutation was attempted.

---

## Milestone: v1.8 - Cloud Server Creation Experience

**Shipped:** 2026-06-21
**Phases:** 7 | **Plans:** 26 | **Tasks:** 48 reported by archive tooling

### What Was Built

- Guided `/servers` cloud entry for creating or selecting an Arma 3 game server, choosing an active billing owner, entering cloud planning inputs, and validating readiness before billable action.
- Generated-client/repository-backed cloud workflow results with allowlisted projection models, policy continuation identity, and fail-closed redaction guards.
- Durable non-mutating Arma 3 dry-run review state, safe input summaries, regeneration/supersession handling, stale-plan blockers, and sanitized plan review UI.
- Policy gate and approval UX for allow, warning, approval-required, and denied outcomes without bypassing scope, expiration, or decision identity.
- Server-scoped provisioning, runtime operation, retry/cleanup visibility, explicit provider teardown, and clear cost-stop semantics.
- Selected-server cost responsibility history and sanitized audit context for authorized server-scoped readers.
- v1.8 evidence matrix and Phase 30 handoff checklist that preserve live Hetzner, Steam, Arma, authenticated browser, and provider billable-resource verification as deferred.

### What Worked

- Building the `/servers` workflow on top of existing provider, policy, lifecycle, cost, and reconciliation foundations avoided reimplementing the control plane.
- Repository facades and source-boundary tests kept generated DTOs, raw payloads, and secret-bearing values out of Razor-facing UI.
- Keeping dry-run review non-mutating made plan review, policy checks, and stale/superseded blockers testable without provider cost exposure.
- Explicit provider teardown copy kept runtime cleanup and provider cost-stop semantics separate.
- Documentation-boundary tests prevented accidental live UAT pass claims.

### What Was Inefficient

- The milestone had repeated generated-client and repository-boundary work because several API surfaces were added in narrow slices.
- Some verification artifacts remain `human_needed` because authenticated browser and live provider/runtime checks still need operator credentials and budget.
- No `v1.8-MILESTONE-AUDIT.md` existed at close, so completion proceeded with acknowledged verification gaps rather than a passed milestone audit.
- The milestone close entry generated by tooling required manual cleanup because summary extraction included review-finding text.

### Patterns Established

- Server-owner cloud actions should be driven from `/servers`; `/servers/cloud` remains the provider/fleet administration console.
- Review state and continuation identity should be server-generated and validated before any provider mutation path can consume them.
- User-visible cloud workflow results should be allowlisted and repository-projected even when generated clients are available.
- Live UAT handoff artifacts should be test-guarded when false pass claims would create operational risk.

### Key Lessons

1. The guided frontend path is most effective when it composes existing durable control-plane services instead of adding parallel browser logic.
2. Dry-run review, policy gates, and provider teardown need separate UI states because they represent different risk and billing boundaries.
3. Source-boundary tests are useful for preventing raw secret fields, commercial billing language, and live-UAT pass claims from creeping into UI or evidence docs.
4. A milestone audit should be generated before close; otherwise accepted `human_needed` evidence needs explicit deferred-item records.

### Cost Observations

- Model mix: not measured.
- Sessions: not measured.
- Notable: v1.8 did not autonomously run live Hetzner, Steam, or Arma operations; provider mutation/live UAT remains a separate operator-approved evidence pass.

---

## Milestone: v1.9 - Cloud Server Templates And Runtime Profiles

**Shipped:** 2026-06-22
**Phases:** 4 | **Plans:** 12 | **Tasks:** 8 reported by archive tooling

### What Was Built

- Durable, versioned cloud server templates with admin-only management APIs, audit history, validation, and safe projections.
- Reusable Arma 3 runtime profiles and mission references with compatibility validation and allowlisted dry-run and worker snapshots.
- Template/profile-aware guided planning defaults, dry-run review blockers, provider provisioning preflight checks, and context projection through cost, policy, reconciliation, ownership, and audit surfaces.
- Generated WebApp client refresh plus repository facades for template/profile APIs and selection context.
- `/servers/cloud` admin configuration surfaces and `/servers` template/profile selection UX without raw HTTP bridges or generated DTO binding.
- Explicit milestone evidence that live UAT, live Hetzner, Steam/Arma, authenticated browser, teardown, cleanup, and Phase 30 classification remain deferred.

### What Worked

- Treating templates and runtime profiles as versioned snapshots preserved historical dry-run/provisioning context when definitions are disabled or superseded.
- Enforcing stale, disabled, mismatched, and mutated snapshot blockers before provider token resolution kept provider mutation ordering auditable.
- Repository boundary tests again worked well for keeping Razor bound to hand-written safe models instead of generated transport DTOs.
- Carrying selection context through cost, policy, reconciliation, and audit projections avoided a later visibility retrofit.

### What Was Inefficient

- v1.9 initially started as a live-UAT milestone, then had to be realigned after the operator directive to skip UAT until CR-0001 is otherwise complete.
- Kiota refresh created broad route/model churn because several cloud workflow surfaces had accumulated since the previous client generation.
- No `v1.9-MILESTONE-AUDIT.md` existed at close, so the remaining Phase 40 `human_needed` verification gap was accepted as explicitly deferred rather than audit-passed.
- Some phase summaries use different structures, so archive tooling undercounted accomplishments and required manual cleanup.

### Patterns Established

- Template and runtime profile choices are persisted as versioned snapshots and validated before provider mutation.
- Provider provisioning must reject stale or disabled template/profile snapshots before resolving provider tokens.
- WebApp cloud configuration and creation flows should consume generated clients through repository facades and safe page models only.
- Live-UAT guardrails can be parked as evidence scaffolding without authorizing live provider mutation or pass claims.

### Key Lessons

1. When product scope changes from UAT to implementation, planning artifacts should be realigned before new phases start to avoid stale audit/open-artifact noise.
2. Versioned administrator definitions are safer than mutable references for cloud provisioning because billing, policy, reconciliation, and audit records need stable historical context.
3. Selection context should be threaded through existing visibility projections in the same milestone that introduces the selectable entities.
4. Milestone audits should be generated before close, even when known live-UAT gaps are intentionally deferred.

### Cost Observations

- Model mix: not measured.
- Sessions: not measured.
- Notable: v1.9 did not autonomously run live Hetzner, Steam, or Arma operations; live UAT remains deferred until CR-0001 implementation is otherwise complete.

---

## Milestone: v1.12 - Server UX Rework And Generic Game Hosting

**Shipped:** 2026-11-18 (override closeout; implementation finished 2026-07-05)
**Phases:** 6 | **Plans:** 20

### What Was Built

- Generic game-hosting capability contracts and game-type-selected runtime/template/plan snapshots.
- A guided server creation journey and a unified selected-server workspace.
- Durable lifecycle status reasons, log freshness, and cost-safe next-action feedback.
- Fake-state catalog, mock-backed browser walkthrough, and redaction guardrails.

### What Worked

- Mock-backed fake states verified UX without live provider mutation.

### What Was Inefficient

- The milestone sat unclosed for months while substantial work continued outside GSD, which made the
  verification stale and left v1.11 parked indefinitely.

### Key Lessons

1. Close a milestone promptly after its audit; parked milestones and long approval gaps turn into reset work.
2. When work moves outside GSD, either capture it as quick tasks or reset planning deliberately, as done here.

### Cost Observations

- Model mix: not measured.
- Sessions: not measured.
- Notable: v1.11 was closed as superseded at the same time; planning restarted from the current codebase map.

---

## Milestone: v2.1 - Server Experience Reimagined

**Shipped:** 2026-11-26
**Phases:** 11 (61-71) | **Plans:** 201

### What Was Built
- One shared server state model, one cost confirmation, React-owned en/de locales, and a capability parity matrix guarded by tests.
- A live, claim-scoped fleet; a deep-linkable workspace with cost strip, job progress and activity timeline.
- Capability-driven guided creation and a staged change flow (mods, configuration, credentials, files) with review and leave guard.
- Accrued cost and alerts (in-app, Discord, email), worker metrics, auto-stop and schedules as durable control-plane jobs.
- Consistency and accessibility pass with an audited screen registry, drawn form controls and collapsible Settings panels.
- Per-server access grants and cost-owner transfer in React (gap-closure Phase 71 from the milestone audit).

### What Worked
- Wave-based worktree execution with cross-plan findings (XPF ids) handed from each executor to later plans kept 201 plans coherent.
- Strict mocked-API Playwright specs with an empty `session.unexpected` caught missing mocks and real defects (layout shift, listbox children).
- The milestone audit's integration check found consumer-less endpoints that every phase verification had missed.
- Code review with `--fix --all --auto` caught regressions introduced by earlier fix rounds (iteration 3 found an iteration 2 regression).

### What Was Inefficient
- `npm run ci` cannot run on the Windows CRLF checkout (`format:check`); every gate had to run its steps one by one.
- The full Vitest and e2e gates exceed the 10 minute foreground limit and had to run in the background.
- Leftover shells holding executor worktrees as their working directory blocked worktree removal.
- A fix that replaced a literal with a `cref` silently changed the OpenAPI snapshot; the snapshot check was not part of the fixer's checks.
- Removing an endpoint after verification marked every phase's verification stale, forcing an override closeout.

### Patterns Established
- Disabled-with-reason controls with visible reasons and reserved reason space so nothing shifts when a precondition changes.
- One serial mutation scope per server with single-pair optimistic rollback and exact query invalidations.
- A hook-level settle step after every access mutation that handles lost access even after the panel unmounts.
- Milestone-end batching of real-environment, real-device, screen-reader and visual-baseline checks recorded as "Deferred follow-up" skips.

### Key Lessons
1. Run the OpenAPI export comparison in every gate that can touch controller XML docs, not only in CI.
2. Make API removals before the final phase verification, or expect a stale-verification override at close.
3. Code-review fix loops need the re-review to look specifically for regressions of earlier fixes.
4. Integration audits should list every endpoint without a client consumer early, not at milestone end.

### Cost Observations
- Model mix: orchestration on Opus; executors, reviewers, fixers, verifiers and auditors on Sonnet
- Sessions: multiple long autonomous sessions with context compaction
- Notable: the milestone-end human pass was deferred by policy, so the milestone closed without the VER-03 visual baselines

## Cross-Milestone Trends

### Process Evolution

| Milestone | Sessions | Phases | Key Change |
|-----------|----------|--------|------------|
| v1.0 | n/a | 4 | Established durable lifecycle, worker, billing, audit, and visibility foundation before cloud provisioning. |
| v1.1 | n/a | 4 | Added Hetzner dry-run provider, metadata, inventory, estimate, and admin visibility foundation without real cloud mutations. |
| v1.2 | n/a | 6 | Added real Hetzner provisioning, teardown, bootstrap, registration, visibility, and generated cloud WebApp clients while deferring live UAT. |
| v1.3 | n/a | 4 | Added cloud-worker Arma 3 runtime execution, recovery, visibility, and worker-local cleanup while keeping live UAT operator-gated. |
| v1.4 | n/a | 4 | Added running cost snapshots, accumulated cost, provider reconciliation, safe labels, and cost/reconciliation visibility. |
| v1.5 | n/a | 4 | Added spending policy enforcement, approval workflow, automatic actions, and policy visibility while keeping invoice-grade billing out of scope. |
| v1.6 | n/a | 4 | Added invoice/export billing import, matching, adjustment overlays, reconciled/adjusted cost visibility, and generated billing clients. |
| v1.7 | n/a | 3 completed plus 1 deferred | Added live UAT evidence scaffolding, validation metadata backfill, and no-UAT-run blocker classification while preserving deferred live evidence truth. |
| v1.8 | n/a | 7 | Added guided `/servers` cloud creation, dry-run review, policy gates, operation/teardown UX, cost responsibility history, and Phase 30 handoff. |
| v1.9 | n/a | 4 | Added cloud templates, runtime profiles, template/profile-aware provisioning gates, and WebApp admin plus creation selection flows while live UAT stayed deferred. |
| v2.1 | multiple | 11 | Rebuilt the React server section (fleet, creation, workspace, change flow), added accrued cost, metrics and schedules, a consistency and accessibility pass, and access and cost-owner UI; milestone-end human pass deferred. |

### Cumulative Quality

| Milestone | Tests | Coverage | Zero-Dep Additions |
|-----------|-------|----------|-------------------|
| v1.0 | API lifecycle/billing tests added; solution tests passed in phase verification | Not measured | Most foundation work reused existing .NET/EF/Blazor stack |
| v1.1 | Cloud tests passed (41), Lifecycle/Billing tests passed (10), full solution build passed with known advisory warning | Not measured | Provider work used HttpClient, EF Core, existing audit/authorization, and Blazor patterns |
| v1.2 | API cloud tests passed, parser tests discovered and passed, package advisory scan passed, solution build passed after AppHost lock cleanup | Not measured | Provider/runtime control used existing EF, controller, DTO, audit, Kiota, and Blazor repository patterns |
| v1.3 | Focused worker/runtime tests passed (17, 14, 19, and 29 test runs by phase), CloudWorker build passed, full solution build passed | Not measured | Runtime work reused EF lifecycle jobs, worker registry, update streams, controller authorization, and Blazor repository patterns |
| v1.4 | Focused cost/reconciliation checks passed; parser tests passed; Phase 20 API verification was blocked by environment NuGet access before compilation | Not measured | Cost/reconciliation work reused EF hosting records, provider read clients, audit, authorization, generated clients, and repository facades |
| v1.5 | Focused policy tests passed by phase; Phase 24 visibility tests passed (5); API/WebApp builds passed with existing warning debt | Not measured | Policy work reused EF, persisted lifecycle jobs, provider inventory, cost snapshots, reconciliation findings, authorization, generated clients, and repository facades |
| v1.6 | Focused CloudBilling and cost visibility tests passed (39); API/WebApp builds passed after generated billing client refresh | Not measured | Billing work reused EF hosting/cost records, provider inventory, owner assignment intervals, audit, authorization, Kiota clients, and repository facades |
| v1.7 | Phase 29 automated/documentation verification passed with UAT skipped; Phase 31 verification passed; Phase 32 verification passed for no-UAT-run scope | Not measured | Evidence and validation work reused planning artifacts, runbooks, milestone audits, and archived phase verification metadata |
| v1.8 | Phase 39 broad evidence filter passed 188 tests; focused handoff/repository/cost responsibility tests passed 31 tests; solution build passed with 0 warnings/errors | Not measured | Guided creation work reused EF hosting records, provider inventory, policy gates, lifecycle jobs, cost/reconciliation projections, Kiota clients, and repository facades |
| v1.9 | Focused template, runtime profile, planning/provisioning, WebApp boundary, full solution build, and full test suite passes are recorded in phase summaries | Not measured | Template/profile work reused EF hosting records, provider inventory, policy gates, lifecycle jobs, cost/reconciliation projections, Kiota clients, and repository facades |
| v2.1 | Vitest 221 files, 3,806 passed; dotnet test 93 + 364 + 2 + 1,794 passed; Playwright audit registry (axe, reflow, German walk) and hosting specs on chromium, mobile-390, mobile-320 | Not measured | One dev dependency added (`@axe-core/playwright`); everything else reused React, TanStack Query and existing API patterns |

### Top Lessons (Verified Across Milestones)

1. Durable ownership and audit records should precede resource-creating cloud operations.
2. Generated-client and authenticated browser UAT prerequisites should be planned before WebApp/API expansion phases.
3. Phase verification metadata must be kept current because milestone audits depend on it as a separate evidence source.
4. Live-provider UAT should be handled as an explicit operator gate when real credentials, budget, and provider-side cleanup evidence are required.
5. Worker-local runtime cleanup must stay distinct from provider teardown because only provider deletion stops billable cloud resources.
6. Spending policy enforcement should snapshot decision context before mutation so retries, approvals, automatic actions, and visibility share the same durable truth.
7. Every API change that feeds a committed contract snapshot needs the snapshot comparison in the local gate, not only in CI.
7. Provider billing reconciliation should preserve imports, matches, estimates, and adjustments as separate evidence layers.
8. Skipped UAT should clear workflow wait state but remain non-passing requirement evidence.
9. Server-owner cloud workflows should compose control-plane projections through repository facades rather than adding raw browser HTTP paths.
10. Live UAT handoff docs need test coverage when the main risk is accidentally claiming deferred provider/runtime evidence.
11. Versioned template/profile snapshots should be introduced before they become cost, policy, reconciliation, and provider-mutation inputs.
