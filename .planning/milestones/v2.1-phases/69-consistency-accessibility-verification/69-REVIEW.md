---
phase: 69-consistency-accessibility-verification
reviewed: 2026-11-26T09:00:00Z
depth: standard
diff_base: 5efa7c7e31ed7c20693e4e764a9631d254b4e90f
files_reviewed: 17
files_reviewed_list:
  - sources/ReactApp/scripts/check-locales.mjs
  - sources/ReactApp/src/components/CommandCenterLayout.tsx
  - sources/ReactApp/src/components/CommunityLayout.tsx
  - sources/ReactApp/src/components/ProfileMenu.tsx
  - sources/ReactApp/src/components/useDrawerFocus.ts
  - sources/ReactApp/src/components/useResponsiveNavigation.ts
  - sources/ReactApp/src/features/admin/GalleryAssetPicker.tsx
  - sources/ReactApp/src/locales/de.json
  - sources/ReactApp/src/locales/en.json
  - sources/ReactApp/src/locales/identical-allowed.json
  - sources/ReactApp/src/locales/shared-words-allowed.json
  - sources/ReactApp/tests/admin-media-pages.test.tsx
  - sources/ReactApp/tests/admin-site-images.test.ts
  - sources/ReactApp/tests/e2e/public-shell.spec.ts
  - sources/ReactApp/tests/locales/check-locales.test.ts
  - sources/ReactApp/tests/profile-menu.test.tsx
  - sources/ReactApp/tests/shell-layouts.test.tsx
findings:
  critical: 0
  warning: 2
  info: 8
  total: 10
  necessity: 0
status: issues_found
---

# Phase 69: Code Review Report (gap closure 69-40 to 69-42)

**Reviewed:** 2026-11-26T09:00:00Z
**Depth:** standard
**Files Reviewed:** 17 (changes since `5efa7c7e`; `CostAlertMenuItem.tsx`, `siteImages.ts`, `styles.css`, `EventDetailPage.tsx` and the real locale files were read to trace behavior)
**Status:** issues_found

## Summary

This review replaces the previous 69-REVIEW.md. It covers the second gap-closure round:

- 69-40: `[english-carryover]` locale rule, `shared-words-allowed.json`, and German values
- 69-41: document-level Tab handling in `useDrawerFocus`, `tabIndex={-1}` scrims, inert skip links, and the ProfileMenu/`useResponsiveNavigation` Escape ordering
- 69-42: `GalleryAssetPicker` null mapping for a 404

Checks run:

- `node scripts/check-locales.mjs` reports 0 failures and 0 warnings. With `--unused` it reports 46 `[unused-warn]` lines, which is 3 fewer than before because the three dead keys were deleted.
- I read the Tab and Escape code paths and traced the event order by hand. React handlers sit on the root container and run first. The document listeners (`ProfileMenu.closeOnEscape` and the new `cycleTab`) run next, and the window listener (`useResponsiveNavigation`) runs last.

All four findings of the previous review (WR-01 to WR-04) are fixed as specified. The remaining defects are at the edges:

- **Focus trap:** the new `nowhere` branch of `cycleTab` has no knowledge of native modal dialogs. A pointer click inside the sign-in dialog opened over the community menu leaves Tab dead (WR-01).
- **German copy:** the translated `Pages_Events_Detail_Status_ConfirmedTeam` label tells German users "confirmed member" for a row with two or more people (WR-02).
- **Locale gate:** hyphenated compounds bypass `[english-carryover]` on purpose. The rule has no check that a shared-word reason is one of the five documented categories. The English-allow stale check is blind to redundancy.

The rAF focus retry is leak-free: the cleanup cancels the pending frame on close, on dependency change and on unmount.

Necessity (category 4) was assessed against `necessity-review.md`. The focus retry guards a real condition (see IN-04), so no candidate met the evidence rule. `necessity: 0`.

## Warnings

### WR-01: `cycleTab` swallows Tab when a modal dialog is open over the community menu

**File:** `sources/ReactApp/src/components/useDrawerFocus.ts:115-131`; `sources/ReactApp/src/components/CommunityLayout.tsx:141-169,208-245`
**Issue:** The 69-41 fix handles a pointer click on non-focusable surface by treating `activeElement === body` as "nowhere" and sending focus to the first or last stop of the cycle. The handler has no knowledge of native modal dialogs. On a mobile viewport the community header actions stay visible and clickable above the scrim, and the signed-out sign-in button is one of them. The steps are:

1. Open the community menu. The hook is now listening on the document.
2. Tap "Sign in". `loginDialog.showModal()` opens the modal and makes everything else inert, including the menu toggle.
3. Tap the dialog's heading or other blank area. A non-focusable click moves `activeElement` to `body`.
4. Press Tab. `nowhere` is true, `cycle` is `[toggle, ...navLinks]`, and `position` is -1. The handler calls `event.preventDefault()` and then `first.focus()`.
5. `first` is the toggle, which is inert behind the modal. `focus()` is a no-op, but the default Tab action is already cancelled.

Tab and Shift+Tab then do nothing until the user clicks a control. The same applies to any `ConfirmDialog` opened while the menu is open. All the new tests use `.focus()` or a click on the drawer's own surface, so none of them covers this path.

**Fix:** Leave events alone while a modal dialog is open, and add a test for it.

```ts
const cycleTab = (event: KeyboardEvent) => {
  if (event.key !== "Tab" || event.defaultPrevented) return;
  // A modal dialog owns the keyboard; the drawer behind it is inert.
  if (document.querySelector("dialog:modal")) return;
  ...
```

If `:modal` support is a concern in the test environment, use `dialog[open]` and check `dialog.matches(":modal")` inside a try block, or compare against a ref the layout owns. Also reject a `cycle` whose `first.focus()` did not take focus: only call `preventDefault()` after `document.activeElement === target`.

### WR-02: The German label for a team row repeats "confirmed member"

**File:** `sources/ReactApp/src/locales/de.json:2258-2259`; `sources/ReactApp/src/locales/en.json:2258-2259`; `sources/ReactApp/src/features/events-member/EventDetailPage.tsx:782-790`
**Issue:** `Pages_Events_Detail_Status_ConfirmedTeam` is rendered when `row.assignedCount > 1`, and `ConfirmedMember` when `assignedCount === 1`. 69-40 translated both German values to the identical "Bestätigtes Mitglied", copying a defect from the English source, where both keys read "Confirmed member". A slot row with two or more assignees therefore shows a singular "member" badge in both languages. The gate cannot see this, because the German value is correct German for its English value.

**Fix:** Give the team key its own wording in both locale files, for example "Confirmed team" and "Bestätigtes Team" (or "Bestätigte Besetzung"). The two keys differ, so no `identical` rule applies.

## Info

### IN-01: Escape inside a native dialog also closes the navigation

**File:** `sources/ReactApp/src/components/useResponsiveNavigation.ts:7-9,24`; `sources/ReactApp/src/components/CommunityLayout.tsx:208-213`
**Issue:** The doc comment states that the navigation "closes last" and that Escape closes one layer at a time. That holds for layers that call `preventDefault()` (the profile popover and the React-handled pickers). A native `<dialog>` does not. Chrome closes it as the default action of the keydown, and `defaultPrevented` is still false when the window listener runs. Escape in the sign-in dialog (or in a `ConfirmDialog` opened from the community profile popover) therefore closes both the dialog and the open menu.
**Fix:** Ignore the Escape in the hook when `document.querySelector("dialog:modal")` matches, or have the layouts own the open state and pass a `suspended` flag. Narrow the doc comment if this is left as is.

### IN-02: Keyboard users cannot reach the header actions while the community menu is open

**File:** `sources/ReactApp/src/components/useDrawerFocus.ts:60-65,121-138`; `sources/ReactApp/src/components/CommunityLayout.tsx:141-169`
**Issue:** The doc comment says Tab from other elements "keeps that component's own order", and the 69-41 test pins select to sign-in. That order is only reachable by pointer. With `includeOpener`, the cycle is `[toggle, ...navLinks]`. Tab from the last link wraps to the toggle, and Shift+Tab from the toggle wraps to the last link. The language select and the login or profile control sit after the nav in the DOM and are never reached from the cycle. They are visible and not inert, so a keyboard user sees controls they cannot reach. Shift+Tab from the brand link also lands on the inert skip link and then leaves the page.
**Fix:** Either include the `.header-actions` controls in the cycle for the community menu, or make `.header-actions` and the brand link inert while the menu is open (matching `main` and the footer). Pick one model and state it in the hook's doc comment.

### IN-03: Hyphenated compounds bypass `[english-carryover]` by design

**File:** `sources/ReactApp/scripts/check-locales.mjs:162-170,205-207`; `sources/ReactApp/tests/locales/check-locales.test.ts` ("treats a hyphenated compound as one word")
**Issue:** `wordsOf` keeps hyphens inside a word, so `Hero-Kicker` or `Ablageort-Label` never matches the English word `label` or `hero`. I ran the same comparison with hyphens split. It finds 103 (key, word) pairs that the gate skips, such as `asset` in `Asset-Operatoren` and `Asset-Größe`, `inline` in `Inline-Bearbeitung`, and `label` in `Ablageort-Label`. Most are legitimate German loan compounds. The cost is a rule that behaves differently for the same word: a standalone `Asset` fails unless `asset` is listed, while `Asset-Größe` passes silently. It also means `Mission-image ausstehend` would pass. The test pins this behavior, but the gap is not recorded where a translator would look.
**Fix:** Either split on hyphens and add the loan parts (`asset`, `inline`, `hero`, `cloud`, `label`, ...) to `shared-words-allowed.json` with a reason, or keep the rule and state the blind spot in the `wordsOf` JSDoc and in the plan's summary.

### IN-04: The reduced-motion focus retry works around a CSS rule

**File:** `sources/ReactApp/src/components/useDrawerFocus.ts:20-21,86-100`; `sources/ReactApp/src/styles.css:4480-4492,4941-4953`
**Issue:** The retry is justified, but its cause is in CSS. The drawer opens with `transition: translate 180ms, visibility 0s linear 180ms` plus `transition-delay: 0s`, so visibility changes instantly. The reduced-motion block forces `transition-duration: 0.01ms !important` on everything. The visibility change then becomes a real 0.01 ms transition that starts at progress 0, where the element is still `hidden`. The first `focus()` fails. The JS retry then bridges one or two frames, and it gives up silently after 10. The delay of 180 ms on close is also left in place under reduced motion.
**Fix:** Fix the cause in the reduced-motion block, for example `.command-nav, .primary-nav { transition: none; }`, or keep the retry and document the CSS dependency. The retry itself needs no change: its cleanup cancels the frame on close, on a dependency change and on unmount. Note that a dependency change while open would cancel the retry without restarting it; the current dependencies are all stable refs.

### IN-05: Shared-word and English-allow validation is weaker than the plan claims

**File:** `sources/ReactApp/scripts/check-locales.mjs:345-365`; `sources/ReactApp/src/locales/shared-words-allowed.json`
**Issue:** The 69-40 summary says each word carries "one of five fixed reasons". The script only checks that the reason is not blank. Nothing enforces the five categories, and nothing checks that a key is lower case, a single word and at least three characters. A typo such as `"Server"` never matches (`Object.hasOwn(sharedWords, word)` uses the lower-cased word) and only produces an `[allow-stale]` warning. The `npm run locales:check` step still exits 0, so only the real-file Vitest test (`0 warning(s)`) catches it. The English-allow stale check calls `carriedOverWords(..., {}, ...)` with no shared words, so an entry that the shared list has made redundant is never reported. The reason for `gib` is "acronym, unit, file format...". That is correct for GiB, but `gib` is also an ordinary German imperative, so the entry hides a real word.
**Fix:** Fail `[allow-reason]` when the reason is not one of the five strings, fail `[allow-key]` for a key that is not `/^\p{Ll}{3,}$/u`, pass `sharedWordList` to the English-allow stale check, and make `[allow-stale]` fail in the script, not only in the test.

### IN-06: German terminology is still mixed in the lines 69-40 touched

**File:** `sources/ReactApp/src/locales/de.json:51,162-163,234-235`
**Issue:** The new values are correct German. A few conflict with their neighbors:

- Lines 162-163 now say "Karte, Mod-Paket und Bild", while the same page's keys say "Gelände" ("Geländebild erforderlich", "Gelände bestätigen") and "Modpack" ("Modpack deaktivieren"). Line 51 says "Material" where other keys say "Asset-Operatoren" and "Asset-Steuerung".
- `AccountStatusLabel` and `LinkedAccounts` (lines 236 and 256) were fixed, but lines 234-235 in the same block are still "Konto Zugriff Zusammenfassung" and "Konto Einstellungen". The second is visible link text in the profile menu and should be "Kontoeinstellungen".

**Fix:** Pick one noun per concept and run one pass over the file with the gate. Fix 234-235 with "Zusammenfassung des Kontozugriffs" and "Kontoeinstellungen".

### IN-07: `consoleError.mockRestore()` is not guarded by `finally`

**File:** `sources/ReactApp/tests/admin-media-pages.test.tsx:679,700`
**Issue:** The new test mocks `console.error` and restores it on its last line. `vitest.config.ts` sets none of `restoreMocks`, `mockReset` or `clearMocks`. A failing assertion between the two lines leaves the spy installed, hides console errors in later tests of the file, and makes the failure harder to read.
**Fix:** Move the body after the spy into `try { ... } finally { consoleError.mockRestore(); }`, or restore it in `afterEach`.

### IN-08: `loadMediaAsset` still breaks the null-on-404 rule and relies on its caller

**File:** `sources/ReactApp/src/features/admin/siteImages.ts:151-161`; `sources/ReactApp/src/features/admin/GalleryAssetPicker.tsx:86-87`; `sources/ReactApp/tests/admin-site-images.test.ts`
**Issue:** The 69-42 fix is correct for the one caller, and the test proves the query settles with `null` instead of erroring. The cause is not fixed. The function declares `expectedStatuses: [404]` and returns `undefined`. The 69-35 rule in `services.ts` says expected-none reads return `null`. The new `admin-site-images` test now pins the `undefined` contract. The next caller that passes the result to `useQuery` repeats the defect.
**Fix:** Return `MediaAsset | null` from `loadMediaAsset`, drop the `?? null` in the picker, and update the 404 test to expect `null`.

## Prior findings status

Each status was checked against the current code. Open items are out of this gap closure's scope and are not counted in the totals above.

| Prior ID | Status | Evidence |
|---|---|---|
| WR-01 (Command Center trap leaks through scrim and skip link) | **Resolved** | `CommandCenterLayout.tsx:87,138-141`: skip link has `inert={drawerOpen}`, scrim has `tabIndex={-1}`. `useDrawerFocus.ts:113-142` registers the handler on the document and handles the body case. Tests cover a click on the brand text and a measured blank area (e2e). |
| WR-02 (community trap breaks popover Tab order; one Escape closes both layers) | **Resolved, residual in IN-01/IN-02** | `cycleTab` returns for elements outside the cycle (`useDrawerFocus.ts:120`). `ProfileMenu.tsx:130` calls `preventDefault()`. `useResponsiveNavigation.ts:24` honors `defaultPrevented`. Unit and e2e tests press Tab from a middle popover item and Escape twice. A native dialog is not covered (IN-01). |
| WR-03 (four half-English German values) | **Resolved** | `de.json:57,81,142,2272` are translated. `[english-carryover]` is active and tested against the real files (0 failures, 0 warnings). Blind spots are IN-03 and IN-05. |
| WR-04 (`GalleryAssetPicker` passes `undefined` to TanStack Query) | **Resolved** | `GalleryAssetPicker.tsx:87` maps to `null`; `admin-media-pages.test.tsx:677` asserts query state `success` with `data === null`. The cause remains (IN-08). |
| IN-01 (toast announcement reads the dismiss name) | Open, out of scope | `AppToasts.tsx:159,167` unchanged since `5efa7c7e`. |
| IN-02 (toast in an inert region under a modal dialog) | Open, out of scope | `AppToasts.tsx` and `primitives.tsx` unchanged. |
| IN-03 (toast order) | Open, out of scope | `AppToasts.tsx` unchanged. |
| IN-04 (`alert-made-polite` accepts any conditional) | Open, out of scope | `a11y-static.test.ts` unchanged. |
| IN-05 (stale allow-list entries only warn) | **Partially mitigated** | The real-file test now asserts `0 warning(s)`, so a stale entry fails Vitest. `npm run locales:check` still exits 0 (see IN-05 above). |
| IN-06 (German wording and terminology) | **Partially resolved** | "Initiale Slots", "Command-Center-Navigation", "Kontostatus" and "Beschriftung für „Zuletzt geprüft“" are fixed. "Karte"/"Gelände" and "Modpack"/"Mod-Paket" remain mixed (IN-06 above). |
| IN-07 (hook selector generality) | **Resolved** | `useDrawerFocus.ts:7-37` now has a complete selector and filters disabled and `[inert]`. Visibility is documented as the caller's concern. |
| IN-08 (non-error `StatePanel` regions mount with content) | Open, out of scope | `primitives.tsx` unchanged. |
| IN-09 (focus returns to the toggle, not the new page) | Open, out of scope | `useDrawerFocus.ts:101-106` unchanged in intent; no route-change focus management exists. |
| Older: plural nouns for a count of 1; delete reuses "update failed" | Open, out of scope | `RoleDetailPage.tsx:345,348,359` and `:631` unchanged. |
| Older: disclosure chevron under reduced motion | Open, out of scope | Not touched by this round. |
| Older: role counts use the browser locale | Open, out of scope | `RolesPage.tsx:136,387,388` unchanged. |
| Older: tree-scanning guard tests time out under load | Open, out of scope | `tests/locales/ownership.test.ts` has no explicit timeout. |
| Older: Files panel tab pattern, `window.confirm` | Open, out of scope | `ServerFilesPanel.tsx:595`, `ConfigurationProfileCreatePage.tsx:102`, `primitives.tsx:602`. |
| Older: translation string surgery | Open, out of scope | `ConfigurationProfileCreatePage.tsx:129,163` still call `.replace(": {0}", "")`. |
| Older: dead and duplicated locale keys | Open, out of scope | 46 `[unused-warn]` lines, down from 49. |
| Older: `EventAdminPage` memoization defeated; duplicate `formatBytes` | Open, out of scope | `EventAdminPage.tsx:84`, `GalleryAssetPicker.tsx:29` unchanged. |

---

_Reviewed: 2026-11-26T09:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
