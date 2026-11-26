---
status: complete
phase: 64-guided-server-creation
source: [64-VERIFICATION.md]
started: 2026-11-20T10:58:31Z
updated: 2026-11-21T11:40:28.893Z
---

## Current Test

[testing complete]

## Tests

### 1. Create and start against a real development API, in English and German
expected: Keep off lands in the workspace with no confirmation. Start opens the shared confirmation; confirming starts the job; dismissing leaves the server off with the not-started notice.
result: skipped
reason: "Deferred follow-up: not deploying until all phases are worked through onto prod; verify create and start on the real environment then."

### 2. Confirm decision OQ1: a mod pack is never required at create
expected: Product owner agrees a game whose capability says requiresModPack (Arma 3) may be created without a mod pack, matching the old form and the API.
result: pass

### 3. Confirm that "cannot pay" at create means a frozen cost owner only
expected: Product owner accepts that an owner with an insufficient balance can still create and is stopped only at start (affordability error shown by the Phase 63 failure panel and the not-started notice).
result: pass

### 4. First paint for a game that requires a terrain or scenario (WR-01)
expected: Advanced options stays collapsed because terrain and scenario receive defaults. Code review indicates it may open on first load.
result: skipped
reason: "Deferred follow-up: WR-01 (Advanced options auto-opening on first paint) needs the running app; checkpoint was unclear and the behavior was never confirmed as required."

### 5. Visual review at 320 and 390 px in English and German
expected: Default, blocked owner, Advanced open and invalid submit look right. No screenshot baselines exist until Phase 69.
result: pass
note: "User: create at phone sizes looks good (checked informally, English/German not separately confirmed)."

## Summary

total: 5
passed: 3
issues: 0
pending: 0
skipped: 2
blocked: 0

## Gaps

## Deferred Follow-Ups

- test: 1
  idea: "Verify create (keep off / start now, confirm / dismiss) on the real environment in English and German once all phases are on prod"
  deferred_at: 2026-11-21
- test: 4
  idea: "Check WR-01 (Advanced options first paint for terrain/scenario games) in the running app; also clarify whether the Advanced disclosure behavior is wanted"
  deferred_at: 2026-11-21
