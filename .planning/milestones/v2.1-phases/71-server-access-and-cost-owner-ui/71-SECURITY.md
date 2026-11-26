---
phase: "71"
slug: "server-access-and-cost-owner-ui"
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: "2026-11-26"
---

# Phase 71 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Browser ↔ API (access) | The Access panel lists, grants and revokes per-server permissions; the API stays the authorization authority (ManageAccess policy plus the assignment-scope rule) | user and role ids, permission names, member nicknames, medium |
| Browser ↔ API (cost owner) | The Cost owner panel reads usable owners and transfers the owner; Modify policy plus a service re-check | owner ids and names, billing responsibility, medium |
| API projection | `canManageAccess`, `costOwnerIsFrozen`, `costOwnerAssignedAt` added to the server DTO | flags and a timestamp behind the owner visibility rule, low |
| Test harness | Strict mocked API in Playwright; every unexpected request is recorded | test-only |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-71-01 | Elevation of privilege | `GameServerAccessService` grant/revoke | high | mitigate | `AssignablePermissionsFor` (`GameServerAccessService.cs:437`) checked before any change (`:425`); `ManageAccessAlone_CannotGrantFullControlToSelf` test | closed |
| T-71-02 | Information disclosure | access user/role options | medium | mitigate | `[Authorize(ManageAccess)]` (`GameServersController.cs:490,517,552`); verified, not deleted, not banned filter (`GameServerAccessService.cs:138`); take 1–50 (`GameServersController.cs:528,563`) | closed |
| T-71-03 | Information disclosure | candidate and grant `detail` | medium | mitigate | `detail` is `#` plus the last six id characters; no linked-account field projected (71-01 grep gate) | closed |
| T-71-04 | Tampering / DoS | nickname and title search | low | mitigate | `Helpers/LikePattern.cs` escapes wildcards, used by the access service | closed |
| T-71-05 | Spoofing (IDOR) | `gameServerId` route value | low | mitigate | guid route constraint, per-server policy, existence check after authorization | closed |
| T-71-06 | Tampering (markup) | `SearchPicker` options and token | low | mitigate | React text inside `bdi`; no `innerHTML` in `SearchPicker.tsx` | closed |
| T-71-07 | Information disclosure | `GET cost-owner/options` | medium | mitigate | `[Authorize(Modify)]` (`GameServersController.cs:334`) plus `HasServerPermission(…Modify)` re-check (`GameServerLifecycleService.cs`); usable owners only, current owner excluded, take capped | closed |
| T-71-08 | Information disclosure | `CostOwnerIsFrozen`, `CostOwnerAssignedAt` | medium | mitigate | both only when `canSeeOwner` (`GameServerProjectionService.cs:963,1065`); hidden case asserted in `GameServerProjectionServiceTests` | closed |
| T-71-09 | Tampering (race) | transfer while a deployment appears | low | accept | see AR-71-01 | closed |
| T-71-10 | Information disclosure | `ServerAccessPanel` | low | mitigate | returns null and sends no request without `canManageAccess` (`ServerAccessPanel.tsx:162`); test asserts no request | closed |
| T-71-11 | Tampering (markup) | `AccessSubjectRow` | low | mitigate | names in `bdi`, descriptions as text; no `innerHTML` | closed |
| T-71-12 | Elevation of privilege | Give access | medium | mitigate | unavailable without assignable `sv:view`; API scope rule answers 403, mapped to the forbidden message | closed |
| T-71-13 | Denial of service | candidate search | low | mitigate | 250 ms debounce, one character minimum, abortable query, client asks 20, API caps 50 | closed |
| T-71-14 | Elevation of privilege (stale state) | own flags after a grant | low | mitigate | `invalidateAfterAccessChange` refetches server, fleet and signed-in user (exact) | closed |
| T-71-15 | Information disclosure | transfer errors | low | mitigate | `costOwnerTransferFailure` maps statuses to fixed keys (`ServerCostOwnerPanel.tsx:273`); server text never rendered | closed |
| T-71-16 | Tampering (race) | transfer while a deployment appears | low | mitigate | `transferBlocker` mirrors the API predicate; 409 maps to the conflict message and refetches | closed |
| T-71-17 | Information disclosure | facts row for a hidden owner | low | mitigate | `serverCostFacts` shows "Assigned owner"; frozen and since come from fields that are null in that case | closed |
| T-71-18 | Tampering (accidental transfer) | Transfer button | medium | mitigate | choose then confirm; `initialFocus="dismiss"` (`ServerCostOwnerPanel.tsx:270`), ConfirmDialog default `closeOnBackdrop = false`, `busy` blocks dismissal | closed |
| T-71-19 | Elevation of privilege | transfer to an unusable owner | low | mitigate | picker lists usable owners only; API 403 mapped to the forbidden message | closed |
| T-71-20 | Elevation of privilege (self lock-out) | own-row switches | medium | mitigate | `requiresSelfConfirm` (`serverAccessModel.ts:162`, used at `ServerAccessPanel.tsx:313`) gates Full control, View, Manage access removal behind a danger dialog | closed |
| T-71-21 | Tampering (lost update) | concurrent toggles | medium | mitigate | `accessMutationScope` serial scope (`useAccessMutations.ts:33`); inverse-only rollback; invalidation after the last toggle; unit tested | closed |
| T-71-22 | Elevation of privilege | toggles outside the assignable set | low | mitigate | disabled with reason from `assignablePermissions`; API 403 backstop | closed |
| T-71-23 | Repudiation (false green) | strict e2e mock (cost owner) | low | mitigate | every test asserts `session.unexpected` empty and exact PUT bodies | closed |
| T-71-24 | Information disclosure (stale view) | workspace after losing View | low | mitigate | settle step navigates to the fleet with `replace: true` (`ServerAccessPanel.tsx:229`) on null server or 403 | closed |
| T-71-25 | Tampering (partial change) | Remove all access | low | mitigate | serial revokes, stop at first failure, always refetch, partial-failure alert; unit tested | closed |
| T-71-26 | Elevation of privilege (stale state) | own flags after any access change | low | mitigate | `settleAccessChange` in hook-level `onSettled`, runs after unmount | closed |
| T-71-27 | Repudiation (false green) | strict e2e mock (access) | low | mitigate | `session.unexpected` empty (`hosting-access.spec.ts:448,476`) and logged grant/revoke calls asserted | closed |
| T-71-28 | Repudiation (unaudited state) | screen registry | low | mitigate | `requiredRegistry` plus `screen-registry.test.ts`; S-15, S-16, O-10, O-11 run axe, reflow, spacing, German and heading audits | closed |
| T-71-SC | Tampering | npm installs | high | accept | see AR-71-02 | closed |

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-71-01 | T-71-09 | `ChangeCostOwnerAsync` is unchanged: it keeps its transaction and the 409 for an unresolved deployment; the UI predicate (T-71-16) mirrors it | plan threat model | 2026-11-26 |
| AR-71-02 | T-71-SC | No package installed (D-18); `package.json` and `package-lock.json` unchanged since 5fbb2d99, guarded by the 71-12 gate | plan threat model | 2026-11-26 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-11-26 | 29 | 29 | 0 | orchestrator (ASVS L1 grep depth, block_on high; register authored at plan time, short-circuit per secure-phase §3) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-11-26
