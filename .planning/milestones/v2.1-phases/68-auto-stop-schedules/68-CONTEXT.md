# Phase 68: Auto-Stop & Schedules - Context

**Gathered:** 2026-11-22
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

A server's owner can make it stop itself (when idle, after a maximum runtime, or on a schedule) and start on a
schedule, with a warning beforehand and the same verified cost-stop as a manual stop. The control plane owns
schedules and timers.

Requirements: AUT-01, AUT-02, AUT-03, AUT-04, AUT-05, AUT-06.

Success criteria (from ROADMAP):
1. The owner can set an idle shutdown (N minutes without players or activity) and a maximum runtime from the server
   workspace; a member who is not the owner sees these controls disabled with the reason.
2. The owner can schedule start and stop times, and the server starts and stops at those times.
3. The owner is warned shortly before an automatic stop, through the alert channels from Phase 66.
4. An automatic stop appears in the activity timeline as a lifecycle job with an audit entry, and completes only
   after verified non-billable teardown.
5. Schedules and idle timers survive an API restart, and a stop that came due while the API was down still happens
   exactly once.

</domain>

<decisions>
## Implementation Decisions

### Auto-decided after research (autonomous mode, reversible, logged for user review)
- D-AUTO-1 Stop timing: when the idle or maximum-runtime limit is reached, the owner is warned and the automatic stop happens one warning lead (10 minutes) later. Keeps the approved notice copy true and avoids conflict with the 5-minute minimum idle time.
- D-AUTO-2 A failed automatic stop never escalates to Terminate (data-loss decision stays with a human); it surfaces as a failed job with the normal failure panel.
- D-AUTO-3 The stop warning goes to the cost-owner alert recipients (Phase 66 channels); every viewer of the server sees the in-app notice.

### Claude's Discretion
All implementation choices are at Claude's discretion — discuss phase was skipped per user setting. Use ROADMAP phase
goal, success criteria, and codebase conventions to guide decisions.

</decisions>

<code_context>
## Existing Code Insights

Codebase context will be gathered during plan-phase research. Known anchors: Phase 63 reserved an automation
settings area in the workspace shell; Phase 66 owns the alert channels (in-app, email, Discord DM); Phase 67 owns
the players/activity metrics signal used for idle detection.

</code_context>

<specifics>
## Specific Ideas

No specific requirements — discuss phase skipped. Refer to ROADMAP phase description and success criteria.

Carry-forward working agreements from earlier phases of this milestone:
- Extend the UAT seed (`sources/Api/Data/UatFleetScenarioSeeder.cs`, `scripts/Start-Uat.ps1`) with a scenario that
  makes the automation observable on the local stack within minutes, so its UAT can be automated with the
  seeded-stack Playwright suite instead of handed to the user.
- Real-environment checks (real worker, real Hetzner) are deferred to the end of the milestone.

</specifics>

<deferred>
## Deferred Ideas

None — discuss phase skipped.

</deferred>
