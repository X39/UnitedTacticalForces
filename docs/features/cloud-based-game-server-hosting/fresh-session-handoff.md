# Fresh Session Handoff

Copy the prompt below into a fresh Codex session.

```text
We are in A:\dev\git\UnitedTacticalForces.

We are collaboratively designing a fresh spec for Cloud Based Game Server Hosting. Ignore old CR/planning files unless explicitly asked. The current spec pack lives at:

A:\dev\git\UnitedTacticalForces\docs\features\cloud-based-game-server-hosting\

Do not use GSD skills for this discussion.

Process:
- Discuss one topic at a time; do not invent the whole spec upfront.
- Help challenge and shape decisions, not just transcribe them.
- Before asking a question, explain the concrete user/admin/developer situation and why the distinction matters.
- When a decision is agreed, update the relevant spec files and decision log.
- Track assumptions, open questions, and definition-of-done items.
- Keep exact database/schema/API implementation details out of the spec unless explicitly decided.
- Do not commit changes unless explicitly asked.
- The user is exhausted by speculative questioning. Do not reopen resolved topics or ask about low-value UI wording/tuning. Derive routine fields, schemas, and configurable defaults from the established decisions. Ask only when a remaining choice materially changes product behavior or architecture and cannot safely be derived.

Terminology and communication rules:
- The platform may run deployments for many server records concurrently. The one-deployment limit is per individual server record, never platform-wide.
- Never describe workers as a fleet or pool. There is exactly one dedicated worker per expanded game-defined service node, with no ready-made workers, sharing, or reassignment. A single-node game has one worker and a fully stopped server record has none.
- A server is the durable platform record. A deployment is one runtime/provider-resource lifetime created by starting that record.
- Use Terminate for the extraordinary user action. Use cancelled by cost-stop for interrupted commands and terminating/terminated due to fault for fault states. Do not reintroduce Abort terminology.
- Arma 3 is the first implementation of game-neutral abstractions, not the product's main conceptual model.
- Distinguish linked late/unexpected resources from true dangling resources: if a resource can be linked to a provisioning intent/deployment, it is adopted and keeps that deployment's cost owner; it is not dangling.

Established server/deployment and teardown model:
- Every deployment inventories all provider/runtime resources allocated or adopted for it.
- A server record remains active while any deployment resource remains allocated or unresolved.
- The same server record cannot start another deployment until the previous deployment has no allocated or unresolved resources.
- Full stop completes only after every deployment resource is verified non-billable, normally through deletion or release.
- Persistent platform-managed server data and reusable content are outside deployment teardown.
- Teardown is durable automatic reconciliation. Users request stop once; they do not retry provider cleanup failures.
- The system retries individual outstanding resources and does not destructively process resources already verified non-billable.
- Administrators see failures, resources, attempt history, and possible continuing cost, but routine recovery is automatic.
- Provider provisioning, observation, and teardown are control-plane/provider responsibilities and do not depend on the target worker.

Dedicated worker, durable operations, and communication:
- The worker is provisioned as part of one server record's deployment, controls only that server instance, and is torn down with the deployment.
- It performs in-runtime work: game install/restore, external content, persistent-data placement, configuration generation, process control, live operations, and health reporting.
- Lifecycle requests persist asynchronous operations independent of the HTTP request/browser session. State survives API, processor, and worker restarts.
- Recovery reconciles persisted and external reality before repeating side effects and must not duplicate provider resources after a crash.
- No protocol-version negotiation or compatibility matrix is required. Worker software can be updated by restarting that deployment's dedicated worker and using normal reconciliation.
- New workers register outbound using a short-lived single-use bootstrap credential. Registration binds a non-secret deployment-bound identity and issues a revocable, scoped credential.
- Identity and credential are distinct. A scoped token is expected first, but the contract may support other secure mechanisms later.
- Credentials are limited to one deployment/permitted work, become invalid after teardown, and never reach frontend clients or unredacted logs.
- Normal operation requires no inbound management/job-control port. The worker holds an authenticated outbound targeted-command connection.
- Commands are persisted before delivery, have stable identities, target only that deployment's worker, and distinguish receipt from completion.
- On reconnect/restart the worker requests outstanding commands; persisted stages plus runtime reconciliation prevent blind duplicate effects.
- Conflicting commands for one deployment do not run concurrently.
- The worker registration/startup window is bounded and configurable by provider, game, or other deployment context. Failure to register in time is a confirmed fault and triggers fault termination/cost-stop.
- Missing heartbeats initially degrade communication health while allocation and possible cost remain. A configurable threshold makes loss of control a confirmed fault and triggers fault termination/cost-stop.
- Before destructive teardown after suspected worker failure, collect diagnostics best-effort within a configurable maximum window. Diagnostics cannot delay cost-stop indefinitely.
- Diagnostics are deployment-linked, claim-protected, secret-redacted, and retention-controlled. Continuous off-worker log replication is not required; provider loss may make logs unrecoverable.

Normal command queue:
- Accepted normal commands execute strictly in request order and are not coalesced, even when a sequence looks redundant.
- During temporary worker communication loss, commands persist as queued and the server UI visibly shows busy/pending.
- A separate per-server queue view shows queued, running, completed, failed, retried, skipped, and cancelled work plus useful failure context.
- Users cannot cancel an accepted queued command in v1.
- A failed normal command pauses later commands. A user with Server Operate can retry or skip it; any failed normal command is skippable in v1. Retry/skip is audited.
- Starting/stopping are actions, not separate privileges. Server Operate covers all currently projected non-admin Server actions and queue retry/skip. Admin/emergency powers remain separately claim-protected.
- Restarting the dedicated worker/resource preserves the durable queue and resumes it after reconnection.
- Full stop/deprovisioning closes the queue because it is no longer needed. Queued/running normal commands are cancelled by cost-stop and new runtime/game/live-file work is unavailable.
- Full cost-stop outranks all normal commands; worker cancellation or safe completion cannot block teardown indefinitely.
- Command history has a separate configurable maximum-age policy, initially one month. Cleanup applies only to completed, skipped, and cancelled entries; queued, running, and failed entries remain until resolved.

Worker restart and the per-record single-worker invariant:
- At most one worker resource may be allocated for each service identifier and ordinal, including replacement.
- A replacement worker waits until the prior worker is verified fully deprovisioned.
- Worker restart is a convenience operation composed of ordered stop then start; it is not a separate magical replacement path.
- Restart uses the same preservation synchronization before old-worker removal. If it fails, the restart fails/pauses for intervention and no replacement starts.

Persistent versus live files and preservation selections:
- File management exposes separate persistent/expected files and live-worker files. Both are viewable while active where authorized.
- Synchronization occurs only at lifecycle boundaries: expected data goes to the worker at start; selected live data returns to persistent storage before intentional stop/restart.
- There is no general apply/pull/merge synchronization action between start and stop.
- While active, selected persistent paths are read-only because stop synchronization may replace them. Unselected persistent files may be edited as expected data for the next start. Live files remain separately editable where allowed.
- Each Server record stores a game-neutral preservation selection across deployments. The game implementation seeds it once at Server creation; users with Server Modify can add/remove entries, including seeded entries. Game updates do not silently re-add removed paths.
- Server Operate uses the saved selection for Stop/restart but does not grant path inspection/editing; that requires Server Modify.
- Selection modes are exact file, flat folder/direct files, and recursive folder/all descendants. Matching is evaluated against live contents at synchronization time.
- Each selection chooses create/update-only or mirror deletions. Manual selections default to create/update-only; game-seeded selections may specify behavior.
- Most specific match controls deletion behavior: recursive folder < flat folder < exact file; within one scope, deeper paths win.
- Paths are server-relative and game-neutral. The platform must not blindly copy the entire runtime filesystem.
- Before intentional deprovisioning, ask the game implementation to save and gracefully stop. Per-server configuration controls repeated attempts and maximum wait; a stuck process is eventually force-killed.
- Synchronization begins after the process stops or the game declares files ready. It still runs after a forced kill and marks the result potentially inconsistent.
- New persistent state is published only after the full transfer succeeds. Failure retains the previous persistent state. Temporary staging is allowed, but stop sync creates no retained generation/backup history.
- Ordinary preservation transfer has no separate timeout. A failure or stall remains visible and allocated/potentially billable until resolved or Terminate is used.
- Automatic/fault/cost-enforcement/emergency stop makes only a bounded preservation attempt; preservation cannot delay cost-stop indefinitely.

Terminate:
- Terminate is always available whenever an individual server record has an active deployment, including during provisioning.
- Server Operate grants Terminate; administrators also have it through established bypass behavior.
- It requires an explicit unsaved/unsynchronized-data and full-stop warning plus a mandatory typed server-specific challenge. It is audited.
- It is terminal for the active deployment's provisioned resources: bypass the normal queue, cancel all normal work, skip graceful shutdown and preservation, discard unpublished staging, retain the durable record and last published persistent data, then immediately reconcile full provider teardown. No replacement worker is started.
- During provisioning, Terminate first persists a barrier preventing further create calls, cancels pending work where possible, then reconciles already-issued requests and cleans late-created attributable resources.
- Terminate is not complete while issued provider intent or an attributable resource remains unresolved.

Provider attribution, dangling resources, and capability degradation:
- Provisioning intent and deployment attribution are persisted before an API create call so a resource remains discoverable if the response is lost.
- A late/untracked/duplicate/unexpected resource linked to intent is adopted into its deployment, retains the deployment cost owner, and is cleaned up if unexpected or terminating.
- A scheduled provider-neutral scan examines every configured provider scope independently of active jobs.
- A true dangling resource is recognizable as platform-managed but cannot be linked to a valid intent, deployment, or cost owner. It is automatically terminated and accounted as platform/Verein operational cost, never charged to a user or unrelated deployment.
- Unknown external/unattributed resources are ignored in v1. Marking/classifying external resources is deferred. Provider resource groups/projects should isolate platform resources where available.
- One-off successfully cleaned dangling incidents remain in history without immediate alert. Cleanup failure, prolonged possible cost, or repetition within a configurable window alerts admins. Repetition indicates a severe provisioning/reconciliation bug even when cleanup succeeds.
- Dangling-incident retention is separately configurable, default one year. Active/unresolved incidents never age out; financial/audit evidence follows its own governance.
- Every provider adapter must reliably attribute and later discover every cost-generating resource type. A provider structurally unable to do this is always unusable for provisioning; starts are blocked with an administrative reason and there is no unsafe override.
- Temporary degradation of a normally capable provider delays provider-dependent stages. New starts may wait before provisioning; in-progress provisioning/teardown waits at the affected stage. Provider-independent work proceeds only if safe.
- Stop/Terminate cannot falsely complete while non-billable verification is unavailable; possible cost remains visible. On recovery, reconcile observed reality before resuming.
- Provider observation uncertainty alone does not trigger teardown. Preserve last-known allocation/cost state, keep checking, and escalate prolonged/repeated uncertainty after a configurable threshold.
- Confirmed absence, shutdown, or irrecoverable failure of a required resource is a fault and triggers fault termination/cost-stop; missing infrastructure is not automatically recreated.

Delayed-start revalidation:
- Before the first cost-generating provider action after a delay, revalidate current execute authorization, cost owner, credit/allowance/safety, server eligibility, launch-disabled state, and provider capability.
- A transient provider-capability issue keeps the operation waiting.
- A non-transient authorization/business failure, such as insufficient credit or revoked execute access, terminally fails that start without creating resources, records the reason, and never auto-resumes. A new authorized start request is required after correction.

History/manual resolution:
- Detailed completed-deployment history has configurable retention, default one year; cleanup retains a minimal timeline summary whose retention is independently configurable.
- Active/unresolved deployments and separately governed financial/audit evidence are never removed by deployment-history cleanup.
- Manual resolution of unverifiable cleanup is a dangerous dedicated-claim exception for one identified resource. It warns that no cloud resource is deleted and cost may continue, requires an explanation and typed resource-specific challenge, and records actor/time/provider identity/last state/explanation/optional evidence.
- History must distinguish manual assertion from provider-verified cleanup.

Repository state at handoff:
- The entire docs/features/ directory is untracked in git but valuable; do not discard it.
- .idea/.idea.X39.UnitedTacticalForces/.idea/dataSources.xml has an unrelated user modification; preserve it.
- No services or background processes were started.
- No commit was made.

Most recently resolved topic:
A start delayed by temporary provider degradation uses the server record's current expected configuration, modpack and Scenario selections, and persistent expected files when provisioning can proceed. It does not retain a request-time content snapshot. Changes made during the wait are intentionally included, and this edge case does not require special conflict handling, version-selection UX, or another confirmation.

Create is separate from Start and never implicitly launches anything. Create persists the durable server record. Start and Stop are required well-known actions supplied by every game implementation alongside any custom actions. Start provisions the dedicated worker, prepares the server, and invokes the game's start behavior; Stop is the ordinary full cost-stop lifecycle and invokes the game's stop behavior during graceful shutdown before preservation and teardown.

A server can expose multiple concurrent game-specific operational states. Before the game server starts, applicable preparation/runtime states come from the dedicated worker; afterward, game-server states come from the game integration through the worker. Arma 3 may expose states such as mission selection and running session that other games do not have. Platform lifecycle, provider, communication, and cost facts remain available alongside these states.

Modpack revisions immutably declare membership, roles, configuration, and selected sources, but source reproducibility is separate. Steam Workshop is a floating latest-only source in practice: Start resolves the latest content Steam currently exposes, even for a pinned modpack revision. The deployment records the observed source/version metadata but cannot promise to retrieve the same historical bytes later. Running deployments keep their already resolved content until a later Start.

A Mod has exactly one source, either Steam Workshop downloaded through DepotDownloader or platform-managed file-based content, and exactly one usage role: client-only, server-only, or shared. A Modpack can contain multiple Mods and other Modpacks. File-based content belongs to the Mod rather than being copied into every containing Modpack.

Nested Modpacks are flattened recursively into the effective Mod set, with duplicate Mods removed.

Modpack and terrain read/access claims are administrative Command Center visibility, not user-consumption authorization. Any scoped Modpack permission unlocks the Command Center and shows that Modpack in its management list; a generalized permission shows all. Event/server selection, runtime consumption, and Modpack download do not depend on these management-visibility claims, and consuming a Modpack does not grant management access to it.

ManageAccess is separate from Modify. Modify permits resource content/configuration changes but cannot delegate permissions. ManageAccess permits scoped grants and revocations for users or roles, subject to allowed claim types. Full/generalized access includes both.

Creating a Server, Modpack, or Scenario grants the creator explicit full scoped access, including Modify and ManageAccess. CreatedBy is separate audit/accountability provenance so administrators know who introduced the resource and whom they can contact; it is not authorization-bearing ownership or transferable responsibility.

When credit exhaustion triggers cost-stop, the default policy continues charging unresolved teardown cost to the deployment's original cost owner, even beyond credit and allowance. Accounting must be prepared to switch to a policy where the Verein absorbs cost incurred after the persisted enforcement boundary. This is one installation-wide, explicitly controlled setting intended for recurring systemic overruns, not a per-owner/server choice or automatic switch. Administrators need aggregate overrun frequency/cost visibility. The switch changes ledger attribution only, not deployment/resource attribution or teardown behavior.

The credit-enforcement safety range is a configurable installation-wide minimum in EUR applied to every cost owner and server. Future context-specific policy may require more reserve but cannot reduce the effective reserve below that global minimum.

Event Quick Create creates and preconfigures an ordinary durable Server record from event context; it does not create a separate Event Server type or lifecycle. Create remains distinct from Start. The resulting short-lived Server uses all standard actions and durable worker/queue/preservation/teardown/billing rules. Event automation may invoke the ordinary Stop action at its configured time and archive the record after successful full cost-stop cleanup. Event hosting has an optional configured cost center; otherwise the event host's individual cost owner is the default payer. Standard Start revalidates the effective owner's permission and affordability before provisioning.

Every Event has a required estimated duration, which may be a best guess. It establishes an automatic Stop safety deadline that the event host can extend while the Server is active without bypassing credit/fault cost-stop rules. A game-supported condition such as all players having left can Stop earlier, and game support may emit configured staged in-game warnings before shutdown. Every trigger queues the ordinary durable Stop action. Relevant Servers appear in the profile Notification Center for authorized users; event-created Server items show the deadline and allow the host to extend it. The notification-item model should also support a future dedicated Notification Center.

Event Quick Create copies the Event's Scenario selection mode into the ordinary Server. Latest mode resolves the newest Scenario version when Start runs; pinned mode retains the explicitly selected version.

Event participants can see join information and obtain the event Modpack but receive no Server-management claims merely by participating. Planned/accepted participation is separate from actual attendance. When supported, the game server reports observed players through the worker so attendance can be recorded automatically and used for participation statistics and derived trust/reliability indicators.

Automatic attendance requires a configurable minimum connected duration based on reported connection intervals; momentary joins do not count. The event host can manually correct attendance when observation is missing or wrong, and corrections are audited with actor/time/reason/outcome.

Attendance for an unmatched external game identity is retained without affecting any UTF user's statistics. If that identity is linked later, historical attendance can be associated with the user while preserving original observation and later-link provenance.

Participation facts such as accepted, attended, accepted-but-missed, participation-rate inputs, and qualifying duration are authoritative. The platform derives a trust score from them and maps it to one user-facing trust label. The score/label are explainable projections, not manually maintained truth.

Trust scoring and label thresholds use one installation-wide community policy. Policy changes recalculate current scores/labels from unchanged attendance facts rather than preserving stale labels as truth.

Workers prevent duplicate command execution through a local durable journal keyed by stable command identity. Known redelivery returns recorded state/outcome; uncertain crash outcomes reconcile runtime reality before retry. Handlers are idempotent or operation-specifically reconcilable. Journal storage is local to the worker resource (for example SQLite or flat files); workers must not depend on an external database. The control plane remains the durable queue system of record.

Game configuration uses definitions supplied by the game implementation. Definitions have stable game-owned keys such as `server.cfg@port`, lightweight typed values, validation/display metadata, generated target paths, ordering, and sensitivity. Effective values resolve as `game defaults < selected immutable profile revision < sparse mutable Server-local values < physical persistent file`. Profiles are optional and existing Servers are not backfilled. The persistent browser mixes physical storage with game-declared virtual directories/files; physical collisions appear once as overrides and deleting one reveals the generated target again.

Configuration Profiles have immutable revisions. Servers follow latest or pin an explicit revision; Start resolves the effective revision. Editing creates a new revision and does not mutate pinned setups.

Definition evolution uses current game-provided types/defaults. Missing optional values use defaults; required values without compatible stored values/defaults block Start. Removed definitions and incompatible historical values remain in revision history but are ignored during rendering. Incompatible values are not coerced; a compatible default is used when possible, and the editor shows an inline warning at the mismatched setting so a new revision can correct it.

At Start, preparation order is game preparation, Mod preparation/generated output, Scenario staging, Configuration Profile rendering, final persistent expected-file synchronization, then the game Start action. Persistent expected files are the final override layer and can override matching generated or staged files. The UI identifies overridden generated targets; removing the persistent override restores the underlying generated/staged output on the next Start.

Completed-deployment cost misattribution is corrected by authorized finance users through append-only compensating ledger entries: credit the incorrectly charged owner and debit the correct one. Original deployment attribution, provider evidence, and ledger entries are not rewritten. The correction records actor/time/reason/affected records/amount/source/destination.

V1 supports manually recorded refunds/withdrawals of unused prepaid credit after the outgoing bank transfer is performed externally. Refunds cannot exceed unused credit and record actor/beneficiary owner/amount/date/reference/reason. Payments, refunds, and corrections are append-only; mistakes use linked reversal entries rather than mutation or deletion.

Production roles are administrator-created; no role name has built-in finance semantics. A theoretical Kassenwart role may exist in demo seed data only as an example claim bundle. Finance authority comes solely from assigned claims.

Finance claims are split into resource-scoped CostOwner.Use and CostOwner.View plus global Finance.ViewAll, Finance.RecordPayment, Finance.RecordRefund, Finance.CorrectLedger, Finance.ManageAllowance, Finance.FreezeCostOwner, and Finance.ManagePolicy. Finance.All is an explicit global umbrella but does not replace CostOwner.Use for payer selection. Mutation claims are independently assignable; users can view their own personal cost-owner position without a finance claim.

Kassenbuch entries record owner/type/amount/currency/effective date/recorded actor-time/external reference/optional reason/linked original entry; full bank details are unnecessary. The append-only ledger is authoritative. A derived current-balance projection is updated atomically with each entry, and periodic checkpoints tied to an ordered ledger position allow recovery/validation to replay only the bounded tail. Projections/checkpoints are rebuildable and never rewrite history.

The minimum Server scoped permissions are View, Operate, Modify, ManageAccess, and DeleteData. Operate covers Start, Stop, and projected non-administrative game actions; Modify covers eligible configuration/files/preservation selections; irreversible persistent-data removal requires DeleteData. Modpack, Scenario, terrain, and Configuration Profile use View, Modify, ManageAccess, and Archive/Delete. Create permissions are collection-scoped and creation then issues the explicit full resource grant. Referencing or consuming reusable content neither requires nor grants its administrative visibility/management permissions; no generic access inheritance occurs through Server or Event relationships.

Every Scenario is scoped to exactly one game type. Each immutable Scenario version immutably declares zero or more required Modpacks. At Start those requirements are combined with the Server-selected Modpack and passed through the normal recursive flattening/deduplication resolver. An incompatible game, unresolvable requirement, or dependency conflict blocks Start before game launch. Runtime evidence records the effective Scenario version, dependency selections, and observed effective Mod content.

Each immutable Scenario version references a logically identified artifact with a checksum. Physical layout is replaceable storage-adapter detail; local storage may use `{ConfiguredScenarioPath}/{ScenarioId}/{VersionId}/...`, but Scenario/Event/Server contracts do not depend on that path and object storage can replace it later. The worker stages the artifact into the game-specific runtime path during Start. Strict preparation order is game preparation -> Mod preparation -> Scenario staging -> Configuration Profile rendering -> final persistent expected-file synchronization -> game Start.

Scenario versions record version label, game type, source/original filename where applicable, artifact size/checksum, creation actor/time, required-Modpack declaration, optional description/changelog, and game-defined metadata. Metadata belongs to the immutable version. Arma 3 requires mission display name and terrain/world identifier; the adapter extracts them when reliable, otherwise the uploader must provide them before the version is accepted.

Scoped Scenario View includes downloading immutable version artifacts from the Command Center. Selecting/consuming a Scenario, operating its Server, or participating in its Event does not grant Scenario View or artifact download.

Game-specific Modpack interchange/download formats are game-provided derived artifacts, not part of the authoritative generic Modpack schema. A game advertises supported format label/direction/extension/content type and owns parsing, validation, generation, and warnings; the generic API/UI exposes and transports them. Arma 3 Launcher HTML is the first format. Import maps Workshop IDs, reuses Mods, creates missing Mods as Shared, deduplicates, and creates a Modpack/new revision. Export flattens the effective set, includes client-only/shared Workshop Mods, excludes server-only Mods, and warns about file-based Mods that HTML cannot represent.

Client download tracking uses a resolved output fingerprint covering all contributing Modpack revisions, flattened client/shared Mods, observed Workshop versions or file checksums, and game-specific output format. Scenario-required content participates; floating Workshop changes therefore change the fingerprint without a new pack revision. UTF stores each user's last successfully delivered or explicitly acknowledged fingerprint for the relevant context/format. A mismatch means an update is required, not that UTF knows the user's current installation. Per-Mod user tracking is deferred.

The existing WebApp is the v1 user-facing artifact discovery/download surface. Event and Modpack pages show status, warnings, and game-provided artifacts. The API authorizes and serves the artifact or issues a short-lived storage URL; large bytes may come directly from the backend. There is no separate download website, and storage/delivery can change without changing the WebApp workflow.

Modpack latest/pinned mode is conveyed primarily by icons: sync/latest and pin. Latest tooltip/accessible label: "Follows the newest Modpack revision whenever the Server starts. Steam Workshop Mods also resolve to the latest files Steam currently provides. Running Servers are not updated automatically." Pinned tooltip/label: "Uses Modpack revision {revision}. Its declared Mod list and settings remain fixed until another revision is selected. Steam Workshop files may still change because Steam generally provides only their latest version." Meaning must not depend on icon/color alone.

Start and Stop remain the only universal semantic action identities. Custom action presentation group/order is supplied by the game/platform provider; there is no rigid generic category taxonomy. Every projected action includes stable ID, localized label/description/icon, group/order, enabled state with safe user-facing unavailable reason, danger/confirmation behavior, pending-operation status, optional expected duration, and cost-impact description. Authorization stays server-side and claim identifiers are not exposed.

V1 persistent Server data is stored only on the website/API host's configured local filesystem. It is Verein operational infrastructure cost, not charged to the Server cost owner, so a stopped Server has no attributable storage/runtime/provider cost. Cloud/object/selectable storage modes are out of scope.

Commands that have not begun can be cancelled without side effects or technical cost, although v1 needs no routine pending-cancellation UI. Running cancellation is operation-specific and must reconcile partial effects; otherwise the applicable lifecycle control is Stop or Terminate.

Arma 3 v1 configuration/file rules are derived from Bohemia's official [Startup Parameters](https://community.bohemia.net/wiki/Arma_3:_Startup_Parameters), [Server Config](https://community.bohemia.net/wiki/Arma_3:_Server_Config_File), [Basic Config](https://community.bohemia.net/wiki/basic.cfg), [Server Profile](https://community.bohemia.net/wiki/Arma_3:_Server_Profile), and [BattlEye](https://community.bohemia.net/wiki/BattlEye) documentation. Worker/platform owns structural paths and arguments; typed profiles expose operational settings. Launch-read configuration edits apply on next Start, logs are read-only, and supported live changes use projected Arma/BattlEye/RCon actions. Scenario/Mod artifacts stay reusable; game and Workshop binaries are rebuilt, not preserved per Server.

Next-session objective: close and normalize the spec rather than continuing broad discovery.

1. Run a consistency sweep across all focused specs and remove stale Open Questions already answered elsewhere (notably finance-role visibility, running cost-owner transfer, emergency Terminate behavior, existing cost-owner types, Arma HTML mapping, Stop/archive/delete distinctions, old ExecuteActions/ManageFiles terminology, and speculative badge wording).
2. Derive and document without user interrogation: the minimal game-capability contract, Event Quick Create required fields, File DTO/classification baseline, and compatibility mapping for existing claims.
3. Choose conservative configurable v1 defaults for heartbeat/registration/diagnostic/escalation and reservation-slice timing; mark them tunable implementation defaults rather than product invariants.
4. Defer non-blocking UI polish such as badge ranking, affordability-graph detail, launch-disabled-record naming, confirmation copy, and exact click counts.
5. Ask the user only if the sweep reveals a genuine unresolved contradiction or a choice that materially changes the product/architecture. Otherwise present the cleaned spec status and recommended implementation-planning handoff.
```
