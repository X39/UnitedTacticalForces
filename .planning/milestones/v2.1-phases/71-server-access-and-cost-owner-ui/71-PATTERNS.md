# Phase 71: Server Access and Cost Owner UI - Pattern Map

**Mapped:** 2026-11-26
**Files analyzed:** 52 new or modified files (API 14, React source 21, tests and test support 17)
**Analogs found:** 50 / 52 (2 partial, see "No Analog Found")

All analog paths were verified as git-tracked source. Paths are relative to the repository root. React paths are
under `sources/ReactApp/` and API paths under `sources/Api/` unless they start with `tests/` (API tests are
`tests/Api.Tests/GameServerHosting/`, React tests are `sources/ReactApp/tests/`).

Read first: `71-RESEARCH.md` "Critical Findings" corrects `71-UI-SPEC.md`. This map assumes those corrections
(S-15/S-16/O-10/O-11 ids, no Discord username in `detail`, `server.deployment === null` rule, `exact: true`
invalidations, scope-serial single-pair rollback, no `Server_Access_Summary_Held` key, hook-level self-lost handling,
panel-owned `SettingsPanel`).

## File Classification

### API (sources/Api)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DTO/GameServerUserAccessCandidateDto.cs` (new) | model (DTO) | request-response | `DTO/GameServerUserAccessGrantDto.cs` | exact |
| `DTO/GameServerRoleAccessCandidateDto.cs` (new) | model (DTO) | request-response | `DTO/GameServerRoleAccessGrantDto.cs` | exact |
| `DTO/GameServerUserAccessGrantDto.cs` (+`Detail`) | model (DTO) | request-response | itself | exact |
| `DTO/GameServerRoleAccessGrantDto.cs` (+`MemberCount`) | model (DTO) | request-response | itself | exact |
| `DTO/GameServerCostOwnerOptionDto.cs` (+`Kind`) | model (DTO) | request-response | itself | exact |
| `DTO/GameServerDto.cs` (+3 fields) | model (DTO) | request-response | `CanManageSecrets` / `CanModify` fields in same file | exact |
| `Helpers/LikePattern.cs` (new, internal static) | utility | transform | `Controllers/CommandCenterController.cs:500-507` | exact (copy) |
| `Controllers/GameServersController.cs` (3 new GETs) | controller | request-response | same file `GetAccessAsync` (453-469) + `ConfigurationProfilesController.cs:379-450` | exact |
| `Services/GameServerLifecycle/IGameServerAccessService.cs` (+2 methods) | service interface | request-response | itself | exact |
| `Services/GameServerLifecycle/GameServerAccessService.cs` (candidate queries) | service | CRUD (read) | same file `GetAsync` (36-96) | exact |
| `Services/GameServerLifecycle/IGameServerProjectionService.cs` + `GameServerProjectionService.cs` (flags, shared usable-owner query, options) | service | request-response | same file `GetCreationCostOwnerOptionsAsync` (282-318), `ProjectServerAsync` (~852-990) | exact |

### API tests (tests/Api.Tests/GameServerHosting)

| File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `GameServerAccessServiceTests.cs` (extend: candidates) | test | CRUD | itself (`CreateService`, `AccessPrincipal`, `CreateUser`) | exact |
| `GameServerAccessControllerAuthorizationTests.cs` (extend: 2 new GETs, cost-owner options Modify policy) | test | request-response | itself, `AccessEndpoints_RequireServerManageAccess...` (101-122) | exact |
| `NoOpGameServerAccessService.cs` + private `RecordingAccessService` (implement new members) | test double | n/a | itself | exact |
| `GameServerCreationCostOwnerOptionsTests.cs` (extend: `kind`, new options method) | test | request-response | itself | exact |
| `GameServerListAccessTests.cs` / projection tests (extend: `CanManageAccess`, frozen, assigned-at) | test | request-response | itself | exact |
| `CostOwnerViewAudienceTests.cs` pattern for offline Npgsql `ToQueryString` search translation | test | transform | `AudienceQueries_TranslateForNpgsql` (420-430), `CreateNpgsqlModelDbContext` (~501) | exact |

### React source (sources/ReactApp/src)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `features/hosting/ServerAccessPanel.tsx` (new) | component (gated Settings panel) | CRUD + optimistic | `features/hosting/ServerSecretsPanel.tsx` | role-match (gating, query, `SettingsPanel`) |
| `features/hosting/ServerCostOwnerPanel.tsx` (new) | component (Settings panel) | request-response | `features/hosting/ServerSecretsPanel.tsx` + `AutomationPanel.tsx` | role-match |
| `components/SearchPicker.tsx` + `search-picker.css` (new) | component (combobox primitive) | event-driven | `features/hosting/ConfigurationProfileAccessSection.tsx:128-215` (combobox on `TextField`) + `components/SearchField.tsx` | role-match |
| `components/server/AccessGrantForm.tsx` (new) | component | request-response | `ConfigurationProfileAccessSection.tsx:121-215` (segmented + picker) | role-match |
| `components/server/AccessSubjectRow.tsx` (new) | component | CRUD | `components/Disclosure.tsx` + `SwitchField` (`FormFields.tsx:458`) | role-match |
| `components/server/access.css` (new) | config (style) | n/a | `components/server/credentials.css` | exact |
| `features/hosting/serverAccessModel.ts` (new) | utility (pure model) | transform | `features/hosting/automationModel.ts` / `serverState.ts` | role-match |
| `features/hosting/useServerAccess.ts`, `useAccessMutations.ts`, `useCostOwnerTransfer.ts` (new) | hook | CRUD | `features/hosting/useAutomationSettings.ts` | exact |
| `app/useDebouncedValue.ts` (new) | hook | transform | none (see No Analog) | none |
| `features/hosting/services.ts` (7 methods, keys, `normalizeServer`) | service | request-response | same file: `profileAccess`, `profileUserCandidates`, `changeProfileUserAccess`, `creationCostOwners` (880-1090) | exact |
| `features/hosting/types.ts` (types, `GameServer` fields) | model | n/a | `canManageAutomation` handling (55-80) | exact |
| `features/hosting/sections/SettingsSection.tsx` (render 2 panels, banner rule, `headingFocus`) | component | composition | itself (288-300) | exact |
| `components/server/CostStrip.tsx` (Change cost owner link) | component | request-response | itself (`ActionLink`, 95-115) + `AutoStopNotice.tsx:48` hash link | exact |
| `app/urlParameters.ts` (2 anchors before `server-record`) | config | n/a | itself (67-73) | exact |
| `locales/en.json`, `locales/de.json` | config | n/a | `Server_Settings_Summary_Credentials` (line 663), `Server_Settings_AutomationTitle` (3722) | exact |

### React tests and support (sources/ReactApp/tests)

| File | Role | Closest Analog | Match Quality |
|---|---|---|---|
| `hosting/server-access-panel.test.tsx`, `hosting/server-cost-owner-panel.test.tsx` (new) | test | `hosting/credentials-panel.test.tsx` | exact |
| `search-picker.test.tsx` (new) | test | `search-field.test.tsx`, `form-fields.test.tsx` | role-match |
| `hosting/server-access-model.test.ts` (new) | test | `hosting/automation-model*.test.ts` style (pure) | role-match |
| `hosting/services.test.ts`, `hosting/contract-shape.test.ts` (extend) | test | `hosting/services.test.ts:1-50` | exact |
| `hosting/settings-section.test.tsx`, `hosting/cost-strip.test.tsx`, `app/url-parameters.test.ts:536-547` (extend) | test | themselves | exact |
| `helpers/settingsPanels.ts`, `e2e/support/settingsPanels.ts` (`allSettingsPanelIds`) | test support | themselves | exact |
| `helpers/fleetFixtures.ts` `fleetServer` (+3 fields) | test support | itself (139-170) | exact |
| `e2e/support/screens/registryTypes.ts`, `workspaceScreens.ts`, `keyScreens.ts` (S-15, S-16, O-10, O-11, K-18, K-19, `keyScreenCount` 19) | test support | S-14 entry (`registryTypes.ts:165-171`, `workspaceScreens.ts:1030-1055`) | exact |
| `e2e/hosting-access.spec.ts` (new) + mock handlers in `e2e/support/automationMocks.ts` / workspace handlers | test | `e2e/hosting-automation.spec.ts` + `e2e/support/automationMocks.ts` | exact |
| `ratchets/rules.json` (`tierAPaths` += SearchPicker), `styles/server-style-guard.test.ts` (+`search-picker.css`), `styles/tokens-contrast.test.ts` | test config | `rules.json:47-56` | exact |
| `hosting/parity-matrix.test.ts` + `.planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md` (9 rows, phase 71 block) | test + doc | Phase 69 block (`parity-matrix.test.ts:92-125, 295-320`) and matrix rows at lines 96, 221-224 | exact |

---

## Pattern Assignments

### API-1: `GameServersController.cs` (controller, request-response) - three new GETs

**Analog A (same file, route + policy + exception mapping):** `GetAccessAsync`, lines 453-469.

```csharp
[HttpGet("{gameServerId:guid}/access")]
[Authorize(Claims.ResourceBased.Server.ManageAccess)]
[ProducesResponseType<GameServerAccessDto>(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetAccessAsync(
    [FromRoute] Guid gameServerId,
    CancellationToken cancellationToken)
{
    try
    {
        return Ok(await accessService.GetAsync(gameServerId, User, cancellationToken).ConfigureAwait(false));
    }
    catch (Exception ex) when (TryMapException(ex, out var result))
    {
        return result;
    }
}
```

**Analog B (take bounds + candidate shape):** `Controllers/ConfigurationProfilesController.cs:379-450`.

```csharp
if (take is < 1 or > 50)
    return BadRequest(new ProblemDetails
    {
        Title = "The candidate limit is invalid.",
        Detail = "Take must be between 1 and 50.",
        Status = StatusCodes.Status400BadRequest,
    });
```

Apply: `GET {gameServerId:guid}/access/users/options` and `.../access/roles/options` with
`[FromQuery] string? search, [FromQuery] int take = 20` (ManageAccess policy, delegate to `IGameServerAccessService`,
wrap in the `TryMapException` try/catch, keep the 404 from `EnsureServerExistsAsync`). `GET
{gameServerId:guid}/cost-owner/options` uses `Claims.ResourceBased.Server.Modify` (policy precedent: `PUT
{gameServerId:guid}/cost-owner`, lines 323-343) and delegates to the projection service. Add XML docs to every new
member (AGENTS.md). Do not use `Forbid()` here; the existing service pattern throws `HostingForbiddenException`
which `TryMapException` maps.

### API-2: `GameServerAccessService.cs` candidate queries (service, CRUD read)

**Analog:** same file `GetAsync` lines 36-96 (exact-grant predicate) and `Authorize`/`ResourceValue` helpers
(lines 275-307).

```csharp
_ = Authorize(gameServerId, principal);
await EnsureServerExistsAsync(gameServerId, cancellationToken).ConfigureAwait(false);
var value = ResourceValue(gameServerId);
... .Where(q => !q.IsDeleted && q.Claims!.Any(claim =>
        PermissionAllowlist.Contains(claim.Identifier)
        && claim.Value == value
        && claim.ValueType == null
        && !claim.IsPrefix))
.OrderBy(q => q.Nickname).ThenBy(q => q.PrimaryKey)
```

Apply: invert the `Any(...)` for exclusion (`!q.Claims!.Any(...)`), add `!IsBanned && IsVerified`, project to the
candidate DTOs. `detail` is `"#" + last six characters of PrimaryKey.ToString("N")` (Research Critical Finding 2;
never the Discord username). `memberCount` counts non-deleted, non-banned role members (Open Question 3), document in
the DTO XML doc. Search: isolate as `internal static IQueryable<T> ApplySearch(...)` using the shared `LikePattern`
helper so an offline Npgsql `ToQueryString` test can cover it (SQLite cannot run `ILike`).

### API-3: `Helpers/LikePattern.cs` (utility, transform)

**Analog (copy, do not edit the controller):** `Controllers/CommandCenterController.cs:500-507` and call style
`EF.Functions.ILike(q.Nickname, searchPattern, "\\")` (lines 446-452).

```csharp
private static string EscapeLikePattern(string value)
    => value.Trim()
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal)
        .Replace("[", "\\[", StringComparison.Ordinal)
        .Replace("]", "\\]", StringComparison.Ordinal)
        .Replace("^", "\\^", StringComparison.Ordinal);
```

Make it `internal static` with XML docs, in its own file named after the type.

### API-4: `GameServerProjectionService.cs` (service, request-response)

**Analog:** `GetCreationCostOwnerOptionsAsync`, lines 282-318 (usable-owner rule). Extract the `Where` rule into a
shared private queryable used by the creation endpoint and the new `GetCostOwnerOptionsAsync`.

```csharp
var canUseEveryOwner = principal.HasAnyEmptyClaim(
    Claims.Administrative.All, Claims.Administrative.Server, Claims.Administrative.Hosting);
...
.Where(q => canUseEveryOwner || q.UserFk == userId || grantedOwnerIds.Contains(q.PrimaryKey))
.OrderByDescending(q => q.UserFk == userId)
.ThenBy(q => q.DisplayName)
.ThenBy(q => q.PrimaryKey)
.Select(q => new GameServerCostOwnerOptionDto { CostOwnerId = q.PrimaryKey, DisplayName = q.DisplayName,
    IsFrozen = q.IsFrozen, IsCurrentUser = q.UserFk == userId })
```

New endpoint additionally: `authorizationService.HasServerPermission(..., Modify)` else `HostingForbiddenException`
(precedent at `GetStorageDemandAsync`, ~line 320), load the server with `!IsArchived` else 404, exclude
`CurrentCostOwnerFk`, search + `take`, add `Kind = q.Kind`.

**`ProjectServerAsync` flags** (lines ~852-990): follow the existing `canSeeOwner` gating and `CanModify` /
`CanManageSecrets` property style (lines 933-954). `CanManageAccess = ServerManageAccessRequirement.HasExactPermission(
principal, server.PrimaryKey.ToString("D"))`; `CostOwnerIsFrozen` and `CostOwnerAssignedAt` only when `canSeeOwner`
(detail only for assigned-at). `GameServerDto` fields: add after `CanManageSecrets` (`DTO/GameServerDto.cs:56`) with
full XML docs (the neighbouring `HasChangesPendingStart` shows the `<summary>` + `<value>` shape).

### API-5: DTOs (model)

**Analog:** `DTO/GameServerUserAccessGrantDto.cs` - `public sealed record`, `/// <summary>` on type and every
property, `init` accessors, non-null defaults (`string.Empty`, `[]`). One type per file, file named after the type.

```csharp
namespace X39.UnitedTacticalForces.Api.DTO;

/// <summary>
/// Direct server-scoped permissions assigned to one user.
/// </summary>
public sealed record GameServerUserAccessGrantDto
{
    /// <summary>User identity.</summary>
    public Guid UserId { get; init; }
    ...
}
```

New: `GameServerUserAccessCandidateDto { UserId, Nickname, Detail }`, `GameServerRoleAccessCandidateDto { RoleId, Title,
Description, MemberCount }`, `Detail` (`string?`) / `MemberCount` (`int`) on the grant DTOs, `Kind`
(`ECostOwnerKind`, `sources/HostingContracts/ECostOwnerKind.cs`) on `GameServerCostOwnerOptionDto`. Doc text becomes
OpenAPI descriptions, so write final wording. Do not hand-edit generated Kiota output.

### API-6: `IGameServerAccessService.cs` interface members

**Analog:** same file. Pattern is `Task<T> XAsync(Guid gameServerId, ..., ClaimsPrincipal principal,
CancellationToken cancellationToken = default);`. Add `SearchUserCandidatesAsync` / `SearchRoleCandidatesAsync`
(`string? search`, `int take`). Add XML docs (the existing file has none; new members must have them per AGENTS.md).
**Same task must update both test implementers** (Pitfall 2): `tests/NoOpGameServerAccessService.cs` and the private
`RecordingAccessService` inside `GameServerAccessControllerAuthorizationTests.cs`.

### API tests

**Service test analog:** `GameServerAccessServiceTests.cs` - `await using var store = await HostingTestStore.CreateAsync();
var seed = await HostingTestData.SeedServerAsync(store.DbContext); ... var service = CreateService(store); var principal
= AccessPrincipal(seed);` then `Assert.Single`, `Assert.Equal`. Tests with `search: null` on SQLite; add one offline
translation test following `CostOwnerViewAudienceTests.AudienceQueries_TranslateForNpgsql`
(`using var dbContext = CreateNpgsqlModelDbContext(); ... .ToQueryString()`).

**Controller authorization analog** (`GameServerAccessControllerAuthorizationTests.cs:101-122`): extend the method-name
array with the two new GETs; they must have exactly one `AuthorizeAttribute` with `Claims.ResourceBased.Server.
ManageAccess`. Add the cost-owner options method to the `StorageEstimateRoutes_RequireExpectedAuthorization` style
theory expecting `Claims.ResourceBased.Server.Modify`.

**Cost owner options analog** (`GameServerCreationCostOwnerOptionsTests.cs:20-80`): copy `Principal(userId, new
Claim(Claims.ResourceBased.CostOwner.Use, id))`, `Owner(id, name, userId, frozen)`, and `CreateService(dbContext)`
(projection service ctor with `new EmptyGameServerDefinitionCatalog()`, `null!` placeholders). Cases: administrator
all owners, `co:use`, own owner first, current owner excluded, frozen flagged, unknown/archived 404, `kind` filled.
`take` 0 and 51 answer 400, default 20.

---

### React-1: `features/hosting/ServerAccessPanel.tsx` (component, CRUD + optimistic)

**Analog:** `features/hosting/ServerSecretsPanel.tsx` (gating, query, loading/error/body states, own `SettingsPanel`).

**Imports pattern** (lines 1-11, trim to what is used):
```tsx
import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useId, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { StatePanel, StatusBadge } from "@/components/primitives";
import { SettingsPanel } from "@/components/server/SettingsPanel";
import { useSettingsPanelState } from "@/components/server/useSettingsPanelState";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
```

**Gate-then-query pattern** (lines 415-426, 527-570): all hooks first, then `if (!server.canManageAccess) return null;`
(no heading, no summary, no request). The query uses `enabled: server.canManageAccess === true`.
```tsx
const statuses = useQuery({
  queryKey: hostingQueryKeys.secrets(server.primaryKey),
  queryFn: ({ signal }) => hostingService.secrets(server.primaryKey, signal),
  enabled: server.canManageSecrets === true,
});
...
if (!server.canManageSecrets) return null;
const loaded = !statuses.isLoading && !statuses.isError && statuses.data !== undefined;
let body;
if (statuses.isLoading) body = <StatePanel kind="loading">{t("Server_Credentials_Loading")}</StatePanel>;
else if (statuses.isError) body = (<StatePanel kind="error" action={<ActionButton variant="ghost"
  onClick={() => void statuses.refetch()}>{t("Server_Settings_Retry")}</ActionButton>}>...</StatePanel>);
return (
  <SettingsPanel id="server-credentials" title={t("Server_Settings_CredentialsTitle")}
    summary={loaded ? t("Server_Settings_Summary_Credentials", { count: secrets.length }) : undefined}>
    {body}
  </SettingsPanel>
);
```
Use the literal `"server-access"` in this file (anchor test requires it). Query options must be explicit:
`staleTime: 0, refetchOnWindowFocus: true` (global defaults are `staleTime 30_000`, no focus refetch, Finding 10);
`refetch()` from an effect on the panel's open flip via `useSettingsPanelState().isOpen("server-access")`. For list,
empty and error use `EmptyState` / `ErrorState` / `LoadingState` from `components/StateViews`, as
`ConfigurationProfileAccessSection.tsx:1-10, 154-162`. `ConfirmDialog` usage: `SettingsSection.tsx:316-335`
(`open`, `busy`, `title`, `description`, `confirmLabel`, `cancelLabel`, `onCancel`, `onConfirm`; add `error`,
`confirmVariant="danger"`, `dismissVariant="primary"`, `initialFocus="dismiss"` per spec).

### React-2: `useAccessMutations.ts` / `useServerAccess.ts` / `useCostOwnerTransfer.ts` (hook, CRUD)

**Analog:** `features/hosting/useAutomationSettings.ts` (typed failure classifier + `useMutation` + exact
invalidation).

```ts
export function useSaveAutomationSettings(serverId: string): UseMutationResult<...> {
  const queryClient = useQueryClient();
  return useMutation<ServerAutomation, Error, ServerAutomationUpdate>({
    mutationFn: (update) => hostingService.saveAutomationSettings(serverId, update),
    onSuccess: (automation) => {
      const queryKey = hostingQueryKeys.server(serverId);
      queryClient.setQueryData<GameServer | undefined>(queryKey, (current) => current ? { ...current, automation } : current);
      void queryClient.invalidateQueries({ queryKey, exact: true });
    },
  });
}
```
Apply: every `fleet` and `server(id)` invalidation passes `exact: true` (Finding 4); `costOwnerOptions` is invalidated
by prefix on purpose. For toggles add `scope: { id: \`server-access-${serverId}\` }`, roll back only the single
(subject, permission) pair with the inverse of `applyOptimistic` (never restore a whole snapshot, Finding 5),
invalidate in `onSettled` only when `queryClient.isMutating({ mutationKey }) <= 1`. Lost-access detection is
hook-level in `onSettled` (Research Pattern 4 snippet), because the panel unmounts and `mutate()` callbacks then do
not run. Status mapping: classify `ApiError.status` (400/403/404/409) like `automationSaveFailure`.

### React-3: `components/SearchPicker.tsx` (component, event-driven)

**Analog:** the combobox in `ConfigurationProfileAccessSection.tsx:128-215` (built on `TextField`, no raw input).
```tsx
<TextField
  role="combobox"
  label={...}
  aria-expanded={search.trim().length > 0}
  aria-controls={`${profileId}-principal-options`}
  aria-activedescendant={activeCandidateIndex >= 0 ? `${profileId}-principal-option-${activeCandidateIndex}` : undefined}
  autoComplete="off"
  value={search}
  onChange={(event) => { setSearch(event.target.value); setActiveCandidateIndex(-1); }}
  onKeyDown={(event) => { /* ArrowDown / ArrowUp / Enter / Escape */ }}
/>
```
Keep from the analog: `TextField` as the only input (it forwards `role`, `aria-*`, `onKeyDown`, `ref`; see
`FormFields.tsx:157-222`), key handling skeleton. Replace (do not copy): the options as `ActionButton role="option"`
inside a `div role="listbox"`; use `ul[role=listbox]` / `li[role=option]` with stable ids, `aria-selected`,
`aria-disabled`, select on `onPointerDown` with `preventDefault`, wrap on ArrowDown/Up, Home/End, Tab closes, second
Escape clears (mirror `SearchField.tsx` `handleKeyDown`: `if (event.key === "Escape" && value !== "") { event.preventDefault(); clear(); }`).
Chosen token + icon-only clear is `ActionButton iconOnly` with `Dismiss16Regular`. Every string is a required prop (no
literal in the file); add the file to `tierAPaths` in `tests/ratchets/rules.json:47-56`. Do not add it to
`primitiveFiles`. The label must stay outside the input/token conditional (token has no input to `htmlFor`).
Debounce lives in `app/useDebouncedValue.ts` (see No Analog). Use `placeholderData: keepPreviousData` (precedent
`EventAdminPage.tsx:89`).

### React-4: `components/server/AccessGrantForm.tsx` and `AccessSubjectRow.tsx`

**Grant form analog:** `ConfigurationProfileAccessSection.tsx:121-141` (segmented `RadioGroupField`).
```tsx
<RadioGroupField legend={...} name="profile-access-subject" layout="segmented" value={subject}
  options={[{ value: "user", label: ... }, { value: "role", label: ... }]}
  onChange={(kind) => { setSubject(kind === "role" ? "role" : "user"); setSearch(""); ... }} />
```
Add `hideLegend` per spec. Submit `ActionButton type="submit"` with `unavailableReason`. `ConfirmDialog` stops submit
bubbling so a dialog opened inside the form never submits it.

**Row analog:** `Disclosure` (`components/Disclosure.tsx:6-50`): `variant="inline" headingLevel={3}`, `title`,
`summary`, `badge`, controlled `open`/`onOpenChange` (open state local, keyed by subject id). Switches:
`SwitchField` (`FormFields.tsx:458-512`) requires `label`, `hint`, `onLabel`, `offLabel`; it is a native
`input role="switch"`, no authored `aria-checked`. Staging: none; `disabled` for Full-control-included rows. Names are
`<bdi>` React text. Presentational: receive data and callbacks from the panel.

### React-5: `features/hosting/ServerCostOwnerPanel.tsx` (component, request-response)

**Analog:** `ServerSecretsPanel.tsx` (same panel shape, but renders unless `server.isArchived`), and
`SettingsSection.tsx:256-262` for the availability rule precedent.
```tsx
const canDelete = server.canModify && !server.isArchived && !server.deployment
  && server.pendingJobCount === 0 && jobs.every((job) => !job.canRetry && !job.canSkip);
```
Apply: transfer available iff `server.canModify && server.deployment === null && server.pendingJobCount === 0`
(Finding 3: not `isDeploymentActive`); reasons by priority via `ActionButton unavailableReason`. Facts row reuses
`serverCostFacts(server)` and the owner-text resolution in `components/server/CostStrip.tsx:41-44`:
```tsx
if (owner.kind === "name") ownerText = <bdi>{owner.name ?? ""}</bdi>;
else if (owner.kind === "hidden") ownerText = t("Server_Confirm_OwnerHidden");
else ownerText = t("Server_Confirm_OwnerUnknown");
```
Options query: `enabled` only for `server.canModify` after first open (derive "opened once" with the
set-state-during-render pattern, not a ref write; `SettingsSection.tsx` `if (reviewOpen && count === 0)
setReviewOpen(false)` is the in-repo precedent). Use literal `"server-cost-owner"` in this file. Success: exact
invalidations (Research "TypeScript: exact invalidations after a transfer"), `useAppToast().showToast(..., "success")`
(`SettingsSection.tsx` uses `showToast(t(...), "success")`), focus the owner value (`tabIndex={-1}`).

### React-6: `features/hosting/services.ts` (service, request-response)

**Analog:** same file, `profileAccess` / `profileUserCandidates` / `changeProfileUserAccess` / `creationCostOwners`
(lines 880-1090).

```ts
async profileUserCandidates(profileId: string, search: string, signal?: AbortSignal) {
  const route = "/configuration-profiles/{profileId}/access/users/options" as const;
  const path = fillApiPath(route, { profileId });
  const query = new URLSearchParams({ search, take: "20" });
  return (await getApiClient().request<ConfigurationProfileUserCandidate[]>(
    `${path}?${query.toString()}`, { method: apiMethod(route, "get"), signal })) ?? [];
},
async changeProfileUserAccess(profileId, userId, permission, grant, signal) {
  const path = grant ? ("/configuration-profiles/{profileId}/access/users/grant" as const)
                     : ("/configuration-profiles/{profileId}/access/users/revoke" as const);
  await getApiClient().request<void>(fillApiPath(path, { profileId }),
    { method: apiMethod(path, "post"), body: { userId, permission }, signal });
},
```
Apply the seven methods with `fillApiPath(..., { gameServerId: serverId })`. `serverAccess` normalizes
`permissions ?? []` as `profileAccess` does (959-969). `changeServerCostOwner` is `PUT` (`apiMethod(path, "put")`),
body `{ costOwnerId }`. Query keys go in `hostingQueryKeys` (line 214-262), under `server(id)`:
`access: (id) => ["hosting","servers",id,"access"]`, `accessCandidates: (id, kind, search) => [..., "access",
"candidates", kind, search]`, `costOwnerOptions: (id, search) => [..., "cost-owner", "options", search]` (JSDoc each,
like `costAlertSettings`). `normalizeServer` (99-137): add `canManageAccess: server.canManageAccess ?? false,
costOwnerIsFrozen: server.costOwnerIsFrozen ?? null, costOwnerAssignedAt: server.costOwnerAssignedAt ?? null`
after `canManageAutomation` (line 131).

### React-7: `features/hosting/types.ts`

**Analog:** the `canManageAutomation` treatment, lines 55-80: `Omit<Required<Schema<"GameServerDto">>, ... > & { ...?: ... | null }`
with a doc comment per optional field. New types: `export type ServerAccess = ...`, `ServerAccessUserGrant`,
`ServerAccessRoleGrant`, `ServerAccessUserCandidate`, `ServerAccessRoleCandidate`, `ServerCostOwnerOption` as
`Required<Schema<"...">>` (pattern at lines 1-10); `GameServer` gains the three optional-or-null fields so older
fixtures and payloads still compile.

### React-8: `SettingsSection.tsx` modification

**Analog:** itself lines 288-300 (panel order) and 283-286 (banner). Render, in order: `ServerSecretsPanel`,
`AutomationPanel`, `<ServerAccessPanel server onAccessLost={() => setHeadingFocus((c) => c + 1)} />`,
`<ServerCostOwnerPanel server />`, then the `showRecord` panel. `headingFocus` effect already focuses the h2
(`if (headingFocus > 0) headingRef.current?.focus();`, line 187). Banner (Pitfall 6): change
`!server.canModify && <StatePanel>` to also require `!server.canManageAccess` (Open Question 4) and add a test.

### React-9: `components/server/CostStrip.tsx` link

**Analog:** same file lines 99-110 (`ActionLink variant="ghost" size="compact"` after a `dd` value) and
`components/server/AutoStopNotice.tsx:48` for the hash target:
```tsx
to={{ search: "?tab=settings", hash: "#server-automation" }}
```
New link: `to={{ pathname: ..., search: "?tab=settings", hash: "#server-cost-owner" }}` (match the `AutoStopNotice` shape
so `tests/app/route-links.test.ts` scan passes), shown iff `server.canModify && !server.isArchived`, text
`t("Server_Create_BilledTo_Change")`.

### React-10: `access.css` / `search-picker.css`

**Analog:** `components/server/credentials.css` - header block comment stating the token-only rule, grid layouts with
`var(--utf-space-*)`, 520px/800px breakpoints only, a 1px border the only literal. New token
`--utf-listbox-max-height: 16rem` is declared with the other control tokens (find where `--utf-review-name-min` is
declared and add beside it). `search-picker.css` is not under `src/components/server`, so add it to the file list in
`tests/styles/server-style-guard.test.ts` (lines 17-26 list `shared-primitives.css`, `action-control.css`).
`access.css` is auto-scanned (directory scan, line 30).

### React-11: locales

**Analog:** `src/locales/en.json:663` / `de.json:663` (`"Server_Settings_Summary_Credentials": "Credentials: {{count}}"`
and its German `"Zugangsdaten: {{count}}"`), plurals as `_one`/`_other`. Add every key of the UI-SPEC Copywriting
Contract **except** `Server_Access_Summary_Held` (identical EN/DE fails `locales:check`; render the `Intl.ListFormat`
string directly). Every `Server_*`/`Hosting_*` key must be referenced in source (`locales:unused` fails otherwise;
`Picker_*` only warns). Run `npm run locales:check` early on the German text (English function words, shared-words
rule).

### React-12: `urlParameters.ts` anchors

**Analog:** itself lines 67-73. Insert `"server-access"`, `"server-cost-owner"` before `"server-record"`. Also update
the test map `tests/app/url-parameters.test.ts:537-543` (`"server-access": "src/features/hosting/ServerAccessPanel.tsx"`,
`"server-cost-owner": "src/features/hosting/ServerCostOwnerPanel.tsx"`); each anchor must appear as a string literal in
its panel file.

---

### React tests

**Panel tests analog:** `tests/hosting/credentials-panel.test.tsx`: `renderWithProviders` (`../helpers/renderWithProviders`),
server fixture builder over `fleetServer` cast `as unknown as GameServer`, and a `mockHostingQueries` that
`vi.spyOn(hostingService, "...").mockResolvedValue(...)`.
```tsx
function credentialServer(options: FleetServerOptions = {}, overrides = {}): GameServer {
  return { ...fleetServer({ title: "Alpha Team", gameIdentifier: "counter-strike-2", ...options }),
    canModify: true, canManageSecrets: true, canRevealSecrets: false, ...overrides } as unknown as GameServer;
}
vi.spyOn(hostingService, "secrets").mockResolvedValue(secrets);
```
Render `SettingsSection` with the new fields. Assert the explicit query options in the first test (test QueryClients
use library defaults, Finding 10). Assert switches with `toBeChecked()` not authored `aria-checked` (Finding 11).
Assert invalidation spies pass `exact: true`.

**Service tests analog:** `tests/hosting/services.test.ts:1-50` (`vi.stubGlobal("fetch", fetchMock)`,
`initializeApiClient(configuration)`, assert `url.pathname`, `init.method`, body). Add `it.each(Object.keys(hostingService))`
parity row coverage in `parity-matrix.test.ts`.

**Fixture/helper updates (pitfall 1):**
- `tests/helpers/settingsPanels.ts` and `tests/e2e/support/settingsPanels.ts`: insert `"server-access"`,
  `"server-cost-owner"` before `"server-record"` in `allSettingsPanelIds`.
- `tests/helpers/fleetFixtures.ts:139-170` `fleetServer`: add `canManageAccess` (default false), `costOwnerIsFrozen`
  (null), `costOwnerAssignedAt` (null) options. Also `tests/mocks/react-icons.tsx` must export every new glyph name.

**E2E registry analog:** S-14 entries: `registryTypes.ts:165-171` (`{ id, kind: "screen", routePattern: patterns.workspace,
tab: "settings", slice: "workspace" }`) and `workspaceScreens.ts:1030-1055` (`setup` with `allowLongWalk()` and
`installWorkspace(page, culture, {...})`, `states: [{ name, open, ready, baseline }]`). **Use ids S-15, S-16, O-10, O-11,
K-18, K-19**; `keyScreenCount` 17 -> 19 in `tests/e2e/support/keyScreens.ts`. Turn the keyboard walk off for
many-state entries and cover keyboard in `hosting-access.spec.ts`.

**E2E spec analog:** `tests/e2e/hosting-automation.spec.ts` (copy blocks: `copy = { en: {...}, de: {...} }`, route mock
handle, `mockFleetStream`) and `tests/e2e/support/automationMocks.ts` (`automationServer()` builder, constants, route
mocking). Add handlers for `GET {base}/access` and `GET {base}/cost-owner/options` to the shared workspace handlers and
`mockAutomationRoutes`, otherwise the strict mock records `session.unexpected` (Finding 9). Add `canManageAccess` etc.
to the e2e server builders.

**Parity matrix:** add nine rows to `.planning/phases/61-foundations-localization-member-access/61-PARITY-MATRIX.md`
following lines 96 and 221-224 (columns: id, capability, today, status, new location, phase, evidence sentence "Phase
71: ... tests/..."). Add a `phase71CapabilityIds`/offenders block in `tests/hosting/parity-matrix.test.ts` mirroring
92-125 and 295-320.

---

## Shared Patterns

### Authorization on the API
**Source:** `Controllers/GameServersController.cs:453-469` + `Services/GameServerLifecycle/GameServerAccessService.cs:275-307`
**Apply to:** the three new endpoints and both new service methods. Policy attribute on the action, then re-check in the
service (`Authorize(...)` throws `HostingForbiddenException`), then `EnsureServerExistsAsync` for 404.
```csharp
if (!principal.TryGetUserId(out var actorId)
    || !ServerManageAccessRequirement.HasExactPermission(principal, ResourceValue(gameServerId)))
    throw new HostingForbiddenException("Server ManageAccess permission is required.");
```

### Claim-gated panel (React)
**Source:** `ServerSecretsPanel.tsx:527-570` (`if (!server.canManageSecrets) return null;` after hooks, query `enabled` by the claim flag).
**Apply to:** `ServerAccessPanel` (flag `canManageAccess`); `ServerCostOwnerPanel` gates on `!server.isArchived` and `canModify` for the picker.

### Exact invalidation (never prefix for `fleet` or `server(id)`)
**Source:** `useAutomationSettings.ts` (`invalidateQueries({ queryKey, exact: true })`), `ServerWorkspacePage.refresh()`.
**Apply to:** all access and transfer hooks. Counter-example to avoid: `CostAlertSettingsPanel.tsx:171` and
`CostConfirmationProvider.tsx:270` call it without `exact`.

### Status-mapped errors
**Source:** `useAutomationSettings.ts` `automationSaveFailure` (typed error to a union); Phase 71 maps `ApiError.status`
400/403/404/409 to the UI-SPEC copy keys. React never prints the server message.

### Panel anchors and hash links
**Source:** `SettingsPanel.tsx` (hash opens panel), `urlParameters.ts:67-73`, `AutoStopNotice.tsx:48`.

### Toast and polite announcements
**Source:** `useAppToast().showToast(text, "success")` (`SettingsSection.tsx`), the `sr-only` `aria-live="polite"
aria-atomic="true"` region (`SettingsSection.tsx` ~line 288).

### Drawn controls only
**Source:** ESLint `no-restricted-syntax` and `tests/ratchets/rules.json`. Use `TextField`, `SwitchField`, `ActionButton`,
`Disclosure`, `ConfirmDialog`, `SearchField`. No raw `button`, `input`, `select`, `details`, `dialog`. Tier A files
(`src/features/hosting`, `src/components/server`, `SearchPicker`) fail the literal scan on hard-coded text.

### API contract regeneration
**Source:** Research "Contract regeneration" (CI lines `.github/workflows/dotnet.yml:73-76`): build, `dotnet run --project
sources/Api/Api.csproj --no-build -- --export-openapi <abs path>/sources/ReactApp/openapi/utf-api.json`, `npm run
generate`, `npm run contracts:check`, then `dotnet build sources/WebApp/WebApp.csproj` as the compatibility proof.
Do not regenerate `sources/WebApp/UtfApi` unless the user asks (Open Question 1).

---

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `app/useDebouncedValue.ts` | hook | transform | No debounce hook exists in `src/app`. Use the lint-safe form from Research Pattern 3: `useEffect(() => { const id = setTimeout(() => setValue(input), 250); return () => clearTimeout(id); }, [input]);` (setState inside the timer callback, `react-hooks/set-state-in-effect` compliant). |
| `components/SearchPicker.tsx` (listbox part) | component | event-driven | The only in-repo combobox (`ConfigurationProfileAccessSection`) uses `ActionButton role="option"` in a `div role="listbox"`, which the spec rejects; keyboard model follows the WAI-ARIA APG combobox (Research Pattern 2). The input, keys skeleton and `SearchField` Escape behavior are reusable. |

## Metadata

**Analog search scope:** `sources/Api/{Controllers,Services/GameServerLifecycle,DTO,Helpers}`,
`tests/Api.Tests/GameServerHosting`, `sources/ReactApp/src/{features/hosting,components,components/server,app,locales}`,
`sources/ReactApp/tests/{hosting,helpers,e2e,ratchets,styles,app}`, `.planning/phases/61-*`.
**Files scanned:** about 45 read in whole or in relevant ranges (plus tracked-file listings of `features/hosting`,
`components`, `tests/Api.Tests`).
**Tracked-source check:** `git ls-files` run on the React test, support, ratchet and CSS analogs (10 of 10) and the API
analogs; no mirror paths are named.
**Pattern extraction date:** 2026-11-26
