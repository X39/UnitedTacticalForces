---
phase: 67-server-metrics
reviewed: 2026-11-24T12:00:00Z
depth: standard
files_reviewed: 13
files_reviewed_list:
  - scripts/Start-Uat.ps1
  - sources/Api/Data/EUatPlayerProfile.cs
  - sources/Api/Data/UatFleetScenarioSeeder.cs
  - sources/Api/HostedServices/UatWorkerChannelEmulator.cs
  - sources/ReactApp/src/features/hosting/FleetLiveUpdatesProvider.tsx
  - sources/ReactApp/src/features/hosting/metricsFrames.ts
  - sources/ReactApp/src/features/hosting/metricsModel.ts
  - sources/ReactApp/src/features/hosting/useServerMetrics.ts
  - sources/ReactApp/tests/hosting/fleet-live-updates.test.tsx
  - sources/ReactApp/tests/hosting/metrics-frames.test.ts
  - sources/ReactApp/tests/hosting/metrics-panel.test.tsx
  - tests/Api.Tests/GameServerHosting/UatFleetScenarioCostReconciliationTests.cs
  - tests/Api.Tests/GameServerHosting/UatFleetScenarioSeederTests.cs
findings:
  critical: 0
  warning: 3
  info: 9
  total: 12
  necessity: 1
status: issues_found
---

# Phase 67: Code Review Report

**Reviewed:** 2026-11-24T12:00:00Z
**Depth:** standard
**Files Reviewed:** 13
**Status:** issues_found

## Summary

This review is incremental relative to 9c06d7e2. It covers only the changes since that commit, with the full files read for context. The changes are the funded UAT cost owner (seed credit, audit record, reconciler recurrence test) and the replacement of `isNewerMetrics` and `isOlderReading` by the single `replacesCachedReading` rule shared by the live frame path and the REST path.

I ran `Api.Tests --filter UatFleetScenario` and all 16 tests pass. I also checked the following:

- The seed credit satisfies `CK_HostingCostLedgerEntries_Shape` for kind 1 (`BankTransferPayment`): `RecordedByFk`, `RecordIdentifier` and `ExternalReference` are set and `DeploymentFk` is null.
- The unique filtered index on `RecordIdentifier` exists.
- The seeder is reachable only when `IsDevelopment()` and the UAT configuration key is set (`Program.cs:172`), so the 1000 EUR credit cannot reach production.
- `isNewerMetrics` and `isOlderReading` have no remaining references.

I found no blockers. One robustness gap in the new replacement rule contradicts its own documented guarantee, and four minor items remain.

The incremental review of commit 36da6568 (multi-session UAT worker emulator) follows the original findings. It adds two warnings and five info items and no blockers.

## Warnings

### WR-01: A sample with an older as-of time replaces a cached reading that has no as-of time, and is then stamped as fresh

**File:** `sources/ReactApp/src/features/hosting/metricsFrames.ts:193-199`
**Issue:** The refusal rule only applies when both `nextAt` and `currentAt` are non-null. The docs promise that a reordered sample "must never show an old number as live". That promise is not kept in this sequence:

1. The cache holds a "stopped" or "new run, no sample yet" reading with `asOf === null`.
2. An older sample arrives that carries values. The common source is a REST refetch that was in flight across the stop or restart, for example one started by an invalidation after a socket reconnect. A stale live frame is the other source.
3. `currentAt` is null, so the guard is skipped and `sameReadings` is false. `replacesCachedReading` returns true.
4. The caller writes the sample with `receivedAtMs: performance.now()` (`useServerMetrics.ts:33`, `FleetLiveUpdatesProvider.tsx:220`).
5. The browser age check (`metricsModel.ts:134`) is based only on `receivedAtMs`. The old CPU, memory and players therefore show as live for a full `staleAfterSeconds` window on a server that has stopped.

The old code had the same hole, because a non-null as-of time beat a null one. The new doc comment and the new test "takes a reading with an as-of time over one without" make the behaviour deliberate, but the stated invariant still does not hold.

**Fix:** The control plane's null as-of reading carries no ordering information, so the cache cannot tell a legitimately new run from a late sample by as-of time alone. Cheapest robust options:

- Let a reading with an as-of time replace a null-as-of cached reading only when its `runningSince` is not earlier than the cached `runningSince`. A stopped reading has no `runningSince`, so this alone does not help.
- Remember the as-of time of the last value-carrying sample in the cache entry, for example `lastAsOfMs` next to `receivedAtMs`. Keep it when a null-as-of reading replaces the entry, and compare against it in the refusal rule:
```ts
const currentAt = asOfMs(current) ?? current.lastSampleAtMs ?? null;
```
- At minimum, correct the doc comment so it states that a null-as-of cached reading accepts any sample.

## Info

### IN-01: `sameReadings` treats `undefined` and `null` as different, so a REST refetch can reset the receive stamp

**File:** `sources/ReactApp/src/features/hosting/metricsFrames.ts:140-143`
**Issue:** `holdsAnyValue` normalises `undefined` to `null` with `?? null`, which shows the type allows optional fields. `sameReadings` compares with `===` over the keys of `left` only. A REST payload that omits a nullable field (`undefined`) is unequal to the cached `null`. The result is a spurious replace and a refreshed `receivedAtMs`, which delays the browser age check. That is the exact thing the "equal facts keep the stamp" rule is meant to prevent. A key present only on `right` is also ignored.
**Fix:** Compare the union of keys and normalise: `(left[key] ?? null) === (right[key] ?? null)`, ignoring `receivedAtMs`.

### IN-02: An as-of time that does not parse is treated as missing, so a malformed value can replace a newer cached sample

**File:** `sources/ReactApp/src/features/hosting/metricsFrames.ts:151-155`
**Issue:** `asOfMs` returns null for `Date.parse` failures. A frame or REST reading with a garbage `asOf` and values then bypasses the refusal rule. `parseMetricsFrame` accepts any string for `asOf` through `optionalText`, so this input passes parsing. The test at `metrics-frames.test.ts` ("counts an as-of value that does not parse as missing") locks the behaviour in.
**Fix:** Reject frames whose non-null `asOf` does not parse in `parseMetricsFrame`, as already done for other invalid shapes. Keep the REST path trusted.

### IN-03: The seed credit idempotency comment overstates race safety, and an existing UAT database gets an out-of-order balance chain

**File:** `sources/Api/Data/UatFleetScenarioSeeder.cs:77-81, 200-223`
**Issue:**
- The XML doc says the credit "cannot be booked twice, not even by a race". The `AnyAsync` check followed by `Add` does not make a race harmless. The second writer would fail `SaveChangesAsync` with a unique-index violation and abort startup seeding rather than skip silently. The index protects integrity, not startup. The seeder runs once in `MigrateAndSeedDataAsync`, so this is only a wording problem.
- On an existing UAT database that was seeded before this change, the credit is added with `BalanceAfter` equal to the current balance (for example 998.7 after the seeded slice) and an effective time one day before the pre-existing slice, whose `BalanceAfter` is -1.3. A ledger ordered by effective time then shows a balance chain that does not add up.
- The audit record is stamped by `HostingAuditWriter` with the wall clock, while the ledger entry is backdated from the injected `now`. The two disagree whenever `now` is injected.

**Fix:** Reword the doc ("a second booking would fail on the unique index"). Optionally anchor the credit's effective time to the earliest ledger entry of the owner when one exists, or accept the inconsistency as a dev-only artefact and document it.

### IN-04: The reconciler recurrence test hard-codes the safety range

**File:** `tests/Api.Tests/GameServerHosting/UatFleetScenarioCostReconciliationTests.cs:112`
**Issue:** `Assert.True(owner.Balance >= 5m, ...)` hard-codes 5 EUR as the safety range. The production value lives in the reconciler or its options. If that range changes, the test either asserts a stale number or gives a false pass. The test also depends on the real clock (`NodaSystemClock`) and the 30 s default `ReconciliationInterval`. It passed here, but the 15-minute case has an expiry boundary that is only slightly inside the renewal threshold.
**Fix:** Read the safety range from the options or the reconciler constant instead of the literal. Keep the clock dependency documented, as it is.

---

## Incremental review (commit 36da6568)

**Scope:** `feat(api): emulate several UAT workers with player profiles`. It touches `sources/Api/Data/EUatPlayerProfile.cs` (new), `sources/Api/Data/UatFleetScenarioSeeder.cs` and `sources/Api/HostedServices/UatWorkerChannelEmulator.cs`. The whole files were read, together with `UatLoopbackWebSocketPair`, `WorkerChannelRegistry`, `WorkerProtocolService.AuthenticateAsync` and the existing `UatWorkerChannelEmulatorTests` (which already has a `SeveralServers_...` case, commit 97139ade).

**Checked and sound:**

- **Registration gate.** The emulator is registered only through `UatWorkerChannelEmulator.AddTo`, which requires `IsDevelopment()` and `Testing:UatScenarios:Enabled` (`Program.cs:227`). The new `EmulatedWorkers` property and the seeder map add no registration path. The seam is an `init` property on an `internal sealed` class, so it cannot be set from configuration.
- **Per-session lifecycle.** Each `DriveChannelAsync` call owns its own `UatLoopbackWebSocketPair`, linked `CancellationTokenSource`, handler task and receive task. Nothing is shared between sessions except the injected singleton handler and registry. The registry is keyed by worker id, and every session authenticates a different worker row. The stop of one session cancels only its own linked token and disposes only its own pair. Host shutdown cancels all of them through `stoppingToken`.
- **No race on a session map.** There is none. The sessions list is built sequentially before `Task.WhenAll` and is read-only afterwards.
- **Player profile.** The `switch` is exhaustive for the defined enum values. `EUatPlayerProfile` follows the AGENTS.md rules (E prefix, documented members, own file).

### WR-02: One failing preparation aborts the whole emulation and strands the sessions that were already prepared

**File:** `sources/Api/HostedServices/UatWorkerChannelEmulator.cs:145-154`
**Issue:** The sessions are prepared in a plain sequential loop with no per-server error handling. `PrepareAsync` can throw. `AuthenticateAsync` can raise `WorkerProtocolAuthenticationException`, and the credential issue and the `SaveChangesAsync` call can fail too. The exception leaves `RunAsync`, and `ExecuteAsync` logs one warning and ends the service. Every session prepared earlier in the loop was already issued a fresh credential and authenticated, but it is never driven. Its seeded server therefore silently gets no live feed, although the requirement is that one server's trouble must not affect the others. With the old single-server code this was acceptable. With a map of several servers it is not. The failure also hides which title failed, because `PrepareAsync` does not add the title to the log.
**Fix:** Isolate each preparation and log the title:
```csharp
foreach (var (title, profile) in EmulatedWorkers)
{
    EmulatedSession? session = null;
    try
    {
        session = await PrepareAsync(title, profile, stoppingToken).ConfigureAwait(false);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        logger.LogWarning(
            "UAT worker emulation could not prepare '{Title}': {Failure}",
            title,
            HostingFailureSanitizer.Sanitize(ex.Message));
    }

    if (session is null) skippedTitles.Add(title); else sessions.Add(session);
}
```

### WR-03: A failed or finished session is isolated from the others but is neither logged nor surfaced until every session has ended

**File:** `sources/Api/HostedServices/UatWorkerChannelEmulator.cs:171-173, 253, 310-322`
**Issue:** `Task.WhenAll` keeps the other sessions running when one throws, which is the wanted isolation. The failure itself is only observed when every session has ended. On a UAT stack that runs for hours, the other sessions run until host shutdown, so the failure is reported late or never. Three concrete paths:

- `UatLoopbackWebSocketPair.CreateAsync` is outside the `try` block, so a socket error there escapes as a session fault.
- Exceptions outside the filter at line 310 are not handled in the loop. An example is the `ArgumentOutOfRangeException` from `MetricsFrame` for an unknown profile. Another is a non-timeout failure from the handler task inside `WaitQuietlyAsync`, which rethrows from the `finally` block and also skips the `receiveTask` wait.
- When several sessions fail, `await Task.WhenAll` rethrows only the first exception. `ExecuteAsync` logs that one and the rest are lost.

A normal end is also invisible. When a Stop succeeds, no line names the server whose session ended. The single "UAT worker emulation ended" line only appears after the last session ended.
**Fix:** Wrap each session in its own method that carries the title, catches and logs its failure, and logs its end:
```csharp
private async Task RunSessionAsync(string title, EmulatedSession session, ...)
{
    try { await DriveChannelAsync(session, settings, stoppingToken).ConfigureAwait(false); }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    catch (Exception ex)
    {
        logger.LogWarning("UAT worker emulation of '{Title}' ended after a failure: {Failure}",
            title, HostingFailureSanitizer.Sanitize(ex.Message));
        return;
    }
    logger.LogInformation("UAT worker emulation of '{Title}' ended", title);
}
```
Keep the `EmulatedSession` title (or add a `Title` member to the record) so the log can name it. Also validate the profile when the session is prepared, so an unknown value is rejected up front instead of after the first heartbeat.

### IN-05: The seeded server is looked up by a non-unique title with an unordered `FirstOrDefault`

**File:** `sources/Api/HostedServices/UatWorkerChannelEmulator.cs:194-199`
**Issue:** `GameServers.Title` has only a plain index (`DatabaseContextModelSnapshot.cs:4362`), not a unique one. The seeder already matches by title. The emulator's query has no `OrderBy`. If a second server with a seeded title exists (for example a user-created "UAT 10 Metrics"), the emulator picks an arbitrary row. It then drives the real worker of whichever server it picked, which may not be the seeded one. This is a development-only concern, but it is easy to avoid.
**Fix:** Order the lookup by `TimeStampCreated` ascending so the seeded server wins deterministically, or filter by the seeded `CreatedByFk` owner.

### IN-06: Generality without instantiation, `NoPlayers` and `FewPlayers` profiles and the multi-title map

**File:** `sources/Api/Data/UatFleetScenarioSeeder.cs:45-49`, `sources/Api/Data/EUatPlayerProfile.cs:14-18`, `sources/Api/HostedServices/UatWorkerChannelEmulator.cs:531-537`
**Category:** necessity
**Issue:** The production map holds exactly one entry, `[MetricsScenarioTitle] = EUatPlayerProfile.Synthetic`. `NoPlayers`, `FewPlayers` and `FewPlayersCount` are referenced by no production caller (`grep` over `sources/` finds only the emulator's own `switch`). Only tests instantiate them through the `EmulatedWorkers` seam. The doc comment says "later UAT scenarios add their titles here", so the generality is planned but not yet used. This rests on judgment, because a follow-up scenario is plausible. There is also an undocumented coupling: a title added to the map is only driven when the seeder also gives that scenario `Metrics: true`, since only that flag seeds the primary worker. Without it the title falls into `skippedTitles` with an info log, which is easy to miss.
**Fix:** Keep the profiles only if a scenario adds them in the next phase. Otherwise remove them. Either way, state the `Scenario.Metrics` coupling in the XML doc of `EmulatedWorkers`, or derive the map from the scenario list so the two cannot drift.

### IN-07: Members of the seeder without XML documentation, against the AGENTS.md rule

**File:** `sources/Api/Data/UatFleetScenarioSeeder.cs:126, 247`
**Issue:** AGENTS.md requires meaningful XML documentation on every C# member, and the file is in this review's scope. The `Scenarios` field (line 126) and the `AddServer` method (line 247, with five parameters) have none. Both predate commit 36da6568, but the commit touched the file and left them.
**Fix:** Add `<summary>` to `Scenarios` and `<summary>` plus `<param>` elements to `AddServer`, matching the style of `AddPrimaryWorker`.

### IN-08: The primary service identifier is duplicated between the seeder and the emulator

**File:** `sources/Api/HostedServices/UatWorkerChannelEmulator.cs:56`, `sources/Api/Data/UatFleetScenarioSeeder.cs:52`
**Issue:** Both files define their own `"game-server"` constant. The emulator finds the worker the seeder created by that literal. If the seeder constant changes, the emulator silently finds no worker and skips every server with only an info log.
**Fix:** Make the seeder constant `internal` and reference it from the emulator, or reference the production constant that names the primary service.

### IN-09: A stopped session is never re-established, and the note about this is missing

**File:** `sources/Api/HostedServices/UatWorkerChannelEmulator.cs:145-173`
**Issue:** Sessions are prepared once, at start. After a Stop succeeds, the session ends for good. If the user starts the same UAT server again in the same API run, the new deployment gets no emulated worker until the API restarts. The other sessions keep running, as required, but a restarted server then shows no live readings. The class doc says only that the session "ends, so the deployment can resolve". It does not say that a restart needs an API restart.
**Fix:** Document the limitation in the class remarks. Or, if UAT needs restarts, loop per title: wait, find the next unresolved deployment and prepare again.

---

_Reviewed: 2026-11-24T12:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
