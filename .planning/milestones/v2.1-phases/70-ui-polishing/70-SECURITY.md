---
phase: "70"
slug: "ui-polishing"
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: "2026-11-26"
---

# Phase 70 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Browser ↔ API | Settings, review, leave, Files and Credentials screens send the same mutations as before; the server stays the authorization authority | user-entered values, low–medium |
| Browser storage | `utf.settings.open` in sessionStorage holds open Settings panel ids | anchor ids only |
| Secret display | One-time credential values and staged secret changes appear in the Credentials panel and the review dialog | secrets, high |
| Test harness | Mocked e2e seeds panel state through an init script | test-only |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-70-01 | Spoofing | danger versus primary button look | low | mitigate | danger keeps a distinct outline and red label, asserted at 4.5:1 in `tokens-contrast.test.ts` | closed |
| T-70-02 | Elevation of privilege | `useDrawerFocus` cycleTab | low | mitigate | `dialog[open]` early return defers to native modal focus; jsdom and browser tests | closed |
| T-70-03 | Tampering | ConfirmDialog backdrop on destructive confirms | medium | mitigate | `closeOnBackdrop = false` by default, maps only to `onCancel` (`primitives.tsx:541,598`) | closed |
| T-70-04 | Tampering | click-through while busy | low | mitigate | backdrop handler ignores clicks while busy and requires pointerdown on the backdrop; unit tested | closed |
| T-70-05 | Spoofing | destructive confirms styled as recommended | low | mitigate | every danger confirm stays danger; More actions grid never gold (70-15) | closed |
| T-70-06 | Tampering | labels in drawn controls | low | mitigate | labels stay React text nodes; no `dangerouslySetInnerHTML` | closed |
| T-70-07 | Elevation of privilege | disabled drawn controls | low | mitigate | native `disabled` stays on the input; unit tested | closed |
| T-70-08 | Information disclosure | `utf.settings.open` | low | mitigate | ids only; non-string entries dropped; unit tested | closed |
| T-70-09 | Information disclosure | Credentials panel without `canManageSecrets` | high | mitigate | `ServerSecretsPanel.tsx:529` returns null and the query is disabled (`:425`); `settings-section.test.tsx` asserts no request | closed |
| T-70-10 | Tampering | corrupt storage value | low | mitigate | unreadable value means all collapsed; membership checks only | closed |
| T-70-11 | Tampering | file and folder names | low | mitigate | rendered as text nodes | closed |
| T-70-12 | Tampering | drop zone targeting | low | mitigate | reuses `handleDrop`, preflight and collision approval; destination stays shown | closed |
| T-70-13 | Elevation of privilege | read-only viewer on switch or chip | low | mitigate | disabled props moved unchanged onto `SwitchField` and chip; server authoritative | closed |
| T-70-14 | Tampering | backdrop on leave or discard dialog | low | mitigate | backdrop maps to Stay / Keep editing only; delete confirm does not opt in; unit tested | closed |
| T-70-15 | Tampering | boolean entry value | low | mitigate | switch stages only "true" and "false"; unit tested | closed |
| T-70-16 | Tampering | pack titles and entry names | low | mitigate | rendered through `ApiText` or `bdi` | closed |
| T-70-17 | Information disclosure | search over sensitive or redacted entries | medium | mitigate | `matchesQuery` in `configurationFilterRules.ts` reads name, group and description only; unit tested | closed |
| T-70-18 | Tampering | API text in filter results | low | mitigate | filter returns entries rendered through `ApiText` | closed |
| T-70-19 | Information disclosure | one-time credential during focus and scroll | medium | mitigate | focus moves only the showing element; e2e no-trace helpers assert after rotate | closed |
| T-70-20 | Information disclosure | typed credential in a review row | high | mitigate | `ChangeDiffItem.tsx` renders `secretChanged` / `secretRotated` as placeholder text; registry `reviewReady` and review tests assert the value never appears | closed |
| T-70-21 | Tampering | queued file paths | low | mitigate | text nodes; upload pipeline unchanged | closed |
| T-70-22 | Repudiation | seeded open panels hiding regressions | low | mitigate | S-14 audits the collapsed default and auto-open rules | closed |
| T-70-23 | Repudiation | seeded panels in every browser flow | low | mitigate | collapsed-default test passes `settingsPanels: []`; absent-only seed | closed |
| T-70-24 | Tampering | `seedSettingsPanels` init script | low | accept | test-only, never bundled; see AR-70-01 | closed |
| T-70-SC | Tampering | npm installs | high | accept | no package change in the phase; see AR-70-02 | closed |

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-70-01 | T-70-24 | Test-only code under `tests/e2e`, never imported by `src`; writes anchor ids only | plan threat model | 2026-11-26 |
| AR-70-02 | T-70-SC | No package installed (D-17); `package.json` and `package-lock.json` have no commits since 22a4cace | plan threat model | 2026-11-26 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-26 | 25 | 25 | 0 | orchestrator (ASVS L1 grep depth, block_on high; register authored at plan time, short-circuit per secure-phase §3) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-11-26
