---
phase: 64-guided-server-creation
plan: 07
subsystem: ui
tags: [react, react-hook-form, hosting, create-server, guided-create, early-block, live-estimate]
status: complete
requires:
  - phase: 64-01
    provides: Server_Create_* copy, createStartState router state
  - phase: 64-02
    provides: resolveCreateDefaults, deriveCreateReadiness, estimateCreateCost, isStorageMisfit and the create fixtures
  - phase: 64-04
    provides: CreateSummary, CostOwnerLine, AfterCreatingChoice
  - phase: 64-05
    provides: AdvancedOptions, StorageOptions
  - phase: 64-06
    provides: SizeField
provides:
  - CreateServerPage rewritten as the guided, capability-driven create page (composition and form state only)
  - Page grid, flat sections and the bottom-bar scroll reserve in create.css
  - Page tests for CRT-01 to CRT-04 and the start-flow integration test on the After creating choice
affects: [64-08, 64-09]
requirements-completed: [CRT-01, CRT-02, CRT-03, CRT-04]
commits: 3
plan_head_before: 18ea7712225a115922e8b74fef8cd6da2436e9eb
plan_head_after: b29323153e663f8c1cef729b16aea174690f62d6
actuals:
  tokens: 60000
  tasks: 3
  commits: 3
key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/hosting/CreateServerPage.tsx
    - sources/ReactApp/src/components/server/create.css
    - sources/ReactApp/tests/hosting/create-server-page.test.tsx
    - sources/ReactApp/tests/hosting/create-start-flow.test.tsx
key-decisions:
  - "Validation requirements (required terrain and scenario, provider storage bounds) are derived inside the form resolver from the values being validated, not from a memoised schema, because the schema needs values that only exist after useForm"
  - "Defaults are applied with form.register(field) followed by form.resetField(field, { defaultValue }); resetField is a no-op for a field without a registered input (single game, single provider, hidden terrain), so those fields are registered first"
  - "SizeField and StorageOptions are keyed by game and provider so their uncontrolled selects remount and receive the resolved value once the new options exist"
  - "The post-create baseline reset is wrapped in flushSync so the unsaved-changes guard is quiet before navigation even when the invalidations settle at once"
  - "Switching the game resets provider, size, storage, capacity, mod pack, terrain, scenario and configuration profile and drops them from touched so the defaults re-resolve (64-02 flagged item); cost owner and launch-disabled are kept"
coverage:
  - id: D1
    description: "A member who only types a name can create; every other value is a default and a member choice is never overwritten"
    requirement: "CRT-01"
    verification:
      - kind: unit
        ref: "tests/hosting/create-server-page.test.tsx#guided create page"
        status: pass
    human_judgment: false
  - id: D2
    description: "Live estimate follows the money rule and never invents an amount"
    requirement: "CRT-02"
    verification:
      - kind: unit
        ref: "tests/hosting/create-server-page.test.tsx#live estimate"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every blocking reason shows before any request; Enter and click never submit while blocked"
    requirement: "CRT-03"
    verification:
      - kind: unit
        ref: "tests/hosting/create-server-page.test.tsx#early block"
        status: pass
    human_judgment: false
  - id: D4
    description: "Keep it off and Start it now both land in the workspace; start opens the shared confirmation"
    requirement: "CRT-04"
    verification:
      - kind: unit
        ref: "tests/hosting/create-start-flow.test.tsx#create then start flow"
        status: pass
    human_judgment: false
---

# Phase 64 Plan 07: Guided Create Page Summary

The 12-field create form is replaced by one capability-driven page: Name, Game and Size up front, a read-only Billed to line, a collapsed Advanced options disclosure, an After creating choice defaulting to Keep it off, and the sticky cost summary DOM-last.

## Accomplishments

- **Task 1 (tracer)**: Rewrote `CreateServerPage.tsx` from scratch as composition plus form state: breadcrumbs, header, the loading, forbidden, load-error (with Try again) and empty states, the Your server section, After creating, Leave without creating and the summary. Defaults resolve through `resolveCreateDefaults` without dirtying the form. Create resets the baseline, invalidates fleet, notifications and the cost-owner list, shows the toast and opens the workspace (with `createStartState` only for Start it now). Added the page layout to `create.css`. Commit `07f73806`.
- **Task 2**: Composed `AdvancedOptions` and `StorageOptions`; Change cost owner opens Advanced inside `flushSync` and focuses the owner select; a provider change re-resolves size and storage; turning launching off forces Keep it off (turning it back on changes nothing). Commit `d50b2cf3`.
- **Task 3**: Proved every blocking reason (frozen owner, owners loading and failed, required list loading and failed, no provider, estimate running and failed, storage misfit), Enter-key blocking, replacing a frozen owner, the live estimate and the no-price case through the page; the start-flow test now uses the shared fixtures. Commit `b2932315`.

## Verification

- `tests/hosting tests/locales tests/styles`: 75 files, 1168 tests passed (includes `create-capability-driven`, which scans the page for game identifier literals).
- `typecheck` and `lint --max-warnings 0` exit 0; `prettier --check --end-of-line auto` passes for all four touched files (repo-wide `format:check` fails in this CRLF checkout for pre-existing files).
- Acceptance greps: no `useHostingCopy`, `errorMessage`, `next-hosting-form-grid`, raw `<input>`, `<select>`, `<button>` or `Server_Create_Submit_Start` in the page; `routes.json` has no diff; no commit touches `sources/WebApp`, `sources/Api` or `src/generated`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Defaults did not reach fields without an input**
- **Found during:** Task 1 design (verified in the installed react-hook-form 7.85 source)
- **Issue:** `resetField` does nothing for a field that is not registered, so the game (single capability), provider (single provider) and the unmounted terrain would never receive their default.
- **Fix:** the defaults effect registers each changed field first (`form.register(field)`, idempotent) and then calls `resetField`.
- **Files modified:** `sources/ReactApp/src/features/hosting/CreateServerPage.tsx`
- **Commit:** `07f73806`

**2. [Rule 1 - Bug] Uncontrolled selects showed the wrong option after the options changed**
- **Found during:** Task 1 design
- **Issue:** a registered select receives its value when the field default is written, which can happen before the options of the resolved provider exist, leaving the DOM and the form out of step.
- **Fix:** `SizeField` and `StorageOptions` are keyed by game and provider, so they remount and the form writes its value after the options exist.
- **Commit:** `07f73806`, `d50b2cf3`

**3. [Rule 1 - Bug] Unsaved-changes prompt after a successful create**
- **Found during:** Task 1 (the page test saw `window.confirm` called)
- **Issue:** with the plan's order (reset, await invalidations, navigate), the reset had not rendered before navigation when the invalidations settled immediately, so the guard prompted.
- **Fix:** `flushSync(() => form.reset(values))`.
- **Commit:** `07f73806`

**4. [Plan wording] Schema built in the resolver, not memoised**
- The plan asks for a `useMemo` schema with capability requirements, but the requirements depend on watched values that need the form that needs the schema. The resolver now derives the requirements from the values it validates; behavior is identical and takes effect at once.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. T-64-13 mitigated (the submit handler returns while a readiness reason exists or a create is pending; the summary button prevents blocked clicks and Enter). T-64-14 mitigated (the workspace URL uses only `server.primaryKey`; the router state carries no id). T-64-15 mitigated (fixed copy for load and create failures; no `Error.message` rendered, covered by tests that reject with secret-bearing errors).

## Notes for plans 64-08 and 64-09

- Plan 64-08 owns presets, auto-opening Advanced on an error in a hidden field, focus on the first invalid field (`shouldFocusError` is false here), the pending and request-error focus and the polite announcements (`Server_Create_Game_Announce`, `Server_Create_Cost_Announce`).
- The page keeps one `touched` set and one `markTouched` callback; presets should write through `form.setValue` and call `markTouched` for the two fields they set.
- Assumption carried from 64-02 (OQ1): a mod pack is never required at create. The user should confirm.

## Self-Check: PASSED

- FOUND: sources/ReactApp/src/features/hosting/CreateServerPage.tsx
- FOUND: sources/ReactApp/src/components/server/create.css
- FOUND: sources/ReactApp/tests/hosting/create-server-page.test.tsx
- FOUND: sources/ReactApp/tests/hosting/create-start-flow.test.tsx
- FOUND commits: 07f73806, d50b2cf3, b2932315
