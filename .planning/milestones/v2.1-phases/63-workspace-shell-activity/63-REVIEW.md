---
phase: 63-workspace-shell-activity
reviewed: 2026-11-19T00:00:00Z
depth: standard
files_reviewed: 89
files_reviewed_list:
  - docs/features/react-ui-beta.md
  - sources/Api/Controllers/LifecycleJobsController.cs
  - sources/Api/DTO/HostingOperationDto.cs
  - sources/Api/DTO/LifecycleJobDto.cs
  - sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs
  - sources/ReactApp/openapi/utf-api.json
  - sources/ReactApp/src/app/format.ts
  - sources/ReactApp/src/app/useNow.ts
  - sources/ReactApp/src/components/Breadcrumbs.tsx
  - sources/ReactApp/src/components/CopyButton.tsx
  - sources/ReactApp/src/components/SectionNavigation.tsx
  - sources/ReactApp/src/components/commandCenter.css
  - sources/ReactApp/src/components/server/ActivityEntry.tsx
  - sources/ReactApp/src/components/server/ActivityTimeline.tsx
  - sources/ReactApp/src/components/server/CostStrip.tsx
  - sources/ReactApp/src/components/server/JobFailurePanel.tsx
  - sources/ReactApp/src/components/server/JobProgressStrip.tsx
  - sources/ReactApp/src/components/server/LiveBadge.tsx
  - sources/ReactApp/src/components/server/ServerEndpoint.tsx
  - sources/ReactApp/src/components/server/SlotPlaceholder.tsx
  - sources/ReactApp/src/components/server/WorkspaceHeader.tsx
  - sources/ReactApp/src/components/server/activity.css
  - sources/ReactApp/src/components/server/costConfirmationModel.ts
  - sources/ReactApp/src/components/server/job-status.css
  - sources/ReactApp/src/components/server/navigation.css
  - sources/ReactApp/src/components/server/sections.css
  - sources/ReactApp/src/components/server/useCostConfirmation.ts
  - sources/ReactApp/src/components/server/workspace-shell.css
  - sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx
  - sources/ReactApp/src/features/hosting/HostingAuditFeed.tsx
  - sources/ReactApp/src/features/hosting/ServerRuntimePanel.tsx
  - sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx
  - sources/ReactApp/src/features/hosting/WorkspaceShell.tsx
  - sources/ReactApp/src/features/hosting/activityLabels.ts
  - sources/ReactApp/src/features/hosting/activityModel.ts
  - sources/ReactApp/src/features/hosting/hosting.css
  - sources/ReactApp/src/features/hosting/jobFailure.ts
  - sources/ReactApp/src/features/hosting/jobProgress.ts
  - sources/ReactApp/src/features/hosting/sections/ActivitySection.tsx
  - sources/ReactApp/src/features/hosting/sections/CloudMachinePanel.tsx
  - sources/ReactApp/src/features/hosting/sections/MachineSection.tsx
  - sources/ReactApp/src/features/hosting/sections/OverviewSection.tsx
  - sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx
  - sources/ReactApp/src/features/hosting/useFleetLiveUpdates.ts
  - sources/ReactApp/src/features/hosting/useJobAnnouncement.ts
  - sources/ReactApp/src/features/hosting/useWorkspaceLiveUpdates.ts
  - sources/ReactApp/src/features/hosting/workspaceFreshness.ts
  - sources/ReactApp/src/features/hosting/workspaceModel.ts
  - sources/ReactApp/src/features/hosting/workspaceSections.ts
  - sources/ReactApp/src/generated/api.ts
  - sources/ReactApp/src/locales/de.json
  - sources/ReactApp/src/locales/en.json
  - sources/ReactApp/src/locales/identical-allowed.json
  - sources/ReactApp/src/styles/tokens.css
  - sources/ReactApp/tests/breadcrumbs.test.tsx
  - sources/ReactApp/tests/copy-button.test.tsx
  - sources/ReactApp/tests/e2e/hosting-placement.spec.ts
  - sources/ReactApp/tests/e2e/hosting-workspace.spec.ts
  - sources/ReactApp/tests/helpers/workspaceFixtures.ts
  - sources/ReactApp/tests/hosting/activity-entry.test.tsx
  - sources/ReactApp/tests/hosting/activity-labels.test.ts
  - sources/ReactApp/tests/hosting/activity-model.test.ts
  - sources/ReactApp/tests/hosting/activity-section.test.tsx
  - sources/ReactApp/tests/hosting/audit-feed.test.tsx
  - sources/ReactApp/tests/hosting/cost-confirm-model.test.ts
  - sources/ReactApp/tests/hosting/cost-strip.test.tsx
  - sources/ReactApp/tests/hosting/deep-link.test.tsx
  - sources/ReactApp/tests/hosting/fleet-live-updates.test.tsx
  - sources/ReactApp/tests/hosting/fleet-page.test.tsx
  - sources/ReactApp/tests/hosting/job-failure.test.tsx
  - sources/ReactApp/tests/hosting/job-progress-strip.test.tsx
  - sources/ReactApp/tests/hosting/job-progress.test.ts
  - sources/ReactApp/tests/hosting/live-badge.test.tsx
  - sources/ReactApp/tests/hosting/parity-matrix.test.ts
  - sources/ReactApp/tests/hosting/workspace-fixtures.test.ts
  - sources/ReactApp/tests/hosting/workspace-freshness.test.ts
  - sources/ReactApp/tests/hosting/workspace-header-bar.test.tsx
  - sources/ReactApp/tests/hosting/workspace-header.test.tsx
  - sources/ReactApp/tests/hosting/workspace-live-updates.test.tsx
  - sources/ReactApp/tests/hosting/workspace-model.test.ts
  - sources/ReactApp/tests/hosting/workspace-page.test.tsx
  - sources/ReactApp/tests/hosting/workspace-section-panels.test.tsx
  - sources/ReactApp/tests/hosting/workspace-sections.test.ts
  - sources/ReactApp/tests/hosting/workspace-shell.test.tsx
  - sources/ReactApp/tests/hosting/workspace-vocabulary.test.ts
  - sources/ReactApp/tests/i18n-format.test.ts
  - sources/ReactApp/tests/locales/server-keys.test.ts
  - sources/ReactApp/tests/mocks/react-icons.tsx
  - tests/Api.Tests/GameServerHosting/GameServerProjectionServiceTests.cs
findings:
  critical: 0
  warning: 6
  info: 6
  total: 12
  necessity: 0
status: issues_found
---

# Phase 63: Code Review Report

**Reviewed:** 2026-11-19
**Depth:** standard
**Files Reviewed:** 89
**Status:** issues_found

## Summary

Scope was the 89 non-planning files changed between `eab7b5fa` and `HEAD`: the nickname projection in the API, the
React workspace shell, the Activity section, live updates, and their tests. The three uncommitted working-tree changes
(`FleetLiveUpdatesProvider.tsx`, `useWorkspaceLiveUpdates.ts`, `workspace-live-updates.test.tsx`) differ from `HEAD`
only in line endings. Generated `api.ts` and `utf-api.json` match the DTO change (`requestedByName` and `actorName`,
both `null | string`).

Checks that came back clean:

- **Actor-name audience.** Names are projected only after the existing `Server.View` checks. The list endpoints are
  guarded by `[Authorize(Claims.ResourceBased.Server.View)]`. `LifecycleJobsController.GetAsync` calls
  `HasServerPermission` before `ProjectJob`. Only `Nickname` is projected, never an id or e-mail, and deleted users are
  masked. No secret exposure was found.
- **XSS.** API text is rendered only as React text children or inside `ApiText`. No `dangerouslySetInnerHTML` is used.
  The `?tab=` and `?action=` parameters are matched with `find` and `includes`, never used as an index.
- **Locales.** en and de have identical key sets and identical placeholder sets per key. Every `Server_*` key used in
  source resolves; plural keys use `_one` and `_other`.
- **Live-update subscriptions.** Subscriptions, coalescers and timers are cleaned up on unmount. Invalidations use
  `exact: true`.
- **AGENTS.md C# rules.** The new C# members carry XML docs and there is no enum or file-layout violation. The tests are
  documented.

There are no critical issues. The most consequential problems are an API contract bug that labels the caller as
"Hidden member" in the 202 responses (WR-01), a relative-time formatter that renders "in N seconds" (WR-02), and a
workspace that is torn down by a single failed background refetch (WR-03).

## Warnings

### WR-01: Retry, skip and action 202 responses report the live requester as "Hidden member"

**File:** `sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs:656,707-712`; callers
`sources/Api/Controllers/LifecycleJobsController.cs:53,76` and `sources/Api/Controllers/GameServersController.cs:155`
**Issue:** `ProjectActorName` maps "`RequestedByFk` set but `RequestedBy` navigation null" to the hidden placeholder.
Only the read paths (`GetJobsAsync`, `LifecycleJobsController.GetAsync`) `Include(RequestedBy)`. The three write
endpoints call `ProjectJob` on entities freshly built by `GameServerLifecycleService`:

- `EnqueueActionAsync` sets only `RequestedByFk = actorId` (`GameServerLifecycleService.cs:553`).
- `RetryAsync` builds `retry` the same way (`:651`).
- `SkipAsync` loads the job without `.Include(RequestedBy)`.

As a result the `LifecycleJobDto` in every `202 Accepted` response reports `RequestedByName = "Hidden member"` for a
living user. For retry and enqueue that user is the caller. For skip it is the original requester. The SPA discards
these bodies today, so nothing is visibly wrong yet. The contract is nonetheless incorrect for any other API consumer
and for any later optimistic cache insert. The test `RequestedByName_UsesPlaceholderWhenNavigationIsMissing` asserts
this behaviour as intended, and no test covers the write endpoints.
**Fix:** Resolve the nickname for the write paths instead of treating "not loaded" as "deleted". For example, have
the controllers re-read the job with `.Include(q => q.RequestedBy)` after the service call. Alternatively, give
`ProjectJob` an async overload that looks up `User` by `RequestedByFk` when the navigation is null. Replace the
placeholder-on-missing-navigation test with one that exercises `RetryAsync`, `SkipAsync` and `EnqueueActionAsync`
responses and expects the nickname.

### WR-02: `formatRelativeTime` renders future text ("in 12 seconds") because callers pass a stale `now`

**File:** `sources/ReactApp/src/app/format.ts:57-70`; callers `src/components/server/ActivityEntry.tsx:364`,
`JobProgressStrip.tsx:186`, `JobFailurePanel.tsx:70`, `LiveBadge.tsx:87`
**Issue:** `now` comes from `useNow`, which ticks every 15 to 30 s (`useNow(clockIntervalMs)`) and is captured in
state. Any timestamp newer than the last tick yields a positive delta. Examples: a job just requested, a refetch that
completes between ticks (the live badge's "Updated ..." line), or a server clock slightly ahead of the client. With
`numeric: "auto"` the formatter then returns "in 14 seconds" or "in 1 minute". The "just now" case is the common one.
It appears right after the user triggers an action and the new job entry or progress strip renders ("Requested in 9
seconds"), and every time the badge says "Updated in 12 seconds". `tests/i18n-format.test.ts` covers only past values.
**Fix:** Clamp to the past in the formatter, or pass `Math.max(now, Date.now())` at the call sites:
```ts
const deltaSeconds = Math.min(0, Math.round((time - now) / 1000));
```
Add a test for a value a few seconds after `now` that expects "now" or "0 seconds ago".

### WR-03: A failed background refetch replaces the whole workspace with the error panel

**File:** `sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx:137`
**Issue:** The page branches on `server.isError`. In TanStack Query v5 a failed refetch sets `status: "error"` while
keeping the previous `data`, so `isError` is true with data present. This phase adds a 30 s fallback poll and
live-signal invalidations to the server query, which makes that case reachable on any transient failure. The branch
then unmounts the entire shell. That discards Activity filters, expanded entries and paging, the Settings
configuration editor's unsaved input, and the Files panel state. It also replaces a usable page with `HostingError`.
The activity props right below correctly use `isError && data === undefined`, so the page is inconsistent.
**Fix:**
```tsx
) : server.isError && server.data === undefined ? (
  <HostingError onRetry={() => void server.refetch()} />
```
Optionally surface a non-blocking "could not refresh" notice (the `refreshFailed` pattern) when a refetch fails but
data exists.

### WR-04: Job-outcome announcement is cleared by the next operations refetch

**File:** `sources/ReactApp/src/features/hosting/ServerWorkspacePage.tsx:130-131`;
`sources/ReactApp/src/features/hosting/useJobAnnouncement.ts:962-974`
**Issue:** `useJobAnnouncement(jobs.data, Math.max(jobs.dataUpdatedAt, operations.dataUpdatedAt))` treats every change
of the combined timestamp as a new snapshot and resets `message` to "" when no transition is found. A live signal
invalidates the jobs and operations queries together, and they resolve at different times. If the jobs response lands
first, the "finished" or "did not finish" message is set. When the operations response lands milliseconds later,
`dataUpdatedAt` changes again, `jobs` is unchanged, no transition is found, and the message is blanked. The polite
live region is therefore filled and emptied within one or two frames, and screen readers frequently announce nothing.
This undermines the accessibility feature the hook exists for.
**Fix:** Key the hook on the jobs query only: `useJobAnnouncement(jobs.data, jobs.dataUpdatedAt)`. If the hook should
keep the message for a short time, also stop clearing it when `jobs` is reference-equal to the stored snapshot.

### WR-05: Deep-linked activity entries are scrolled underneath the sticky workspace header

**File:** `sources/ReactApp/src/features/hosting/sections/ActivitySection.tsx:143-149`;
`sources/ReactApp/src/components/server/activity.css:8`; `src/components/server/workspace-shell.css:13-17`
**Issue:** The "Open activity details" links and the deep-link effect call `element.scrollIntoView(...)` and
`element.focus()` on `article#activity-...`. `.workspace-header` is `position: sticky; top: var(--command-header-height)`
and opaque, but no `scroll-margin-top` is set on `.activity-entry` (the only `scroll-margin-top` in the app is an
unrelated GDPR editor rule). The entry lands at the viewport top, hidden behind the command topbar plus the sticky
header, so the focused target is obscured (WCAG 2.4.11). The same happens for the Show-more focus move once the list
is long.
**Fix:** In `activity.css`, add `scroll-margin-top: calc(var(--command-header-height) + <header height token> + var(--utf-space-md))`
to `.activity-entry`. Optionally expose the header height as a token so the two stay in sync.

### WR-06: Workspace live updates miss changes when the page mounts during a disconnect or before the first open

**File:** `sources/ReactApp/src/features/hosting/useWorkspaceLiveUpdates.ts:53,64-67`
**Issue:** `handledEpoch` is initialised to the epoch at mount and recovery only runs when `disconnectEpoch` rises
above it. Two gaps follow:

- If the workspace mounts while the shared socket is already `reconnecting`, the epoch is already above zero. On the
  next `open` the epoch equals `handledEpoch`, so nothing is invalidated, although signals were lost after the mount
  fetch.
- A first open after mount never invalidates. The fleet provider's `onOpen` recovers only the fleet and
  notifications keys, so changes between the mount-time fetch and the socket's first open are not recovered by the
  workspace.

The job fallback poll only covers a server that already has an active job.
**Fix:** Track the last status in a ref and invalidate on every transition into `open` that was not the status at mount
(or, simpler, whenever the socket reaches `open` and the workspace queries' `dataUpdatedAt` predates that open). Add a
test that mounts during `reconnecting` and then opens.

## Info

### IN-01: Deleting a server does not invalidate the fleet or notifications cache

**File:** `sources/ReactApp/src/features/hosting/sections/SettingsSection.tsx:35`
**Issue:** `onSuccess` only navigates. The fleet query has `staleTime: 30_000` (`App.tsx`) and no polling, so within 30 s
of the last fetch the fleet page can still list the deleted server. Opening it ends in the not-found panel. This was
carried over from the previous page.
**Fix:** Call `queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet, exact: true })` (and notifications)
before navigating. Alternatively, rely on the server emitting a fleet-changed signal for delete, which is not shown.

### IN-02: Disk telemetry `<progress>` has no accessible name and can get `max=0`

**File:** `sources/ReactApp/src/features/hosting/sections/CloudMachinePanel.tsx:105`
**Issue:** `<progress max={diskTotalBytes} value={used} />` has no `aria-label` or `aria-labelledby` and sits inside a
section whose heading is not associated with it. If `diskTotalBytes` is 0, `max=0` is invalid (the element falls back
to a max of 1) and the bar renders misleadingly.
**Fix:** Add `aria-labelledby` pointing at the `h3`, and guard with `deployment.diskTotalBytes > 0`.

### IN-03: `aria-controls` points at an element that is not in the DOM while collapsed

**File:** `sources/ReactApp/src/components/server/ActivityEntry.tsx:372`
**Issue:** The disclosure button sets `aria-controls={detailsId}` always, but the details container renders only when
`expanded`. A dangling IDREF is ignored by most AT but is flagged by validators.
**Fix:** Set `aria-controls` only while `expanded`, or always render the container and toggle `hidden`.

### IN-04: "Download all logs" builds one unbounded `data:` URL on every render

**File:** `sources/ReactApp/src/features/hosting/sections/ActivitySection.tsx:327-331`;
`src/features/hosting/workspaceModel.ts:312-314`
**Issue:** The href concatenates and percent-encodes the diagnostic output of up to 100 jobs' commands into a single
`data:` URL. Browsers cap data-URL size (Chromium roughly 2 MB for navigation-class URLs), so a long-running server
can produce a link whose download fails silently. The string is also recomputed on each render.
**Fix:** Create a `Blob` and `URL.createObjectURL` inside the click handler (revoke afterwards), and `useMemo` or defer
the protocol build.

### IN-05: The actor placeholder is a localized English sentinel shared by literal on both sides

**File:** `sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs:39`;
`sources/ReactApp/src/features/hosting/activityLabels.ts:473`
**Issue:** API and SPA both hard-code "Hidden member" and compare strings. A user whose nickname is literally "Hidden
member" is shown as a hidden member. A rename on one side silently turns the placeholder into a displayed name. The
same pattern already exists for "Assigned owner", so this is consistent. It is still a stringly-typed contract that
the generated OpenAPI cannot check.
**Fix:** Prefer `null` name plus an explicit flag in the DTO, for example `ActorHidden` or `RequestedByHidden`, so the
client does not depend on a magic string. If the sentinel stays, add a contract test on each side that pins the literal.

### IN-06: Overlapping `CopyButton` copies leak the first revert timer

**File:** `sources/ReactApp/src/components/CopyButton.tsx:46-60`
**Issue:** `copy` clears the timer, then awaits `writeText`. Two quick clicks both pass `clearTimer()` before either
awaited write resolves. Each then sets `state` to `copied` and overwrites `timer.current`, so the first timer is never
cleared and flips the button back to idle early. It is benign but wrong.
**Fix:** Call `clearTimer()` immediately before `window.setTimeout`, after the awaited write.

---

_Reviewed: 2026-11-19_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
