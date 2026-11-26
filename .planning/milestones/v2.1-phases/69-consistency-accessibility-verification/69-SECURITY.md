---
phase: "69"
slug: "consistency-accessibility-verification"
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: "2026-11-25"
---

# Phase 69 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Browser ↔ API | React screens send mutations (files, profiles, finance, events, Kassenbuch, join form) to the existing API; the server stays the authorization authority | user-entered payloads, low–medium |
| URL → app state | Query parameters (`returnUrl`, `action`, filters) are parsed by `src/app/urlParameters.ts` | untrusted navigation input |
| Test harness ↔ backend | Mocked e2e (`tests/e2e`) must never reach a real API; seeded UAT (`tests/uat`) must only target localhost | fixtures, screenshots, traces |
| Supply chain | Vendored test fonts, pinned baseline container image, npm lockfile | test-only assets |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-69-01 | Tampering | ConfirmDialog accidental confirm | low | mitigate | initial focus on dismiss (`primitives.tsx:536`; `tests/primitives.test.tsx:208`) | closed |
| T-69-02 | Info disclosure | Disclosure hidden region | low | accept | see Accepted Risks | closed |
| T-69-03 | Elevation | AccessBoundary is client-only | medium | mitigate | documented client-only; no fetch when forbidden (`tests/state-views.test.tsx:175-185`) | closed |
| T-69-04 | Tampering | Ratchet ceilings raised | low | mitigate | reason required, raise refused (`tests/ratchets/scan.ts:220,262`; `legacy-ratchet.test.ts`) | closed |
| T-69-05 | Tampering | Vendored test fonts | medium | mitigate | SHA-256 `MANIFEST.json` + `tests/visual/font-manifest.test.ts` | closed |
| T-69-06 | Tampering | Baseline container image | medium | mitigate | digest-pinned image, read-only mount (`scripts/Update-VisualBaselines.ps1`) | closed |
| T-69-07 | Info disclosure | Screenshots/traces in git | low | mitigate | `playwright-report/`, `test-results/` ignored | closed |
| T-69-08 | Tampering | Unmocked request reaching a backend | low | mitigate | `mockApi.ts:157-164` aborts + records; `session.unexpected` asserted empty | closed |
| T-69-09 | Info disclosure | Fixture data with real tokens | low | mitigate | synthetic fixtures, `.test` TLD | closed |
| T-69-10 | Spoofing | `returnUrl` open redirect | medium | mitigate | origin/base-path check (`auth.tsx:34-46`; `url-parameters.test.ts:547-563`) | closed |
| T-69-11 | Tampering | `action` query parameter | low | accept | see Accepted Risks | closed |
| T-69-12 | Spoofing | Sign-in provider buttons | low | mitigate | `AuthenticationPage.tsx`; `tests/shell-layouts.test.tsx:412-432` | closed |
| T-69-13 | Elevation | Fleet/workspace boundary swap | low | mitigate | identical `allowed` expressions; authorization tests | closed |
| T-69-14 | Tampering | Inline file Delete | low | mitigate | confirm with dismiss focus (`files-panel-controls.test.tsx:107-124`) | closed |
| T-69-15 | Elevation | Row actions without file rights | low | mitigate | `canMutate && entry.canModify` (`ServerFilesPanel.tsx:895`) | closed |
| T-69-16 | Tampering | Access matrix payload | low | mitigate | full-payload assertions (`configuration-profiles.test.tsx`) | closed |
| T-69-17 | Tampering | Finance correction payload | medium | mitigate | argument assertions (`finance-pages.test.tsx:253-295`) | closed |
| T-69-18 | Tampering | Cost confirmation after note change | low | mitigate | `CostConfirmDialog.tsx:208`; `cost-confirm-dialog.test.tsx:277-296` | closed |
| T-69-19 | Info disclosure | Audit evidence in disclosures | low | accept | see Accepted Risks | closed |
| T-69-20 | Elevation | Role detail forbidden header | low | mitigate | queries gated on `authorized` (`RoleDetailPage.tsx:106,118`) | closed |
| T-69-21 | Repudiation | Membership approve/reject labels | low | mitigate | buttons name their action, reject needs a reason (no dialog exists; 69-15 deviation) | closed |
| T-69-22 | Tampering | Event form payload | low | mitigate | payload assertions (`admin-event-authoring.test.ts:185-292`) | closed |
| T-69-23 | Tampering | Account deletion dialog | low | mitigate | focus on Keep account; single call after confirm (`member-pages.test.tsx:410-450`) | closed |
| T-69-24 | Tampering | Accidental publish/delete | low | mitigate | no request before the named action (`admin-content-pages.test.tsx:214-234`); confirm dialog is product decision X69-18-01 | closed |
| T-69-25 | Tampering | Raw file chooser input | low | accept | see Accepted Risks | closed |
| T-69-26 | Tampering | Mod pack payload after button conversion | low | mitigate | payload code untouched; existing validation tests | closed |
| T-69-27 | Tampering | Kassenbuch entry payload | medium | mitigate | `admin-kassenbuch.test.tsx:182-193` | closed |
| T-69-28 | Info disclosure | Public pages after header change | low | accept | see Accepted Risks | closed |
| T-69-29 | Tampering | Join form payload | low | mitigate | full `toEqual` (`public-form-pages.test.tsx:174-215`) | closed |
| T-69-30 | Tampering | Destructive dialogs at narrow widths | low | mitigate | `dialog-geometry.spec.ts:176,198` on all three projects | closed |
| T-69-31 | Repudiation | Vacuous German leak check | low | mitigate | non-vacuity tests (`tests/audit/german-leak.test.ts`) | closed |
| T-69-32 | Tampering | Cost confirmation a11y fixes | low | mitigate | pre-confirm request throws (`fleetScreens.ts:700-730`) | closed |
| T-69-33 | Tampering | Scheduled-start consent dialog | low | mitigate | `consentReady` guard (`workspaceScreens.ts:486-498`) | closed |
| T-69-34 | Info disclosure | Finance/admin fixtures | low | mitigate | synthetic fixtures + abort layer | closed |
| T-69-35 | Spoofing | Classic redirect target | low | mitigate | origin assertion (`route-walk.spec.ts:457-480`) | closed |
| T-69-36 | Tampering | Journey mocks reaching a real API | low | mitigate | `session.unexpected` empty in all 4 journeys | closed |
| T-69-37 | Info disclosure | Baseline images | low | mitigate | none tracked; synthetic fixtures | closed |
| T-69-38 | Tampering | Seeded journeys aimed at a real deployment | high | mitigate | localhost-only guard at module load (`tests/uat/support/uatTarget.ts:21-34`; `tests/audit/uat-target.test.ts`) | closed |
| T-69-39 | Info disclosure | UAT screenshots/traces | low | mitigate | ignored output dirs (`playwright.uat.config.ts:18,26`) | closed |
| T-69-40 | Repudiation | Known findings left as tolerances | low | mitigate | 0 `XPF-69`/`F-69-` ids in `sources/ReactApp` (one-time grep, not a CI test) | closed |
| T-69-41 | Tampering | Allowance list widened later | low | mitigate | closed `allowedRemainders` (`rules.json`; `ratchet.test.ts:275-312`) | closed |
| T-69-SC | Tampering | npm/pip/cargo installs | high | accept | see Accepted Risks | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-69-01 | T-69-02 | Collapsed content was already in the DOM; no secrets render inside disclosures; credentials stay masked | plan threat model | 2026-11-25 |
| AR-69-02 | T-69-11 | `action` is a closed enum (`serverActionIds`); the cost confirmation still guards every deep-link action | plan threat model | 2026-11-25 |
| AR-69-03 | T-69-19 | Same API text already shown through `ApiText`; no new fetch | plan threat model | 2026-11-25 |
| AR-69-04 | T-69-25 | Upload validation stays server-side and in page code; the wrapper is on the ratchet allow-list | plan threat model | 2026-11-25 |
| AR-69-05 | T-69-28 | Public pages fetch and render no new data | plan threat model | 2026-11-25 |
| AR-69-06 | T-69-SC | No package added in the phase (D-18); lockfile git history empty for the phase window | plan threat model | 2026-11-25 |

*Accepted risks do not resurface in future audit runs.*

**Hardening notes (non-blocking):** T-69-26 coverage is thin if the mod pack payload code changes later; T-69-40 is a one-time grep, not a persistent CI guard. The 69-33 emulated-provider change (`6ec88fdc`) is Development-only and adds no attack surface.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-25 | 42 | 42 | 0 | gsd-security-auditor (ASVS L1, block_on high) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-11-25
