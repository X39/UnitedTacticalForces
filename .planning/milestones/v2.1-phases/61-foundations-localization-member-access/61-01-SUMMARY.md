---
phase: 61-foundations-localization-member-access
plan: 01
subsystem: webapp-localization
tags: [react, i18next, localization, typescript, vitest]

requires:
  - phase: none
    provides: first plan of the phase
provides:
  - React-owned flat-key English and German locale files with an allow-list for identical values
  - Pure-node locale parity gate wired into build, build:assets and ci
  - deriveServerState, the single pure server state and cost model with the FND-03 invariant
  - ServerStateBadge, the shared icon plus label state badge, used by the fleet card
  - Typed plural keys Server_Fleet_PendingJobs_one and _other
affects: [61-02, 61-05, 61-07, 61-09, 61-12, phase-69-localization-walk]

actuals:
  tokens: 9000
  tasks: 3
  commits: 3
plan_head_before: 4aceeb3de5f822f332ad4d612f7d68d86cebe8f9
plan_head_after: f963d48b45bbf6687b89feb288ba066127989d62

tech-stack:
  added: []
  patterns:
    - "Locale JSON is hand edited, flat, sorted by localeCompare, and typed through TranslationKey"
    - "Status tables use satisfies Record<EDeploymentStatus, ...> so a new API status is a compile error"
    - "API-provided strings are looked up with Object.hasOwn before indexing a table"

key-files:
  created:
    - sources/ReactApp/src/locales/en.json
    - sources/ReactApp/src/locales/de.json
    - sources/ReactApp/src/locales/identical-allowed.json
    - sources/ReactApp/scripts/check-locales.mjs
    - sources/ReactApp/src/features/hosting/serverState.ts
    - sources/ReactApp/src/components/server/ServerStateBadge.tsx
    - sources/ReactApp/tests/hosting/server-state.test.ts
    - sources/ReactApp/tests/locales/check-locales.test.ts
    - sources/ReactApp/tests/locales/ownership.test.ts
    - sources/ReactApp/tests/locales/plural.test.ts
  modified:
    - sources/ReactApp/src/app/i18n.ts
    - sources/ReactApp/src/app/localizationTypes.ts
    - sources/ReactApp/src/i18next.d.ts
    - sources/ReactApp/src/features/hosting/components.tsx
    - sources/ReactApp/tests/mocks/react-icons.tsx
    - sources/ReactApp/tests/hosting/fleet.test.tsx
    - sources/ReactApp/package.json
    - sources/ReactApp/.prettierignore
    - docs/features/react-ui-beta.md
  removed:
    - sources/ReactApp/scripts/export-locales.mjs
    - sources/ReactApp/src/generated/locales/en.json
    - sources/ReactApp/src/generated/locales/de.json
    - sources/ReactApp/src/generated/locales/keys.ts

key-decisions:
  - "Plural spike (A3) passed: the typed i18n.t call with count compiles, so the _one/_other mechanism stands and Plan 61-12 uses it; no single-form fallback keys were created"
  - "React_ServerAudit_CategoryLifecycle stays English in German (allow-listed) because tests/hosting/audit-feed.test.tsx asserts the German label Lifecycle"
  - "Assets-labelled keys were translated to Material to match the existing German bare Assets key"

patterns-established:
  - "Locale gate: scripts/check-locales.mjs --en/--de/--allow, categories bad-key, missing-german, empty-german, orphan-german, placeholder, allow-reason; identical-warning and allow-stale are warnings"
  - "Server state surfaces call deriveServerState and render ServerStateBadge; no component compares deployment.status"

requirements-completed: [L10N-01, L10N-02, FND-02]

coverage:
  - id: D1
    description: "React app owns its EN/DE JSON locales; RESX exporter and generated locale files are gone"
    requirement: "L10N-01"
    verification:
      - kind: unit
        ref: "tests/locales/ownership.test.ts"
        status: pass
    human_judgment: false
  - id: D2
    description: "locales:check fails on missing, empty, orphan, placeholder-mismatched German values and dotted or colon keys, and runs first in build, build:assets and ci"
    requirement: "L10N-02"
    verification:
      - kind: unit
        ref: "tests/locales/check-locales.test.ts"
        status: pass
      - kind: other
        ref: "npm --prefix sources/ReactApp run locales:check"
        status: pass
    human_judgment: false
  - id: D3
    description: "A server that may still cost money is never labelled Off; the fleet card renders its state from deriveServerState and the German JSON"
    requirement: "FND-02"
    verification:
      - kind: unit
        ref: "tests/hosting/server-state.test.ts#never reports a server that may still cost money as off or not costing"
        status: pass
      - kind: unit
        ref: "tests/hosting/fleet.test.tsx#never labels a stopped server that may still cost as off"
        status: pass
    human_judgment: false
  - id: D4
    description: "Seed pruned to referenced keys; every identical EN/DE value is translated or allow-listed with a reason"
    verification:
      - kind: other
        ref: "node check of identical values not in identical-allowed.json prints 0"
        status: pass
    human_judgment: true
    rationale: "Quality of the German wording chosen for about 50 short UI labels needs a native-speaker read; the Phase 69 localization walk covers it"

duration: 17min
completed: 2026-11-18
status: complete
---

# Phase 61 Plan 01: Localization ownership and server state model Summary

**React now owns typed English and German JSON locales gated by a pure-node parity check, and the fleet card state badge renders from one pure `deriveServerState` function with a "may still cost" invariant.**

## Performance

- **Duration:** about 17 min (the start time was not captured at spawn; derived from STATE.md execution start 17:05Z)
- **Completed:** 2026-11-18T17:22Z
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files changed:** 23 (10 created, 9 modified, 4 removed)

## Accomplishments

- Tracer slice: locale JSON, typed `t()`, `deriveServerState`, `ServerStateBadge` and `ServerCard` are connected; the German fleet test shows "Läuft" with the `CheckmarkCircle16Regular` glyph and "Stopp nicht bestätigt" for a stopped server that may still cost.
- `deriveServerState` holds the FND-03 invariant over every `EDeploymentStatus` x `possibleCost` x `pendingJobCount` in {0,1} x deployment present/absent plus an unmapped string; `constructor`, `__proto__` and `toString` fall to unknown through `Object.hasOwn`.
- The 6,066-key seed (6,055 generated plus the 11 `Server_*` keys) was pruned to 3,034 referenced keys by an AST scan of every string and template literal under `src` and `tests`, including template families (`HostingFinance_LedgerKind_`, `React_ServerFiles_Status_`, `Pages_Public_Join_GuestStep`, `VereinStep`, `Pages_Unit_Index_Value`).
- 208 identical EN/DE values were resolved: 53 received a real German value, 155 are allow-listed with a reason. The `ModPacks_ImportExport` placeholder defect is fixed.
- `scripts/check-locales.mjs` gates build, build:assets and ci; the exporter, three generated files and the `locales:generate` script are removed, and `docs/features/react-ui-beta.md` describes the new ownership.

## Task Commits

1. **Task 1: Tracer - fleet card state label from pure model through React-owned locales** - `506a9ff5` (feat)
2. **Task 2: Prune the seed, complete German, plural spike** - `7291acb8` (chore)
3. **Task 3: Locale gate, wiring, RESX retirement** - `f963d48b` (build)

**Plan metadata:** committed separately (docs commit that carries this SUMMARY, STATE.md and ROADMAP.md).

## Verification Results

- `npm run typecheck`, `npm run lint`, `npm run locales:check`, `npm run build:assets`: pass.
- Full Vitest suite: 76 files, 368 tests pass (includes the 16 new locale tests, 7 state tests, 2 new fleet tests).
- `npm run format:check`: passes for every file except the pre-existing `tests/hosting/arma-placement.test.tsx` (see Deferred Issues).
- Task 2 acceptance node checks: no dotted or colon keys, no missing or empty German value, 3,034 keys (below 6,055), 0 identical values outside the allow-list.
- Task 3 acceptance: `git ls-files -- sources/ReactApp/src/generated/locales` prints nothing, the exporter is deleted, `check-locales.mjs` appears once in package.json.
- Tracer gate: verify re-run end to end after the commit, passed ("Tracer verified end-to-end - expanding").

## Decisions Made

- The plural spike succeeded, so `Server_Fleet_PendingJobs_one` and `_other` are the plural mechanism for Plan 61-12 (named `{{count}}` placeholder, `count` option on a typed `t()`); the single-form fallback was not needed.
- German wording follows existing seed terminology: Modpack, Operation, Jobs, Host, Worker, Claims and Material (for Assets) stay consistent with already-translated keys.
- `Lifecycle` stays English in the German audit category because an existing German test asserts that label.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Test path resolution under jsdom**
- **Found during:** Task 3 (RED run)
- **Issue:** `new URL(..., import.meta.url)` throws "The URL must be of scheme file" in the jsdom test environment.
- **Fix:** the locale tests resolve paths with `import.meta.dirname`.
- **Files modified:** `tests/locales/check-locales.test.ts`, `tests/locales/ownership.test.ts`
- **Commit:** `f963d48b`

**2. [Rule 1 - Bug] ESLint no-undef for beforeEach**
- **Found during:** Task 3 verification
- **Fix:** imported `beforeEach` from `vitest` as the plan's project rules require.
- **Commit:** `f963d48b`

**3. [Rule 1 - Bug] Lifecycle translation broke an existing German test**
- **Found during:** Task 2 (full suite run)
- **Issue:** translating `React_ServerAudit_CategoryLifecycle` made `tests/hosting/audit-feed.test.tsx` fail.
- **Fix:** restored the identical value and allow-listed it.
- **Commit:** `7291acb8`

**Total deviations:** 3 auto-fixed (1 blocking, 2 bugs). **Impact:** none on scope; all three were caught by the plan's own verification.

## Deferred Issues

- `tests/hosting/arma-placement.test.tsx` fails `prettier --check` locally because its working-tree copy has CRLF line endings while the index holds LF (`git ls-files --eol` reports `i/lf w/crlf`). It was not modified by this plan; a fresh checkout is unaffected. Normalizing its working copy is out of scope here.
- `Dockerfile` line 35 still copies `Language.resx` and `Language.de.resx` into the React build stage for the removed exporter. It is now dead but harmless; removing it belongs with a Docker-focused change (not in this plan's file list).
- Promoting the identical-value warning to a failure is deferred to Phase 69 as planned.

## Known Stubs

None.

## Threat Flags

None. T-61-01 and T-61-03 are mitigated by `check-locales.mjs` (spawn-tested); T-61-02 holds because `ServerStateBadge` renders `t()` output only as React text children.

## Next Phase Readiness

Plans 61-02 onward can add typed `Server_*` keys to `src/locales/{en,de}.json` and call `deriveServerState`; `ServerStatus` and `serverTone` remain exported in `features/hosting/components.tsx` for the workspace page until Plan 61-09 removes them.

## Self-Check: PASSED

- Created files exist on disk; commits `506a9ff5`, `7291acb8`, `f963d48b` are ancestors of HEAD; all task acceptance criteria re-run green.
