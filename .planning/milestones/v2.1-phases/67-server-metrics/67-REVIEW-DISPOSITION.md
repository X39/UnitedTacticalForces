---
phase: 67
review: 67-REVIEW.md
titles: json
findings:
  - id: WR-01
    severity: warning
    disposition: open
    title: "A sample with an older as-of time replaces a cached reading that has no as-of time, and is then stamped as fresh"
  - id: IN-01
    severity: info
    disposition: open
    title: "`sameReadings` treats `undefined` and `null` as different, so a REST refetch can reset the receive stamp"
  - id: IN-02
    severity: info
    disposition: open
    title: "An as-of time that does not parse is treated as missing, so a malformed value can replace a newer cached sample"
  - id: IN-03
    severity: info
    disposition: open
    title: "The seed credit idempotency comment overstates race safety, and an existing UAT database gets an out-of-order balance chain"
  - id: IN-04
    severity: info
    disposition: open
    title: "The reconciler recurrence test hard-codes the safety range"
  - id: WR-02
    severity: warning
    disposition: open
    title: "One failing preparation aborts the whole emulation and strands the sessions that were already prepared"
  - id: WR-03
    severity: warning
    disposition: open
    title: "A failed or finished session is isolated from the others but is neither logged nor surfaced until every session has ended"
  - id: IN-05
    severity: info
    disposition: open
    title: "The seeded server is looked up by a non-unique title with an unordered `FirstOrDefault`"
  - id: IN-06
    severity: info
    disposition: open
    title: "Generality without instantiation, `NoPlayers` and `FewPlayers` profiles and the multi-title map"
  - id: IN-07
    severity: info
    disposition: open
    title: "Members of the seeder without XML documentation, against the AGENTS.md rule"
  - id: IN-08
    severity: info
    disposition: open
    title: "The primary service identifier is duplicated between the seeder and the emulator"
  - id: IN-09
    severity: info
    disposition: open
    title: "A stopped session is never re-established, and the note about this is missing"
  - id: CR-01
    severity: critical
    disposition: fixed
    title: "A reading without an as-of time never replaces a cached one, so \"server not running\" and \"measuring\" are never shown after a value was cached"
  - id: WR-04
    severity: warning
    disposition: open
    title: "New private members lack the XML documentation AGENTS.md requires for \"every type and member\""
  - id: WR-05
    severity: warning
    disposition: open
    title: "Protocol size constants are dead, duplicated, and the control-plane cap is never enforced"
  - id: WR-06
    severity: warning
    disposition: open
    title: "New options are not validated where they are used"
  - id: WR-07
    severity: warning
    disposition: open
    title: "Time-based synchronization in tests"
  - id: WR-08
    severity: warning
    disposition: open
    title: "A server PolicyViolation close of any kind is treated as a credential rejection and can end the worker"
  - id: WR-09
    severity: warning
    disposition: open
    title: "Best-effort metrics loop is not isolated, so a metrics fault terminates the whole worker"
  - id: WR-10
    severity: warning
    disposition: open
    title: "`Completion` is documented to carry loop faults, but unexpected loop exceptions are reported as a clean completion"
  - id: WR-11
    severity: warning
    disposition: open
    title: "Mandatory C# XML-documentation rule (AGENTS.md) is not met for many new private members"
  - id: WR-12
    severity: warning
    disposition: fixed
    title: "REST refetch refreshes the browser receive stamp of an unchanged reading, delaying the browser age check"
  - id: WR-13
    severity: warning
    disposition: open
    title: "`sameReadings` treats an absent REST field and a null frame field as different, so the first frame after a REST load always replaces the reading"
  - id: WR-14
    severity: warning
    disposition: open
    title: "Dependency test pins exact versions of every runtime dependency"
  - id: IN-10
    severity: info
    disposition: open
    title: "Clean close handshake is cut short on dispose"
  - id: IN-11
    severity: info
    disposition: open
    title: "`Server_Overview_MetricsUnavailable` is now an unused locale key"
  - id: IN-12
    severity: info
    disposition: open
    title: "Chart plot height is a hard-coded pixel constant that must stay equal to a rem token"
  - id: IN-13
    severity: info
    disposition: open
    title: "History rows and chart ticks rely on unique keys that the data does not guarantee"
open: 26
total: 28
recorded: 2026-11-24T19:09:01.740Z
---

# Phase 67: Code Review Disposition

| Finding | Severity | Disposition | Source |
|---------|----------|-------------|--------|
| WR-01 | warning | open | - |
| IN-01 | info | open | - |
| IN-02 | info | open | - |
| IN-03 | info | open | - |
| IN-04 | info | open | - |
| WR-02 | warning | open | - |
| WR-03 | warning | open | - |
| IN-05 | info | open | - |
| IN-06 | info | open | - |
| IN-07 | info | open | - |
| IN-08 | info | open | - |
| IN-09 | info | open | - |
| CR-01 | critical | fixed | 67-21; not in current review (prior review 9c06d7e2) (not in the current review) |
| WR-04 | warning | open | not in current review (prior review 9c06d7e2) (not in the current review) |
| WR-05 | warning | open | not in current review (prior review 9c06d7e2) (not in the current review) |
| WR-06 | warning | open | not in current review (prior review 9c06d7e2) (not in the current review) |
| WR-07 | warning | open | not in current review (prior review 9c06d7e2) (not in the current review) |
| WR-08 | warning | open | not in current review (prior review 9c06d7e2) (not in the current review) |
| WR-09 | warning | open | not in current review (prior review 9c06d7e2) (not in the current review) |
| WR-10 | warning | open | not in current review (prior review 9c06d7e2) (not in the current review) |
| WR-11 | warning | open | not in current review (prior review 9c06d7e2) (not in the current review) |
| WR-12 | warning | fixed | 67-21; not in current review (prior review 9c06d7e2) (not in the current review) |
| WR-13 | warning | open | not in current review (prior review 9c06d7e2) (not in the current review) |
| WR-14 | warning | open | not in current review (prior review 9c06d7e2) (not in the current review) |
| IN-10 | info | open | not in current review (prior review 9c06d7e2) (not in the current review) |
| IN-11 | info | open | not in current review (prior review 9c06d7e2) (not in the current review) |
| IN-12 | info | open | not in current review (prior review 9c06d7e2) (not in the current review) |
| IN-13 | info | open | not in current review (prior review 9c06d7e2) (not in the current review) |

Dispositions: `open` (recorded, not yet triaged), `fixed`, `skipped`, `deferred`.
Set `deferred` by hand and put the reason in the Source cell; both are preserved. A `|` in the reason is kept as prose and escaped on the next run.
Re-running the gate keeps every row it can. A row the current review no longer reports is kept and its Source cell flagged, so a finding does not leave this record silently. ONE exception: when a finding id is REUSED by a different finding, the earlier decision cannot keep a row — the id is taken — and it is dropped. A RECORDED decision (anything but `open`) is named on the console when that happens; a row still at `open` is replaced silently, because `open` records no decision to lose.
