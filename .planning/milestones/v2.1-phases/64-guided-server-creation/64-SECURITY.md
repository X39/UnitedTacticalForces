---
phase: "64"
slug: "guided-server-creation"
status: verified
threats_open: 0
asvs_level: 1
created: "2026-11-21"
---

# Phase 64 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Router state to workspace | Create page hands a start intent to the workspace through history state | Boolean intent flag (no ids) |
| API text to UI | Option, owner, and reason strings from the API rendered in React | Display strings, never HTML |
| UI to API (create) | Create submission with cost owner, preset, and storage choices | Create payload; API re-validates and re-authorizes |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-64-01 | Spoofing | createStartIntent / workspace | low | mitigate | Own-property check, state replaced after read, id taken from loaded server (`createStartIntent.ts:20-26`, `ServerWorkspacePage.tsx:113-149`) | closed |
| T-64-02 | Tampering | CreateStartNotice | low | mitigate | Title rendered as React text; no raw HTML sinks in ReactApp/src | closed |
| T-64-03 | Elevation of privilege | create-then-start seam | low | mitigate | Start only via confirmation dialog; API re-authorizes (`GameServersController.cs:165-166`) | closed |
| T-64-04 | Tampering | presetSelection | low | mitigate | Exact `Array.find` matching, prototype keys tested (`createServerModel.ts:674-679`) | closed |
| T-64-05 | Elevation of privilege | frozen-owner readiness rule | low | accept | Client check is advisory; API guard at `GameServerLifecycleService.cs:209-210` | closed |
| T-64-06 | Tampering | FormFields / Disclosure | low | mitigate | Values rendered as React children | closed |
| T-64-07 | Information disclosure | CostOwnerLine | low | mitigate | Only scoped owner names or viewer nickname, no balances | closed |
| T-64-08 | Tampering | CreateSummary blocked submit | low | mitigate | aria-disabled plus preventDefault (`ActionControl.tsx:116-133`) | closed |
| T-64-09 | Information disclosure | CreateSummary error | low | mitigate | Fixed `Server_Create_Error` sentence only | closed |
| T-64-10 | Tampering | AdvancedOptions / Storage components | low | mitigate | API text wrapped in `ApiText`, no raw HTML | closed |
| T-64-11 | Tampering | external capacity entry | low | mitigate | Client bounds in schema; API re-validates (`ValidateStorageSelection`) | closed |
| T-64-12 | Tampering | PresetField / SizeField | low | mitigate | Resolved only through `Array.find`; API re-validates profile | closed |
| T-64-13 | Tampering | create submit (double submit) | medium | mitigate | Submit guard on readiness, pending, and in-flight ref; tested | closed |
| T-64-14 | Spoofing | post-create navigation | low | mitigate | URL uses `server.primaryKey` from the response only | closed |
| T-64-15 | Information disclosure | page error states | low | mitigate | Bare catch, fixed copy; secret-bearing rejections tested | closed |
| T-64-16 | Elevation of privilege | owner frozen after load | low | mitigate | Fixed error, owners refetch, no navigation; API rejects | closed |
| T-64-17 | Denial of service | invalid fields in collapsed disclosure | low | mitigate | noValidate, first invalid focus opens Advanced | closed |
| T-64-18 | Information disclosure | e2e spec | low | mitigate | Unmocked requests aborted and asserted empty; no secrets in spec | closed |
| T-64-19 | Repudiation | parity matrix | low | mitigate | 17 CRT rows asserted kept or relocated | closed |
| T-64-SC | Tampering | npm installs (plans 01-09) | low | accept | No package manifest changed in Phase 64 | closed |

*Status: open · closed · open — below high threshold (non-blocking)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-64-01 | T-64-05 | Client frozen-owner block is advisory; the API is the real guard and the page handles its rejection | Plan 64-02 | 2026-11-21 |
| AR-64-02 | T-64-SC | No packages installed in this phase; manifests verified unchanged | Plans 64-01..09 | 2026-11-21 |

---

## Informational (not blocking)

Unfixed code-review findings tied to closed threats (see `64-REVIEW.md`): WR-02 (start intent is a bare boolean, not scoped to the created server; start still needs explicit confirmation and API authorization) and WR-04 (post-create steps share the create `try`, so a late throw can show the "nothing created" copy).

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-21 | 20 | 20 | 0 | gsd-security-auditor |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-11-21
