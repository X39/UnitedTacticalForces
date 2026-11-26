---
phase: 65-change-a-server
plan: 17
subsystem: ui
tags: [react, game-server-secrets, rotation, one-time-value, credentials, a11y]
gap_closure: true
gap_ids: [G-65-6]

requires:
  - phase: 65-change-a-server
    provides: "65-16 rotate endpoint returning GameServerSecretValueDto once; 65-11 credentials panel; draft provider and apply runner from the Settings section"
provides:
  - "hostingService.rotateSecret returns the generated GameServerSecretValue; GameServerSecretValue derives from the generated schema"
  - "oneTimeValues state with showOneTimeValue and dismissOneTimeValue beside the draft in ServerChangeDraftProvider"
  - "Rotate executor hands the generated value to the provider and resolves value-free, so ItemResult and mutation data never carry it"
  - "Credential row shows a rotated value once in a code element (credential__code--once) with Copy value and Hide value, no Reveal button, announced by name only"
affects: [65-18, 65-19]

actuals:
  tokens: 10600   # chars/4 over the full diff (12 files, 42303 chars)
  tasks: 2
  commits: 3
plan_head_before: 8a1f805014802640bc3e5677fa15f983bd4296bd
plan_head_after: 3f7d38c28f17891e05030719c5877063509710ad

tech-stack:
  added: []
  patterns:
    - "Plaintext one-time values live in provider reducer state beside the draft, handed over by the executor, never through the pure apply runner or the mutation cache"
    - "Staleness is derived from loaded saved data during render and cleaned up by an effect, so a loading list never drops the value and a stale value never flashes"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/types.ts
    - sources/ReactApp/src/features/hosting/services.ts
    - sources/ReactApp/src/features/hosting/useServerChangeDraft.tsx
    - sources/ReactApp/src/features/hosting/useServerChangeApply.ts
    - sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx
    - sources/ReactApp/src/components/server/credentials.css
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/hosting/credentials-panel.test.tsx
    - sources/ReactApp/tests/hosting/review-changes-dialog.test.tsx
    - sources/ReactApp/tests/hosting/contract-shape.test.ts
    - sources/ReactApp/tests/locales/server-keys.test.ts

key-decisions:
  - "The one-time value has no timer and ignores tab visibility: a member without canRevealSecrets cannot fetch it again, so it stays until Hide value, a new staged change of that credential, a newer saved version, a missing or unconfigured credential, or unmount"
  - "The panel passes the row a fresh-filtered value as a prop instead of the row reading the context, so a stale value never renders for a frame"
  - "Several values arriving in one apply are announced in one joined message, because the single polite region keeps only the last announce call"

patterns-established:
  - "Value-free apply: executors may hand secrets to provider state through a callback and resolve void"

requirements-completed: [CHG-07, CHG-03]

coverage:
  - id: D1
    description: "A member with canManageSecrets and without canRevealSecrets applies a staged rotation and sees the new value once in the credential row; the review stays value-free"
    requirement: "CHG-07"
    verification:
      - kind: unit
        ref: "tests/hosting/credentials-panel.test.tsx#Credentials panel one-time value shows the generated value once in its row, without Reveal, to a member who cannot reveal"
        status: pass
      - kind: unit
        ref: "tests/hosting/credentials-panel.test.tsx#Credentials panel one-time value shows no value in the review before the apply"
        status: pass
    human_judgment: false
  - id: D2
    description: "The value never reaches the apply result, the query or mutation cache, storage, console, URL, router state, announcements or a toast, and it is removed on unmount, a new staged change, Hide value, a newer version or a missing credential"
    requirement: "CHG-07"
    verification:
      - kind: unit
        ref: "tests/hosting/credentials-panel.test.tsx#Credentials panel one-time value (leak hygiene, unmount, staging, newer version, missing credential, no timer)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Copy value writes to the clipboard only on click; a refused clipboard shows the shared failure text and keeps the value visible; Hide value restores the mask and announces by name"
    requirement: "CHG-03"
    verification:
      - kind: unit
        ref: "tests/hosting/credentials-panel.test.tsx#Credentials panel one-time value copies the value only on click, exactly once per click"
        status: pass
      - kind: unit
        ref: "tests/hosting/credentials-panel.test.tsx#Credentials panel one-time value shows the shared failure text and keeps the value visible when the clipboard refuses"
        status: pass
    human_judgment: false
  - id: D4
    description: "The rotate path's 200 JSON response type is assignable to GameServerSecretValueDto at compile time; EN/DE copy is complete"
    requirement: "CHG-07"
    verification:
      - kind: unit
        ref: "tests/hosting/contract-shape.test.ts; tests/locales/server-keys.test.ts; npm run locales:check; npm run typecheck"
        status: pass
    human_judgment: false
  - id: D5
    description: "Visual placement of the one-time value inside the current row layout (65-19 moves it into the restructured value slot) and browser evidence for storage, cookies, URL and console after Hide and reload"
    requirement: "CHG-07"
    human_judgment: true
    rationale: "Layout adequacy and real-browser persistence evidence are not asserted by unit tests; 65-19 owns the browser evidence"

duration: 14min
completed: 2026-11-21
status: complete
---

# Phase 65 Plan 17: Credential Values, React Side Summary

**A rotated credential's generated value now travels from the apply flow into a one-time store beside the Settings draft and is shown once in its row with Copy value and Hide value, announced by name only and never reaching results, caches, storage, URL or console.**

## Performance

- **Duration:** about 14 min
- **Tasks:** 2 of 2
- **Files changed:** 12 (507 insertions, 32 deletions)

## Accomplishments

- **Task 1 (tracer):** `rotateSecret` returns `GameServerSecretValue` (now `Required<Schema<"GameServerSecretValueDto">>`). `ServerChangeDraftProvider` gained `oneTimeValues`, `showOneTimeValue` and `dismissOneTimeValue`; `stageSecret` deletes the key's value. The rotate executor calls `showOneTimeValue` and resolves void, so `ApplyExecutors`, `ItemResult` and the mutation data are unchanged and value-free. The row shows the value in `code.credential__code.credential__code--once` under `Server_Credentials_OneTime_Label` and omits Reveal while it is shown. `Server_Credentials_Description` updated (EN/DE).
- **Task 2:** Copy value (shared `CopyButton`, copy only on click, shared failure text) and Hide value (existing `Server_Credentials_Hide*` keys) in the action row. The panel derives staleness from loaded data (missing, not configured, or newer version), dismisses stale keys in an effect, and announces each newly arrived key once by name via a ref. Keys added: `OneTime_Label`, `OneTime_Announce`, `Copy`, `CopiedAnnounce` (phase65RequiredKeys 145 to 149, `Server_Credentials_Copy` in the button keys).
- Tests: new "Credentials panel one-time value" describe with 13 cases (display, review value-free, apply result and caches, unmount and remount, new staged change, announcement, copy once on click, clipboard refusal, Hide, newer version, missing and unconfigured credential, no timer at 60 s, leak hygiene across storage, console, caches, router, regions, cookie and DOM); contract-shape compile-time assignment of the rotate 200 body; rotate mocks in the two existing suites resolve a value object.

## Task Commits

1. **Task 1 (tracer): rotated value shown once in its row** - `47b4bd51` (feat(webapp))
2. **Task 2: Copy value, Hide value, announcement, staleness** - `0b96495e` (feat(webapp))
3. **Formatting of the touched panel and test file** - `3f7d38c2` (style(webapp))

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Prettier format check failed on two touched files**
- **Found during:** plan-level `npm run ci`
- **Issue:** `format:check` flagged `ServerSecretsPanel.tsx` and `credentials-panel.test.tsx` (both were prettier-clean at the plan base).
- **Fix:** `prettier --write` on the files this plan changed.
- **Commit:** `3f7d38c2`

**Tracer gate:** the tracer's `<verify>` was run end-to-end after Task 1 and passed before Task 2 started.

**Total deviations:** 1 auto-fixed (formatting). **Impact:** none on behavior.

## Issues Encountered

- `npm run ci` stops at `format:check`: prettier also reports 32 other files this plan did not touch (for example `SizeField.tsx`, `CreateServerPage.tsx`, `tests/hosting/create-*.test.tsx`). They are pre-existing and out of scope; the rest of the ci chain was run separately and passes (`locales:check`, `contracts:check`, `lint`, `typecheck`, `test`: 150 files / 1804 tests, `build:assets`).
- Focus handling after Hide value is not managed (the button unmounts; `ActionButton` takes no ref), the same as for any removed control in this panel. Not in scope for the plan.

## Verification Results

- Plan task verify commands (both tasks): all tests green (tracer set 193 tests; task 2 set 133 tests), `locales:check` 3530/3530 keys, `typecheck` and `lint --max-warnings 0` clean.
- Full suite: 150 files, 1804 tests passed. `contracts:check` clean (api.ts regeneration produced no diff). `build:assets` succeeds.
- Acceptance criteria: all greps meet their expectations (services 2x `Promise<GameServerSecretValue>`, `showOneTimeValue` in provider and apply hook, one `export type GameServerSecretValue`, `credential__code--once` in panel and css, new locale keys once per file, 0 storage/cookie/console patterns in the panel, CopyButton and `dismissOneTimeValue` in the panel).
- `git status --porcelain -- sources/WebApp` empty; shared components (`primitives.tsx`, `ActionControl.tsx`, `CopyButton.tsx`, `FormFields.tsx`, `ReviewChangesDialog.tsx`) unchanged since the plan base; the only commits touching `utf-api.json` or `api.ts` since c24a26c8 are the two 65-16 commits.

## Known Stubs

None.

## Threat Flags

None. T-65-44 is mitigated as specified (provider-only state, value-free ItemResult, unit-tested hygiene); T-65-49 accepted by design (copy only through an explicit click).

## Next

Ready for 65-18. 65-19 moves the one-time value into the restructured row's fixed value slot and adds browser evidence for storage, cookies, URL, console and DOM after Hide and reload.

## Self-Check: PASSED

- All 12 modified files exist; commits `47b4bd51`, `0b96495e` and `3f7d38c2` are ancestors of HEAD; `git rev-list --count` from the plan base measures 3 commits.
