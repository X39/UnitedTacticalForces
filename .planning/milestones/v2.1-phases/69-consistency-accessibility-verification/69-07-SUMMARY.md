---
phase: 69-consistency-accessibility-verification
plan: 07
subsystem: webapp-verification
tags: [react, vitest, route-certification, url-parameters, ast-scan]
status: complete
requires: []
provides:
  - "src/app/urlParameters.ts: closed list of query parameters and hash anchors per route id"
  - "tests/app/url-parameters.test.ts: type-checker AST scan of URL-state readers and writers against the contract"
  - "tests/app/route-links.test.ts: AST scan proving every internal link resolves through matchRoute with declared keys and anchors"
affects: [69-verification]
tech-stack:
  added: []
  patterns:
    - "TypeScript compiler API scan with the type checker (receiver typed URLSearchParams, SetURLSearchParams callee) instead of name heuristics"
    - "Explicit file tables (URL state files, API query builder files, pass-through files) so new URL state cannot appear unclassified"
key-files:
  created:
    - sources/ReactApp/src/app/urlParameters.ts
    - sources/ReactApp/tests/app/url-parameters.test.ts
    - sources/ReactApp/tests/app/route-links.test.ts
  modified: []
key-decisions:
  - "Receivers are recognized through the type checker (about 4 s for 33 files), not by variable names"
  - "API and WebSocket query builders (14 files) are excluded through an explicit table, each file must still contain a URLSearchParams call"
  - "slotSetup is declared for command-event-edit only; event-detail has no reader (see Deviations)"
requirements-completed: [VER-01]
duration: 25 min
completed: 2026-11-25
commits: 2
plan_head_before: 0dc5b44741413cd711bd82c9b7a606378e97977a
plan_head_after: bf5e9e59af31517192f632c8bf7949c4af70fa19
actuals:
  tokens: 21000
  tasks: 2
  commits: 2
coverage:
  - deliverable: "URL parameter contract per route id (urlParameters.ts)"
    verification:
      - kind: test
        ref: "tests/app/url-parameters.test.ts"
        status: pass
    human_judgment: false
  - deliverable: "Reader and writer scan with stale-key and enum reachability checks"
    verification:
      - kind: test
        ref: "tests/app/url-parameters.test.ts"
        status: pass
    human_judgment: false
  - deliverable: "Internal link resolution scan (195 links)"
    verification:
      - kind: test
        ref: "tests/app/route-links.test.ts"
        status: pass
    human_judgment: false
  - deliverable: "returnUrl local-path guard asserted against absolute and protocol-relative URLs"
    verification:
      - kind: test
        ref: "tests/app/url-parameters.test.ts#returnUrl"
        status: pass
    human_judgment: false
---

# Phase 69 Plan 07: URL Parameter Certification Summary

A typed per-route contract of query keys and hash anchors (`urlParameters.ts`) plus two Vitest AST scans: one keeps every URL-state reader and writer inside the contract in both directions, the other proves that all 195 internal link literals resolve through `matchRoute` with declared keys and anchors.

## Accomplishments

- `src/app/urlParameters.ts` declares `server` (`tab` from `workspaceTabIds`, `action`, `path`, `area`; anchors `server-automation` and the `activity-<type>-<id>` pattern), `servers` (`q`, `filter` from `fleetFilters`), `server-cost-alerts` (`owner` guid), `configuration-profile` (`tab`), `command-hosting-finance` (`search`, `kind`, `status`, `page`), `command-event-edit` (`slotSetup`), `authentication-action` (`returnUrl` as local path) and `events-upcoming` (`mode`). `isDeclaredQueryKey` uses `Object.hasOwn`, so prototype keys never match. `routes.json` and all certification flags are unchanged.
- `tests/app/url-parameters.test.ts` builds a TypeScript program over the app, finds the sites (at least 10 reads and 20 in total, asserted; reads, writes, `setSearchParams({...})` keys, `?key=` literals), and fails on: an undeclared key, a non-literal key outside a registered helper (`setFilter` of the finance page), a file in neither the URL-state nor the API-builder table, an unclassified `location.search` reader, a declared key with no reader (stale), and an enum value that its writer cannot produce or that never appears in the code. An inline virtual-source sample proves the scan reports violations. It also asserts that `localReactReturnPath` and `localReturnUrl` reject `https://evil.example`, `//evil.example` and `javascript:` values (T-69-10).
- `tests/app/route-links.test.ts` scans `to=` and `href=` attributes, `to:`/`href:` properties and `navigate(` calls (templates expanded with guid, long and free samples, conditional branches both followed) and checks path, query keys and anchors. 195 links scanned, no dead link found, including `?tab=settings#server-automation` and the relative `{ search, hash }` location of the auto-stop notice. Helper unit cases cover `sectionTo`, `activityEntryTo` and `filesLocationTo`.

## Task Commits

1. Task 1 (tracer): `57ac49c6` test(webapp): certify URL parameters per route
2. Task 2: `bf5e9e59` test(webapp): prove every internal link resolves

Tracer gate: the tracer `<verify>` is automated-only, re-run end to end after the commit (`url-parameters`, `routes`, `hosting/routes`: 27 passed), then expansion proceeded.

## Verification

- `npm test -- tests/app tests/routes.test.ts tests/hosting/routes.test.ts tests/events-member/route-contract.test.ts`: 7 files, 59 tests passed.
- `npm run lint` (`--max-warnings 0`) and `npm run typecheck`: clean. Prettier check clean on the three files.
- Acceptance greps: `server-automation` and `workspaceTabIds` in `urlParameters.ts`, `localReturnUrl` in `url-parameters.test.ts`, `matchRoute` and `tab=settings#server-automation` in `route-links.test.ts`: all present.

## Deviations from Plan

### Auto-fixed Issues

None.

### Plan interpretation

**1. slotSetup declared for `command-event-edit` only.** The plan and UI-SPEC list `event-detail` and `command-event-edit`. `slotSetup` is written by `EventCreatePage` toward `/command/events/{guid}` (route `command-event-edit`) and read by `EventEditPage`; `EventDetailPage` (`/events/{guid}`) never reads it. Declaring it for `event-detail` would be a stale key and fail the stale check, so it is not declared there.

**2. Extra enum values declared beyond the UI-SPEC table.** `kind` (`User`, `Verein`, `ExternalOrganization`), `status` (`active`, `frozen`), `mode` (`board`, `calendar`) and the profile tab ids (`configuration`, `revisions`, `access`) are declared as closed lists taken from the code (the UI-SPEC left them unspecified or free). Each value is checked to appear as a literal in its owning file.

**3. Action ids kept as a local constant.** `actionVocabulary.ts` does not export its id list, and it is outside this plan's file list. `serverActionIds` lives in `urlParameters.ts` and the test asserts each id resolves through `lookupActionVocabulary`.

**Total deviations:** 0 auto-fixed, 3 interpretation notes. **Impact:** none on scope; no existing file was modified.

## Cross-Plan Findings

None. The link scan found no dead link, so no file owned by another Wave 1 plan needed a change.

## Authentication Gates

None.

## Known Stubs

None.

## Threat Flags

None. T-69-10 (open redirect through `returnUrl`) is mitigated by the contract declaration plus the `localReturnUrl` assertions; T-69-11 is accepted as planned.

## Notes

- `npm ci` was run in the worktree to obtain `node_modules` (lockfile-pinned, no package added or changed; `git status` showed no lockfile change).
- The scan needs about 4 s to build the type-checked program; the `beforeAll` timeout is 120 s.

## Self-Check: PASSED

- FOUND: sources/ReactApp/src/app/urlParameters.ts
- FOUND: sources/ReactApp/tests/app/url-parameters.test.ts
- FOUND: sources/ReactApp/tests/app/route-links.test.ts
- FOUND commit 57ac49c6, FOUND commit bf5e9e59
