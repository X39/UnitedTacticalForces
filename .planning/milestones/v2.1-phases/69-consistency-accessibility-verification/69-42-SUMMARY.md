---
phase: 69-consistency-accessibility-verification
plan: 42
subsystem: webapp
tags: [react, tanstack-query, gallery-picker, expected-none, wr-04]
requires:
  - phase: 69-35
    provides: "Rule that every expected-none read maps to null for TanStack Query"
provides:
  - "GalleryAssetPicker selected-image query that resolves null for a deleted image"
  - "Service-level contract test for loadMediaAsset (200, 404, 403, 500)"
affects: [69-verification]
tech-stack:
  added: []
  patterns: ["Expected-none read mapped with ?? null at the useQuery call site"]
key-files:
  created: []
  modified:
    - sources/ReactApp/src/features/admin/GalleryAssetPicker.tsx
    - sources/ReactApp/tests/admin-media-pages.test.tsx
    - sources/ReactApp/tests/admin-site-images.test.ts
key-decisions:
  - "Only the declared 404 becomes null; 403 and 500 still reject, so the picker never shows an authorization or server failure as no selection"
  - "No notice panel for a deleted image; the designed outcome is no preselection with Apply unavailable"
requirements-completed: [CON-02]
status: complete
duration: 15min
completed: 2026-11-25
actuals:
  tokens: 1200
  tasks: 2
  commits: 2
plan_head_before: 5efa7c7e31ed7c20693e4e764a9631d254b4e90f
plan_head_after: 9e58682c52cfae17cb7f10941d97e307fe166541
commits: 2
---

# Phase 69 Plan 42: Gallery Picker Deleted-Image Handling Summary

**GalleryAssetPicker maps the 404 of its selected-image read to null, so a deleted image yields a successful query with no preselection instead of a failed query and a console error (WR-04).**

## Accomplishments

- Task 1 (tracer): `selectedQuery` in `GalleryAssetPicker.tsx` now awaits `loadMediaAsset` and returns `?? null`. A new picker test ("keeps the picker usable when the selected image no longer exists") first failed with query status "error", and now proves status "success" with data null, no console error mentioning "undefined", Apply staying `aria-disabled`, and choosing then applying `terrain.png` still calling `onSelect`. `renderPicker` now also returns its `QueryClient`.
- Task 2: a "loadMediaAsset expected-none contract" describe in `admin-site-images.test.ts` pins that a 404 resolves `undefined`, a 200 resolves the normalized asset (`tags` and `usages` empty), a 403 rejects with `forbidden`/403 and a 500 rejects with `server`/500. All four passed on the first run, so `siteImages.ts` needed no change.

## Task Commits

| Task | Commit | Message |
| ---- | ------ | ------- |
| 1 | a9a4fbdd | fix(webapp): treat a deleted gallery image as no selection |
| 2 | 9e58682c | test(webapp): pin the gallery image read contract |

## Verification

- `npm --prefix sources/ReactApp test -- tests/admin-site-images.test.ts tests/admin-media-pages.test.tsx`: 2 files, 44 tests passed.
- `npm run typecheck` (tsc -b) and `npm run lint` (eslint, max-warnings 0): clean.
- Tracer gate: the end-to-end verify command ran after Task 1 and passed before Task 2 started.

## Deviations from Plan

None - plan executed exactly as written. `npm ci` restored the lockfile-pinned dependencies in `sources/ReactApp` because `node_modules` was absent; no package was added or changed.

## Known Stubs

None.

## Threat Flags

None. T-69-56 is mitigated: only the declared 404 maps to null and the 403/500 rejections are pinned by Task 2.

## Self-Check: PASSED

- FOUND: sources/ReactApp/src/features/admin/GalleryAssetPicker.tsx (contains `?? null`)
- FOUND: test "keeps the picker usable when the selected image no longer exists" (1 occurrence)
- FOUND: `loadMediaAsset` and `"forbidden"` in sources/ReactApp/tests/admin-site-images.test.ts
- FOUND commits a9a4fbdd and 9e58682c
