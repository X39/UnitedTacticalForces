# Phase 58: Unified Server Workspace - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md - this log preserves the alternatives considered.

**Date:** 2026-07-05
**Phase:** 58-Unified Server Workspace
**Areas discussed:** Summary/status hierarchy, Workspace action placement, Cleanup vs provider teardown, Owner/admin boundary

---

## Summary/status hierarchy

| Option | Description | Selected |
|--------|-------------|----------|
| Readiness snapshot | Compact top band with runtime, latest job, owner, cloud/provider state, cost/policy, and cleanup risk. | |
| Action-first dashboard | Lead with what the user can do next, with state details secondary. | Partial |
| Detailed status grid | Show all major facts equally in one larger overview section. | Partial |
| Session-oriented | Show server age, runtime/job, owner, cost exposure, and cleanup/teardown next step when relevant. | Agent discretion |
| Always session-timed | Keep age and cleanup framing visible for every server. | Yes |

**User's choice:** Mix action-first dashboard and detailed status grid. The user must immediately see core status and core actions. Every server is session-timed; the UI and technical model do not differentiate short-lived and forever servers.
**Notes:** The expected standard server lifespan is about 3 hours, with only a few living forever.

---

## Workspace action placement

| Option | Description | Selected |
|--------|-------------|----------|
| Primary next action + secondary row | One obvious next action based on state plus a compact row of common actions. | Agent discretion |
| Administrative or detailed actions stay on tabs | Owner transfer, files, full audit, full job table, and diagnostics stay off Overview. | Yes |
| State-dependent cloud actions | Cloud actions appear on Overview only when they are the next relevant step. | Yes |
| Dedicated Logs tab | Split logs away from Runtime entirely. | Superseded |

**User's choice:** Overview uses one state-driven primary next action plus compact secondary actions. Detailed/admin work stays on tabs. Cloud actions may surface when state-relevant.
**Notes:** User corrected the log decision: remove user-facing `Collect logs` entirely. Log collection should be API/internal plumbing; users read logs in the UI and only download from the Logs tab.

---

## Cleanup vs provider teardown

| Option | Description | Selected |
|--------|-------------|----------|
| Workspace Cloud tab + risk-driven Overview | Show teardown in Cloud tab and promote to Overview only for active cost or cleanup risk. | Agent discretion |
| Direct and repeated wording | Label provider teardown as cost-stop; label runtime cleanup as not stopping provider cost. | Agent discretion |
| Provider teardown first | Make teardown primary when provider resources are still billable. | Yes |
| Cost-stopped state | After teardown, show cost stopped plus any remaining billable count or cleanup failure. | Agent discretion |

**User's choice:** Provider teardown is prioritized when billable provider resources remain.
**Notes:** Clarified distinction between runtime cleanup and provider teardown before selection. Provider teardown stops cost; runtime cleanup does not.

---

## Owner/admin boundary

| Option | Description | Selected |
|--------|-------------|----------|
| Server-scoped summary only | Owners see provider state, billable count, cleanup risk, and safe actions for this server only. | Yes |
| Admin diagnostics panel inside workspace | Admins can inspect deeper provider/fleet detail inside the selected-server workspace. | Yes |
| Collapsed secondary panel | Admin diagnostics are available but visually secondary. | Agent discretion |
| Explain available scope | Non-admin owners see owner-safe summaries and actions; hidden admin inventory is not mentioned unless admin help is needed. | Agent discretion |

**User's choice:** Server owners get server-scoped summaries only; admins get an in-workspace diagnostics panel.
**Notes:** Normal owner flow must not require admin-only provider/fleet pages.

---

## the agent's Discretion

- Pick the exact Overview layout, state-ranking rules, and primary-next-action algorithm.
- Pick exact tab names and component split, preserving a dedicated Logs tab and keeping detailed/admin work off Overview.
- Pick exact cost-stop wording, as long as provider teardown is direct and repeated as the cost-stop action.
- Pick exact admin diagnostics placement, as long as it is collapsed/secondary and permission-gated inside the workspace.

## Deferred Ideas

None.
