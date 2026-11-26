---
phase: 65-change-a-server
plan: 18
subsystem: ui
tags: [react, game-server-secrets, chosen-values, validation, credentials, a11y]
gap_closure: true
gap_ids: [G-65-6]

requires:
  - phase: 65-change-a-server
    provides: "65-16 acceptsChosenValue and valueFormat on GameServerSecretStatusDto and the EGameSecretValueFormat schema; 65-17 credential row, draft provider and apply runner"
provides:
  - "GameServerSecretStatus derives from the generated GameServerSecretStatusDto; the value format type is GameServerSecretStatus[\"valueFormat\"], with no hand-written union"
  - "Value input for every credential whose game accepts a chosen value, generated ones included; typing replaces a staged rotation and Undo change replaces Rotate value while a typed value is staged"
  - "credentialValueIssue, validateStagedSecrets and SecretValueIssue in serverChangeDraft.ts mirroring the API rules"
  - "Credentials in the Settings review gate and invalid count, so the existing alert and first-invalid focus cover them"
  - "Show typed value toggle (aria-pressed) that masks again whenever the staged set leaves the draft"
affects: [65-19]

actuals:
  tokens: 9100    # chars/4 over the full diff (10 files, 36476 chars)
  tasks: 2
  commits: 2
plan_head_before: 1beb45d1a22ab1fe123020232e76d30f0e1e902d
plan_head_after: db51e4e6ec8be84c48f46364125496c5bbb4284f

tech-stack:
  added: []
  patterns:
    - "Pure value rules in the draft module mapped to fixed Server_Credentials_Invalid_* keys; only the max count is interpolated, never the value"
    - "Message keys held in an as-const satisfies Record map so the typed t() accepts them"
    - "State adjusted during render (not in an effect) to mask the typed value when the staged set disappears"

key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/types.ts
    - sources/ReactApp/src/features/hosting/serverChangeDraft.ts
    - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
    - sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/tests/hosting/credentials-panel.test.tsx
    - sources/ReactApp/tests/hosting/server-change-draft.test.ts
    - sources/ReactApp/tests/hosting/contract-shape.test.ts
    - sources/ReactApp/tests/locales/server-keys.test.ts

key-decisions:
  - "The Show typed value toggle renders only while a typed value is staged: an empty input has nothing to show, and the control then cannot outlive the value it reveals"
  - "The value error appears after blur or after a review attempt, and the Settings section reads the secrets through the same query key as the panel, so both share one cached list"

patterns-established:
  - "Credential gate parity with configuration: validateStagedSecrets joins validateStagedSettings in review() and invalidCount"

requirements-completed: [CHG-07, CHG-03]

coverage:
  - id: D1
    description: "A member with canManageSecrets types a deliberate value for a generated credential whose game accepts one; it is reviewed as 'Changed. The value is not shown.' and applied through setSecret as a user-provided value, never through rotateSecret"
    requirement: "CHG-07"
    verification:
      - kind: unit
        ref: "tests/hosting/credentials-panel.test.tsx#Credentials panel chosen values stages a typed value, reviews it as changed and applies it as a set, never as a rotation"
        status: pass
      - kind: unit
        ref: "tests/hosting/credentials-panel.test.tsx#Credentials panel chosen values shows no input but Rotate value for a generated credential that accepts no chosen value"
        status: pass
    human_judgment: false
  - id: D2
    description: "One staged entry per credential: typing replaces a staged rotation, and a staged typed value swaps Rotate value for Undo change"
    requirement: "CHG-07"
    verification:
      - kind: unit
        ref: "tests/hosting/credentials-panel.test.tsx#Credentials panel chosen values replaces a staged rotation when a value is typed"
        status: pass
      - kind: unit
        ref: "tests/hosting/credentials-panel.test.tsx#Credentials panel chosen values offers Undo change instead of Rotate value while a typed value is staged"
        status: pass
    human_judgment: false
  - id: D3
    description: "Values a game cannot use are refused before review with fixed copy that mirrors the API; Review changes stays closed with the invalid alert and focus moves to the first invalid input"
    requirement: "CHG-07"
    verification:
      - kind: unit
        ref: "tests/hosting/server-change-draft.test.ts#credentialValueIssue; #validateStagedSecrets"
        status: pass
      - kind: unit
        ref: "tests/hosting/credentials-panel.test.tsx#Credentials panel value rules and typed value toggle (token message, review gate and focus, single-line control characters, blank and too long)"
        status: pass
    human_judgment: false
  - id: D4
    description: "The Show typed value toggle switches the input between password and text, returns to masked after Undo change and after apply, is disabled while applying, and no error text, announcement or review line contains the typed value"
    requirement: "CHG-03"
    verification:
      - kind: unit
        ref: "tests/hosting/credentials-panel.test.tsx#Credentials panel value rules and typed value toggle (toggle, Undo change, apply, applying, no echo)"
        status: pass
    human_judgment: false
  - id: D5
    description: "The status type derives from the generated schema; EN/DE copy is complete (158 Phase 65 keys)"
    requirement: "CHG-07"
    verification:
      - kind: unit
        ref: "tests/hosting/contract-shape.test.ts; tests/locales/server-keys.test.ts; npm run locales:check; npm run typecheck"
        status: pass
    human_judgment: false
  - id: D6
    description: "Visual placement of the input, the toggle and the context button inside the current row layout (65-19 moves them into the fixed edit slots) and real-browser behavior of the toggle"
    requirement: "CHG-07"
    human_judgment: true
    rationale: "Layout adequacy and real-browser rendering are not asserted by unit tests; 65-19 restructures the row and owns the browser evidence"

duration: 14min
completed: 2026-11-21
status: complete
---

# Phase 65 Plan 18: Credential Values, React Side (Chosen Values) Summary

**Members can now type a deliberate value for every credential their game accepts one for, generated Arma 3 and Counter-Strike 2 passwords included; unusable values are refused before review with fixed API-mirroring copy, never echoed, and the member can check what they typed with a Show typed value toggle that masks again when the staged value leaves the draft.**

## Performance

- **Duration:** about 14 min
- **Tasks:** 2 of 2
- **Files changed:** 10 (559 insertions, 26 deletions)

## Accomplishments

- **Task 1 (tracer):** `GameServerSecretStatus` is now `Required<Schema<"GameServerSecretStatusDto">>`, so `acceptsChosenValue` and `valueFormat` come from the generated contract. The row renders the `TextField` when the credential is not generated or `acceptsChosenValue` is true, with the new `Server_Credentials_SetHintGenerated` hint for generated ones. The rotate toggle renders only while no set is staged, so one staged entry exists per key and Undo change takes its place. `phase65RequiredKeys` 149 to 150.
- **Task 2:** `credentialValueIssue` (trimmed value; blank, over 4096, control characters below 0x20 and 0x7F-0x9F, and for Token anything outside printable ASCII or containing a quote or backslash) and `validateStagedSecrets` in `serverChangeDraft.ts`. `SettingsSection` reads the secrets through the shared query key and adds `validateStagedSecrets` to the review gate and `invalidCount`. The row maps issues to `Server_Credentials_Invalid_*` after blur or a review attempt, and adds the `aria-pressed` Show typed value / Hide typed value toggle, disabled while read-only or applying. Eight keys added (150 to 158), two registered as button keys.
- Tests: 5 chosen-value cases and 10 value-rule/toggle cases in the panel suite, 8 pure-rule cases in `server-change-draft.test.ts`, the compile-time `acceptsChosenValue` and `valueFormat` assertions in `contract-shape.test.ts`.

## Task Commits

1. **Task 1 (tracer): input for generated credentials that accept a chosen value** - `ddfd5dda` (feat(webapp))
2. **Task 2: value rules, review gate and Show typed value toggle** - `db51e4e6` (feat(webapp))

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug in test design] Plan-specified test inputs not reproducible in jsdom**
- **Found during:** Task 2
- **Issue:** A line break typed into an input is stripped by value sanitization, and `fireEvent.change` does not focus the field, so `Tab` did not blur it. The plan also lists an archived-server toggle case, but an archived server disables the input so no value can be staged there.
- **Fix:** The single-line control-character case uses `\u0007` with `fireEvent.blur`; the pure rule still asserts `"a\nb"`. The disabled-toggle case uses an in-flight apply (a never-resolving `setSecret`) instead of an archived server. The "does not echo" assertions compare against the typed phrase rather than single words, because the fixed token copy itself contains the word "spaces".
- **Files modified:** `sources/ReactApp/tests/hosting/credentials-panel.test.tsx`
- **Commit:** `db51e4e6`

**2. [Rule 3 - Blocking] Typed translation keys**
- **Found during:** Task 2 typecheck
- **Issue:** The issue-to-key map typed as `Record<SecretValueIssue, string>` is not accepted by the typed `t()`.
- **Fix:** `as const satisfies Record<SecretValueIssue, string>`.
- **Files modified:** `sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx`
- **Commit:** `db51e4e6`

**Tracer gate:** the tracer's `<verify>` was run end-to-end after Task 1 (140 tests, `locales:check`, `typecheck`, `lint`) and passed before Task 2 started.

**Total deviations:** 2 auto-fixed (test design, typing). **Impact:** none on behavior.

## Issues Encountered

- `npm run ci` stops at `format:check`: prettier reports pre-existing unformatted files this plan did not touch (`CreateServerPage.tsx`, `createServerModel.ts`, `createStartIntent.ts`, `SizeField.tsx` and the `tests/hosting/create-*` suites among them). They are out of scope. The rest of the chain was run separately and passes.
- The toggle is rendered only while a typed value is staged (see key decisions); the plan's wording would also permit an always-visible toggle. Hiding it for an empty input avoids a dead control and makes the "masks again when the staged value leaves the draft" rule fall out of the render condition as well as the state reset.
- Focus after pressing Show typed value is kept on the toggle (the button persists); after Undo change focus returns to the input as in 65-17.

## Verification Results

- Task verify commands: tracer set 140 tests, task 2 set 214 tests, all green. `locales:check` 3539/3539 keys, `typecheck` and `lint --max-warnings 0` clean.
- Full suite: 150 files, 1827 tests passed. `contracts:check` (api.ts regeneration) shows no diff. `build:assets` succeeds.
- Acceptance criteria: all greps meet their expectations (`export type GameServerSecretStatus` 1, `!secret.generated &&` 0, `acceptsChosenValue` in panel and contract-shape, `SetHintGenerated` once in en.json, `credentialValueIssue` 2 in the draft module, `validateStagedSecrets` 2 in the section, `aria-pressed` in the panel, `Invalid_Token` once in de.json, 0 hand-written format unions in types.ts and serverChangeDraft.ts).
- Plan-level: `git status --porcelain -- sources/WebApp` empty; `primitives.tsx`, `ActionControl.tsx`, `CopyButton.tsx`, `FormFields.tsx`, `ReviewChangesDialog.tsx`, `ServerConfigurationPanel.tsx` and `ConfigurationEntryField.tsx` unchanged since c24a26c8. No change to `openapi/utf-api.json` or `src/generated/api.ts`.

## Known Stubs

None.

## Threat Flags

None. T-65-50 mitigated (masked by default, explicit `aria-pressed` toggle, masks again on Undo change, apply and discard, review copy unchanged; all unit-tested). T-65-51 mitigated (fixed message keys, only `max` interpolated, no-echo test across alerts, live region and review). T-65-SC: no package installed.

## Next

Ready for 65-19, which restructures the credential row into fixed edit slots and moves the input, the toggle and the context button into them, and owns browser evidence.

## Self-Check: PASSED

- All 10 modified files exist; commits `ddfd5dda` and `db51e4e6` are ancestors of HEAD; `git rev-list --count` from the plan base measures 2 commits.
