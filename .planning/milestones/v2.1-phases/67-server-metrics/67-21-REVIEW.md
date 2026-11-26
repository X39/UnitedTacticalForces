---
phase: 67-server-metrics
plan: 21
reviewed: 2026-11-22T15:55:28Z
depth: standard
diff: 6603d838..362acad9 -- sources/
files_reviewed: 7
files_reviewed_list:
  - sources/ReactApp/src/features/hosting/metricsFrames.ts
  - sources/ReactApp/src/features/hosting/useServerMetrics.ts
  - sources/ReactApp/src/features/hosting/metricsModel.ts
  - sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx
  - sources/ReactApp/tests/hosting/metrics-frames.test.ts
  - sources/ReactApp/tests/hosting/fleet-live-updates.test.tsx
  - sources/ReactApp/tests/hosting/metrics-panel.test.tsx
findings:
  critical: 1
  warning: 5
  info: 2
  total: 8
  necessity: 0
status: issues_found
---

# Phase 67 (plan 67-21): Code Review Report

**Reviewed:** 2026-11-22T15:55:28Z
**Depth:** standard (with targeted cross-file tracing into the API producers of the readings)
**Files Reviewed:** 7
**Status:** issues_found

## Summary

Plan 67-21 replaces the two old merge helpers with one rule, `replacesCachedReading(next, current)`, used by the
REST query (`useServerMetrics`) and the live frame write (`FleetLiveUpdatesProvider`). The rule fixes the CR-01
scenario as tested: a REST refetch with no as-of time after a stop or restart now replaces the cached reading.
The four plan test files pass (107 tests).

The rule still has gaps against its own stated intent. These were checked against the API producers
(`ServerMetricsService.EvaluateCurrentAsync`, `ServerMetricsPublisher`, `UpdateStreamService` serializer options)
and confirmed by running the bundled rule in Node and serializing both wire formats with the repository's NodaTime
package:

- The ordering guard only works when both readings carry an as-of time. Once the stop reading (as-of null) is
  cached, a late pre-stop frame with values replaces it and gets a fresh receive stamp. The workspace then shows
  the CR-01 symptom again: held values, then "too old", never "not running" (CR-01 below).
- REST serializes instants as `...Z` (NodaTime `Instant`) and frames as `...+00:00` (STJ `DateTimeOffset`).
  `sameReadings` compares strings with `===`, so the same snapshot never compares equal across the two paths.
  WR-12 is therefore fixed only for REST after REST, not for the common case of a refetch after a live frame
  (WR-01).
- Ties and the reason-only exemption lose the high-water mark, so an older value can still be shown as live
  (WR-02). The no-as-of readings that carry a value are unordered with respect to frames (WR-03).
- Opening a workspace after a stop can skip REST entirely and show frame-cached values (WR-04, existed before
  this plan, but it leaves the CR-01 goal open on that path).
- None of these cases has a test, and the fixtures use one string format per path, which hides WR-01 (WR-05).

Necessity: no findings. The guards in `asOfMs` and `holdsAnyValue` act on REST data that crosses a trust
boundary, so they are not impossible-state guards.

## Narrative Findings (AI reviewer)

## Critical Issues

### CR-01: A late pre-stop reading with values replaces the cached stop reading, so the not-running reason is masked again

**File:** `sources/ReactApp/src/features/hosting/metricsFrames.ts:189-200` (rule 2 at line 196); writers at
`sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx:216-222` and
`sources/ReactApp/src/features/hosting/useServerMetrics.ts:28-33`; codified by
`sources/ReactApp/tests/hosting/metrics-frames.test.ts:227-229`

**Issue:** Rule 2 refuses a reordered reading only when `nextAt !== null && currentAt !== null`. After a stop, the
cached reading is the stop reading, which has no as-of time (`ServerMetricsService.cs`: `AsOf = snapshot?.ReceivedAt`
with no active deployment). From then on, every reading that carries an as-of time and differs is accepted,
including an older sample from the run that just stopped. The plan's must-have says: "A reordered older reading that
still carries a value never replaces a newer cached reading". The unit test "takes a reading with an as-of time over
one without" encodes the opposite.

The race can happen in production:

1. The worker sends one last sample while the game is shutting down. `MetricsFrameRoute` calls
   `ServerMetricsPublisher.PublishAsync`, which evaluates the reading while the deployment is still unresolved (CPU
   and memory values, as-of T).
2. The deployment resolves and a fleet signal reaches the browser.
3. The workspace refetches, and the REST stop reading lands in the cache.
4. The publisher's frame is delivered afterwards. Sends are sequential per viewer and wait on a per-socket
   semaphore (`UpdateStreamService`), so one slow viewer delays the frame for everyone after it.

Probe result: `replacesCachedReading(valuedAsOfT, stoppedCached) === true`. The frame is written with
`receivedAtMs: performance.now()`. `isReadingStale` (`metricsModel.ts:134`) looks only at `receivedAtMs`, so the
tiles show the previous run's CPU and memory as live for `staleAfterSeconds`. After that they show "The last reading
is too old to show." and never "The game is not running". No further frame arrives for a stopped server and the
query never polls, so the reason stays hidden until the next fleet signal. This is the CR-01 outcome, reached
through a race instead of on every stop.

**Fix:** Keep the ordering information when a reading without an as-of time replaces one that has it. The smallest
browser-only fix is a high-water mark on the cache entry. Because as-of is the API receive time, a new run's first
sample is always later than the last sample of the previous run:

```ts
// types.ts
export type ServerMetricsReading = GameServerMetrics & {
  receivedAtMs: number;
  /** Latest as-of time ever cached for this server, kept across readings without an as-of time. */
  highWaterAsOfMs: number | null;
};

// metricsFrames.ts
export function replacesCachedReading(
  next: GameServerMetrics,
  current: (GameServerMetrics & { highWaterAsOfMs?: number | null }) | undefined,
): boolean {
  if (current === undefined) return true;
  const nextAt = asOfMs(next);
  const currentAt = asOfMs(current);
  const highWater = current.highWaterAsOfMs ?? currentAt;
  if (nextAt !== null && highWater !== null && holdsAnyValue(next)) {
    if (nextAt < highWater) return false;
    // A cached reading without an as-of time is newer than the sample it replaced; that sample may not return.
    if (currentAt === null && nextAt <= highWater) return false;
  }
  return !sameReadings(next, current);
}

export function highWaterAfter(next: GameServerMetrics, current: ServerMetricsReading | undefined): number | null {
  const values = [asOfMs(next), current?.highWaterAsOfMs ?? null].filter((v): v is number => v !== null);
  return values.length === 0 ? null : Math.max(...values);
}
```

Both writers must set `highWaterAsOfMs: highWaterAfter(next, current)`. The `sameReadings(next, current)` call
already ignores browser-only keys because it iterates the keys of `next`.

The more robust fix is an API-side ordering key that covers CR-01, WR-02 and WR-03 together. Add a monotonic
`evaluatedAt` (API clock) or the deployment id to `GameServerMetricsDto` and `GameServerMetricsMessage`, and order
strictly by it. API changes are in scope under AGENTS.md even though plan 67-21 prohibited them. Add a regression
test: a cached running reading, then the REST stop reading, then a frame with values and the old as-of time. The
tiles must still show "The game is not running".

## Warnings

### WR-01: REST and frames format the same instant differently, so WR-12 is not fixed after a live frame

**File:** `sources/ReactApp/src/features/hosting/metricsFrames.ts:140-143` (`sameReadings`), used at line 199;
`sources/ReactApp/src/features/hosting/useServerMetrics.ts:32`

**Issue:** REST serializes `GameServerMetricsDto.AsOf` and `RunningSince` (NodaTime `Instant`, `ConfigureForNodaTime`
in `Program.cs:289`) as `2026-11-22T10:00:00.123456Z`. Frames serialize `GameServerMetricsMessage.AsOf` and
`RunningSince` (`DateTimeOffset`, web defaults in `UpdateStreamService.cs:42`) as
`2026-11-22T10:00:00.123456+00:00`. This was verified by serializing both with NodaTime.Serialization.SystemTextJson
1.4.0. `sameReadings` compares with `===`, so whenever as-of or running since is set, a REST reading and a frame for
the same snapshot are always "different". Rule 2 does not catch it either, because the parsed times are equal. In a
live workspace the cache is normally written by frames. Every fleet signal, reconnect invalidation and manual
refresh then returns the same snapshot over REST, and `useServerMetrics` re-stamps it with `performance.now()`. That
is exactly the WR-12 scenario: a 25-second-old sample stays on screen for up to about 2x `staleAfterSeconds`. The
first redelivered frame after a REST load is re-stamped the same way. `67-REVIEW-DISPOSITION.md` marks WR-12 as
`fixed`, but that holds only for REST after REST.

Probe result: `replacesCachedReading({...frame, asOf: "…Z", runningSince: "…Z"}, frame) === true`.

**Fix:** Compare instant fields by value, and normalize absent fields to null so WR-13 is closed in the same place:

```ts
const instantKeys = new Set<keyof GameServerMetrics>(["asOf", "runningSince"]);
function sameReadings(left: GameServerMetrics, right: GameServerMetrics): boolean {
  const keys = Object.keys(left) as (keyof GameServerMetrics)[];
  return keys.every((key) => {
    const l = left[key] ?? null;
    const r = right[key] ?? null;
    if (instantKeys.has(key) && typeof l === "string" && typeof r === "string") {
      return Date.parse(l) === Date.parse(r);
    }
    return l === r;
  });
}
```

Alternatively, normalize instants to one canonical string when a reading is parsed or loaded. Then reopen WR-12 in
the disposition until a cross-path test (WR-05) passes.

### WR-02: Ties and the reason-only exemption lose the ordering, so an older value can be shown as live

**File:** `sources/ReactApp/src/features/hosting/metricsFrames.ts:196`

**Issue:** There are two variants. Both were confirmed by probe.

- **Equal as-of times.** When the worker goes offline, the API sends the *same* as-of time as the last sample, with
  `worker-offline` reasons (`ServerMetricsService.cs`: the `Gate` returns `WorkerOffline` while the snapshot stays
  in place). A reordered reading with values for that snapshot then arrives: `nextAt === currentAt`, so rule 2 does
  not apply, the facts differ, and the values are written with a fresh stamp while the worker is offline. No frame
  follows, so the tile goes from values to "too old" and never shows "worker offline".
- **High-water loss.** An older reading with reasons only (for example worker offline or stale, with an as-of time)
  is accepted over a newer reading with values. That is intended. But a reading with values that is newer than the
  reasons-only reading and *older than the reading it replaced* is then accepted too: valued T, then reasons T-30,
  then valued T-15 is accepted. The browser shows a value older than one it has already shown as live, which breaks
  the plan's "never replaces a newer cached reading" truth.

**Fix:** The high-water mark from CR-01 closes the second variant. The tie cannot be resolved from as-of alone,
because a worker that reconnects legitimately turns worker-offline at T back into values at T. It needs the
API-side `evaluatedAt` ordering key proposed in CR-01.

### WR-03: Readings without an as-of time that land after a newer frame move the tiles backwards

**File:** `sources/ReactApp/src/features/hosting/metricsFrames.ts:194-199`;
`sources/ReactApp/src/features/hosting/useServerMetrics.ts:28-33`

**Issue:** A reading without an as-of time is treated as the current truth regardless of when it was produced. On a
start, the fleet signal triggers a REST read evaluated before the first sample: `measuring`, or the new running
since with `measuring`. If the first sample's frame (as-of T, values) is delivered before that REST response
returns, `queryFn` reads the frame from the cache, `replacesCachedReading(noAsOf, frame)` is true, and the tiles go
back from values to "Measuring". The same happens with a stop reading evaluated just before a fast start: the tiles
read "The game is not running" while the first values have already arrived. The next frame heals both cases (about
one sample interval). With two restarts inside one REST round trip, the earlier run's running since can replace the
newer run's, which is an old value shown as live. TanStack's `cancelRefetch` only protects REST against REST, not
REST against frames.

**Fix:** Covered by the API-side ordering key from CR-01. A browser-only partial fix: in `useServerMetrics`, record
`performance.now()` before `await hostingService.serverMetrics(...)` and refuse a loaded reading without an as-of
time when `cached.receivedAtMs` is later than the request start and the cached reading has an as-of time. A frame
that arrived during the request is newer than the response.

### WR-04: Opening a workspace after a stop can skip REST and show values cached from frames (existed before this plan)

**File:** `sources/ReactApp/src/features/hosting/useServerMetrics.ts:36` (`staleTime: Infinity`);
`sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx:216-222`;
`sources/ReactApp/src/features/hosting/useWorkspaceLiveUpdates.ts:59-72`

**Issue:** The provider is mounted in `CommandCenterLayout`/`CommunityLayout` and writes frames into
`hostingQueryKeys.metrics(id)` for every server the viewer may see, including servers whose workspace is not open.
The only metrics invalidation runs in `useWorkspaceLiveUpdates`, and only while that workspace is open (the first
open does not invalidate). Example: a viewer on the fleet page sees server X running, X stops (no stop frame is ever
published), and the viewer opens X's workspace within the default 5-minute gcTime. `useQuery` finds data with
`staleTime: Infinity` and does not fetch. The tiles show the held values as live, then "too old", and never "The
game is not running". The 67-21 must-have is scoped to "an open workspace", so this path is out of its tests, but
roadmap criterion 5 is not scoped that way.

**Fix:** Refetch the readings when the panel mounts, for example `refetchOnMount: "always"` on the readings query
only. The queryFn merge rule already keeps the cached entry and stamp when nothing changed, so this costs one
request and cannot regress WR-12 once WR-01 is fixed. Alternatively, have the provider invalidate (without
refetching) `metrics(serverId)` exactly on every fleet-changed frame, so an inactive entry becomes stale.

### WR-05: Tests miss the cross-path, late-frame and tie cases, and the fixtures hide WR-01

**File:** `sources/ReactApp/tests/hosting/metrics-frames.test.ts:145-247`;
`sources/ReactApp/tests/hosting/fleet-live-updates.test.tsx:541-574`;
`sources/ReactApp/tests/hosting/metrics-panel.test.tsx:314-447`

**Issue:** The unit matrix builds both sides from one helper with `+00:00` instants. The panel tests use `Z`
instants for REST. The fleet tests use `+00:00` for frames. No test mixes the two paths, so WR-01 cannot fail any
test. These cases are also missing:

- (a) A frame for snapshot S, then a REST refetch for S keeps `receivedAtMs` (the real WR-12 path).
- (b) A cached stop reading, then a frame with values and an older as-of time (CR-01).
- (c) A worker-offline reading with as-of T, then a reordered reading with values at T (WR-02).
- (d) A REST response without an as-of time that resolves after a newer frame was written during the request
  (WR-03).
- (e) Opening the workspace with a frame-created entry for a server that has stopped (WR-04).

The test at `metrics-frames.test.ts:227-229` asserts the CR-01 behavior as intended.

**Fix:** Add tests (a) to (e). Use the real wire formats in the fixtures: REST `…Z` from NodaTime, frames `…+00:00`
from `DateTimeOffset`. Change the 227-229 expectation once the high-water rule lands.

## Info

### IN-01: A reading with values and an unparseable as-of time bypasses the ordering guard

**File:** `sources/ReactApp/src/features/hosting/metricsFrames.ts:151-155`;
`sources/ReactApp/tests/hosting/metrics-frames.test.ts:242-246`

**Issue:** `asOfMs` maps an unparseable as-of time to null, the same as the authoritative "no snapshot" shape. A
reading that carries values and a garbage as-of time therefore replaces a newer valid reading, and the test
enshrines this. Frames are validated by `parseMetricsFrame`, but REST readings are stored without validation
(`services.ts:266-272`). The API never sends such a value today, so this is about failing closed against the
"never show a reading it cannot trust" intent, not a reachable defect.

**Fix:** Treat an as-of time that is present but unparseable as untrusted: refuse it when `holdsAnyValue(next)`, or
validate REST readings with the same field checks `parseMetricsFrame` applies.

### IN-02: Restart ordering depends on a monotonic API clock across deployments

**File:** `sources/ReactApp/src/features/hosting/metricsFrames.ts:196`

**Issue:** Under the current API, a new run's as-of time cannot be older than a cached reading from the previous run,
because both are `ServerMetricSnapshot.ReceivedAt` on the API clock. Rule 2 relies on this. If the API clock steps
backwards (NTP correction) or as-of ever moves to the worker clock (prior review WR-01), the new run's frames with
values are refused while a valued reading from the previous run is still cached. That happens when the stop
reading was missed, for example through WR-04 or a disconnect with no workspace open. The tiles would hold the
previous run until browser staleness. The plan's assumption-delta section records this as a promote trigger.

**Fix:** None needed now. If the deployment id or `evaluatedAt` is added for CR-01, order by it, and the assumption
goes away.

---

_Reviewed: 2026-11-22T15:55:28Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
