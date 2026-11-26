---
phase: 68-auto-stop-schedules
plan: 22
subsystem: webapp
tags: [react, i18n, schedules, gap-closure, G-68-3]
status: complete

requires:
  - phase: 68-auto-stop-schedules
    provides: "68-21 WaitingForStop and SkippedStopUnfinished outcomes in the generated React contract"
provides:
  - "Fixed EN/DE last-run sentences for a start that waits for a shutdown and for a start closed by an unfinished shutdown"
  - "UI-SPEC copy rows for both outcomes"
affects: []

requirements-completed: [AUT-03]

actuals:
  tokens: 6500
  tasks: 2
  commits: 3
plan_head_before: 0049bc746ef95164c560718e488fc8cf7d3063fb
plan_head_after: d52b91dddd1b6b3d0ce05b70fa7474f86cec9130

tech-stack:
  added: []
  patterns:
    - "Outcome codes map to a closed LastRunKey union and a Record over it, so a missing sentence fails the type check"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/automationModel.ts
    - sources/ReactApp/src/components/server/ScheduleRuleFields.tsx
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/hosting/automation-schedules.test.tsx
    - sources/ReactApp/tests/hosting/automation-model.test.ts
    - sources/ReactApp/tests/locales/server-keys.test.ts
    - .planning/phases/68-auto-stop-schedules/68-UI-SPEC.md

key-decisions:
  - "The waiting sentence says only that the start waits; it promises no start because consent, cost or a failed stop can still close the wait"

completed: 2026-11-25
---

# Phase 68 Plan 22: Last-run sentences for waiting and unfinished-stop outcomes Summary

A scheduled start that waits for a shutdown, or was closed because the shutdown before it never finished, now shows its own fixed English or German sentence on the rule instead of the plain "Last run: {time}." line.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 | Tracer: waiting / unfinished-stop outcome renders its own sentence | 83dc09c5 |
| 2a | Model and key-list tests | 6bb921ec |
| 2b | UI-SPEC copy rows | d52b91dd |

- Task 1 followed RED then GREEN: the new rendering test failed with the plain "Last run: Fri, Aug 28, 6:00 PM." sentence, then passed once `WaitingForStop` and `SkippedStopUnfinished` were added to `LastRunKey`, the outcome map, the `lastRunSentences` record and both locale files.
- Task 2 renamed the model test to "maps the eleven outcome codes", added both codes, added both keys to the locale key list, and added two rows plus a note to the 68-UI-SPEC copy table (placed before the unknown-outcome row).
- `SkippedRunning`, `SkippedMissed` copy and the unknown-code fallback (`Other`) are unchanged. `src/generated/api.ts` was not committed (the contracts script only rewrites line endings; restored).

## Verification

- `npx vitest run tests/hosting/automation-schedules.test.tsx`: 16 passed.
- Full `npx vitest run`: 178 files, 2601 tests passed.
- `npm run locales:check`: 3832 EN and 3832 DE keys, 0 failures.
- `npm run typecheck` and `npm run lint`: clean.
- `npm run contracts:check`: regenerated `api.ts` with line-ending noise only; no content diff, file restored and not staged.
- `npm run format:check`: reports every file in this checkout (517) because of CRLF line endings; with `--end-of-line crlf` the five changed source/test files are clean. This is pre-existing and not caused by this plan.

## Deviations from Plan

None - plan executed as written. The UI-SPEC note was placed after the table (not inside it) because the table continues past the last-run rows.

## Known Stubs

None.

## Threat Flags

None.

Deferred follow-up: seeded-stack Playwright check of a real stop-then-start run showing the waiting sentence on the rule, at milestone end (per the plan's verification note).

## Self-Check: PASSED

- 83dc09c5, 6bb921ec and d52b91dd are ancestors of HEAD.
- All modified files exist and carry the new keys.
