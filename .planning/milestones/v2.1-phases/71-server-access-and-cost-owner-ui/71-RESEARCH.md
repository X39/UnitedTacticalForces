# Phase 71: Server Access and Cost Owner UI - Research

**Researched:** 2026-11-26
**Domain:** React server workspace (Settings panels, combobox picker, optimistic mutations with React Query) plus additive ASP.NET Core API changes (claims-based access, cost-owner transfer) and OpenAPI contract regeneration
**Confidence:** HIGH (every finding below was read in the code this session; the few exceptions are tagged `[ASSUMED]`)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
None. `71-CONTEXT.md` was auto-generated with discuss skipped (`workflow.skip_discuss`). The scope is the approved design contract `71-UI-SPEC.md` (decisions D-01..D-20, API-01..API-05), which the plan must implement.

### Claude's Discretion
"All implementation choices are at Claude's discretion — discuss phase was skipped per user setting. Use ROADMAP phase goal, success criteria, and codebase conventions to guide decisions."

Standing user preferences that apply (from project memory, copied from CONTEXT.md):
- No native `<details>` elements, no ugly native selects; long lists of people or entries need filter/search.
- No layout shift when values are staged; dialogs aligned and compact.
- The API is not a frozen contract: if an endpoint needs a field or a change for a good UI, change it in the owning layer and regenerate the OpenAPI snapshot and the generated client.
- Never touch the legacy Blazor WebApp unless an API change would break it.

### Deferred Ideas (OUT OF SCOPE)
None - discuss phase skipped. (The UI-SPEC "Non-goals" are closed: no staged access matrix, no permission presets, no migration of `ConfigurationProfileAccessSection`, no new workspace section/route/query parameter, no change to `sources/WebApp`, no push frame for access changes, no change to billing, no new color/size/weight.)
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs exist in REQUIREMENTS.md for this phase. The decisions of `71-UI-SPEC.md` are the scope; this table maps each to the research that enables it.

| ID | Description | Research Support |
|----|-------------|------------------|
| D-01..D-03 | Two Settings panels (Access gated by `canManageAccess`, Cost owner for non-archived), order, collapsed by default | Panels render their own `SettingsPanel` (precedent `ServerSecretsPanel`); anchor literals must be in the panel file (`tests/app/url-parameters.test.ts`); `tests/setup.ts` opens all panels in every test |
| D-04..D-09 | Immediate per-permission grant/revoke, optimistic toggle, self-lockout confirmations | API grant/revoke are idempotent and atomic (`GameServerAccessService`); React Query v5 `scope` + inverse-toggle rollback; `SwitchField` is a native checkbox |
| D-10 | `SearchPicker` ARIA combobox | Build on `TextField` (passes `role`/`aria-*`), no raw `<input>` (ESLint `no-restricted-syntax` plus ratchet); APG combobox pattern confirmed |
| D-11, D-12 | Two-step transfer with confirm; availability rule | API 409 predicate equals `server.deployment !== null`; recommended rule differs from the spec (see Critical Findings 3) |
| D-13 | Cost strip link | `to={{ search: "?tab=settings", hash: "#server-cost-owner" }}` precedent in `AutoStopNotice`; route-link scan test |
| D-14 | Query keys never touched by fleet frame | `invalidateWorkspaceQueries` is exact; the member's own invalidations must also be exact (Critical Findings 4) |
| D-15 | Lost own access handling | Hook-level `onSettled`, server query returns `null` for 404 but throws for 403 (`expectedStatuses: [404]`) |
| D-16 | Filter from eight subjects | `SearchField` exists; threshold constant precedent in Phase 70 |
| D-17 | Contract regeneration | Exact commands, CRLF note, Kiota precedent (Critical Findings 8) |
| D-18 | No new dependency | Verified: nothing needed; `keepPreviousData`, mutation `scope`, `isMutating` exist in the pinned 5.101.4 |
| D-19, D-20 | Phase 69 gates, no migration of the profile section | Registry id collision (Critical Findings 1), ratchet and tier-A scans, locale gates |
| API-01..API-05 | Additive DTO fields and three endpoints | Exact code locations, tests and doubles that must change (Architecture Patterns, Validation Architecture) |
</phase_requirements>

## Summary

The existing API already contains everything the UI needs except discoverability data. `GET /game-servers/{id}/access` and the four grant/revoke POSTs are gated by `[Authorize(Claims.ResourceBased.Server.ManageAccess)]`, delegate to `GameServerAccessService` (idempotent, audited once per real change, allowlist of eight claims) and map errors through `TryMapException` (404/403/400/409). `PUT /cost-owner` is `Modify`-gated and its only blocking rule is an **unresolved deployment** (`TimeStampResolved == null`), which is exactly what `GameServerDto.Deployment` projects. The five additive API changes (API-01..API-05) all fit existing services: `GameServerProjectionService.ProjectServerAsync` (flags), `GameServerProjectionService.GetCreationCostOwnerOptionsAsync` (shared usable-owner rule), and `GameServerAccessService` (candidates, same exact-grant predicate as `GetAsync`).

The React side has all building blocks (`SettingsPanel`, `Disclosure inline`, `SwitchField`, `RadioGroupField segmented`, `ConfirmDialog`, `SearchField`, `TextField`, `ActionButton unavailableReason/iconOnly`, `useAppToast`). The one new primitive, `SearchPicker`, can be built on `TextField` without any raw control. The work is mostly wiring, plus a long tail of guard tests that fail if a new panel, service method, locale key or registry entry is added without the matching bookkeeping. Several statements in the UI-SPEC are contradicted by the code and must be corrected in the plan (next section); these are the highest-value findings.

**Primary recommendation:** Implement the UI-SPEC as approved, but apply the 12 corrections in "Critical Findings" below (registry ids, Discord privacy, transfer predicate, exact invalidation, concurrency-safe optimistic rollback, identical locale key, hook-level self-lost detection, panel-owned `SettingsPanel`, test-double updates, global QueryClient defaults, Kiota precedent, ManageAccess-can-grant-`sv` risk).

## Critical Findings (the UI-SPEC must be adjusted for these)

1. **Screen registry ids collide.** The UI-SPEC adds "S-09 Access panel, S-10 Cost owner panel, O-04 Transfer dialog, O-05 Remove-all dialog", but these ids already exist. [VERIFIED: tests/e2e/support/screens/registryTypes.ts] quote: `{ id: "S-09", kind: "screen", routePattern: patterns.workspace, tab: "machine", slice: "adjacent" }`, `{ id: "S-10", kind: "screen", routePattern: patterns.costAlerts, slice: "adjacent" }`, `{ id: "O-04", kind: "overlay", slice: "workspace" }`, `{ id: "O-05", kind: "overlay", slice: "workspace" }` (grep lines 175, 201, 202). The existing maxima are S-14, O-09, N-11, M-04, K-17 (`export const keyScreenCount = 17;` in `tests/e2e/support/keyScreens.ts`). **Use S-15 (Access), S-16 (Cost owner), O-10 (Transfer dialog), O-11 (Remove-all and self-removal dialogs), K-18, K-19, and raise `keyScreenCount` to 19.** Entries go in `requiredRegistry` (registryTypes.ts, slice `"workspace"`) and `workspaceScreens.ts`; `tests/hosting/screen-registry.test.ts` fails otherwise.

2. **The Discord username is NOT already exposed to ManageAccess holders.** [VERIFIED: sources/Api/Controllers/UsersController.cs:1128-1155, 1172-1176] `DiscordUsername = CanViewUserField(user, currentUserId, Claims.User.ViewDiscordId) ? user.Discord.Username : string.Empty` with `CanViewUserField` = `user.PrimaryKey == currentUserId || User.IsGlobalAdministrator() || User.HasClaim(claim, string.Empty) || User.HasClaim(Claims.Administrative.User, string.Empty)`; `ViewDiscordId = "usr:discord"` (Claims.cs). Putting the username into `detail` for every `ManageAccess` holder would widen a claim-gated field. **Resolution of the UI-SPEC open point: populate `detail` with the fingerprint fallback only** (`#` plus six characters of the user id), never the Discord username. Use the **last** six hex characters, not the first: user ids are EF-generated Guid keys and the Npgsql provider may generate time-ordered (v7) values whose leading characters are a timestamp `[ASSUMED]`; the trailing characters are random in v4 and v7. The member list and profile access already show full GUIDs to access managers (`ConfigurationProfileAccessSection` prints `user.userId`), so a short id fingerprint adds no new disclosure. Alternative that removes the API field: compute the fingerprint client-side from `userId` (then drop `Detail` from API-02/API-04); keep the field if the spec's tests expect it.

3. **Transfer availability should use `server.deployment === null`, not `isDeploymentActive`.** [VERIFIED: GameServerLifecycleService.cs:853-856] `if (await dbContext.GameServerDeployments.AnyAsync(q => q.GameServerFk == gameServerId && q.TimeStampResolved == null, ...)) throw new HostingConflictException("Cost ownership cannot change while a deployment is unresolved.");` and [VERIFIED: GameServerProjectionService.cs:1146-1147] `private static GameServerDeployment? ActiveDeployment(GameServer server) => server.Deployments.SingleOrDefault(q => q.TimeStampResolved == null);` which feeds `GameServerDto.Deployment`. The two predicates are identical. `isDeploymentActive` [VERIFIED: serverState.ts:180-183] returns false for `terminated`/`terminatedduetofault`, so a Terminated-but-unresolved deployment would offer the button and the API would answer 409. The Settings delete flow already uses `!server.deployment` (SettingsSection.tsx `canDelete`). Keep the 409 mapping as the backstop (race).

4. **Fleet invalidation must be `exact: true`.** The global key `hostingQueryKeys.fleet = ["hosting","servers"]` is a prefix of every per-server key (`server(id)`, `jobs`, `configuration`, `secrets`, `access`, candidates...). Existing code calls `invalidateQueries({ queryKey: hostingQueryKeys.fleet })` without `exact` in `CostAlertSettingsPanel.tsx:171` and `CostConfirmationProvider.tsx:270`, but `ServerWorkspacePage.refresh()` and `invalidateWorkspaceQueries` use `exact: true` precisely so editor state is not refetched. A prefix invalidation after an access change would refetch the active candidate and configuration queries (discarding half-typed search state and unsaved editors). Every Phase 71 invalidation of `fleet` and `server(id)` must pass `exact: true`; sub-keys (`access`, `costOwnerOptions`) are invalidated by their own exact or targeted keys.

5. **Snapshot rollback is unsafe with several switches in flight.** TanStack's cache-based optimistic pattern snapshots the whole list and restores it in `onError`; the docs flag concurrent mutations as a caveat. [CITED: tanstack.com/query/v5/docs/framework/react/guides/optimistic-updates] The UI-SPEC lets the other switches of a row stay operable while one request is pending, so restoring a whole snapshot could undo a sibling's successful optimistic change. Use `useMutation({ scope: { id: \`server-access-${serverId}\` } })` (mutation `scope` exists in 5.101.4, verified in `query-core` typings) so toggles for one server run serially, roll back **only the single (subject, permission) pair** with the inverse of `applyOptimistic` instead of restoring the snapshot, and invalidate in `onSettled` only when `queryClient.isMutating({ mutationKey }) <= 1`.

6. **Locale key `Server_Access_Summary_Held` is `{{permissions}}` in both languages and fails `locales:check`** (identical EN/DE values fail unless listed in `src/locales/identical-allowed.json`, a documented-reason list). Drop the key and render the `Intl.ListFormat` string directly (the spec says this is its only content). Other gates to run early on the new German copy: English function words in German values (`to`, `can`, `new`, `for`, `and`, `is`, `or`...), the English carry-over rule (a German word equal to the English word of the same key with at least three letters, such as "Server" or "Name", needs an entry in `shared-words-allowed.json`), and `Server_`/`Hosting_` keys that no source file references fail `locales:unused` (`Picker_*` only warns). [VERIFIED: scripts/check-locales.mjs]

7. **`SearchPicker` needs no ratchet exemption if it is built on `TextField` and `ActionButton`.** ESLint `no-restricted-syntax` and `tests/ratchets/rules.json` ban raw `button`, `input`, `select`, `textarea`, `details`, `dialog` outside `primitiveFiles` (ActionControl, FormFields, Disclosure, SearchField, primitives, AppToasts). `TextField` [VERIFIED: FormFields.tsx:157-222] forwards `role`, `aria-expanded`, `aria-controls`, `aria-activedescendant`, `aria-autocomplete`, `onKeyDown`, `onBlur`, `ref` to the native input (the existing `ConfigurationProfileAccessSection` already uses `<TextField role="combobox" ...>`); the clear control is `ActionButton iconOnly`. `ul`/`li role="option"` are not restricted. Add `src/components/SearchPicker.tsx` to `tierAPaths` in `tests/ratchets/rules.json` so the literal-text scan covers it (the UI-SPEC declares it Tier A); do not add it to `primitiveFiles`.

8. **Kiota client regeneration: follow repository precedent, verify by build.** The two most recent contract refreshes (`29e3959b`, `dcaaa718`) touched only `sources/ReactApp/openapi/utf-api.json` and `src/generated/api.ts`; `sources/WebApp/UtfApi` does not even contain `canManageAutomation`. All Phase 71 changes are additive and the Blazor repository maps `GameServerCostOwnerOptionDto` into its own `GameServerCostOwnerOptionInfo` explicitly. AGENTS.md says "regenerate ... generated clients", while the Phase 71 context says never touch the legacy WebApp unless an API change would break it. **Recommended:** regenerate the snapshot and `api.ts` (CI gates both); do not regenerate `sources/WebApp/UtfApi` in this phase (a regeneration would drag every earlier stale change into the diff); prove compatibility with `dotnet build sources/WebApp/WebApp.csproj` and record the decision. If the user wants the Kiota client refreshed anyway, the sanctioned route is `python sources/WebApp/generate-client.py` (kiota 1.32.2 is installed) as its own task and commit. Flag in Open Questions.

9. **Existing tests and mocks break by design when two panels are added.** `tests/setup.ts` seeds every Settings panel open before each test (`seedSettingsPanels()` with `allSettingsPanelIds`), and e2e `installApp` does the same through `tests/e2e/support/settingsPanels.ts`. Both `allSettingsPanelIds` lists, `tests/app/url-parameters.test.ts` (`panelFiles` map must contain the new anchors and each anchor must be a string literal in its panel file), `tests/hosting/settings-section.test.tsx`, `tests/hosting/workspace-section-panels.test.tsx` and every e2e mock that renders the Settings tab with a `canModify` server need updating: the Cost owner panel will request `GET .../cost-owner/options` as soon as it is open, and the strict Playwright mock aborts and records unmocked requests (`session.unexpected`, asserted empty by every audit). Add handlers for `GET {base}/cost-owner/options` and `GET {base}/access` in `workspaceHandlers`, `mockAutomationRoutes` (automationMocks.ts) and any journey mock that opens Settings. Mitigation in code: enable the options query only for `server.canModify` and after first open.

10. **Global QueryClient defaults differ from the spec's assumptions.** [VERIFIED: src/app/App.tsx:591-603] `staleTime: 30_000`, `refetchOnWindowFocus: false`, retry skips 401/403/404. "Refetch on window focus" and `staleTime 0` for the access query must be explicit per-query options (`refetchOnWindowFocus: true, staleTime: 0`). The panel body stays mounted while collapsed, so "refetch when opened by hand" is not a remount: trigger `refetch()` from an effect on the panel's open flip (`useSettingsPanelState().isOpen(id)`). Test QueryClients (`renderWithProviders`) use library defaults, so the first test must assert the explicit options.

11. **`SwitchField` has no authored `aria-checked`.** It is a native `input type="checkbox" role="switch"`; the browser's checked state is the state (SwitchField.tsx doc comment). The UI-SPEC accessibility rule "with `aria-checked`" must be asserted as `toBeChecked()` / `aria-checked` as computed by the accessibility tree, not as an authored attribute.

12. **Security observation outside the UI scope but made one click easier.** [VERIFIED: GameServerAccessService.cs:17-27, 279-283] `ValidatePermission` checks only the allowlist; a holder of `sv:manage-access` alone may grant `sv` (Full control) to any member, including themselves. The feature pack says ManageAccess is "subject to the claim types the holder is allowed to assign" (resource-scoped-access-control.md:19), which the service does not implement. Do not change in Phase 71 unless the user asks; record in Open Questions.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Who may manage access (`canManageAccess`) | API / Backend | Browser (reads flag) | Exact rule `ServerManageAccessRequirement.HasExactPermission` needs the principal's role and administrator claims, which `FullUser.claims` in the browser cannot reproduce |
| Grant / revoke one permission | API / Backend | Database (claim tables) | Atomic, idempotent, audited per change; the browser only issues one call per toggle |
| Candidate search (members, roles, owners) | API / Backend | Browser (debounce, render) | Server-side exclusion keeps `take` honest and respects the usable-owner rule |
| Optimistic switch state and rollback | Browser / Client | — | Pure cache manipulation; the API stays the authority |
| Cost owner transfer rules (409/400/403) | API / Backend | Browser (pre-check for UX) | Client check `server.deployment === null` is an approximation of the server transaction |
| Live refresh after transfer | API (fleet interceptor + WebSocket) | Browser (`useWorkspaceLiveUpdates`) | `FleetChangeInterceptor` already publishes on `CurrentCostOwnerFk` change |
| Live refresh after access change | none (accepted gap) | Browser (focus, reopen, own change) | Claim tables are not covered by the interceptor |
| Settings panel mechanics, anchors | Browser / Client | — | `SettingsPanel`, `useSettingsPanelState`, `serverSettingsPanelAnchors` |

## Standard Stack

### Core (all already installed; no new dependency, D-18)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| react / react-dom | 19.2.8 | UI | existing |
| @tanstack/react-query | 5.101.4 | queries, optimistic mutations, `keepPreviousData`, mutation `scope`, `isMutating` | existing; v5 features verified in `node_modules/@tanstack/query-core` typings |
| react-router | 7.18.2 | hash/section links, `navigate(..., { replace: true })` | existing |
| i18next / react-i18next | 26.3.6 / 17.0.11 | en/de keys, `_one`/`_other` plurals | existing |
| @fluentui/react-icons | 2.0.320 | `Dismiss16Regular`, `Person20Regular`, `PeopleTeam20Regular`, `Wallet20Regular` | existing (aliased to a mock in Vitest: `tests/mocks/react-icons.tsx`, so every new glyph name must exist in the mock) |
| vitest / @testing-library | 4.1.10 / 16.3.2 + user-event 14.6.1 | component tests | existing |
| @playwright/test + @axe-core/playwright | 1.62.1 / 4.13.0 | e2e, audits | existing, chromium installed locally |
| openapi-typescript | 7.13.0 | `npm run generate` -> `src/generated/api.ts` | existing |
| xunit + EF Core Sqlite | 2.9.3 / 10.0.9 | API tests | existing |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `TextField`-based `SearchPicker` | Fluent `Combobox` | Rejected by the spec (D-10) and by the mocks (`@fluentui/react-components` is aliased to a stub in Vitest) |
| ILike + escaped pattern (codebase convention) | `ToLower().Contains()` | `Contains` translates on Npgsql and SQLite, so search is fully testable in the SQLite suite; ILike is the convention (8+ call sites in `CommandCenterController`) but is untestable under SQLite. Recommended: ILike with escape, SQLite tests with `search: null`, plus an offline Npgsql `ToQueryString` translation test (the precedent in `CostOwnerViewAudienceTests.AudienceQueries_TranslateForNpgsql`) |

**Installation:** none. **Version verification:** `package.json` pins; `npm view` is unnecessary because no package is added.

## Package Legitimacy Audit

No external package is installed in this phase (D-18). Audit table not applicable.

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
Browser (React)                                  API (ASP.NET Core)                         PostgreSQL
-----------------                                ------------------                         ----------
Settings tab (?tab=settings#server-access)
  SettingsSection
    ├─ ServerAccessPanel  (renders only if server.canManageAccess)
    │    useQuery access ───────────────────────► GET /game-servers/{id}/access  ───────────► users/roles + claims
    │    AccessGrantForm
    │      SearchPicker ── debounce 250ms ──────► GET .../access/users|roles/options?search&take  (API-02/03)
    │      Give access ─────────────────────────► POST .../access/users|roles/grant  {sv:view}
    │    AccessSubjectRow x N  (8 SwitchField)
    │      toggle (optimistic, scope-serial) ───► POST .../access/{users|roles}/{grant|revoke}
    │      Remove all access (dialog, sequential)► N x revoke, stop at first failure, always refetch
    │    onSettled: invalidate access/server(exact)/fleet(exact), refetch me
    │        └─ server.canManageAccess === false → panel unmounts, toast, focus Settings h2 (D-15)
    │        └─ server query 404/403           → toast, navigate("/command/servers", replace)
    └─ ServerCostOwnerPanel (renders if !server.isArchived)
         facts from GameServer (+ costOwnerIsFrozen, costOwnerAssignedAt)   [API-01]
         SearchPicker (enabled after first open, canModify) ► GET .../cost-owner/options  (API-05)
         ConfirmDialog ─► PUT /game-servers/{id}/cost-owner ► ChangeCostOwnerAsync (txn, FOR UPDATE)
                                                              ├ 403 cannot use owner / 400 frozen / 404 archived
                                                              └ 409 unresolved deployment
         onSuccess: invalidate server(exact), fleet(exact), accruedCost(exact), options(prefix) + toast + focus owner value
  FleetChangeInterceptor ──(CurrentCostOwnerFk modified)──► WebSocket frame ──► useWorkspaceLiveUpdates ──► exact refetch
```

### Recommended Project Structure
```
sources/Api/
├── DTO/GameServerUserAccessCandidateDto.cs         # API-02 (one type per file, XML docs)
├── DTO/GameServerRoleAccessCandidateDto.cs         # API-03
├── DTO/GameServerUserAccessGrantDto.cs             # + Detail (API-04)
├── DTO/GameServerRoleAccessGrantDto.cs             # + MemberCount (API-04)
├── DTO/GameServerCostOwnerOptionDto.cs             # + Kind (API-05)
├── DTO/GameServerDto.cs                            # + CanManageAccess, CostOwnerIsFrozen, CostOwnerAssignedAt (API-01)
├── Helpers/LikePattern.cs (new, internal static)   # escape helper; do not edit CommandCenterController
├── Controllers/GameServersController.cs            # 3 new GETs
└── Services/GameServerLifecycle/                   # IGameServerAccessService + impl: candidate queries
                                                    # GameServerProjectionService: shared usable-owner query + flags
sources/ReactApp/src/
├── app/useDebouncedValue.ts                        # tiny hook, setTimeout inside effect (lint-safe)
├── components/SearchPicker.tsx + search-picker.css
├── components/server/AccessGrantForm.tsx, AccessSubjectRow.tsx, access.css
├── features/hosting/ServerAccessPanel.tsx, ServerCostOwnerPanel.tsx
├── features/hosting/serverAccessModel.ts, useServerAccess.ts, useAccessMutations.ts, useCostOwnerTransfer.ts
└── features/hosting/services.ts, types.ts, sections/SettingsSection.tsx, components/server/CostStrip.tsx
```

### Pattern 1: The panel owns its `SettingsPanel` and gates by returning null
**What:** `ServerSecretsPanel` calls all hooks, then `if (!server.canManageSecrets) return null;`, then renders `<SettingsPanel id="server-credentials" summary=... badge=...>`. The summary needs the query, so the query lives in the same component as the `SettingsPanel`. The anchor test requires the literal `"server-access"` / `"server-cost-owner"` in the panel file.
**When to use:** both new panels. `SettingsSection` renders them between `AutomationPanel` and the record panel (D-01 order), passing `server` and a `onAccessLost` callback that bumps its existing `headingFocus` counter.
```tsx
// Source: sources/ReactApp/src/features/hosting/ServerSecretsPanel.tsx (precedent, lines 415-426 and 527-570)
const statuses = useQuery({ queryKey: hostingQueryKeys.secrets(id), queryFn: ..., enabled: server.canManageSecrets === true });
if (!server.canManageSecrets) return null;      // no heading, no summary, no request
return <SettingsPanel id="server-credentials" title={...} summary={loaded ? ... : undefined}>...</SettingsPanel>;
```

### Pattern 2: SearchPicker on `TextField` (combobox, list autocomplete, focus stays in the input)
**What:** input = `TextField role="combobox" aria-autocomplete="list" aria-expanded aria-controls aria-activedescendant autoComplete="off" spellCheck={false}`; options `li role="option" id aria-selected aria-disabled`; select on `onPointerDown` with `preventDefault` so the input keeps focus. APG semantics confirmed: `aria-activedescendant` on the combobox, `aria-selected="true"` on the visually focused option, Down/Up/Enter/Escape/Home/End. [CITED: w3.org/WAI/ARIA/apg/patterns/combobox/]
**Sketch (props from the UI-SPEC `SearchPicker` table):**
```tsx
<TextField
  ref={inputRef}
  label={label}
  role="combobox"
  aria-autocomplete="list"
  aria-expanded={open}
  aria-controls={listboxId}
  aria-activedescendant={activeId}   // undefined when no active option
  aria-busy={status === "loading" || undefined}
  autoComplete="off"
  spellCheck={false}
  value={query}
  onChange={(e) => onQueryChange(e.target.value)}
  onKeyDown={handleKeys}             // ArrowDown/Up wrap, Home/End, Enter select, Escape close then clear
  onBlur={close}
/>
```
Notes: the chosen token replaces the input in the same slot, so keep the label element outside the conditional (a token has no input to `htmlFor`; render the label as a `span`/`id` and `aria-labelledby` the token region). Keep previous results while loading with `placeholderData: keepPreviousData` (already used in `EventAdminPage.tsx:89`).

### Pattern 3: Debounce without a lint violation
`react-hooks/set-state-in-effect`, `refs`, `purity`, `set-state-in-render` are all active (eslint-plugin-react-hooks 7, `flat.recommended`, verified by listing its rules). Existing code uses setState-during-render (`if (reviewOpen && count === 0) setReviewOpen(false)`) rather than ref writes during render. For debouncing, `useEffect(() => { const id = setTimeout(() => setValue(input), 250); return () => clearTimeout(id); }, [input])` is accepted (setState inside the timer callback). "Opened once" for the cost-owner options query: derive from `isOpen(id)` with the set-state-during-render pattern, not a ref mutated during render.

### Pattern 4: Hook-level callbacks for lost-access handling (D-15)
Callbacks passed to `mutate()` do not run if the component unmounts; hook-level `useMutation({ onSettled })` do. The panel unmounts when the refetched server reports `canManageAccess === false`, so the detection must be hook-level and call parent-owned callbacks:
```ts
// onSettled (hook-level)
await Promise.all([
  queryClient.invalidateQueries({ queryKey: hostingQueryKeys.server(id), exact: true }),
  queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet, exact: true }),
  queryClient.invalidateQueries({ queryKey: queryKeys.me }),
]);
const state = queryClient.getQueryState(hostingQueryKeys.server(id));
const server = queryClient.getQueryData<GameServer | null>(hostingQueryKeys.server(id));
const err = state?.error;
if (server === null || (err instanceof ApiError && err.status === 403)) { showToast(...ViewLost); navigate("/command/servers", { replace: true }); }
else if (server && !server.canManageAccess) { showToast(...SelfLost); onAccessLost(); }
```
Why `null` and `403` separately: `hostingService.server` uses `expectedStatuses: [404]` (404 resolves to `undefined`, mapped to `null` by the page query), while a 403 from the `View` policy throws `ApiError` and React Query keeps the stale `data`, so `ServerWorkspacePage` would stay on the workspace with an error notice. `showToast` is global to `AppToastProvider` and survives navigation, so no router state is needed.

### Pattern 5: Optimistic toggle with a serial scope (D-08, Critical Finding 5)
```ts
useMutation({
  scope: { id: `server-access-${serverId}` },
  mutationFn: (v: Toggle) => hostingService.changeServerUserAccess(serverId, v.subjectId, v.permission, v.grant),
  onMutate: async (v) => { await queryClient.cancelQueries({ queryKey: accessKey, exact: true }); queryClient.setQueryData(accessKey, (c) => applyOptimistic(c, v)); },
  onError: (_e, v) => queryClient.setQueryData(accessKey, (c) => applyOptimistic(c, invert(v))),  // single pair, not a snapshot
  onSettled: ...
});
```
A subject whose last permission is removed must stay in the cache until settle (spec) so the row does not vanish under the pointer; `applyOptimistic` must never delete a subject.

### Anti-Patterns to Avoid
- **Prefix invalidation of `hostingQueryKeys.fleet`/`server(id)`** (Critical Finding 4).
- **Showing the Discord username** in candidate or grant DTOs (Critical Finding 2).
- **Raw `<input>`/`<button>`/`<details>`/`<dialog>`** anywhere new: ESLint error, ratchet failure.
- **Hard-coded user-facing literals** in Tier A files (`src/features/hosting`, `src/components/server`, `SearchPicker`): the literal scan counts JSX text and `label`/`placeholder`/`aria-label`/`title`/... props.
- **Calling `useNavigate`/toast logic only through `mutate()` callbacks** for the self-lost flow.
- **Using `Required<Schema<"GameServerDto">>` unguarded** for the new fields: older cached payloads and test fixtures lack them; default them in `normalizeServer` (`canManageAccess ?? false`, `costOwnerIsFrozen ?? null`, `costOwnerAssignedAt ?? null`), following the `canManageAutomation` precedent (types.ts `Omit<..., "canManageAutomation"> & { canManageAutomation?: boolean | null }`).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Collapsible rows | custom accordion / `<details>` | `Disclosure variant="inline" headingLevel={3}` | `aria-expanded`/`aria-controls`/`hidden` region already correct and tested |
| On/off controls | custom toggle | `SwitchField` (needs `onLabel`, `offLabel`) | native checkbox `role=switch`, state words, disabled state |
| Member/Role choice | custom tabs | `RadioGroupField layout="segmented" hideLegend` | native radios, keyboard behavior |
| Confirmations | custom modal | `ConfirmDialog` (`error`, `busy`, `confirmVariant`, `dismissVariant`, `initialFocus="dismiss"`, `children`) | portal into `document.body`, stops submit bubbling (so a dialog opened from inside the grant form never submits it), busy blocks Escape |
| Disabled-with-reason buttons | `disabled` + tooltip | `ActionButton unavailableReason` | stays focusable, `aria-describedby`, blocks form submit |
| Icon-only clear | raw button | `ActionButton iconOnly aria-label` | no ratchet hit |
| List filter | new input | `SearchField` (visually hidden label, clear on Escape) | existing a11y behavior |
| Toasts / announcements | custom live region | `useAppToast().showToast`, the SettingsSection `announce`-style `sr-only` polite region | one-shot, no duplicate announcements |
| Debounced search | per-component timers | one `useDebouncedValue` hook | lint-safe, shared by both pickers |
| Owner kind label | new mapping | `normalizeOwnerKind` (`financeModel.ts`, tolerant of numeric and string kinds) + the new `Server_CostOwner_Kind_*` keys | enum serialization is tolerated both ways in the codebase |
| LIKE escaping | inline `Replace` | one `internal static` helper copied from `CommandCenterController.EscapeLikePattern` (private there) | wildcard injection (`%`, `_`) in the new search params |
| Claim rule for `canManageAccess` | re-implement in the projection | `ServerManageAccessRequirement.HasExactPermission(principal, id.ToString("D"))` (`internal static`, same assembly) | one source of truth, the spec test compares them |

**Key insight:** this phase is wiring plus bookkeeping. Almost every behavior already exists as a tested primitive; the risk is in the guard tests (registry, parity matrix, locale, ratchet, anchors) that fail when bookkeeping is missed.

## Runtime State Inventory

Not applicable: not a rename/refactor/migration phase. No database migration is expected (`dotnet ef` not needed): all API changes are DTO fields, projections and queries over existing tables (`Users`, `Roles`, `Claims`, `CostOwners`, `GameServerCostOwnerAssignments`).

## Common Pitfalls

### Pitfall 1: Fixture and mock drift when `GameServer` gains three fields
**What goes wrong:** typed fixtures built with `as unknown as GameServer` compile, but `fleetServer()` (tests/helpers/fleetFixtures.ts), `automationServer()` (e2e automationMocks.ts), `journeyMocks.ts`, `filesMocks.ts` and `workspaceMocks.ts` do not emit `canManageAccess`, so every Access-panel test sees the panel absent.
**How to avoid:** add `canManageAccess`, `costOwnerIsFrozen`, `costOwnerAssignedAt` options to `fleetServer` (default `false`, `null`, `null`) and to the e2e server builders; add a `contract-shape.test.ts` case that a legacy payload without the fields normalizes.
**Warning signs:** panel tests passing only after hand-adding properties to each test.

### Pitfall 2: API test doubles break the build
**What goes wrong:** adding methods to `IGameServerAccessService` breaks the two implementers `tests/Api.Tests/GameServerHosting/NoOpGameServerAccessService.cs` and the private `RecordingAccessService` in `GameServerAccessControllerAuthorizationTests.cs`. (No test implements `IGameServerProjectionService` or `IGameServerLifecycleService`.)
**How to avoid:** update both doubles in the same task as the interface change; extend `AccessEndpoints_RequireServerManageAccessWithoutGlobalRoleAdministration` with the two new GETs and the `StorageEstimateRoutes_RequireExpectedAuthorization` theory with the cost-owner options method expecting `Claims.ResourceBased.Server.Modify`.

### Pitfall 3: ILike cannot run under SQLite
**What goes wrong:** `EF.Functions.ILike` does not translate on the SQLite suite (`HostingTestStore` uses in-memory SQLite). The existing `CommandCenterController` search is only tested with `search: null`.
**How to avoid:** isolate the filter in an `internal static IQueryable<T> ApplySearch(...)` and cover translation offline with the `NpgsqlModelDbContext` + `ToQueryString` pattern; test exclusion, verified/banned/deleted, ordering, `take`, `memberCount` with `search: null` on SQLite. Escape `\ % _ [ ] ^` and pass the `"\\"` escape argument (CommandCenter convention).

### Pitfall 4: OpenAPI route and snapshot rules
**What goes wrong:** `OpenApiRouteCompatibilityTests` rejects templated paths that collide after parameter names are removed; the CI step `cmp --silent` compares the exported JSON byte for byte; the exporter writes LF (`ReplaceLineEndings("\n")` in `Program.cs:153`) while the Windows working copy of `utf-api.json` and `api.ts` is CRLF (autocrlf; `git diff` shows only line-ending warnings today).
**How to avoid:** the new routes (`.../access/users/options`, `.../access/roles/options`, `.../cost-owner/options`) do not collide with `.../access/users/grant` or `creation/cost-owner-options`. Regenerate with the exact CI command; compare through `git diff`, not `cmp` against the CRLF working copy. XML doc comments become OpenAPI descriptions, so new C# docs must be final text.

### Pitfall 5: Losing View while keeping ManageAccess
**What goes wrong:** `sv:manage-access` does not imply `sv:view`. A manager who revokes their own View (after confirming) keeps the Access endpoints but gets 403 on `GET /game-servers/{id}` and the server vanishes from `GET /game-servers`. The Access panel would show a stale list.
**How to avoid:** Pattern 4 handles it; the e2e "self lock-out" flow must also cover the View case, not only Manage access.

### Pitfall 6: `ReadOnly` banner contradicts an operable Access panel
**What goes wrong:** `SettingsSection` shows `Server_Change_ReadOnly` ("You can see these settings but not change them.") whenever `!server.canModify`. A member with `sv:manage-access` but not `sv:modify` sees that banner above a fully operable Access panel.
**How to avoid:** either show the banner only when neither right is held, or word the Cost owner/Access panels so the contradiction is explained. Decide in the plan; add a test.

### Pitfall 7: Cost owner hidden from the viewer
**What goes wrong:** `CostOwnerName` is the placeholder "Assigned owner" and `CostOwnerUserId`/alert data are null when the viewer cannot see the owner (`CanViewCostOwnerAsync`). The transfer dialog's old owner and the facts row must use `ownerFor(...)` (hidden/unknown variants), and `costOwnerIsFrozen`/`costOwnerAssignedAt` must be null in that case (API-01).
**How to avoid:** compute both in `ProjectServerAsync` behind the existing `canSeeOwner`.

### Pitfall 8: Concurrency of Remove all access
**What goes wrong:** sequential revokes (one per stored permission) can half-complete; the section must always refetch, show `Server_Access_Error_Remove` after a partial failure, and never assume the row is gone. Run it inside the same `scope` as toggles so a toggle cannot interleave.

### Pitfall 9: 403 mapping
The access endpoints raise `HostingForbiddenException` -> `ForbidResult` -> HTTP 403 `[ASSUMED: same cookie-scheme behavior as ConfigurationProfilesController.Forbid()]`; the React `ApiError` maps 403 to `code: "forbidden"`; the global retry policy never retries 401/403/404.

### Pitfall 10: Playwright audits are long
The S-14 entry turns the keyboard walk off and calls `allowLongWalk()` because a walk per state exceeds the test timeout. The new S-15/S-16 entries have many states; copy that approach (keyboard off for entries with many states, covered by the dedicated `hosting-access.spec.ts` keyboard test).

## Code Examples

### C#: candidate query with exact-grant exclusion (API-02), derived from `GameServerAccessService.GetAsync`
```csharp
// Source: sources/Api/Services/GameServerLifecycle/GameServerAccessService.cs:42-57 (same predicate), controller bounds from ConfigurationProfilesController.cs:388
var value = ResourceValue(gameServerId);                       // gameServerId.ToString("D")
IQueryable<User> query = dbContext.Users.AsNoTracking()
    .Where(q => !q.IsDeleted && !q.IsBanned && q.IsVerified)
    .Where(q => !q.Claims!.Any(claim =>
        PermissionAllowlist.Contains(claim.Identifier)
        && claim.Value == value && claim.ValueType == null && !claim.IsPrefix));
query = ApplyNicknameSearch(query, search);                    // ILike with escaped pattern, search null = no filter
var rows = await query.OrderBy(q => q.Nickname).ThenBy(q => q.PrimaryKey).Take(take)
    .Select(q => new { q.PrimaryKey, q.Nickname }).ToArrayAsync(cancellationToken);
// detail = "#" + last six characters of the "N" format of PrimaryKey (see Critical Finding 2), not the Discord username
```
Controller: `take is < 1 or > 50` -> `BadRequest`; default `take = 20`; `[Authorize(Claims.ResourceBased.Server.ManageAccess)]` plus the service's own `Authorize(...)` re-check (existing pattern); `EnsureServerExistsAsync` -> 404.

### C#: cost-owner options (API-05): share the creation rule
`GetCreationCostOwnerOptionsAsync` (GameServerProjectionService.cs:282-318) already implements "administrator: all owners; otherwise own owner or exact `co:use` GUID claims; own owner first, then `DisplayName`, then key". Extract that body into a private `UsableCostOwners(principal)` queryable used by both the creation endpoint and the new endpoint (the creation response gains `Kind` only). The new endpoint additionally: requires `HasServerPermission(Modify)` (throw `HostingForbiddenException`), loads the server with `!IsArchived` (404 otherwise), excludes `CurrentCostOwnerFk`, applies the display-name search and `take`.

### TypeScript: exact invalidations after a transfer
```ts
// Source: pattern of ServerWorkspacePage.refresh() (exact keys) and invalidateWorkspaceQueries
await Promise.all([
  queryClient.invalidateQueries({ queryKey: hostingQueryKeys.server(id), exact: true }),
  queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet, exact: true }),
  queryClient.invalidateQueries({ queryKey: hostingQueryKeys.accruedCost(id), exact: true }),
  queryClient.invalidateQueries({ queryKey: hostingQueryKeys.costOwnerOptions(id) }),  // prefix on purpose: options of every search
]);
```

### Contract regeneration (Windows dev machine; CI uses the first two lines on Linux)
```bash
dotnet build X39.UnitedTacticalForces.slnx
dotnet run --project sources/Api/Api.csproj --no-build -- --export-openapi "A:/dev/git/UnitedTacticalForces/sources/ReactApp/openapi/utf-api.json"
cd sources/ReactApp && npm run generate        # openapi-typescript -> src/generated/api.ts
npm run contracts:check                        # contracts:generate + git diff --exit-code on api.ts
dotnet build sources/WebApp/WebApp.csproj      # additive-compatibility proof for the legacy client (Critical Finding 8)
```
[VERIFIED: .github/workflows/dotnet.yml:73-76; package.json scripts; docs/features/react-ui-beta.md:79-84]

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Profile access section: a plain `div role="listbox"` of `ActionButton role="option"` rows plus a staged matrix | Shared `SearchPicker` combobox + per-permission immediate switches | this phase | the profile section stays as is (D-20) |
| Spec says `isDeploymentActive` | `server.deployment === null` | this research | exact parity with the API 409 predicate |
| Snapshot-restoring optimistic rollback | `scope` + inverse single-pair rollback | this research | safe with parallel switches |

**Deprecated/outdated:** nothing in the stack; React Query v5 APIs used (`placeholderData: keepPreviousData`, mutation `scope`, `isMutating`) are current in 5.101.4.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | User primary keys may be time-ordered (v7) Guids from the Npgsql EF default, so the first six characters would be a poor fingerprint | Critical Finding 2 | Low: last six characters work for v4 and v7 anyway |
| A2 | Kiota models in `sources/WebApp/UtfApi` ignore unknown JSON members, so additive DTO fields cannot break the legacy Blazor client at runtime | Critical Finding 8 | Medium: only `dotnet build` is proven here; a seeded-stack smoke of the Blazor server list would settle it (the project defers real-environment checks to milestone end) |
| A3 | `ForbidResult` produces HTTP 403 for these endpoints (same as profile endpoints) | Pitfall 9 | Low: the React error mapping would also accept the status the API actually sends if tested in a mock |
| A4 | Docker daemon is running for `scripts/Update-VisualBaselines.ps1` (client 29.5.3 present, daemon not probed) | Environment Availability | Low: baselines are a human-reviewed item |
| A5 | Counting role `memberCount` as non-deleted members (recommended) matches the user's idea of "reach" | Pattern/API-04 | Low: copy says "{{count}} member(s)" |

## Open Questions

1. **Kiota client refresh.** AGENTS.md says "regenerate ... generated clients"; recent precedent leaves `sources/WebApp/UtfApi` stale and the phase context forbids touching the legacy WebApp unless broken.
   - What we know: all changes are additive; the Blazor cost-owner mapping is explicit.
   - What's unclear: whether the user wants the stale client refreshed now.
   - Recommendation: skip, verify with `dotnet build sources/WebApp/WebApp.csproj`, state it in the plan; offer `generate-client.py` as a separate optional task.
2. **ManageAccess can grant `sv`.** See Critical Finding 12.
   - Recommendation: out of scope; record as a follow-up and surface it in verification so the user can decide whether to restrict assignable claims to the holder's own.
3. **Role `memberCount` definition** (all members vs active members). Recommendation: active (not deleted, not banned); document in the DTO XML doc.
4. **`Server_Change_ReadOnly` banner vs Access panel** (Pitfall 6). Recommendation: show the banner only when neither `canModify` nor `canManageAccess`; otherwise rely on per-panel states.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Node.js / npm | React build, Vitest, Playwright | yes | v25.5.0 (package.json engines `>=24 <25`; `.npmrc` `engine-strict=false`; a 2-file Vitest smoke run passed: 122 tests) | none needed |
| .NET SDK | API build and tests | yes | 10.0.201 (same as CI) | none |
| dotnet-ef | not needed (no migration) | yes | 10.0.9 | n/a |
| Kiota CLI | optional WebApp client refresh | yes | 1.32.2 (matches `generate-client.py` pin) | skip (precedent) |
| Playwright chromium | e2e | yes | several builds in `%LOCALAPPDATA%/ms-playwright` | none |
| Docker | visual baselines (`Update-VisualBaselines.ps1`) | client yes, daemon unverified | 29.5.3 | defer baselines (human item) |
| PostgreSQL | not needed (SQLite tests, offline Npgsql translation) | n/a | — | none |

**Missing dependencies with no fallback:** none.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Vitest 4.1.10 (jsdom, globals, `tests/setup.ts` seeds all Settings panels open and stubs `WebSocket`); Playwright 1.62.1 (projects `chromium`, `mobile-390`, `mobile-320`, strict mocked API); xunit 2.9.3 + SQLite for the API |
| Config file | `sources/ReactApp/vitest.config.ts`, `playwright.config.ts`, `tests/Api.Tests/Api.Tests.csproj` |
| Quick run command | `cd sources/ReactApp && npx vitest run tests/hosting/server-access-model.test.ts tests/search-picker.test.tsx tests/hosting/server-access-panel.test.tsx tests/hosting/server-cost-owner-panel.test.tsx tests/hosting/settings-section.test.tsx tests/app/url-parameters.test.ts` |
| Full suite command | `cd sources/ReactApp && npm run ci` (locales:check, locales:unused, contracts:check, format:check, lint --max-warnings 0, typecheck, test, build:assets); API: `dotnet test X39.UnitedTacticalForces.slnx`; e2e: `npx playwright test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-01, D-02 | order Mods, Configuration, Credentials, Automation, Access, Cost owner, Server record; Access absent without flag and no request; archived keeps Access, hides Cost owner | component | `npx vitest run tests/hosting/settings-section.test.tsx` | extend |
| D-01 | anchors in allowlist and as literals in panel files | unit | `npx vitest run tests/app/url-parameters.test.ts` | extend |
| D-03 | Cost owner read-only sentence, no picker, no options request without `canModify` | component | `npx vitest run tests/hosting/server-cost-owner-panel.test.tsx` | Wave 0 |
| D-04, D-05 | grant calls `sv:view` once, no staging, no leave guard | component | `npx vitest run tests/hosting/server-access-panel.test.tsx` | Wave 0 |
| D-06, D-07 | row anatomy, eight switches in fixed order, Full control makes seven disabled-on | component + pure | `... server-access-model.test.ts server-access-panel.test.tsx` | Wave 0 |
| D-08 | optimistic move, rollback of the single pair, alert under switch, scope-serialized parallel toggles | component | `... server-access-panel.test.tsx` | Wave 0 |
| D-09 | confirm for self Full control/View/Manage access and for Remove all; none for Operate | component | `... server-access-panel.test.tsx` | Wave 0 |
| D-10 | combobox roles, keys, pointer select keeps focus, token/clear, states, live region, all strings are props | component | `npx vitest run tests/search-picker.test.tsx` | Wave 0 |
| D-11, D-12 | dialog copy with names, reasons by priority, 409/400/403/generic mapping, `server.deployment === null` rule | component | `... server-cost-owner-panel.test.tsx` | Wave 0 |
| D-13 | link only with `canModify` and not archived; hash `#server-cost-owner` | component + route-link scan | `npx vitest run tests/hosting/cost-strip.test.tsx tests/app/route-links.test.ts` | extend |
| D-14 | new keys are sub-keys; exact invalidations (spy asserts `exact: true`) | unit | `npx vitest run tests/hosting/services.test.ts tests/hosting/server-access-panel.test.tsx` | extend |
| D-15 | panel unmounts + toast + focus Settings h2; 403/404 redirect with toast | component | `... server-access-panel.test.tsx` | Wave 0 |
| D-16 | filter from eight subjects, never hides warning subject, empty state | component | `... server-access-panel.test.tsx` | Wave 0 |
| D-17 | snapshot contains new fields/paths; `api.ts` in sync | contract | `npm run contracts:check`; CI `--export-openapi` + `cmp` | existing gate |
| D-18 | no dependency change | review | `git diff package.json package-lock.json` empty | manual |
| D-19 | axe, keyboard, reflow, spacing, German, headings for S-15/S-16/O-10/O-11; two key screens K-18/K-19 | e2e | `npx playwright test tests/e2e/audit tests/e2e/hosting-access.spec.ts --project=chromium` (then mobile projects) | Wave 0 + registry |
| D-19 | style guard and contrast pairs | unit | `npx vitest run tests/styles` | extend |
| D-19 | locale parity, glossary, unused, hard-coded text | unit | `npm run locales:check && npm run locales:unused && npx vitest run tests/locales` | extend |
| D-20 | profile section untouched | review | `git diff --stat sources/ReactApp/src/features/hosting/ConfigurationProfileAccessSection.tsx` empty | manual |
| API-01 | `CanManageAccess` equals `HasExactPermission` for admin claims, `sv`, `sv:manage-access`, `sv:modify` only, `sv:view` only, none; detail and list; frozen/assigned-at null when owner hidden | xunit | `dotnet test tests/Api.Tests --filter "FullyQualifiedName~GameServerProjection\|FullyQualifiedName~GameServerListAccess"` | extend |
| API-02, API-03 | 403 without ManageAccess, verified/not deleted/not banned, exclusion of granted subjects, ordering, `take` 0/51 -> 400, default 20, `memberCount`, fingerprint `detail`; Npgsql translation of the search offline | xunit | `dotnet test tests/Api.Tests --filter "FullyQualifiedName~GameServerAccess"` | extend |
| API-05 | `Modify` policy; administrator all owners, `co:use`, own owner first; current owner excluded; frozen flagged; unknown/archived 404; `kind` filled; creation endpoint only gains `kind` | xunit | `dotnet test tests/Api.Tests --filter "FullyQualifiedName~CostOwnerOptions"` | extend |
| API-04 | OpenAPI snapshot has `Detail`, `MemberCount`, `CanManageAccess`, `CostOwnerIsFrozen`, `CostOwnerAssignedAt` | contract | `npm run contracts:check` + CI step | existing gate |
| parity | one row per new `hostingService` method (`it.each(Object.keys(hostingService))`) and the two ADJ rows | unit | `npx vitest run tests/hosting/parity-matrix.test.ts` | extend + matrix rows |

### Sampling Rate
- **Per task commit:** the quick command above, scoped to the touched file; `dotnet test ... --filter` for API tasks.
- **Per wave merge:** `npm run lint && npm run typecheck && npx vitest run` plus the API filter suite.
- **Phase gate:** `npm run ci`, `dotnet test X39.UnitedTacticalForces.slnx`, Playwright `chromium` for the access/transfer specs and the three-project audit for S-15/S-16/O-10/O-11, before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `tests/hosting/server-access-model.test.ts`, `tests/search-picker.test.tsx`, `tests/hosting/server-access-panel.test.tsx`, `tests/hosting/server-cost-owner-panel.test.tsx` — the four new Vitest files of the UI-SPEC.
- [ ] `tests/e2e/hosting-access.spec.ts` plus registry entries S-15, S-16, O-10, O-11 and key screens K-18, K-19 (`keyScreenCount` 17 -> 19) in `tests/e2e/support`.
- [ ] Helper updates: both `allSettingsPanelIds` lists (`tests/helpers/settingsPanels.ts`, `tests/e2e/support/settingsPanels.ts`) gain `server-access` and `server-cost-owner` before `server-record`; `fleetServer` and e2e server builders gain the three fields; `tests/mocks/react-icons.tsx` gains any new glyph; e2e mocks gain `GET .../access` and `GET .../cost-owner/options` handlers.
- [ ] API: new test classes/cases listed above; update `NoOpGameServerAccessService` and `RecordingAccessService`.
- [ ] Parity matrix rows (`61-PARITY-MATRIX.md`): `SVC-serverAccess`, `SVC-serverUserCandidates`, `SVC-serverRoleCandidates`, `SVC-changeServerUserAccess`, `SVC-changeServerRoleAccess`, `SVC-serverCostOwnerOptions`, `SVC-changeServerCostOwner`, `ADJ-server-access-panel`, `ADJ-server-cost-owner-panel`, all `kept`, Phase `71`, "Today" cell naming `hostingService.<method>`, and a new Phase 71 offender block in `parity-matrix.test.ts` following the Phase 66-69 pattern (no row may stay `pending`).
- [ ] `tests/styles/tokens-contrast.test.ts` pairs for the listbox and token; `--utf-listbox-max-height` token; `access.css` is auto-scanned by `server-style-guard.test.ts` (it lists `src/components/server/*.css`), `search-picker.css` is NOT in that directory and must be added to the guard's file list.

## Security Domain

### Applicable ASVS Categories (level 1, `security_enforcement` enabled)

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (existing cookie auth) | unchanged |
| V3 Session Management | no | unchanged; note the principal is rebuilt from the database each request, so grants are effective on the grantee's next request |
| V4 Access Control | yes | `[Authorize(ManageAccess)]` / `[Authorize(Modify)]` policies on all three new routes plus service-level re-check (existing pattern); `canManageAccess` computed by `HasExactPermission`; candidates exclude subjects who already hold grants; tests assert 403 |
| V5 Input Validation | yes | `take` bounds 1..50 -> 400; permission allowlist (existing `ValidatePermission`); LIKE-escaped search; route `guid`/`long` constraints; React does not render API text raw (names in `bdi` as React text) |
| V6 Cryptography | no | none |
| V7 Error handling/logging | yes | audit records `AccessGranted`/`AccessRevoked`/`CostOwnerChanged` already written; no secrets in new DTOs; React maps by status, never prints the server message |
| V8 Data protection | yes | do not expose the Discord username (Critical Finding 2); cost-owner name, frozen flag and assigned-at only to viewers who may see the owner |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| IDOR on `gameServerId` for the new GETs | Information disclosure | policy + service `Authorize`; 404 for unknown server after the 403 check |
| Member enumeration through candidate search | Information disclosure | restricted to ManageAccess holders; verified, non-banned, non-deleted only; `take` cap; same precedent as profile access |
| Wildcard LIKE abuse (`%`, `_`) | Denial of service / tampering | escape helper and `ESCAPE '\'` |
| Privilege escalation: ManageAccess holder grants `sv` | Elevation of privilege | pre-existing; flagged (Critical Finding 12); the UI shows Full control as an explicit, separately worded switch and the self-removal dialog |
| Cost-owner disclosure to a Modify holder | Information disclosure | options list limited to owners the caller may use (own, `co:use`, administrator); current owner excluded; hidden-owner placeholder preserved |
| Race on transfer | Tampering | existing transaction with `FOR UPDATE`; 409/400 mapping; no-op returns 204 |
| Stale permission in the browser after a change | Elevation (UX) | the acting member's `me` and server queries are refetched after every access call |

## Sources

### Primary (HIGH confidence) - read in this session
- `sources/Api/Controllers/GameServersController.cs` (routes, policies, `TryMapException`)
- `sources/Api/Services/GameServerLifecycle/GameServerAccessService.cs`, `IGameServerAccessService.cs`
- `sources/Api/Authorization/Requirements/ServerManageAccessRequirement.cs`, `ServerIdRequirement.cs`
- `sources/Api/Services/GameServerLifecycle/GameServerLifecycleService.cs` (`ChangeCostOwnerAsync`, lines 828-891)
- `sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs` (`ProjectServerAsync`, `GetCreationCostOwnerOptionsAsync`, `ActiveDeployment`)
- `sources/Api/Services/GameServerLifecycle/HostingAuthorizationService.cs`, `FleetChangeInterceptor.cs`
- `sources/Api/Controllers/UsersController.cs` (Discord field gating), `ConfigurationProfilesController.cs` (candidate precedent), `CommandCenterController.cs` (ILike + escape)
- `sources/Api/DTO/*` access and cost-owner DTOs, `sources/Claims/Claims.cs`, `sources/HostingContracts/ECostOwnerKind.cs`
- `sources/Api/Program.cs` (`--export-openapi`, line 153), `.github/workflows/dotnet.yml`, `docs/features/react-ui-beta.md`, `sources/WebApp/generate-client.py`
- `tests/Api.Tests/**` (HostingTestStore, access/controller/creation-options tests, OpenApi route test)
- ReactApp: `SettingsSection.tsx`, `SettingsPanel.tsx`, `useSettingsPanelState.tsx`, `ServerSecretsPanel.tsx`, `ConfigurationProfileAccessSection.tsx`, `FinanceAccountPicker.tsx`, `FormFields.tsx`, `ActionControl.tsx`, `SearchField.tsx`, `Disclosure.tsx`, `primitives.tsx` (`ConfirmDialog`), `AppToasts.tsx`, `CostStrip.tsx`, `costConfirmationModel.ts`, `services.ts`, `types.ts`, `serverState.ts`, `urlParameters.ts`, `ServerWorkspacePage.tsx`, `useWorkspaceLiveUpdates.ts`, `App.tsx` (QueryClient), `auth.tsx`, `api/client.ts`, `api/contract.ts`, `financeModel.ts`
- ReactApp tests: `tests/setup.ts`, `helpers/*`, `hosting/credentials-panel.test.tsx`, `hosting/parity-matrix.test.ts`, `hosting/screen-registry.test.ts`, `app/url-parameters.test.ts`, `app/route-links.test.ts`, `ratchets/*`, `locales/*`, `styles/*`, `e2e/support/**`, `playwright.config.ts`, `eslint.config.js`, `tsconfig.app.json`, `scripts/check-locales.mjs`
- `.planning/phases/61-.../61-PARITY-MATRIX.md`, `docs/features/cloud-based-game-server-hosting/resource-scoped-access-control.md`

### Secondary (MEDIUM confidence)
- [CITED: tanstack.com/query/v5/docs/framework/react/guides/optimistic-updates] cache-based optimistic update steps and the concurrent-mutation caveat
- [CITED: w3.org/WAI/ARIA/apg/patterns/combobox/] combobox ARIA attributes and keyboard model

### Tertiary (LOW confidence)
- Assumptions A1 to A5 above (`[ASSUMED]`).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - nothing new; versions read from `package.json` and `node_modules`.
- Architecture: HIGH - every API and React integration point read in source; registry/ratchet/locale guards read in their tests.
- Pitfalls: HIGH for code-derived ones (1-8, 10); MEDIUM for 9 (HTTP 403 shape assumed).

**Research date:** 2026-11-26
**Valid until:** 2026-12-26 (stable; re-check `registryTypes.ts` ids and `keyScreenCount` if another phase lands first)
