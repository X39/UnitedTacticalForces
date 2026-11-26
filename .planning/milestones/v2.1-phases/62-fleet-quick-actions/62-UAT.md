---
status: complete
phase: 62-fleet-quick-actions
source: [62-VERIFICATION.md]
started: 2026-11-19T10:57:59Z
updated: 2026-11-19T12:44:53.220Z
---

## Current Test

[testing complete]

## Tests

### 1. Open the fleet with a failed (attention) deployment and an unconfirmed stop
expected: The card shows a disabled 'Shut down and stop billing' with the deployment-state reason plus an Open server link. The product owner confirms this literal UI-SPEC behavior or asks for Open server to become the primary action in those two states.
result: pass
note: "User observed the German reason text 'Das ist nicht möglich, solange der Server „Braucht Aufmerksamkeit“ ist.' in an info box on a separate row; button disabled. Accepted."


### 2. Acknowledge the fleet-signal disclosure (review WR-01)
expected: The product owner accepts, or rejects, that holders of any server-section claim (including a single-server sv:view holder) receive the id and change timing of servers they cannot view. Escape hatch: drop ServerId from GameServerFleetChangedMessage and the client frame check.
result: issue
reported: "That is a clear failure then. We must deliver messages in accordance with thje corresponding claims"
severity: major

### 3. Keyboard-only: start or stop a server from a fleet card, then press Tab
expected: Focus returns to the card (or its Open server link) after the confirmation closes. The code review predicts it lands on the page h1 (WR-02), and also drops from a focused Refresh button on a live refetch (WR-03).
result: skipped
reason: "Deferred follow-up: user does not care where focus lands after the confirmation closes (accepted skip)"

### 4. German fleet on a phone (320 and 390 px)
expected: No clipped chips, titles or owners, and every action reachable.
result: pass

### 5. Live signal behind the real nginx proxy
expected: Idle for more than 60 s, change a server elsewhere, and the card updates.
result: skipped
reason: "Deferred follow-up: cannot be tested in UAT without pushing to prod; deploy-time smoke check"

## Summary

total: 5
passed: 2
issues: 1
pending: 0
skipped: 2
blocked: 0

## Gaps
- gap_id: G-62-2
  truth: "Fleet change messages are delivered only to subscribers whose claims permit viewing the affected server (no id/timing disclosure of servers the holder cannot view)"
  status: resolved
  resolved_by: 62-16-PLAN.md
  resolved_at: 2026-11-19
  reason: "User reported: That is a clear failure then. We must deliver messages in accordance with thje corresponding claims"
  severity: major
  test: 2
  root_cause: "Fleet signal is authorized per server section (any server claim) on one shared path game-servers/fleet and published unfiltered; the per-server sv:view rule (HostingAuthorizationService.HasServerPermission) is never applied per (recipient, server) on the push path. Origin: D-13 premise 'no server data crosses the socket' is false (ServerId + timing disclose the server)."
  artifacts:
    - path: "sources/Api/Controllers/UpdateStreamController.cs"
      issue: "Coarse section gate registers every user on shared path game-servers/fleet"
    - path: "sources/Api/Services/GameServerLifecycle/FleetChangeNotifier.cs"
      issue: "Broadcasts each changed server to shared path with no per-recipient claim check"
    - path: "sources/Api/Services/GameServerLifecycle/FleetChangeStreamPaths.cs"
      issue: "Bare Fleet constant is a prefix of any per-user path"
    - path: "sources/Api/Services/GameServerLifecycle/HostingAuthorizationService.cs"
      issue: "No 'who may view server S' audience query"
  missing:
    - "Audience query matching HasServerPermission(View) exactly (user+role claims, non-banned/deleted), batched by server ids"
    - "Notifier resolves audience at publish time and sends to per-user paths game-servers/fleet/{userId}; fail closed on query error"
    - "Per-user path builder; nothing publishes to bare prefix"
    - "Controller returns user-addressed canonical path from authenticated userId; reject extra segments"
    - "Fix stale XML remarks/docs (react-ui-beta.md, D-13, T-62-11); update and add tests (cross-claim, role, admin, banned, revoke, drift, failure)"
  debug_session: ".planning/debug/fleet-signal-claim-scoping.md"

## Deferred Follow-Ups

- test: 3
  idea: "Keyboard focus return after confirmation closes (accepted skip, not a concern)"
  deferred_at: 2026-11-19
- test: 5
  idea: "Live signal behind the real nginx proxy, deploy-time smoke check"
  deferred_at: 2026-11-19
