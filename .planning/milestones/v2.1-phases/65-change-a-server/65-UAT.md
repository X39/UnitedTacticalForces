---
status: complete
phase: 65-change-a-server
source: 65-01-SUMMARY.md through 65-19-SUMMARY.md
started: 2026-11-21T08:04:52Z
updated: 2026-11-21T11:14:40.495Z
---

## Current Test

[testing complete]

## Tests

### 1. Cold Start Smoke Test
expected: Stop everything, start the app from scratch. API boots without errors, the migration applies, and the server list loads with live data.
result: pass
note: Fresh-DB migration-history query error and emulated unconfirmed-release error are expected from UAT seed.

### 2. Stage a mod pack change
expected: Open an existing server's Settings, Mods. Pick a different mod pack. It is staged (not saved yet), a pending-changes bar appears, and nothing changes on the server until you apply.
result: pass
note: UI feedback captured as deferred follow-ups (details element, mod pack dropdown, review dialog layout)

### 3. Edit settings (Common / Advanced)
expected: Settings show a Common group with Advanced collapsed behind a disclosure. Change a value in each; both get staged and counted in the pending bar.
result: pass
note: Functional pass; heavy UI/UX complaints captured as deferred follow-ups

### 4. Review and apply with take-effect notice
expected: Click review. The dialog lists before/after for each staged change (mod pack contents diff included) and states when they take effect (next start). Apply sends them; afterwards the server page shows a "changes pending start" notice.
result: pass
note: Functional pass; review dialog needs redesign for many changes (deferred)

### 5. Leave with unsaved changes
expected: Stage a change, then switch to another section or press Back. A dialog offers Review, Discard, Stay. Stay keeps the draft; Discard drops it.
result: pass
note: Functional pass; leave dialog button design deferred

### 6. Credentials hidden until revealed
expected: In the credentials panel, values show masked. Reveal (if permitted) shows the value briefly and hides it again automatically after about 30 seconds. Staging a new credential shows only "changed" in the review, never the value.
result: issue
reported: "Changing a value to some deliberate one is not possible, the regenerated value is nowhere visible for copying and we have similar layout issues as mentioned previously with button placements. Follow-up: We cannot choose any value ourselves and cannot see the regenerated value ever (unless we save, go there and have the reveal claim, which most users who manage servers will not have)."
severity: major

### 7. Files folder in URL
expected: Open a server's Files, enter a subfolder. The URL contains ?path=... . Reload returns to the same folder; browser Back returns to the previous folder.
result: pass
note: Functional pass; file browser UI is poor, captured as deferred follow-ups

### 8. Credentials retest (gap G-65-6)
expected: In Settings, Credentials, type a deliberate value for a generated password; it is staged, the review says only "changed", apply saves it. Rotate another credential and apply: the new value appears once in the row with Copy value and Hide value (no reveal permission needed). Button positions do not jump while staging, keeping or undoing.
result: pass
note: Functional pass; alignment and scroll-to-value complaints deferred

## Summary

total: 8
passed: 7
issues: 1
pending: 0
skipped: 0
blocked: 0

## Gaps

- gap_id: G-65-6
  truth: "Credentials panel: values masked until revealed; staging a new credential shows only changed/rotated in the review, never the value"
  status: resolved
  resolved_by: 65-19-PLAN.md
  resolved_at: 2026-11-21
  reason: "User reported: Changing a value to some deliberate one is not possible, the regenerated value is nowhere visible for copying and we have similar layout issues as mentioned previously with button placements. Follow-up: We cannot choose any value ourselves and cannot see the regenerated value ever (unless we save, go there and have the reveal claim, which most users who manage servers will not have)."
  severity: major
  test: 6
  root_cause: "ServerSecretsPanel renders the typed-value input only for non-generated secrets (`!secret.generated`); generated secrets only offer a Rotate toggle. Rotation (POST /secrets/{key}/rotate) returns no value, so the new value is visible only afterwards via the audited reveal, which requires canRevealSecrets that most server managers lack."
  artifacts:
    - path: "sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx"
      issue: "Value input hidden for generated secrets; no way to see or copy a rotated value"
    - path: "sources/Api/Controllers/GameServerSecretsController.cs"
      issue: "RotateAsync returns no generated value to the caller"
  missing:
    - "Allow setting a deliberate value for generated secrets (or at least the ones whose game permits it)"
    - "Show the newly generated value once to the rotating member (copyable) without requiring reveal permission, without logging it"
    - "Fix button placement/layout in credentials rows (shared with deferred UI redesign)"
  debug_session: ""

## Deferred Follow-Ups

- test: 2
  idea: "Dislikes the details element used here and in ~90% of places; replace with a proper disclosure component"
  deferred_at: 2026-11-21
- test: 2
  idea: "Mod pack select dropdown looks bad in general; right-hand side lacks padding"
  deferred_at: 2026-11-21
- test: 2
  idea: "Final review-changes dialog before saving has poor alignment and an odd layout; rework (may already be more than needed)"
  deferred_at: 2026-11-21
- test: 3
  idea: "Details/disclosure sections look horrible (same complaint as test 2)"
  deferred_at: 2026-11-21
- test: 3
  idea: "Changing a value shifts the layout downward because new elements appear; the changed stamp and the reset button should not sit where they cause layout shift"
  deferred_at: 2026-11-21
- test: 3
  idea: "Configuration section cannot be filtered/searched and is thrown in as-is; whole section needs a redesign"
  deferred_at: 2026-11-21
- test: 4
  idea: "Review dialog eats too much vertical space with many staged changes (removal of items there is fine) and becomes hard to understand; redesign the review interface to scale for large change sets (e.g. compact, grouped, scannable diff)"
  deferred_at: 2026-11-21
- test: 5
  idea: "Leave dialog uses two button rows and inconsistent button styling: Stay is barely noticeable, Discard is flat dark red, Review is bright UTF gold in a separate row (gold for the recommended action is fine). Unify into one row with clear hierarchy."
  deferred_at: 2026-11-21
- test: 5
  idea: "Clicking the dark backdrop of the leave dialog does not dismiss it (should default to Stay and close); user is forced to make a deliberate choice every time"
  deferred_at: 2026-11-21
- test: 7
  idea: "File browser UI is very poor: file list sticks to the bottom, empty folder title and a subheader saying changes apply immediately, create-folder control disconnected from the list, layout/upload layout problems. Redesign Files panel."
  deferred_at: 2026-11-21
- test: 8
  idea: "Credentials UI is not properly aligned"
  deferred_at: 2026-11-21
- test: 8
  idea: "After saving, the rotated value should be shown immediately in view; having to scroll to find the new value is odd (scroll it into view / focus it)"
  deferred_at: 2026-11-21
