# Pitfalls Research

**Domain:** Redesigning a cost-bearing cloud game-server operations UI (React, Fluent UI v9 plus project primitives) on top of an existing, certified feature set, plus a bounded app-wide consistency pass
**Milestone:** v2.1 Server Experience Reimagined
**Researched:** 2026-11-18
**Confidence:** HIGH for repo-specific findings (read directly from `sources/ReactApp`, `sources/Api`, `.planning`); MEDIUM for general UX and accessibility practice (no external sources were consulted; it rests on repo evidence and established practice)

Evidence paths are relative to `sources/ReactApp` unless stated otherwise.

## Phase vocabulary used below

The roadmap does not exist yet. These labels are a suggestion; map them onto the real phases.

| Label | Meaning |
|-------|---------|
| **P1 Parity and guardrails** | Capability inventory, decisions (RESX, routes, Fluent vs custom), fixtures, test strategy |
| **P2 State and cost model, shared primitives** | Single server-state view-model, cost-confirm component, section navigation primitive |
| **P3 Fleet and start/create flow** | Fleet cards, create flow, member-first start |
| **P4 Workspace IA and change flows** | Tabs/sections, config, mods, files, secrets |
| **P5 Cost, teardown, jobs, activity** | Cost-stop clarity, job/audit presentation |
| **P6 Consistency pass** | Bounded, token-level and shared-component changes for the rest of the app |
| **P7 Verification** | Mobile, German, accessibility, state gallery, e2e |

## Critical Pitfalls

### Pitfall 1: Making "Start" a one-click cost event because the API does not guard it

**What goes wrong:**
A member-first redesign adds a quick "Start" button on fleet cards or a hero CTA, and starting a Hetzner worker (about 30-45 minutes of provisioning, hourly billing) becomes a single click.

**Why it happens:**
The only confirmation Start has today is client-side.
- `Arma3GameServerDefinitionRegistration` defines Start with `isDangerous=false` and `RequiresConfirmation=false`.
- ASKA and Counter-Strike 2 do the same through their `Action(...)` helper, where `confirmation` defaults to false.
- `GameServerProjectionService.ProjectAction` therefore emits no `confirmationChallenge` for Start.
- Today the workspace always opens `ActionConfirmation` (`ConfirmDialog`) before calling `executeAction`. That dialog is the real safeguard, and it lives in the UI layer being rewritten.

Several related repo facts make it worse:
- `ConfirmDialog` (`src/components/primitives.tsx`) hard-codes `variant="danger"` on the confirm button, so Start would be a red button.
- The confirm button is not disabled while `busy`, only relabelled, and `executeAction` sends no idempotency key.
- The `?action=` deep link (`ServerWorkspacePage`) only opens a dialog for actions that are enabled, not pending, and have a `confirmationChallenge`. A "Start" deep link silently does nothing.
- The unit fixture in `tests/hosting/workspace-page.test.tsx` models Start as `dangerous: true` with `confirmationChallenge: "START"`. Real Start has neither, so tests pass against a more protective contract than production has.

**How to avoid:**
- Treat "confirm any action whose `costImpact` is `StartsCost` or `KeepsCostRunning`" as a UI invariant owned by one shared component, for example a `CostActionDialog` composed around `ConfirmDialog`. Do not change `ConfirmDialog` itself; 17 files use it.
- The dialog must show:
  - the hourly and monthly estimate (`estimatedHourlyCostEur`, `estimatedMonthlyCostEur`)
  - the cost owner name
  - the expected duration (`expectedDurationSeconds`)
  - how to stop the cost
- Use a non-danger variant for Start and a danger variant for Stop and Terminate.
- Disable the confirm control while the mutation is pending.
- Make `?action=<id>` resolve for any enabled action, or show an explicit "this action is not available because ..." message. Never fail silently.
- Build fixtures from real projection semantics, not hand-written variants (see Pitfall 7).

**Warning signs:**
- A PR adds an `onClick={() => execute(start)}` that bypasses `ActionConfirmation`.
- A Start button appears on `ServerCard`.
- A test passes `confirmationChallenge: "START"` for Start.
- The confirm dialog for Start is red.

**Phase to address:** P2 (component and invariant), P3 (fleet and create flow), P7 (test that no Start path skips the dialog).

---

### Pitfall 2: Simplified status language hides "still billing"

**What goes wrong:**
The redesign collapses 12 deployment statuses into friendly states such as "Online", "Starting", "Offline". A server whose status is `Stopped`, `Failed`, `PreservationFailed`, or `TerminatedDueToFault` but whose `deployment.possibleCost` is true is shown as "Offline", and a member believes cost has stopped.

**Why it happens:**
- Cost state and lifecycle state are different axes. Today `serverTone()` in `components.tsx` keys off `deployment.possibleCost`, not `status`, so a status-only redesign regresses that.
- "No deployment" and "deployment present but not yet resolved" are different. Only a null deployment with no pending job is safely not billing.
- The project's core value is "verified non-billable teardown". Billing stops only after verified release, not when the user clicks Stop.

**How to avoid:**
- Introduce one pure view-model, for example `serverState(server) -> { phase, billing, nextAction, why }`. Billing has four values: `none`, `billing`, `stopping-still-billing`, `unresolved-possible-cost`.
- Every fleet card, workspace header, and notification reads from it. No component derives state ad hoc.
- Property-test the model across all 12 `EDeploymentStatus` values × `possibleCost` × `pendingJobCount` × `isLaunchDisabled` × paused control plane.
- Never render "Offline" or "Stopped" when `possibleCost` is true. Show "Stopped, cleanup pending - cost may continue".
- Keep the existing `Hosting_Detail_PossibleCost` copy semantics: "not fully stopped until cleanup is resolved".

**Warning signs:**
- A component switches on `deployment.status` to pick a label or colour without reading `possibleCost`.
- The "attention" or "possible cost" metric on the overview disagrees with the card badge.

**Phase to address:** P2 (model and tests), P3 and P5 (consumers).

---

### Pitfall 3: Four names for the cost-stop action, and Stop vs Terminate left ambiguous

**What goes wrong:**
Members cannot tell which button ends the bill, or which one destroys unpublished data.

**Why it happens:**
- The same capability already has several names:
  - API label "Terminate", which becomes the dialog title and confirm-button text.
  - Fleet card "Deprovision now" (EN) / "Jetzt deprovisionieren" (DE), the key `Hosting_Servers_TerminateNow`.
  - Docs and planning call it "provider teardown" or "cost-stop".
  - Arma 3 "Stop" saves, preserves selected data, and releases cloud resources. It is also a cost-stop, but a graceful one.
- Terminate's API description says "Immediately release runtime resources without publishing live data". The dialog requires typing the server title.
- `tests/e2e/public-shell.spec.ts` asserts both strings today ("Jetzt deprovisionieren" on the card and a dialog button named "Terminate"). The inconsistency is baked into evidence.

**How to avoid:**
- Choose one user-facing vocabulary in RESX, for example "Stop server (saves data)" and "Emergency: release now (loses unsaved live data)", and map by action id (`stop`, `terminate`) to RESX keys. Fall back to API text only for unknown ids.
- Show the contrast side by side wherever both appear.
- Keep the typed challenge for the destructive path. Do not weaken it to a checkbox for visual simplicity.
- Update the e2e assertions in the same PR that renames the labels.

**Warning signs:**
- A new string for the same action appears in a second place.
- The same button reads differently on the fleet card and in the dialog.

**Phase to address:** P1 (vocabulary decision), P5 (implementation), P7 (German review).

---

### Pitfall 4: Optimistic or "friendly" state that disagrees with durable jobs

**What goes wrong:**
After a click the UI flips to "Starting..." or "Running" optimistically, but the lifecycle job later fails, is skipped, is paused by the operator, or the lease is lost. The UI claims something the control plane never did. The reverse also happens: the dialog closes on HTTP 200 and the user sees nothing for 5-10 seconds, clicks again, or leaves.

**Why it happens:**
- The truth is `LifecycleJob` plus `GameServerDeployment`, and execution is asynchronous. `executeAction` returns a queued job, not an outcome.
- Polling is 5 s for server, jobs, and operations in the workspace and 10 s for fleet and notifications (`ServerWorkspacePage.tsx`, `FleetPages.tsx`). Cross-view staleness of up to 10 s is already possible between fleet and workspace.
- Start expectations are long: `expectedDurationSeconds` of 30-45 minutes for Arma 3, ASKA, and Counter-Strike 2.
- Lifecycle execution can be paused by the operator. Actions are then disabled and queued work is preserved.

**How to avoid:**
- Do not optimistically mutate deployment status or billing state. After `executeAction` resolves, write the returned job into the jobs cache with `setQueryData` and show "Requested - queued" from the job. Let status change only when the server projection changes.
- Show job `stage`, `progressPercent`, `attemptCount`, and a "last updated N s ago" stamp. Poll faster (2-3 s) only while a job is non-terminal for this server, and back off afterwards.
- Keep failed jobs and their retry/skip affordance prominent until resolved. A `Failed` job is a first-class state, not a toast.
- Make "paused by operator" a view-model state with its own banner, not an English string match. Today `ServerWorkspacePage.tsx` and `components.tsx` compare `unavailableReason` to the literal `"Hosting lifecycle execution is paused by the operator."`. Centralise this in one helper with a test. If it needs a stable code, raise it as an explicit API decision per the milestone constraint.
- Handle 409/conflict outcomes ("Another lifecycle operation is pending") by refetching and showing the current job, not a raw error.
- Keep `AbortSignal` use in services. Do not add fire-and-forget background mutations from components that unmount.

**Warning signs:**
- `onMutate` with `setQueryData` on a server's `deployment.status`.
- A spinner with no job reference.
- A banner or gate that depends on English API text.
- Two views showing different states for the same server after an action.

**Phase to address:** P2 (view-model and polling policy), P3 and P5 (consumers), P7 (state gallery and fake-state tests).

---

### Pitfall 5: Losing existing capabilities during the rewrite

**What goes wrong:**
A cleaner layout silently drops something that was certified: for example the Arma headless-client placement field, generated-file override on upload, preservation selections, secret rotate/reveal, job retry/skip, protocol download, worker disk telemetry, provider resource list, or the `?tab=` and `?action=` deep links. Nothing fails, because tests for the removed code are deleted along with it.

**Why it happens:**
- Capabilities are spread over nine tabs and about 14 000 lines of source and tests (`ServerFilesPanel.tsx` 1372 lines, `CreateServerPage.tsx` 1115, `ServerWorkspacePage.tsx` 797, `services.ts` 758).
- The route manifest certifies the route, not each capability inside it.
- Redesigns are tempted to "start from the member's view" and treat admin capabilities as optional.

Today's workspace capability surface:
- **Overview:** ranked next action, facts (cost owner, launch policy, runtime profile, estimate, config profile, mod pack, terrain, scenario), actions grid, recent activity.
- **Runtime:** change runtime profile, change storage strategy and capacity, mod pack change, terrain/scenario selection.
- **Cloud:** deployment facts, disk telemetry, provider resources, SSH diagnostics, possible-cost and fault panels.
- **Logs:** protocol list, download, collect-logs action.
- **Files:** persistent and live contexts, upload (including folders and collisions), create directory, delete, preservation selections.
- **Configuration:** typed entries, profile selection, Arma placement.
- **Credentials:** set, rotate, reveal. Gated by `canManageSecrets`.
- **Jobs:** retry and skip.
- **Audit:** full operations feed.
- **Page header:** refresh and archive/delete.

Two things also matter:
- Several service methods have UI today only inside these panels, so deleting a panel orphans the method.
- Cost-owner change and per-server access grant endpoints exist in `openapi/utf-api.json` but have no React service method. That is an existing gap, not something the redesign may claim as parity.

**How to avoid:**
- P1 deliverable: a parity matrix with columns `capability | current location | service method | existing test | v2.1 location | retained/merged/retired + reason`. Retiring something requires an explicit line.
- Rewrite presentation, not logic. Keep `services.ts`, `model.ts`, `workspaceModel.ts`, `authorization.ts`, and `createServerModel.ts` as stable seams. They carry most of the 19 files in `tests/hosting/`.
- Do not delete a test unless its parity-matrix row is retired. Moving a test to a new selector is fine; removing the assertion is not.
- Keep `?tab=<id>` working. Map old ids (`runtime`, `cloud`, `jobs`, `audit`, `credentials`, `files`, `configuration`, `logs`, `overview`) to the new sections instead of falling through to Overview (`workspaceTabFromSearch` currently falls back silently).

**Warning signs:**
- A PR deletes a file under `tests/hosting/` or a service method.
- A panel is merged into another and its mutation count drops.
- The e2e only covers terminate and placement.

**Phase to address:** P1 (matrix), enforced in every later phase's definition of done, audited in P7.

---

### Pitfall 6: Localization regressions, with three copy sources and a RESX file outside the editable scope

**What goes wrong:**
New or reworded UI text ships English-only or untranslated in German. Two further problems:
- The "RESX source of truth" cannot be edited without touching `sources/WebApp`.
- The build never flags missing German strings.

**Why it happens:**
- Copy lives in three places today:
  - `t("...")` backed by RESX-generated JSON
  - `copy.ts`, an inline en/de dictionary behind `useHostingCopy()` (used by `FleetPages`, `ServerWorkspacePage`, `CreateServerPage`, and `components.tsx`)
  - literal English strings in JSX
- Literal English found: the "Credentials" tab label, `Terrain`, `Scenario`, "Content", "Terrain and scenario", "Save content selection", all of `ServerSecretsPanel` ("Loading credentials…", "Server credentials", "Set a new value", and so on), and the validation messages in `workspaceModel.ts` ("must be a whole number", "is required").
- `scripts/export-locales.mjs` reads `sources/WebApp/Properties/Language.resx` and `Language.de.resx`. Every new key must be added there, which conflicts with the "never modify sources/WebApp" constraint. Without an explicit exception the team will hard-code strings or grow `copy.ts`.
- The generator does `germanRaw[key] ?? english[key]`. A missing German entry silently becomes English, and `locales:check` only detects drift between RESX and generated JSON, not completeness.
- API-originated text is English-only: action `label`, `description`, `costDescription`, `confirmationText`, `unavailableReason`, `LaunchDisabledReason`, job `failureReason`, `faultSummary`, and configuration `displayName`. Only seven messages are translated, through the string-match map `serverMessageKeys` in `model.ts`. A redesign that surfaces more of this text (for example `costDescription` in the start dialog) puts English into German UI.
- German strings run roughly 20-35% longer, which breaks fixed-width tabs and buttons (see Pitfall 8).
- Formatting helpers are duplicated: `CreateServerPage` defines its own `euro()` and `number()`, while the workspace uses `formatMoney` from `model.ts`.
- Tone: hosting German uses informal "du" (11 hits against 2 "Sie" in Hosting/React keys). Verify and stay consistent.

**How to avoid:**
- P1 decision, to put to the user: allow RESX-only edits to `Language.resx` and `Language.de.resx` under `sources/WebApp/Properties`. These are data files consumed by the React generator, with no Blazor code change. `Language.Designer.cs` is generated; do not hand-edit it, and note the resulting Blazor designer drift. Alternatively, define an explicit React-owned resource location.
- Retire `copy.ts` as a new-copy destination. New text goes to RESX keys only. Migrate its keys opportunistically when a component is rewritten.
- Add a test or script that fails when any `React_*` or `Hosting_*` key's German value is missing or identical to English (with an allowlist for legitimate cognates such as "Audit", "Worker", "Cloud").
- Add an ESLint rule or a grep gate for literal JSX text and `aria-label`/`title` literals under `src/features/hosting`.
- Map action ids and `costImpact` enum values to RESX copy, falling back to API text only for unknown ids.
- Replace `humanizeToken()` for any user-visible status, stage, or kind. It is English-only title-casing and is used widely, for example job kind, stage, and resource state in `ServerWorkspacePage`.
- Run every Playwright journey at least once with `UtfCulture=de`.
- Use `Intl` formatting from one helper.

**Warning signs:**
- New strings with no RESX diff.
- `useHostingCopy` imports growing.
- German screenshot differs in layout only because of overflow.
- English text visible in the German dialog.

**Phase to address:** P1 (RESX decision, gates), P4 and P5 (migrations), P7 (German pass).

---

### Pitfall 7: Tests and evidence tied to markup, selectors, and fixtures that drift from the contract

**What goes wrong:**
Layout changes break dozens of tests for incidental reasons. The team then loosens assertions, and real regressions pass. The opposite also occurs: tests stay green because they test mocks or hand-written fixtures.

**Why it happens (repo facts):**
- Workspace tabs are `<nav>` plus `<button aria-current>`. `tests/hosting/workspace-page.test.tsx` clicks them with `getByRole("button", { name: "Logs" | "Audit" | /Jobs \(1\)/ })`, and e2e uses `getByRole("button", { name: "Configuration" })`. Converting to correct `role="tab"` breaks all of these. This is the right change, so budget for it.
- Selectors include the English API action label ("Start", the dialog button "Terminate"), German strings ("Jetzt deprovisionieren", "Spielserver", "Läuft"), and the exact `?action=terminate` href plus a URL regex.
- `tests/hosting/audit-feed.test.tsx` asserts the CSS class `next-hosting-audit-notice`.
- `ServerWorkspaceView` is exported and rendered directly by tests with a fixed prop contract (`server, jobs, operations, actionBusy, jobBusy, onExecuteAction, onRetryJob, onSkipJob`). Changing it ripples through the tests.
- Fixtures cast with `as unknown as GameServer`, which defeats the generated OpenAPI types, so contract drift is invisible (and Start is modelled incorrectly; see Pitfall 1).
- Vitest aliases `@fluentui/react-components` and `@fluentui/react-icons` to small mocks (`tests/mocks/react-components.tsx` is 22 lines and exports `Spinner`, `FluentProvider`, `webDarkTheme`, `Select`). Real Fluent components are used only in `App.tsx`, `primitives.tsx`, and `ArmaPlacementField.tsx`; the hosting UI is mostly custom CSS (`hosting.css` is 2428 lines) plus `ActionControl`.
  - If the redesign imports real Fluent `Tabs`, `Dialog`, `MessageBar`, or `Card` in hosting code, unit tests either fail on undefined mock exports or exercise a stand-in that does not behave like the real component.
- Route evidence is coupled: `tests/routes.test.ts` asserts `countRoutePatterns() === 80`, compares non-fallback manifest routes to every Blazor `@page`, and `tests/hosting/routes.test.ts` asserts the exact hosting route list. The manifest `sources/Common/Ui/routes.json` is also embedded in `WebApp.csproj` as `wwwroot/ui-routes.json`, so Blazor consumes it.
- Playwright runs a single `chromium` project with `Desktop Chrome` (`playwright.config.ts`), against `vite preview` plus a classic stub.
- CI is strict: `npm run ci` runs locales/contracts drift checks, Prettier, ESLint with `--max-warnings 0`, `tsc -b`, Vitest, and a production build. Playwright needs a full asset build with a 360 s server timeout.
- Previous phase evidence (v1.12 Phase 60 screenshots) came from the Blazor `tests/Ui.Tests` harness, not from React.

**How to avoid:**
- Routes and URLs may change when the design needs it. When they do, edit `routes.json` together with `classicFallback`, the count test, and confirm the Blazor "Try new UI" behaviour. Never mark a route `certified: true` early.
- Decide Fluent vs custom primitives in P1 and stick to it. The existing convention (project `ActionControl`, `button button--*`, native `<dialog>`) is the lower-risk path. If real Fluent components are adopted, extend the mocks deliberately or run those components unmocked in a small set of jsdom tests, and cover them in Playwright.
- Prefer role and accessible-name queries plus `data-testid` on structural landmarks. Never assert CSS classes. Add a shared `buildServer()` / `buildJob()` fixture builder typed with `satisfies GameServer` (no `as unknown as`) and populated from real projection semantics.
- Update tests in the same commit as the markup they cover. Do not defer to a "fix tests" phase.
- Add e2e journeys for the new member flows, not only terminate: "create then start", "change mod pack then see 'applies next start'", "German locale".

**Warning signs:**
- A PR that changes more than about 10 test lines in unrelated files.
- `as unknown as` in a new fixture.
- New code importing `@fluentui/react-components` with no mock or e2e change.
- A manifest diff without a classic fallback.

**Phase to address:** P1 (fixture builder, Fluent decision, route policy), enforced throughout, P7 (e2e).

---

### Pitfall 8: Mobile layouts verified only by "no horizontal overflow" (UI-60-001 recurs)

**What goes wrong:**
The selected-server tab strip is clipped on a 390 px viewport again, or a new element (action bar, dialog, table) breaks on mobile, and nothing catches it.

**Why it happens:**
- Root cause of UI-60-001: `.next-hosting-tabs` is a single `display:flex; overflow-x:auto` row of up to nine `white-space:nowrap` buttons (`padding: .8rem 1rem`) with no scroll affordance, no active-tab scrolling, and no overflow menu. German labels are longer.
- The only mobile tests in the repo cover the public and command shell (`public-shell.spec.ts` at 390x844). No hosting journey runs on a mobile viewport. Playwright has no mobile project, and jsdom cannot detect clipping.
- Wide fixed minimums exist in hosting CSS: `.next-hosting-file-table table { min-width: 49rem }` and `.next-hosting-access-matrix { min-width: 58rem }`.
- The v1.12 review passed the phase on "no document-level overflow" and accepted the clipping as advisory debt, which made it easy to defer again.

**How to avoid:**
- Fix by information architecture, not CSS tweaks: group the nine tabs into about five sections (for example Overview, Setup [config, mods, runtime], Files and logs, Cost and cloud, Activity [jobs, audit]), and use a section menu or wrapped compact tabs on narrow viewports.
- Whatever pattern is chosen, scroll the active section into view and show an overflow cue.
- Build one shared section-navigation primitive and reuse it. Today the app has at least three patterns: `<nav>` buttons, `role="tablist"` in `ConfigurationProfileWorkspacePage` and `EventEditPage`, and `role="tab"` in the files panel.
- Add Playwright mobile coverage: a `mobile-chromium` project (for example Pixel 7) or explicit `setViewportSize` at 320 and 390 wide. Run hosting journeys in EN and DE and assert:
  - no document-level horizontal overflow
  - every section reachable
  - the primary action (and its cost) visible without scrolling
  - the confirmation dialog fully usable
- Keep large tables inside their own scroll container with a visible edge cue, or switch to a stacked list under 800 px.

**Warning signs:**
- A tab label truncated after "Cloud".
- Header actions (Refresh, Back, Delete) wrapping into three rows.
- A primary action below the fold on a phone.

**Phase to address:** P2 (navigation primitive), P4 (IA), P7 (mobile e2e in EN and DE).

---

### Pitfall 9: Accessibility regressions in cost-critical controls

**What goes wrong:**
Keyboard and screen-reader users cannot discover why a cost-bearing action is disabled, miss state changes, or mis-operate navigation.

**Why it happens (repo facts):**
- The fleet card's disabled "Deprovision now" button explains itself only through `title` (`ServerCard`). Titles are unreliable on touch and for assistive technology.
- `<progress>` elements in `JobList` and the storage telemetry have no accessible name.
- Polling (5-10 s) changes status text silently. Only the paused notice uses `role="status"`.
- The workspace section navigation is not a tab widget, has no roving tabindex or arrow-key support, and differs from the other tab patterns.
- Status and danger are conveyed partly by colour and tone; keep text equivalents.
- Revealed secrets and the confirmation challenge input rely on native `<dialog>` behaviour. Verify focus return after close.
- `@axe-core/*` is not in `package.json`, so there is no automated accessibility gate.

**How to avoid:**
- Show disabled reasons as visible text linked with `aria-describedby`. Do not rely on `title`.
- Announce state transitions once, through a polite live region driven by job transitions, not by every poll.
- Give every `<progress>` an accessible name and value text.
- Use a real tabs or section-nav primitive with the keyboard model.
- Keep hit targets at least 44 px on mobile for primary and danger actions, and separate danger from primary actions spatially.
- Add axe checks for fleet, create, and workspace in Playwright (EN and DE, desktop and mobile). Preserve the `prefers-reduced-motion` block in `hosting.css`.

**Warning signs:**
- A disabled button with a `title` and nothing else.
- Status text that changes without any announcement.
- An unlabelled `<progress>`.

**Phase to address:** P2 (primitives), P4 and P5 (usage), P7 (axe and keyboard pass).

---

### Pitfall 10: Starting servers is hard to discover, and disabled-by-default servers dead-end

**What goes wrong:**
Members open a server, cannot start it, and see no explanation or next step. This is the exact success metric ("a member completes 'start a server' without help").

**Why it happens:**
- `primaryServerAction()` (`workspaceModel.ts`) only considers actions that are `enabled || pending`. When Start is disabled (launch disabled, or no Operate permission) and nothing else is enabled, there is no primary action, so the "Next action" panel does not render. The reason appears only in the secondary action list, if that renders at all.
- It also falls back to `candidates.sort(order)[0]`, so an unrelated enabled action (Install, Update) can become the headline.
- The fleet card offers Terminate and Open, never Start.
- `LaunchDisabledReason` is arbitrary English text from the API.
- Servers are reached through `/command/servers` in the Command Center shell, gated by `canAccessGameServers` (admin/create/`sv:view` claims). Whether "any member" can even see the entry point depends on claims. The redesign must not assume visibility.
- `canOperateServer()` in `authorization.ts` is unused. The API computes `enabled` from the Operate permission.

**How to avoid:**
- The view-model always yields a "next step" even when nothing is actionable: "Start is disabled: launch is turned off. Ask a server admin / enable launch (if `canModify`)". Rank disabled-but-relevant Start above unrelated enabled actions.
- Make Start discoverable from fleet and workspace through the same cost-confirm component (Pitfall 1).
- Do not compute permissions client-side. Trust `action.enabled`, `canModify`, and `canManageSecrets`, and hide admin diagnostics (SSH command, deployment root, resource identifiers) from the default member view. Verify what the API projects to non-operators before surfacing it.
- Treat "start" and "update" as two guided flows (create then start; change then see when it takes effect) with an explicit empty state for members who lack create or operate rights.

**Warning signs:**
- A server detail with no highlighted action.
- Usability tests where the member cannot say what to do next.
- A fleet card with Terminate but no Start.

**Phase to address:** P2 (next-step model), P3.

---

### Pitfall 11: "Add a mod" promises that the API cannot keep

**What goes wrong:**
The UI implies a mod change is live, applies to only one server, or is supported for a game that has no definition. The milestone example is a Minecraft server.

**Why it happens:**
- No Minecraft definition exists. The only registered games are `arma3`, `aska`, and `counterstrike2`. Mod and content support is driven by capability flags (`supportsModPacks`).
- Changing a server's mod pack (`changeModPack`) only sets a selection. The existing UI says it "applies at next preparation" (`Hosting_Detail_ModPackAppliesNextPreparation`), so it is not live while a deployment exists.
- Mod packs are shared definitions (`/command/mod-packs`). Editing the pack's content changes every server and event using it; selecting a different pack affects only this server.
- `ServerRuntimePanel` keeps the draft pack and terrain/scenario selection in local `useState` with no `useUnsavedChanges`. Switching tabs (panels are conditionally rendered) silently discards an unsaved selection. `ServerConfigurationPanel` does track dirty state.

**How to avoid:**
- Build every change flow from the capability projection (`supportsModPacks`, content capabilities, config entries). Hard-code no game ids, and do not build Minecraft-specific UI until a definition exists.
- After saving a change, show "Saved. Takes effect on next start" (or "on next preparation" if a deployment exists) and, if running, offer the explicit restart or preparation action with its cost consequence.
- Make shared-pack impact visible ("used by N servers and events") before editing the pack. If the data is not available, flag it as an API gap for an explicit decision rather than changing the API.
- Apply `useUnsavedChanges` to every draft in the runtime and mods area, and warn before switching sections.

**Warning signs:**
- Copy that says "Mod added" with no timing.
- A game-id switch in a component.
- An unsaved select value lost after changing tab.

**Phase to address:** P1 (capability-driven rule), P4.

---

### Pitfall 12: Scope creep from "light consistency pass" into a redesign of the whole app

**What goes wrong:**
The consistency pass touches dozens of certified pages, each with its own parity checklist and tests, and the milestone never lands.

**Why it happens:**
- The app is large: `styles.css` is 7413 lines, `hosting.css` 2428, `commandNavigation.tsx` 541, `App.tsx` 681, and there are about 25 `admin-*` test files plus 76 certified route patterns.
- Shared primitives are widely used. For example, `ConfirmDialog` is used in 17 files, so a "better dialog" ripples through events, wiki, members, and Kassenbuch.
- Visual systems already have compatibility aliases (`--text`, `--line`, `--gold-light`, ...) plus hard-coded hex values (106 in `hosting.css`, 53 in `styles.css`). The temptation is to normalize everything.

**How to avoid:**
- Define the pass in P1 as a closed list of shared-component and token changes, for example: `PageHeader` rhythm, `StatePanel`, status badge tones, spacing scale, section-nav primitive, command nav labels. Per-page re-layout is out of scope.
- Add new components (`CostActionDialog`, `SectionNav`) next to existing ones. Change shared primitives only through backwards-compatible props with defaults.
- Verify shared-primitive changes with a fixed sample of about 8 representative pages (screenshots plus the existing Vitest suite), not by visiting all routes.
- Time-box the pass. Replace hex values only inside files already being touched for the server redesign.
- API changes a feature needs are allowed (see AGENTS.md), but they are not part of the consistency pass.

**Warning signs:**
- Diffs in `admin/*` or `wiki/*` that are not covered by a primitive-change rationale.
- Edits to `routes.json` or the Blazor tree.
- A growing list of "while we are here" items.

**Phase to address:** P1 (boundary), P6.

---

### Pitfall 13: Tab and panel state loss or leakage when restructuring navigation

**What goes wrong:**
- Unmounting panels on section change loses unsaved drafts, upload progress, expanded file-tree state, and in-flight form input.
- The opposite fix, keeping all panels mounted and hidden, retains revealed secret values in the DOM and issues every panel's queries at once.

**Why it happens:**
- Today each tab renders conditionally (`{tab === "..." && ...}`), so unmounting is the default.
- `ServerSecretsPanel` holds revealed values in component state (a good choice, since they stay out of the query cache), and unmounting is what discards them.
- The workspace polls three queries every 5 s regardless of the visible section.

**How to avoid:**
- Keep conditional rendering for the credentials section so revealed secrets are discarded on leave. Never put revealed values in the TanStack cache or the URL.
- Track dirty state centrally and prompt before leaving a section with unsaved work. Verify how `useBlocker` treats search-param-only navigation in the installed react-router 7.18, since section changes are `?tab=` updates.
- Lazy-load heavy panels (files, 1372 lines) and enable their queries only when the section is open.

**Warning signs:**
- A section switch that loses typed input.
- Secret values present in the DOM when the credentials section is not visible.

**Phase to address:** P4, P7.

---

## Moderate Pitfalls

### Pitfall 14: Contract changes smuggled in through the UI

**What goes wrong:**
The redesign needs a field the API does not project (a stable "paused" code, "used by N servers", localised action text, cost-owner transfer) and someone edits the API, `openapi/utf-api.json`, or contracts to unblock the UI.

**Prevention:**
- Collect API gaps in P1 as explicit decisions. If one is approved, regenerate the OpenAPI snapshot through the documented export, run `npm run generate`, and keep `contracts:check` green.
- Never hand-edit `src/generated/*` or `openapi/utf-api.json`.

### Pitfall 15: Secrets and diagnostics leak into the redesigned views

**What goes wrong:**
A friendlier Logs or Cloud view surfaces worker diagnostics, SSH commands, deployment paths, or provider identifiers to every member, or logs revealed values to the console.

**Prevention:**
- Keep the credentials section gated by `canManageSecrets`. Keep `administrativeSshCommand`, `deploymentRoot`, and raw provider identifiers in a collapsed operator-only area.
- Add a test that no secret value appears in rendered output unless explicitly revealed.
- Strip `console.*` of server payloads.

### Pitfall 16: Fleet N+1 and polling cost

**What goes wrong:**
New fleet cards that fetch per-server jobs or details (to show progress) multiply requests at a 10 s interval.

**Prevention:**
- Source card state from the fleet projection (`pendingJobCount`, `deployment`, `actions`).
- Fetch job detail only for the open workspace.
- Community scale is small (tens of servers), so this is a hygiene issue rather than a capacity risk.

### Pitfall 17: Duplicate formatting and naming helpers

**What goes wrong:**
Costs and dates render differently across fleet, create, workspace, and finance (`euro()` and `number()` in `CreateServerPage` vs `formatMoney`/`formatBytes`/`formatDate` in `model.ts` vs `currentLocale()` use elsewhere).

**Prevention:**
- Consolidate into one `Intl`-based formatter module in P2.
- Show `/h` and `/month` consistently and always label estimates as estimates.

### Pitfall 18: Brittle `costImpact` and status string matching

**What goes wrong:**
`primaryServerAction()` does `costImpact.toLowerCase().includes("stopscost")`, and statuses are matched as free strings. A new enum value or casing change breaks ranking silently.

**Prevention:**
- Use the generated enum types with exhaustive switches. Add a test that fails on an unknown value.

---

## Minor Pitfalls

### Pitfall 19: Dialog semantics and wording drift
Reusing one generic confirm title ("Confirm lifecycle action") for different actions hides what is at stake. Title the dialog by the action and its cost consequence.

### Pitfall 20: Empty and error states inconsistent between fleet and workspace
`ServerFleetPage` handles stale-data errors (it keeps the fleet visible with an error banner), while the workspace shows only a full-page error on the first load. Settle on one policy: keep stale data visible with a banner.

### Pitfall 21: Page title and heading duplication
The fleet uses `useDocumentTitle`; other hosting pages do not. Apply it consistently as part of the consistency pass.

### Pitfall 22: Gold CTA overuse
`button--gold` is used for Create. Reserve one visual emphasis for the single primary action per view, and keep cost-bearing actions visually distinct from decorative CTAs.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Add new strings to `copy.ts` | No RESX round trip | Third copy system; no German completeness gate; hosting divergence from the rest of the app | Never for new copy; migrate existing keys when touched |
| Hard-code English in JSX "for now" | Faster UI iteration | Silent English in German UI; no gate catches it | Never; use RESX keys from the first commit |
| Fixtures cast with `as unknown as GameServer` | Quick test setup | Contract drift invisible; Start semantics wrong | Never in new tests; migrate old ones when touched |
| Assert CSS classes or exact English labels in tests | Easy assertions | Every restyle or rename breaks tests | Only for stable `data-testid` and landmark roles |
| Optimistic cache writes for deployment status | Snappy feel | UI contradicts durable job outcome | Never for status or billing; fine for local draft UI |
| Per-page restyling in the consistency pass | Visible polish | Parity re-verification across certified routes | Only inside files already being rewritten |
| Keeping all tab panels mounted | Preserves drafts trivially | Revealed secrets stay in DOM; all queries fire | Never for credentials; acceptable for cheap local panels |
| Adding a React route for a new flow | Clean URLs | `routes.json` edit, count test, Blazor-visible manifest, classic fallback | Allowed when the design needs it; update manifest and tests together |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Generated OpenAPI types (`src/generated/api.ts`) | Re-declaring server shapes in `types.ts` or casting with `as unknown as` | Derive from generated types; use `satisfies` in fixtures; keep `contracts:check` green |
| RESX to JSON generator | Assuming `locales:check` verifies German completeness | It checks drift only (missing German silently falls back to English); add a completeness gate |
| Route manifest `sources/Common/Ui/routes.json` | Treating it as a React-only file | It is shared with Blazor via `WebApp.csproj`; edits change classic behaviour and trip count and parity tests |
| Lifecycle `executeAction` | Assuming synchronous outcome; no idempotency key | Treat the response as a queued job; disable confirm while pending; reconcile via refetch |
| API-provided English text (action labels, reasons, failures) | Rendering it directly in German UI | Map by action id or enum to RESX; fall back to API text only for unknown ids |
| Vitest Fluent mocks | Importing new Fluent components and trusting green tests | Extend mocks deliberately or test the real component; cover in Playwright |
| Playwright stub backend (`scripts/classic-stub.mjs`) | Only stubbing happy paths | Add scenario fixtures for failed job, possible cost, paused, launch disabled |
| TanStack Query polling | Per-component `refetchInterval` multiplying | Centralise policy (fast while a job is active, slow otherwise); no per-card queries |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Three 5 s polls per open workspace plus fleet and notification polls | Chatty network, flicker on re-render | Adaptive interval; enable queries only for the visible section | Tens of concurrent members |
| Fleet cards fetching job detail | N+1 requests every 10 s | Use fleet projection fields only | More than about 20 servers |
| Large files panel (1372 lines) in the initial bundle | Slow first paint of workspace | Lazy-load the section | Always on mobile |
| Re-sorting and re-rendering all cards each poll | Cards jump while the user is about to click | Stable order key plus no reorder while hovering or focused | Any size |
| Hosting CSS monolith (one 2428-line global file) | Specificity wars during redesign | Co-locate new styles with new components; delete dead rules as parity rows retire | During the rewrite |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Showing SSH command, deployment root, or provider ids to all members | Operator-only diagnostics exposed | Gate by capability; collapse into an operator area; verify API projection |
| Putting revealed secret values in query cache, URL, or logs | Credential leakage | Keep in transient component state (current behaviour); clear on unmount |
| Client-side permission computation (`canOperateServer`) driving enablement | UI shows or hides actions inconsistently with the API | Trust API `enabled`, `canModify`, `canManageSecrets` |
| Weakening the typed challenge on Terminate/Stop | Accidental data loss or early cost stop | Keep the typed server-title challenge on destructive paths |
| Any change that exposes provider tokens, Steam or worker credentials | Violates project security constraint | None of these exist in the UI contract; keep it so |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Start button with no cost, owner, or duration | Accidental billing; lost trust | Cost-aware start dialog (hourly estimate, owner, 30-45 min expectation) |
| "Offline" while cost may continue | Member thinks the bill stopped | Billing-aware state labels |
| Nine flat tabs | Findability and mobile clipping | Five grouped sections with a mobile pattern |
| Admin jargon ("stage", "lease", "provider resource") in the default view | Members cannot self-serve | Member summary first; operator detail behind disclosure |
| Saved change with no timing | Member expects live change | "Takes effect on next start" with an explicit follow-up action |
| Disabled action with no reason or next step | Dead end | Visible reason plus who can enable it |
| Long wait with a spinner | Abandonment or double clicks | Job stage and progress, expected duration, safe-to-leave message |
| Error shown only as a toast | Failed start unnoticed | Persistent failed-job state with retry/skip |

## "Looks Done But Isn't" Checklist

- [ ] **Start flow:** Often missing a cost confirmation, because the API requires none. Verify Start cannot execute without the shared cost dialog on every entry point (fleet, workspace, deep link).
- [ ] **State display:** Often missing the "possible cost" axis. Verify every status × `possibleCost` combination in the state gallery test.
- [ ] **German locale:** Often missing translations, because the generator falls back to English. Verify no key has missing or identical German text (outside the allowlist) and run every e2e journey in `de`.
- [ ] **API-text surfaces:** Often English in German UI. Verify action labels, reasons, failures, and cost descriptions are mapped or deliberately left as API text.
- [ ] **Mobile:** Often only tested for document overflow. Verify at 320 and 390 px in EN and DE: all sections reachable, primary action and cost visible, dialog usable.
- [ ] **Deep links:** Often broken by renaming tabs. Verify old `?tab=` and `?action=` URLs still resolve.
- [ ] **Capability parity:** Often silently dropped. Verify every parity-matrix row is retained or explicitly retired.
- [ ] **Accessibility:** Often missing disabled-reason text, live-region announcements, labelled progress bars, and keyboard-operable tabs. Verify with axe and a keyboard run.
- [ ] **Failed and paused states:** Often not rendered anywhere. Verify failed job, `PreservationFailed`, `TerminatingDueToFault`, paused control plane, and launch-disabled all have a designed view and a test.
- [ ] **Route certification:** Often broken by a manifest edit. Verify the route count test, Blazor route comparison, and no premature `certified: true`.
- [ ] **CI gate:** Verify `npm run ci` passes (locales, contracts, format, lint with zero warnings, typecheck, tests, build) and Playwright passes, not only Vitest.
- [ ] **No edits outside scope:** Verify the diff does not touch `sources/WebApp` code, the API, or generated output (RESX data files only if the user approved the exception).

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| One-click Start shipped | MEDIUM | Gate every Start entry point behind the shared cost dialog; add the invariant test; audit audit-feed for unintended starts |
| Status hides possible cost | MEDIUM | Introduce the view-model; replace ad hoc derivations; add the combinatorial test |
| Capability dropped | MEDIUM to HIGH | Restore from git history using the parity matrix; re-add tests; avoid reverting unrelated redesign work |
| Untranslated German strings | LOW | Add keys to RESX; run the completeness gate; re-run German e2e |
| Tests tied to markup broke | LOW to MEDIUM | Migrate to role and testid selectors in the same PR; never weaken assertions |
| Mobile clipping regressed | LOW | Apply the section-nav pattern; add the mobile e2e that was missing |
| Scope creep into other pages | HIGH | Revert per-page edits; keep only primitive and token changes; re-baseline with the sample-page checks |
| Manifest or route edit broke classic | MEDIUM | Restore `routes.json`; re-run route tests; re-decide the route policy |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| 1. One-click Start | P2, P3, P7 | Test that no Start path executes without the cost dialog; deep link test |
| 2. Status hides billing | P2, P3, P5 | Combinatorial view-model test (12 statuses × possibleCost × pending × paused) |
| 3. Cost-stop vocabulary | P1, P5, P7 | One RESX term per action; EN and DE review; e2e asserts updated |
| 4. Optimistic vs durable | P2, P3, P5, P7 | No `onMutate` status writes; fake-state scenarios (failed, paused, conflict) |
| 5. Capability loss | P1, all phases, P7 | Parity matrix fully resolved; test count not reduced without a retired row |
| 6. Localization | P1, P4, P5, P7 | Completeness gate; literal-string lint; German e2e |
| 7. Test and evidence breakage | P1, all phases | Typed fixture builder; no class assertions; routes tests unchanged |
| 8. Mobile | P2, P4, P7 | Mobile Playwright project, EN and DE, 320 and 390 px |
| 9. Accessibility | P2, P4, P5, P7 | axe checks; keyboard run; labelled progress; disabled-reason text |
| 10. Start discoverability | P2, P3 | Usability walkthrough: unassisted start by a member claim set |
| 11. Mod change promises | P1, P4 | "Takes effect next start" copy test; capability-driven, no game ids |
| 12. Scope creep | P1, P6 | Written closed list; 8-page sample check |
| 13. Panel state loss and leak | P4, P7 | Drafts survive or prompt; secrets absent when section not shown |
| 14. Hidden contract changes | P1 | Decision log; `contracts:check` green; no API diffs |
| 15. Diagnostics leak | P4, P5 | Member-claim render test hides operator fields |
| 16-18. Polling, formatting, string matching | P2 | Single policy module; single formatter module; exhaustive enum switches |

## Sources

- Repository inspection (HIGH confidence), all read on 2026-11-18:
  - `sources/ReactApp/src/features/hosting/*` (`ServerWorkspacePage.tsx`, `components.tsx`, `FleetPages.tsx`, `CreateServerPage.tsx`, `ServerRuntimePanel.tsx`, `ServerSecretsPanel.tsx`, `workspaceModel.ts`, `model.ts`, `copy.ts`, `routes.tsx`, `authorization.ts`, `hosting.css`)
  - `sources/ReactApp/src/components/primitives.tsx`, `ActionControl.tsx`, `commandNavigation.tsx`, `src/app/RouteCertificationGate.tsx`, `src/app/routes.ts`
  - `sources/ReactApp/tests/**` (`hosting/workspace-page.test.tsx`, `routes.test.ts`, `e2e/public-shell.spec.ts`, `mocks/react-components.tsx`), `vitest.config.ts`, `playwright.config.ts`, `package.json`, `scripts/export-locales.mjs`
  - `sources/Common/Ui/routes.json`, `sources/WebApp/WebApp.csproj` (manifest embedding)
  - `sources/Api/Services/GameServerLifecycle/GameServerProjectionService.cs`, `sources/Api/Services/GameServerDefinition/*Registration.cs`, `sources/HostingContracts/EDeploymentStatus.cs`
- Planning context: `.planning/PROJECT.md`, `.planning/codebase/CONCERNS.md`, `.planning/codebase/TESTING.md`, `.planning/milestones/v1.12-phases/60-ux-verification-fake-states-and-guardrails/60-UI-REVIEW.md` (UI-60-001), `.planning/milestones/v1.12-MILESTONE-AUDIT.md`, `docs/features/react-ui-beta.md`
- General UX and accessibility practice (MEDIUM): standard guidance on cost-confirmation dialogs, ARIA tabs, live regions, and non-colour status. No external documentation was fetched for this file.

---
*Pitfalls research for: cost-bearing server-management UI redesign (React, v2.1)*
*Researched: 2026-11-18*
