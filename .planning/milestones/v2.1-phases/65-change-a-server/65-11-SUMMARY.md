---
phase: 65-change-a-server
plan: 11
subsystem: ui
tags: [react, hosting, credentials, secrets, staged-changes, accessibility, vitest]

requires:
  - phase: 65-02
    provides: the staged-change draft (stageSecret, unstageSecret) and the Server_Credentials_* copy keys in EN and DE
  - phase: 65-07
    provides: the Settings section draft provider, review dialog and the apply runner that calls setSecret and rotateSecret
provides:
  - Credentials panel that stages a typed value or a rotation in the Settings draft instead of saving at once
  - Fixed masked value run (twelve bullets, accessible text Value hidden) for configured secrets
  - Reveal with a 30-second auto-hide, one value at a time, fetched through a gcTime 0 mutation that is reset right after the copy
  - credentials.css and the credentials-panel test file with the panel source scan
affects: [65-12, 65-13, 65-14, 65-15]

plan_head_before: 54d282e14894df1a421fc93056c55be1eff7860b
plan_head_after: 732c50fb6f25afd907313073fe3b423704444cda

actuals:
  tokens: 10500
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Reveal state lives in the panel (one value at a time); rows only receive the revealed value of their own key"
    - "Hidden-state is derived during render: the revealed key, version and staging status must still match, otherwise the value is dropped, so un-staging can never bring an old value back"
    - "One effect owns the 30-second timer and the visibilitychange listener of the single revealed value and removes both on change or unmount"
    - "Reveal uses mutateAsync with a finally-block reset(); failures are tracked as a failed key, never as mutation or error text"
    - "Rotate and Reveal are one button each whose label toggles, so focus never moves"

key-files:
  created:
    - sources/ReactApp/src/components/server/credentials.css
    - sources/ReactApp/tests/hosting/credentials-panel.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx
    - sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx

key-decisions:
  - "The revealed state also carries the secret display name so the auto-hide announcement can name it from inside the timer effect"
  - "Reveal errors are tracked with a failed-key state instead of the mutation error, because the mutation is reset in a finally block to drop the value"
  - "Clicks on Reveal while a reveal request is pending are ignored in the handler instead of disabling the button, so focus is never lost"
  - "A manual Hide also announces The value of {name} is hidden again., since focus stays on the button and the polite region is the only non-visual feedback"
  - "The generic Undo change button appears only for a staged set; a staged rotation uses Keep current value, as the plan specifies"

requirements-completed: [CHG-07, CHG-03]

coverage:
  - id: D1
    description: "Typing a new value stages a set change with the Changed badge and Undo change, clearing the input removes the bar, the review shows only Changed without the value, applying calls setSecret once and the typed value is gone from the input and the DOM afterwards"
    requirement: CHG-07
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#stages a typed value, shows the Changed badge and Undo, and clearing the input removes the bar"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#applies through the existing endpoint once and leaves no trace of the typed value"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#reviews the credential as Changed without ever showing the typed value"
        status: pass
    human_judgment: false
  - id: D2
    description: "Configured secrets show a fixed twelve-bullet masked run with the accessible text Value hidden; no partial value or length; the panel renders only with canManageSecrets and loading, load error with Try again, and the empty game each show their own copy"
    requirement: CHG-07
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#shows the masked run as twelve bullets without a partial value or a length"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#shows loading, a retryable load error without error text, and the empty game"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#renders no credentials heading, panel or secrets request without canManageSecrets"
        status: pass
    human_judgment: false
  - id: D3
    description: "Reveal is offered only for canRevealSecrets, revealable and configured secrets (also on archived servers), fetches on demand into a code element of its row, keeps focus on the same button and is announced; a failed reveal shows the alert and no value"
    requirement: CHG-07
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#reveals on demand in a code element of its row, keeps focus on the same button and announces it"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#offers Reveal only to members who may reveal, for revealable and configured secrets"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#shows the reveal error as an alert and no value when the reveal fails"
        status: pass
    human_judgment: false
  - id: D4
    description: "A revealed value hides after 30 seconds, on Hide value, when the tab becomes hidden, on unmount, when another value is revealed, when its credential is staged or typed into, and when a refetch brings a new version"
    requirement: CHG-07
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#hides the value after 30 seconds and announces it"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#hides the value when the tab becomes hidden"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#hides the value when its credential is staged for rotation, and Keep current value does not bring it back"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#hides the value when a refetch brings a new version"
        status: pass
    human_judgment: false
  - id: D5
    description: "A rotation is staged with the staged notice and Keep current value, reviewed as A new value will be generated. and applied through rotateSecret once"
    requirement: CHG-03
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#stages a rotation, reviews it as a new value and applies it through the existing endpoint"
        status: pass
    human_judgment: false
  - id: D6
    description: "A revealed value never reaches storage, cookies, the console, the mutation or query cache, the URL, router state, the live region or the DOM after hiding; the panel source holds no storage, cookie, console or error-text access"
    requirement: CHG-07
    verification:
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#never writes a revealed value to storage, the console, the mutation cache, the URL or the DOM afterwards"
        status: pass
      - kind: unit
        ref: "sources/ReactApp/tests/hosting/credentials-panel.test.tsx#holds no storage, cookie, console or error-text access"
        status: pass
    human_judgment: false
  - id: D7
    description: "Layout of the credential rows, the wrapped long revealed value and the touch target sizes at 320 and 390 px in English and German; the geometry is CSS only and jsdom cannot measure it"
    requirement: CHG-07
    verification: []
    human_judgment: true
    rationale: "Measured reflow is an automated Phase 65 e2e item planned for plans 65-14 and 65-15; this plan ships token-only CSS with overflow-wrap anywhere and asserts only the class hooks in jsdom"

duration: 10min
completed: 2026-11-20
status: complete
---

# Phase 65 Plan 11: Staged credentials with masked values and reveal Summary

**The credentials panel now stages a typed value or a rotation in the Settings draft, shows configured secrets as a fixed masked run, and lets allowed members reveal one value at a time for at most 30 seconds, with the value kept only in component state and never in storage, caches, URLs, logs or announcements.**

## Performance

- **Duration:** 10 min (2026-11-20T21:37Z to 21:47Z)
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 4 (2 created, 2 modified)

## Accomplishments

- Tracer: `ServerSecretsPanel` was rewritten as the body of the credentials wrapper. Each secret is an `article` with an `h3`, the status line, the required warning, the masked run (twelve `aria-hidden` bullets plus visually hidden `Value hidden`) and a shared `TextField` (`type="password"`, `autoComplete="new-password"`, `spellCheck={false}`) whose value comes only from the draft. Typing stages `{ kind: "set" }`, clearing unstages, and a Changed badge with Undo change appears while staged (focus returns to the input). The old local draft map, save and rotate mutations, raw controls, hard-coded English, kicker, inner heading and `error.message` are gone; load error shows fixed copy with Try again.
- Expansion: reveal through `useMutation({ gcTime: 0 })` and `mutateAsync` with `reset()` in a `finally` block; the value is copied into one panel-level state, announced through the Settings polite region and hidden by one effect that owns a 30-second timer and a `visibilitychange` listener. Hidden-state is derived during render (key, version and staging status must still match). Rotate stages `{ kind: "rotate" }` with the staged notice and Keep current value.
- `credentials.css` (token-only) and a 32-test file covering staging, apply, review, states, reveal, every hide trigger, rotate, the no-leak assertions (storage, console, mutation and query cache, URL, router state, live region, DOM, cookie) and the panel source scan.

## Task Commits

1. **Task 1 (tracer): stage credential changes in the settings draft** - `893964c9` (feat(webapp))
2. **Task 2: reveal credentials on demand and hide them again** - `732c50fb` (feat(webapp))

## Verification Results

- Tracer gate (Task 1, automated-only): credentials-panel, workspace-section-panels, change-flow and tests/styles green (84 tests), `typecheck` and eslint on touched files clean before expansion. Tracer verified end-to-end, then expanded.
- Plan-level: `vitest run tests/hosting tests/styles tests/locales` 86 files, 1370 tests passed; `locales:check` 3525 keys, 0 failures; `typecheck` exit 0; full `npm run lint` (`--max-warnings 0`) clean; `format:check` clean on every touched file.
- Acceptance greps: `<button` 0, `<input` 0, `error.message` 0, `stageSecret` 4, `Server credentials` 0, `gcTime: 0` 1, `visibilitychange` 2, `30_000` 1, forbidden storage/console pattern 0.
- Guard mutation check: removing the staging condition and the visibility check from the panel made exactly the three matching tests fail (file restored afterwards), so the hide-trigger tests are live.
- No commit touches `sources/WebApp`, `sources/Api` or `src/generated`; `SettingsSection.tsx` was not edited.

## Deviations from Plan

### Auto-fixed Issues

None.

### Plan wording that did not match the model

- The plan names no per-row Undo for a rotation beyond Keep current value; the generic Undo change button therefore renders only for a staged set, while the Changed badge shows for both kinds.
- The plan describes the reveal copy step as an `onSuccess` handler; the panel uses `mutateAsync` in an async handler so the reset can run in a `finally` block after the value was copied, and a failed reveal is tracked as a failed key because the mutation state is reset.
- The revealed state carries the display name as a fourth field next to key, value and version, so the timer effect can announce the hide without a lookup.
- A manual Hide also announces the hidden-again text (the plan lists announcements only for reveal and auto-hide); a pending reveal ignores further clicks in the handler instead of disabling the button, which would drop focus.

**Total deviations:** 0 auto-fixed, 4 wording notes. **Impact:** none on scope; every behavior line of both tasks is covered by a test.

## Issues Encountered

None.

## Known Stubs

None.

## Threat Flags

None. T-65-29 (reveal flow: `gcTime: 0`, immediate `reset()`, value only in component state, every hide trigger, tests over the mutation cache, storage and console), T-65-30 (typed value comes from the draft only and is absent from the DOM after apply, discard and Undo), T-65-31 (Reveal only for `canRevealSecrets && revealable && isConfigured`) and T-65-32 (fixed copy keys only, announcements name the secret and never its value) are mitigated and covered by the tests above.

## Next Phase Readiness

Plans 65-12 and 65-13 can stage profiles and mod packs on the same draft. Plan 65-15 can assert the credential reveal hygiene end to end against the same selectors (`Reveal value of {name}`, `Hide value of {name}`, the `code` element of the row and the `.sr-only` polite region). The measured 320 and 390 px layout of the credential rows remains an e2e item for plans 65-14 and 65-15.

## Self-Check: PASSED

- Created files verified on disk; commits `893964c9` and `732c50fb` verified as ancestors of HEAD.
