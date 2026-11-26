# Phase 59: Lifecycle, Log, And Status Feedback Repair - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md - this log preserves the alternatives considered.

**Date:** 2026-07-05
**Phase:** 59-Lifecycle, Log, And Status Feedback Repair
**Areas discussed:** Job timeline language, Log collection interaction, Unknown status taxonomy, Next-step routing

---

## Job Timeline Language

| Option | Description | Selected |
|--------|-------------|----------|
| Projected labels over persisted states | Keep persisted states and show stale/recovered as labels over timestamps and failure context. | |
| New persisted statuses | Add or revise persisted status fields/enums because there is no deployed migration obligation. | yes |
| Diagnostics only | Keep visible status simple and expose stale/recovery in diagnostics. | |
| Other | Free-form clarification. | yes |

**User's choice:** The user clarified that the service is not deployed anywhere and there are no migration
obligations. Schema/status changes are acceptable when they produce a cleaner model.
**Notes:** The agent decided job rows should show timeline reasons, owner-safe worker/actor context, attempts,
timestamps, sanitized failure/progress detail, and honest cancelled/stale/recovered states.

---

## Log Collection Interaction

| Option | Description | Selected |
|--------|-------------|----------|
| Contextual Logs-tab refresh | Logs tab refresh follows an existing worker collection job when needed. | yes |
| Automatic background collection | Opening Logs starts collection automatically. | |
| Explicit Collect logs action returns | Bring back a visible Collect logs action in Logs only. | |
| Other | Free-form clarification. | yes |

**User's choice:** Automatic collection only if the job is already running. On stop, the system always has to get all
logs too. No background job starts and no manual step.
**Notes:** The agent decided stop may be implemented as one job or linked jobs, but the UI must present one
`Stop server` operation with final log capture included, inline progress, persisted logs visible, and automatic
refresh after completion.

---

## Unknown Status Taxonomy

| Option | Description | Selected |
|--------|-------------|----------|
| Small shared label taxonomy | Use consistent labels with domain-specific detail. | yes |
| Domain-specific labels only | Each domain defines its own labels. | |
| Badges plus explanatory rows | Every missing/unknown state gets a badge and explanation row. | |
| Server-side projection | API/repository models expose operational truth when backend state is required. | yes |

**User's choice:** The user selected server-side projection and clarified that localization can happen server-side,
but localization should be ignored for now.
**Notes:** The agent decided to use strict meanings for `Unknown`, `Not available`, `Not supported`, `Deferred`,
`Waiting for worker`, `Stale`, `Failed`, and `Unavailable until setup completes`; remove visible `n/a` from selected
server operational surfaces only; and promote unknown/stale/failed states on Overview only when actionable.

---

## Next-Step Routing

| Option | Description | Selected |
|--------|-------------|----------|
| Cost and safety first | Provider teardown/cleanup risk outranks retry/blockers/active jobs/normal runtime actions. | yes |
| Current job first | Current job outranks cost/safety concerns. | |
| User task first | Prioritize likely runtime task and show cost/policy as warnings. | |
| Route to exact fix surface | Route blockers to the smallest useful repair target. | yes |
| Diagnostics always visible | Keep diagnostics visible for now with owner-safe content. | yes |

**User's choice:** The user selected cost and safety first and exact fix routing. The user clarified that if an
operation failed and state is not corrupted, leaving the normal action available is sufficient. Diagnostics should
always be visible for now; a future goal will align claims with all features.
**Notes:** The agent decided special retry/recover actions are only needed when normal actions are unsafe because
state is intermediate or corrupted. Active job progress should become primary unless cost/safety outranks it.

---

## the agent's Discretion

- Decide exact lifecycle schema/status-field shape for stale/recovered/cancelled visibility.
- Decide whether final log capture during stop is one job or linked jobs.
- Decide exact component split and Overview action-ranking implementation.
- Decide exact status label field names and English text, with localization deferred.

## Deferred Ideas

- Full feature/claims alignment for diagnostics and all server workspace capabilities.
- Localization plumbing for new operational labels.
