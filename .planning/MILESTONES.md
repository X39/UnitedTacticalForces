# Project Milestones: United Tactical Forces - Cloud Game Server Orchestration

## v2.1 Server Experience Reimagined (Shipped: 2026-11-26)

**Phases completed:** 11 phases (61-71), 201 plans, 362 tasks
**Timeline:** 2026-11-18 → 2026-11-26 (9 days, 1,290 commits)
**Closeout type:** override_closeout
**Known verification overrides:** 11 newly acknowledged (every phase's verification marked stale by the post-verification removal of `GET /game-servers/{id}/actions`, which touched `GameServersController.cs` and `utf-api.json`; the change was verified by the OpenAPI snapshot comparison, 1,794 API tests, the contract tests and the legacy WebApp build), 0 carried forward
**Known deferred items at close:** 17 acknowledged (4 diagnosed debug sessions, 1 UAT file, 10 deferred-item notes, 2 todos; see STATE.md `## Deferred Items`)

**Delivered:** The React server section rebuilt so any member can start a new server or change an existing one
without help, with one unambiguous state, cost and next action everywhere.

**Key accomplishments:**
- One shared server state model and cost confirmation across fleet, workspace, profile menu and command center, with React-owned English and German locales and a capability parity matrix guarding every hosting service method (Phase 61).
- A live, claim-scoped fleet with search, filters and quick start/stop, and a deep-linkable workspace with a persistent cost strip, honest job progress and one activity timeline (Phases 62-63).
- Capability-driven guided creation with a live estimate and early cost-owner check, and a staged change flow for mods, configuration, credentials and files with review and leave guard (Phases 64-65).
- Control-plane accrued cost with in-app, Discord and email alerts, worker-reported metrics with short history, and durable auto-stop, maximum runtime and scheduled start/stop (Phases 66-68).
- A consistency, accessibility and localization pass with an audited screen registry, axe and reflow gates, drawn form controls and collapsible Settings panels (Phases 69-70).
- Per-server access grants and cost-owner transfer operable from the React workspace, closing the audit's integration finding; the redundant actions list endpoint was removed (Phase 71).

**Deferred to follow-up (milestone audit tech debt):** VER-03 visual baselines (K-18, K-19 and cost strip key
screens included), seeded-stack UAT run, human visual, screen-reader and real-device passes, and real-environment
checks (Hetzner, nginx, real worker, Discord DM); 68 WR-01 unbounded wait; legacy raw colors; missing XML docs on
older controller actions; legacy Kiota client not regenerated.

**Archives:** `milestones/v2.1-ROADMAP.md`, `milestones/v2.1-REQUIREMENTS.md`, `milestones/v2.1-MILESTONE-AUDIT.md`, `milestones/v2.1-phases/`

---

## v1.12 Server UX Rework And Generic Game Hosting (Shipped: 2026-11-18)

**Phases completed:** 6 phases, 20 plans, 42 tasks

**Key accomplishments:**
- Generic API capability projection and server-scoped cloud planning routes with Arma 3 as the first cloud-managed implementation
- Repository-backed server workspace projection with session status, owner/cost/cleanup summaries, ranked next actions, and protected no-collect-logs boundary
- Selected-server Overview now shows session status plus next actions, while persisted logs live on a dedicated Logs tab
- Provider teardown is now the explicit selected-server cost-stop action, distinct from runtime cleanup.
- Phase 58 is covered by focused source, API, and UI tests.
- Durable lifecycle status reasons and selected-server operational labels for job/runtime/worker/provider feedback
- Visible log freshness, stop-final-log capture, and Logs tab follow behavior without a manual collect action
- Cost-safe next-action routing, visible redacted diagnostics, and regression coverage for lifecycle feedback states
- Fake state catalog and mock-backed browser/source test harness
- Mock-backed browser walkthrough, fake-state matrix, and responsive evidence
- Redaction guardrails, final verification evidence, and Phase 60 closure

**Closeout type:** override_closeout. Phase 60 verification was stale at close (covered sources changed through
work done outside GSD after 2026-07-22); it was not re-run.

**Known verification overrides:** 11 newly acknowledged, 0 carried forward (2 debug sessions, 1 quick task,
1 UAT gap, 5 verification gaps, 2 deferred items; see STATE.md `## Deferred Items`).

**Accepted tech debt:** UI-60-001 mobile selected-server tab clipping; existing compiler/analyzer warning debt.

**Archived:** `.planning/milestones/v1.12-ROADMAP.md`, `v1.12-REQUIREMENTS.md`, `v1.12-MILESTONE-AUDIT.md`,
`v1.12-phases/`, `v1.12-quick/`, and stale planning context (research, sketches, UI reviews, localization audit)
under `v1.12-context/`.

**What's next:** Clean-slate restart. Significant work (CI release pipeline, containerized production deploy,
configuration profiles, Steam worker fixes, Hetzner contract fixes) landed outside GSD; the next milestone is
planned against the current codebase map, not against prior milestone requirements.

---

## v1.11 CR-0001 Operator Live Evidence Closure (Closed: 2026-11-18, superseded)

**Phases completed:** Phase 50 (operator approval and preflight classification). Phases 51-54 (live provisioning,
runtime, teardown, and final acceptance evidence) were parked and never executed.

**Closure reason:** Superseded. The live-evidence plan targeted a codebase state that has since changed
substantially outside GSD. Any live acceptance work is re-scoped in a future milestone if still wanted.

**Archived:** `.planning/milestones/v1.11-ROADMAP.md`, `v1.11-REQUIREMENTS.md`, `v1.11-phases/`.

---

## v1.10 CR-0001 Live Acceptance And Operational Closure (Shipped: 2026-06-22)

**Delivered:** Operator-gated live acceptance evidence surfaces, approval/preflight classification, controlled provisioning/runtime/teardown evidence workflows, CR-0001 acceptance matrix, and final handoff. CR-0001 final acceptance remains blocked pending operator-provided live evidence.

**Phases completed:** 45-49 (14 plans total)

**Key accomplishments:**

- Added and verified evidence workflows for approval, authenticated access, provider readiness, dry-run/template/profile readiness, cost, policy, reconciliation, and cleanup-window checks before live provider mutation.
- Added controlled live provisioning evidence surfaces that preserve owner attribution, reviewed-plan identity, provider resource context, duplicate-resource checks, cleanup risk, and secret redaction boundaries.
- Added live runtime evidence surfaces for cloud worker bootstrap, heartbeat, Steam/Arma lifecycle execution, logs, authenticated visibility, and explicit environment blocker classification.
- Added provider teardown, manual cleanup fallback, provider-side count, final billable-resource count, and redaction review evidence surfaces.
- Produced `49-CR-ACCEPTANCE-MATRIX.md`, `49-HANDOFF.md`, and `v1.10-MILESTONE-AUDIT.md` without claiming live Hetzner, Steam/Arma, authenticated browser, teardown, cleanup, Phase 30, or CR acceptance pass status.

**Stats:**

- 5 phases and 14 plans completed.
- Focused verification included handoff boundary tests, repository boundary tests, full solution build, forbidden live-pass scan, secret/session marker scan, and GSD roadmap analysis as recorded in the v1.10 audit.

**Git range:** `aa067d2 docs(v1.10): audit live acceptance gaps` is the final pre-archive milestone commit.

**Known deferred items at close:** 6 live evidence requirements remain unsatisfied: live provisioning, partial-failure/provider inspection, live worker and Steam/Arma runtime evidence, authenticated live runtime visibility, provider teardown/final billable count `0`, and full redaction review of actual evidence.

**Archived:** `.planning/milestones/v1.10-ROADMAP.md`, `.planning/milestones/v1.10-REQUIREMENTS.md`, `.planning/milestones/v1.10-MILESTONE-AUDIT.md`, and `.planning/milestones/v1.10-phases/`.

**What's next:** Start v1.11 as the operator-controlled live evidence closure pass, or keep CR-0001 final acceptance blocked by explicit operator decision.

---

## v1.9 Cloud Server Templates And Runtime Profiles (Shipped: 2026-06-22)

**Delivered:** Administrator-managed cloud server templates, reusable Arma 3 runtime profiles and mission references, template/profile-aware dry-run and provisioning gates, and WebApp admin plus `/servers` selection flows, with live UAT still deferred until CR-0001 implementation is otherwise complete.

**Phases completed:** 41-44 (12 plans total; 8 tasks reported by archive tooling)

**Key accomplishments:**

- Added durable, versioned cloud server templates with admin-only management APIs, audit history, validation, and secret-safe projections.
- Added reusable Arma 3 runtime profiles and mission references with compatibility validation and allowlisted dry-run/worker snapshots.
- Enforced template/profile snapshots through guided planning defaults, dry-run review blockers, cost/policy/reconciliation context, and provisioning preflight checks before provider token resolution.
- Refreshed generated WebApp clients and repository facades for template/profile APIs without raw browser HTTP bridges or generated DTO leakage.
- Added `/servers/cloud` admin configuration surfaces and `/servers` template/profile selection UX with focused boundary and regression verification.
- Preserved Phase 40 live-preflight guardrails as parked future evidence scaffolding without claiming live Hetzner, Steam/Arma, browser, teardown, or cleanup UAT as passed.

**Stats:**

- 352 files changed from `v1.8` to the pre-archive v1.9 closeout commit.
- 4 implementation phases, 12 plans, 8 tasks reported by archive tooling.
- Focused verification included cloud template, runtime profile, planning/provisioning, WebApp boundary, full solution build, and full test suite passes as recorded in phase summaries.

**Git range:** `aa72ba9 docs: start milestone v1.9 Live Provisioning Runtime And Cleanup Evidence` -> `ab9a2bf feat(44-03): wire template profile server creation`

**Known deferred items at close:** 1 verification gap acknowledged by operator decision: Phase 40 `40-VERIFICATION.md` remains `human_needed` for live/authenticated UAT evidence. Live UAT, live Hetzner evidence, Steam/Arma runtime proof, authenticated browser evidence, provider teardown proof, cleanup proof, and Phase 30 final classification remain deferred until CR-0001 implementation is otherwise complete. No `v1.9-MILESTONE-AUDIT.md` was present at close.

**Archived:** `.planning/milestones/v1.9-ROADMAP.md`, `.planning/milestones/v1.9-REQUIREMENTS.md`, and `.planning/milestones/v1.9-phases/`.

**What's next:** Start the next milestone with `$gsd-new-milestone`; keep Phase 30 live evidence deferred until the whole CR-0001 implementation is otherwise complete.

---

## v1.8 Cloud Server Creation Experience (Shipped: 2026-06-21)

**Delivered:** A server-oriented WebApp cloud creation and operation path that guides Arma 3 server setup from owner-scoped entry, dry-run review, policy gates, provisioning/runtime controls, provider teardown, cost responsibility history, and Phase 30 live evidence handoff without claiming deferred live UAT.

**Phases completed:** 33-39 (26 plans total, 48 tasks reported by archive tooling)

**Key accomplishments:**

- Added guided `/servers` cloud entry with Arma 3 server creation/selection, owner assignment, readiness validation, and scoped planning inputs.
- Replaced cloud workflow UI action surfaces with generated-client/repository-backed results, allowlisted projection models, and fail-closed redaction guards.
- Added durable non-mutating dry-run review state, stale/superseded/fingerprint mismatch blockers, regenerated review flows, and sanitized review UI.
- Added policy gate and approval UX for allow, warning, approval-required, and denied outcomes with exact continuation identity and durable audit context.
- Added server-scoped provisioning, runtime operation, retry/cleanup visibility, and explicit provider teardown as the cost-stop action.
- Added selected-server cost responsibility history and sanitized audit context for authorized server-scoped readers.
- Consolidated v1.8 implementation evidence and Phase 30 live UAT handoff prerequisites without claiming authenticated browser, live Hetzner, Steam, or Arma UAT as passed.

**Stats:**

- 7 phases, 26 plans, 48 tasks reported by archive tooling.
- 204 files changed from `v1.7` to the pre-archive v1.8 closeout commit.
- Focused Phase 39 evidence filter passed 188 tests; focused handoff/repository/cost responsibility tests passed 31 tests; solution build passed with 0 warnings and 0 errors as recorded in phase verification artifacts.

**Git range:** `4fd6ef1 docs: start milestone v1.8 Cloud Server Creation Experience` -> `bb7f349 docs(39): complete v1.8 implementation evidence`

**Known deferred items at close:** 4 verification gaps acknowledged by operator decision: Phase 34, Phase 37, Phase 38, and Phase 39 remain `human_needed` for authenticated browser/live provider/runtime evidence. Live UAT is now deferred until the whole CR-0001 implementation is otherwise complete. No `v1.8-MILESTONE-AUDIT.md` was present at close.

**Archived:** `.planning/milestones/v1.8-ROADMAP.md`, `.planning/milestones/v1.8-REQUIREMENTS.md`, and `.planning/milestones/v1.8-phases/`.

**What's next:** Continue CR-0001 implementation with cloud server templates, runtime profiles, and template-aware planning/provisioning. Resume Phase 30 only after the rest of CR-0001 is implemented.

---

## v1.7 Operational UAT And Evidence Hardening (Shipped: 2026-06-19)

**Delivered:** Operational evidence scaffolding, validation metadata backfill, and scoped reliability triage for the deferred CR-0001 live UAT path, with live provider/runtime execution intentionally left deferred.

**Phases completed:** 29, 31, and 32 (9 plans total). Phase 30 remains deferred.

**Key accomplishments:**

- v1.7 live UAT evidence template with approval, preflight, redaction, cleanup, and final classification gates.
- Administrator and server-owner browser preflight evidence is explicitly marked pending until real authenticated sessions are available.
- Provider, metadata, billing-owner, dry-run, cost, policy, reconciliation, and approval readiness are explicitly blocked pending operator evidence.
- Backfilled validation metadata for v1.6 phases 25-28, v1.3 phases 13-16, and v1.0 phases 02-04 without changing shipped requirement truth.
- Classified the no-UAT-run blocker state and confirmed no provider mutation or product-code expansion occurred.

**Stats:**

- 3 completed phases, 9 completed plans, and 7 tasks reported by milestone archive tooling.
- Focused verification: Phase 29 automated/documentation checks passed with UAT skipped by operator decision; Phase 31 verification passed; Phase 32 verification passed for the no-UAT-run scope.
- Milestone audit status: `gaps_found`, accepted for close with deferred live/operator evidence.

**Known gaps at close:**

- OPS-02, OPS-04, EVID-01, EVID-02, and EVID-03 remain deferred until Phase 30 live provisioning/runtime/cleanup evidence exists.
- OPS-03 remains partial: the approval/preflight artifact exists, but required operator fields and authenticated/provider evidence were skipped/deferred.
- Phase 30 has no executed plans and must not run live provider mutation until a fresh approval record, authenticated sessions, provider readiness, billing owner, dry-run plan, cost/policy/reconciliation evidence, and cleanup window are recorded.

**Archived:** `.planning/milestones/v1.7-ROADMAP.md`, `.planning/milestones/v1.7-REQUIREMENTS.md`, `.planning/milestones/v1.7-MILESTONE-AUDIT.md`, and `.planning/milestones/v1.7-phases/`.

**What's next:** Continue CR-0001 implementation; resume Phase 30 only after the rest of CR-0001 is implemented and live UAT prerequisites are explicitly approved and recorded.

---

## v1.6 Invoice Reconciliation And Cost Adjustments (Shipped: 2026-06-18)

**Delivered:** Durable provider billing import and normalization, provider-row matching and discrepancy review, immutable adjusted cost overlays, reconciled/adjusted cost visibility, and generated WebApp billing clients for invoice/export reconciliation workflows.

**Phases completed:** 25-28 (12 plans total)

**Key accomplishments:**

- Added durable Hetzner billing import batches, normalized line items, duplicate detection, bounded diagnostics, admin-only API boundaries, and redaction tests.
- Matched provider billing rows to cloud inventory, sessions, historical billing assignments, and estimated cost snapshots with confidence, review status, discrepancy amounts, and idempotent reruns.
- Added immutable billing adjustment overlays with reason, actor, status, owner/resource/session scope, audit history, and preserved source import/match evidence.
- Projected estimated, provider-reconciled, and adjusted costs into admin and server-owner visibility without mutating raw imports, match records, or original estimated snapshots.
- Generated v1.6 billing import, adjustment, and match Kiota client surfaces and routed match operations through the existing Cloud admin repository facade.
- Added focused verification for CSV fixtures, duplicate imports, matching confidence, ownership transfers, adjustment immutability, visibility projection, authorization, and secret/raw-row redaction.

**Stats:**

- 4 phases and 12 plans completed.
- Focused verification: Phase 28 CloudBilling and cost visibility tests passed (39), API build passed, and WebApp build passed after generated billing client refresh.
- Full `Api.Tests` still has four known out-of-scope failures documented in `.planning/milestones/v1.6-MILESTONE-AUDIT.md`.

**Known deferred items at close:** OPS-02 authenticated browser/live Hetzner/live Steam-Arma UAT remains operator-gated; v1.6 Nyquist validation metadata is missing but functional phase verification passed; full Kiota regeneration was scoped to billing surfaces to avoid unrelated older generated type-name churn.

**Archived:** `.planning/milestones/v1.6-ROADMAP.md`, `.planning/milestones/v1.6-REQUIREMENTS.md`, `.planning/milestones/v1.6-MILESTONE-AUDIT.md`, and `.planning/milestones/v1.6-phases/`.

**What's next:** Start the next milestone with `$gsd-new-milestone`, likely around OPS-02 live/authenticated UAT closure, stricter validation metadata, operational hardening, or future billing export/reporting extensions.

---

## v1.5 Spending Policy And Automated Shutdown (Shipped: 2026-06-18)

**Delivered:** Durable cloud spending policies, billable-action gates, scoped approvals, policy-triggered automatic shutdown/teardown attempts, and admin/server-owner policy visibility built on the v1.4 cost and reconciliation foundations.

**Phases completed:** 21-24 (13 plans total)

**Key accomplishments:**

- Added durable spending-policy configuration, provider-aware effective lookup, dry policy evaluation, owner-attributed decisions, stale-data/reconciliation risk handling, and sanitized persisted evidence.
- Gated billable provisioning, lifecycle, teardown, and future resize extension points before mutation, with visible allow/warn/approval-required/deny outcomes.
- Added a durable approval and exception workflow with scoped approval/denial records, expiration checks, authorization-sensitive APIs, and bypass tests.
- Added idempotent automatic policy action attempts that enqueue lifecycle stop/cleanup or explicit provider teardown jobs only when configured and safe.
- Surfaced effective policy, threshold usage, pending approvals, recent denials, active exceptions, automatic action history, and next-step guidance through admin and server-owner API/WebApp views.
- Added focused verification for policy persistence, threshold math, stale-risk handling, approval expiry, authorization, automatic-action idempotency, visibility projections, generated-client repository use, and redaction boundaries.

**Stats:**

- 187 files changed in the milestone slice from `4f70a88` to `628fa87`
- 34,545 insertions and 406 deletions from the v1.4 close commit through the v1.5 audit commit
- 4 phases, 13 plans; task counts are tracked in phase summaries rather than milestone archive tooling
- Focused verification: Phase 24 visibility tests passed (5), API build passed, WebApp build passed with 39 existing warnings, schema drift passed, and codebase drift reported only pre-existing root-file mapping/config warnings

**Git range:** `086e20d docs: start milestone v1.5 spending policy` -> `628fa87 docs(v1.5): add milestone audit`

**Known deferred items at close:** OPS-02 authenticated browser/live Hetzner/live Steam-Arma UAT remains operator-gated; v1.5 Nyquist metadata is partial; the broad `CloudSpendingPolicy` test filter still has three known legacy evaluator failures outside the Phase 24 visibility path.

**Archived:** `.planning/milestones/v1.5-ROADMAP.md`, `.planning/milestones/v1.5-REQUIREMENTS.md`, `.planning/milestones/v1.5-MILESTONE-AUDIT.md`, and `.planning/milestones/v1.5-phases/`.

**What's next:** Start the next milestone with `$gsd-new-milestone`, likely around OPS-02 live/authenticated UAT closure, invoice/export reconciliation, adjusted cost corrections, or operational hardening.

---

## v1.4 Running Cost Visibility And Provider Reconciliation (Shipped: 2026-06-18)

**Delivered:** Durable running-cost snapshots, accumulated historical cloud-session cost, read-only Hetzner provider reconciliation, admin/server-owner cost and reconciliation visibility, safe UTF provider labels, and verification around redaction and read-only reconciliation boundaries.

**Phases completed:** 17-20 (12 plans total)

**Key accomplishments:**

- Added cost snapshot persistence and calculation services that preserve provider resource lifetime, billing owner snapshots, metadata freshness, source labels, confidence, and deletion-as-cost-stop behavior.
- Added accumulated historical cost summaries that split cloud-session cost across billing assignment intervals instead of attributing all history to the current owner.
- Added secret-safe, read-only Hetzner reconciliation runs and findings for known inventory, live provider resources, missing resources, unknown resources, state mismatches, orphan indicators, and cleanup-needed billable resources.
- Surfaced current cost, accumulated cost, source label, reconciliation status, finding counts, orphan indicators, and cleanup risk to admins and authorized server owners through generated Kiota clients and WebApp repository facades.
- Added safe UTF-owned labels for newly provisioned Hetzner servers, floating IPs, volumes, and firewalls to improve future reconciliation without exposing credentials.
- Added focused redaction, cost math, provider pagination/failure, reconciliation-finding, authorization, and read-only safety guards.

**Stats:**

- 139 files changed in the milestone slice from `db5f3c5` to `4f70a88`
- 20,729 insertions and 323 deletions from the v1.4 start commit through the Phase 20 close commit
- 4 phases, 12 plans, tasks tracked in phase summaries rather than milestone archive tooling
- Focused verification: cost/reconciliation static checks passed, parser tests passed (89), and Phase 20 recorded that targeted API tests and API build were environment-blocked by `NU1301` socket-denied NuGet access before compilation

**Git range:** `db5f3c5 docs: start milestone v1.4 Running Cost Visibility And Provider Reconciliation` -> `4f70a88 docs(20-03): close labeling safety phase`

**Known deferred items at close:** OPS-02 authenticated browser/live Hetzner/Steam/Arma UAT remains future operator-gated evidence; invoice-grade provider billing reconciliation, adjusted cost corrections, spending policy controls, and multi-session workers remain future scope.

**Archived:** `.planning/milestones/v1.4-ROADMAP.md`, `.planning/milestones/v1.4-REQUIREMENTS.md`, and `.planning/milestones/v1.4-phases/`.

**What's next:** v1.5 Spending Policy And Automated Shutdown, using v1.4 cost snapshots and reconciliation findings as the policy input.

---

## v1.3 Cloud Arma 3 Runtime Execution (Shipped: 2026-06-17)

**Delivered:** Durable cloud-worker Arma 3 runtime execution: worker-scoped job claiming, install/configure/update support, process lifecycle control, restart recovery, persisted runtime logs/status, server-owner/admin runtime visibility, and safe worker-local cleanup without conflating runtime stop with provider cost stop.

**Phases completed:** 13-16 (12 plans total)

**Key accomplishments:**

- Added credential-authenticated worker job claiming and report endpoints with bounded execution snapshots scoped to the registered cloud session and game server.
- Added a standalone `sources/CloudWorker` project that registers/resumes from bootstrap configuration, stores worker credentials locally, heartbeats, claims jobs, and executes Arma 3 lifecycle work without depending on API internals.
- Implemented worker-side SteamCMD/DepotDownloader validation and command construction, Arma 3 configuration rendering, workshop content preparation, and secret-safe progress/log reporting.
- Implemented native Arma 3 process start, stop, restart, observation, log collection, persisted runtime state, and worker restart recovery through durable lifecycle jobs.
- Added server-scoped cloud runtime status and cleanup APIs plus WebApp selected-server visibility for readiness, endpoint, heartbeat, active job, progress, recent logs, sanitized failure, retry state, and cleanup action gating.
- Added runtime cleanup that stops owned processes, uploads remaining logs, deletes only guarded worker-local runtime files, and leaves provider teardown/cost-stop state untouched.
- Added focused API and worker tests for worker authentication, session-scoped claiming, local worker compatibility, install/configure/start/stop/log/cleanup transitions, restart recovery, authorization-sensitive paths, and secret redaction.

**Stats:**

- 104 files changed in the milestone slice from `v1.2` to `cf58fee`
- 9,116 insertions and 401 deletions from `v1.2` through the v1.3 audit commit
- 4 phases, 12 plans, 0 tasks reported by milestone archive tooling
- Focused verification: Phase 13 worker dispatch tests passed (17), Phase 14 install/config tests passed (14), Phase 15 runtime tests passed (19), Phase 16 runtime visibility/cleanup tests passed (29), CloudWorker build passed, and full solution build passed with 0 warnings/errors as recorded in phase verification files

**Git range:** `2cece6b docs: start milestone v1.3 Cloud Arma 3 Runtime Execution` -> `cf58fee docs: audit v1.3 milestone`

**Known deferred items at close:** Live Steam/Arma UAT, authenticated browser UAT, live Hetzner UAT, and uniform `VALIDATION.md` artifacts remain documented tech debt in `.planning/milestones/v1.3-MILESTONE-AUDIT.md`.

**Archived:** `.planning/milestones/v1.3-ROADMAP.md`, `.planning/milestones/v1.3-REQUIREMENTS.md`, `.planning/milestones/v1.3-MILESTONE-AUDIT.md`, and `.planning/milestones/v1.3-phases/`.

**What's next:** Start the next milestone with `$gsd-new-milestone`, likely around OPS-02/live UAT closure, provider reconciliation, running cost snapshots, or spending policy controls.

---

## v1.2 Real Hetzner Provisioning & Cloud Worker Bootstrap (Shipped: 2026-06-17)

**Delivered:** Real Hetzner provisioning and teardown control, owner-attributed cloud resource records, short-lived cloud worker bootstrap credentials, persisted cloud worker registration/heartbeat state, safe admin and owner visibility, and generated WebApp API clients for the cloud surfaces.

**Phases completed:** 9-12.2 (16 plans total)

**Key accomplishments:**

- Added explicit mutating Hetzner provider methods for creating and deleting servers, firewalls, floating IPs, and optional volumes while preserving the read-only metadata/dry-run boundary.
- Converted validated owner-linked dry-run plans into durable provider resource records with provider ids, endpoint projection, billing owner snapshots, audit records, and partial-failure cleanup-needed state.
- Added explicit cloud session teardown that treats provider deletion as the cost-stop boundary and records cleanup failure or orphan risk when billable resources remain.
- Added short-lived, hash-only cloud worker bootstrap credentials and cloud-init user data that excludes provider tokens, Steam credentials, worker hashes, and bearer material from DTOs and logs.
- Added cloud worker registration and heartbeat endpoints with persisted local/cloud worker distinction, capability reporting, restart-safe state, and safe admin/server-owner status projections.
- Regenerated the WebApp Kiota client and replaced the temporary raw cloud admin and selected-server cloud status bridge.
- Closed observed high/moderate package advisories and named nullable warning groups, with remaining forced-rebuild XML documentation warnings recorded as accepted policy debt.
- Recorded OPS-02 authenticated browser/live Hetzner UAT as deferred until the full change request is implemented across later milestones.

**Stats:**

- 394 files changed in the milestone slice from `v1.1` to `0de9586`
- 45,204 insertions and 6,108 deletions from `v1.1` through the v1.2 audit/operator-gate commit
- 6 phases, 16 plans, 13 tasks reported by milestone archive tooling
- Focused verification: cloud provisioning, teardown, inventory, worker, lifecycle, parser, package-advisory, build, and anonymous browser shell checks passed as recorded in phase verification files

**Git range:** `0de9586 docs(12.2-03): refresh audit operator gate` is the final pre-archive milestone commit on `gsd/v1.2-milestone-audit-gaps`.

**Known deferred items at close:** 1 UAT gate acknowledged; OPS-02 remains future operational UAT until the full change request is implemented across milestones.

**Archived:** `.planning/milestones/v1.2-ROADMAP.md`, `.planning/milestones/v1.2-REQUIREMENTS.md`, `.planning/milestones/v1.2-MILESTONE-AUDIT.md`, and `.planning/milestones/v1.2-phases/`.

**What's next:** Start the next milestone with `$gsd-new-milestone`, likely around cloud Arma 3 runtime execution, live-provider UAT closure, provider reconciliation, or policy/cost controls.

---

## v1.1 Hetzner Provider Dry-Run & Resource Inventory (Shipped: 2026-06-16)

**Delivered:** Secret-safe Hetzner provider configuration, read-only metadata cache, planned cloud inventory, billing-owned Arma 3 dry-run plans, cost estimate snapshots, and admin visibility without creating real provider resources.

**Phases completed:** 5-8 (12 plans total)

**Key accomplishments:**

- Secret-safe Hetzner provider configuration with admin endpoints, durable verification state, and redaction tests
- Read-only Hetzner Cloud client with typed metadata cache schema, pagination, and sanitized auth handling
- Transactional metadata sync with freshness projection, admin endpoints, and cache-preservation tests
- Durable cloud session, resource inventory, and dry-run plan storage with safe API projections
- Local-only dry-run creation service with admin API, audit record, ownership snapshots, and no provider dependency
- Explicit, auditable inventory state transitions with durable ownership and no-provider-mutation tests
- Local-only Arma 3 dry-run planning with owner enforcement, validation failures, assumptions, and inventory persistence
- Durable cost estimate snapshots with cached metadata pricing, configured assumptions, and owner attribution
- Resource-scoped dry-run generation and admin estimate audit APIs with controller-level safety tests

**Stats:**

- 193 files changed in the milestone slice from `v1.0` to `a81bff8`
- 27,380 insertions and 4,149 deletions from `v1.0` through the v1.1 audit commit
- 4 phases, 12 plans, 36 tasks
- Focused verification: Cloud tests passed (41), Lifecycle/Billing tests passed (10), full solution build passed with the known OpenTelemetry advisory warning

**Git range:** `c872716 docs: start milestone v1.1 Hetzner Provider Dry-Run` -> `a81bff8 docs: audit v1.1 milestone`

**Known deferred tech-debt items at close:** 3 (see `.planning/STATE.md` Deferred Items)

**What's next:** Start the next milestone with `/gsd-new-milestone`, likely around real provisioning, worker bootstrap, or operational hardening.

---

## v1.0 Local Worker Foundation (Shipped: 2026-06-15)

**Delivered:** Durable local-worker game-server lifecycle orchestration with persisted jobs, worker recovery, billing ownership, audit history, and user/admin visibility.

**Phases completed:** 1-4 (13 plans total)

**Key accomplishments:**

- Added persistent lifecycle jobs for configure, install, start, stop, restart, update, collect logs, and delete operations.
- Replaced API-local fire-and-forget lifecycle command paths with durable job enqueue and inspection endpoints.
- Added local worker registration, heartbeat, leases, stale-worker recovery, and execution through the existing Arma 3 game-controller abstraction.
- Added user-backed billing accounts, active billing assignment enforcement, assignment history, and immutable hosting audit records.
- Added secret-safe lifecycle failure/audit handling and focused tests around queue transitions, recovery, billing ownership, and redaction.
- Added WebApp server visibility for lifecycle jobs, active owners, worker health, stuck jobs, cleanup indicators, billing history, and audit records.

**Stats:**

- 179 files changed in the milestone slice
- 16,735 insertions and 836 deletions from v1 requirements through milestone audit
- 4 phases, 13 plans, 41 tasks
- 2026-06-15 18:11 -> 2026-06-15 20:44 local time

**Git range:** `8a50146 docs: define v1 requirements` -> `31a6eb2 docs: audit v1.0 milestone`

**Known deferred items at close:** 4 (see `.planning/STATE.md` Deferred Items)

**What's next:** Continue the broader cloud game-server orchestration change request across later milestones, starting with `$gsd-new-milestone`.

---
