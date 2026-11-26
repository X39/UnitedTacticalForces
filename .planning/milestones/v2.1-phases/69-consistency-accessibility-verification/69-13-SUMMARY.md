---
phase: 69-consistency-accessibility-verification
plan: 13
subsystem: ui
tags: [react, i18n, disclosure, shared-controls, ratchet, hosting, accessibility]

requires:
  - phase: 69-03
    provides: "React_ServerWorkspace_Content* keys, Hosting_Detail_NoDeployment* and Hosting_Detail_OperationsEmpty* keys, EmptyState and LoadingState"
  - phase: 69-04
    provides: "family.hosting-runtime.json ratchet and the UTF_RATCHET_UPDATE writer"
provides:
  - "ServerRuntimePanel without raw controls or hard-coded English, on SelectField, TextField and ActionButton"
  - "Machine diagnostics, each log command, the audit evidence and the cost note on the shared Disclosure"
  - "An empty hosting-runtime ratchet family"
  - "tests/hosting/runtime-panels.test.tsx covering the runtime, machine, log and storage detail panels in English and German"
affects: [69-33, hosting workspace]

plan_head_before: c29899a31ab6ec50f1c861328f3ed9180bb50214
plan_head_after: 18865742fd5bc407aac3e01f33d1275c92ab80ae

actuals:
  tokens: 22000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Disclosure with local useState for per-entry expansion (logs keep the first entry open by initial state)"
    - "Compute fact written as one template literal so the unit text RAM stays literal without a locale key"

key-files:
  created:
    - sources/ReactApp/tests/hosting/runtime-panels.test.tsx
  modified:
    - sources/ReactApp/src/features/hosting/ServerRuntimePanel.tsx
    - sources/ReactApp/src/features/hosting/sections/CloudMachinePanel.tsx
    - sources/ReactApp/src/features/hosting/ServerLogsPanel.tsx
    - sources/ReactApp/src/features/hosting/HostingAuditFeed.tsx
    - sources/ReactApp/src/components/server/CostConfirmDialog.tsx
    - sources/ReactApp/src/features/hosting/hosting.css
    - sources/ReactApp/src/components/server/server.css
    - sources/ReactApp/tests/hosting/audit-feed.test.tsx
    - sources/ReactApp/tests/hosting/cost-confirm-dialog.test.tsx
    - sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx
    - sources/ReactApp/tests/ratchets/family.hosting-runtime.json

key-decisions:
  - "StorageDemandDetails needed no source change: it was already on Disclosure and the ratchet scan counts no literal in it, so the plan's one literal hit was stale; a German test now proves it."
  - "The compute fact `N vCPU · M GiB RAM` is one template literal because RAM is not a neutral token in rules.json and rules.json is owned by 69-04."
  - "Audit evidence puts its count inside the Disclosure title, so the button name reads `Technical evidence N`; the visible text node `Technical evidence` is unchanged for existing getByText queries."

patterns-established:
  - "Log protocol entries: Disclosure title carries action name and sequence, the summary line carries status badge and time"

requirements-completed: [CON-03, FND-06, L10N-03]

coverage:
  - id: D1
    description: "ServerRuntimePanel renders in German without its former English literals and uses only shared controls with labels"
    requirement: L10N-03
    verification:
      - kind: unit
        ref: "tests/hosting/runtime-panels.test.tsx#shows the content section in German without its former English literals"
        status: pass
      - kind: unit
        ref: "tests/ratchets/ratchet.test.ts (hosting-runtime family empty)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Machine diagnostics, each log command (first open) and audit evidence toggle through a Disclosure button with aria-expanded"
    requirement: CON-03
    verification:
      - kind: unit
        ref: "tests/hosting/runtime-panels.test.tsx#opens only the first log command and toggles each one through its own button"
        status: pass
      - kind: unit
        ref: "tests/hosting/audit-feed.test.tsx (technical evidence toggle)"
        status: pass
    human_judgment: false
  - id: D3
    description: "The cost note expands through Disclosure while initial focus, facts block and Tab order stay intact"
    requirement: FND-06
    verification:
      - kind: unit
        ref: "tests/hosting/cost-confirm-dialog.test.tsx#server note disclosure"
        status: pass
      - kind: e2e
        ref: "playwright: tests/e2e/hosting-workspace.spec.ts and hosting-a11y.spec.ts --project=chromium (35 passed)"
        status: pass
    human_judgment: false
  - id: D4
    description: "The disclosure restyle in hosting.css and server.css looks right next to the surrounding panels"
    verification: []
    human_judgment: true
    rationale: "Visual spacing of the restyled log, evidence, diagnostics and note disclosures is not asserted by a screenshot test"

duration: 45min
completed: 2026-11-25
status: complete
---

# Phase 69 Plan 13: Hosting Runtime Convergence Summary

**ServerRuntimePanel is localized and fully on shared controls, five native disclosure blocks (machine diagnostics, log commands, audit evidence, cost note) now use the shared Disclosure button, and the hosting-runtime ratchet family is empty.**

## Performance

- **Duration:** about 45 min
- **Completed:** 2026-11-25
- **Tasks:** 3 (1 tracer, 2 auto)
- **Files modified:** 11 (1 created)

## Accomplishments

- `ServerRuntimePanel`: the profile, storage strategy, terrain and scenario selects are `SelectField`, the external capacity is a `TextField` of type number, the three save buttons are primary `ActionButton`s, and the loading state is `LoadingState`. "Content", "Terrain and scenario", the unpinned-selection sentence and the save button label come from the 69-03 keys; the generic terrain and scenario labels reuse `Server_Overview_Terrain` and `Server_Overview_Scenario`. Units (vCPU, GiB, RAM) stay literal.
- `CloudMachinePanel`, `ServerLogsPanel` and `HostingAuditFeed` replace every `<details>` with a controlled `Disclosure`. Logs keep the first command open by initial state; the per-entry state survives job list refreshes. The audit evidence is shared with the Activity section, which gets the same behavior.
- `CostConfirmDialog` shows the server note through `Disclosure`; initial focus on dismiss and the facts block markup are asserted unchanged.
- Empty and loading states of the same files moved to `EmptyState` and `LoadingState` with the 69-03 title and body keys (`Hosting_Detail_NoDeployment*`, `Hosting_Detail_OperationsEmpty*`, `Hosting_Detail_NoLogsTitle`).
- `family.hosting-runtime.json` is `{"files": {}}`.

## Task Commits

1. **Task 1 (tracer): localize and converge the runtime panel** - `86108162` (refactor)
2. **Task 2: machine, log and audit details on Disclosure** - `82861182` (refactor)
3. **Task 3: cost note on Disclosure, runtime family empty** - `18865742` (refactor)

Commits carry the Co-Authored-By trailer; the plan metadata commit follows.

## Verification

- `npx vitest run tests/hosting tests/ratchets tests/locales tests/styles`: 2352 passed, 2 failed. Both failures are 5000 ms timeouts in `workspace-page.test.tsx` under machine load (eight sibling executors); the file passes alone (11 of 11) before and after Task 3.
- `npm run lint` (`--max-warnings 0`) and `npm run typecheck`: exit 0.
- `npm run test:e2e -- tests/e2e/hosting-workspace.spec.ts tests/e2e/hosting-a11y.spec.ts --project=chromium`: 35 passed, no flaky.
- Acceptance greps: `Terrain and scenario` count 0; raw control count in `ServerRuntimePanel.tsx` 0; `<details` count 0 in the three Task 2 files and in `CostConfirmDialog.tsx`; ratchet family file count 0.
- `npm run build:assets` regenerated `src/generated/api.ts` with a line-ending diff; it was restored with `git checkout --` and is not committed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Disclosure styling in hosting.css and server.css**
- **Found during:** Tasks 2 and 3
- **Issue:** The old rules targeted `details` and `summary` (`.next-hosting-protocol-list`, `.next-hosting-audit-evidence`, `.cost-confirm__note`); with Disclosure they no longer matched, so the entries would have lost their layout.
- **Fix:** Re-targeted the selectors to `.disclosure`, `.disclosure__summary`, `.disclosure__title` and `.disclosure__summary-line`, and dropped the now redundant focus and marker rules. No new color or off-grid value was added, so the legacy CSS ratchet is unchanged or lower. Neither file is in the plan's `files_modified`; neither is `styles.css`.
- **Files modified:** `sources/ReactApp/src/features/hosting/hosting.css`, `sources/ReactApp/src/components/server/server.css`
- **Commits:** `82861182`, `18865742`

**2. [Rule 1 - Bug] Existing tests asserted removed English text and native disclosure semantics**
- **Found during:** Task 2
- **Issue:** `workspace-section-panels.test.tsx` asserted the old one-line no-deployment text, and `audit-feed.test.tsx` asserted the old empty text and `closest("details")`.
- **Fix:** Updated them to the title and body pair and to the Disclosure button and `aria-controls` region. `workspace-section-panels.test.tsx` is outside the plan's `files_modified`.
- **Commit:** `82861182`

**3. [Scope] Empty and loading states converted in the same files**
- The 69-03 key map assigns the `Hosting_Detail_NoDeployment*` and `Hosting_Detail_OperationsEmpty*` keys to `CloudMachinePanel` and `HostingAuditFeed`, so those states moved to `EmptyState` and `LoadingState` here; `ServerLogsPanel` empty state moved to `EmptyState` with its existing keys.

**Total deviations:** 3 (1 blocking, 1 bug, 1 scope). **Impact:** all necessary to keep layout and tests coherent; no change to cost confirmation facts, focus behavior or the cost-bearing flow.

## Issues Encountered

- The worktree had no `node_modules`; a directory junction to the main checkout's install was created (git-ignored). Nothing was installed.
- Plan premise stale: `StorageDemandDetails` has no literal hit and already used `Disclosure` (see key-decisions).
- jsdom does not compute an accessible name for a hidden `role="region"`, so the audit feed test locates the evidence region through the toggle's `aria-controls`.

## Known Stubs

None.

## Threat Flags

None. T-69-18 is mitigated by the focus, facts and Tab order tests plus the passing e2e specs; T-69-19 is accepted (same API text, no new fetch).

## Next Phase Readiness

The hosting-runtime family is at zero. 69-33 can retire the unused `Hosting_Detail_NoDeployment`, `Hosting_Detail_OperationsEmpty` keys if no other reader remains.

## Self-Check: PASSED

- Created file exists: `sources/ReactApp/tests/hosting/runtime-panels.test.tsx`.
- Task commits are ancestors of HEAD: 86108162, 82861182, 18865742.
