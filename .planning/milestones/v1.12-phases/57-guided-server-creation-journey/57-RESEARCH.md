# Phase 57: Guided Server Creation Journey - Research

**Phase:** 57-guided-server-creation-journey  
**Date:** 2026-07-05  
**Mode:** Inline research, text-mode fallback  
**Requirements:** CREATE-01, CREATE-02, CREATE-03, CREATE-04

---

## Research Questions

1. Which existing server creation, advanced cloud setup, workspace, repository, and test surfaces already support the guided creation journey?
2. Where does the current UI save state before the user has completed the initial journey?
3. How can cloud setup continue from the server workspace without creating provider resources or hidden setup drafts?
4. Which copy and validation changes are required to keep normal creation task-worded while preserving detailed diagnostics?
5. Which focused tests can prove the journey without live Hetzner, Steam, or Arma runtime mutation?

---

## Key Findings

### Current Create Page

- `sources/WebApp/Pages/Servers/Create.razor` is the normal entry point at `/command/servers/create`.
- The page currently starts with a mode toolbar: `Record`, `LocalManaged`, and `Arma3Cloud`.
- Record and local managed modes already call `CreateGameServerRecordAsync` and `CreateGameServerAsync`.
- Cloud mode already loads `GetGuidedCloudEntryOptionsAsync`, `GetCloudPlanningSelectionOptionsAsync`, and mod packs, then calls `CreateGuidedCloudPlanningServerAsync`.
- Cloud mode currently navigates directly to `/command/servers/advanced?...` after creation.
- Cloud mode filters runtime profiles with `profile.Arma3 is not null` in the Razor page. That is acceptable as an implementation detail for the current game but should not be a leading user-facing label.
- Existing cloud selection values can become wizard choices, but they must stay in component state until final submit.

### Existing Save Boundaries

- The current record/local/cloud final buttons are the only server-creation persistence boundaries in `Create.razor`.
- Intermediate mode selection, field changes, template/profile changes, and default application are local state only.
- The separate billing owner helper can persist a billing account before server creation. Phase 57 can preserve that explicit action if the copy makes clear it creates only the billing owner, not wizard progress.
- The advanced page can assign owners, validate readiness, create planning servers, and create dry-run plans. The initial wizard should not reuse those save actions until after final submit.

### Workspace And Cloud Continuation

- `sources/WebApp/Pages/Servers/Details.razor` is the server workspace and already has server tabs for overview, runtime, cloud, configuration, files, jobs, ownership, audit, and danger.
- `sources/WebApp/Pages/Servers/Advanced.razor` already has the richer guided cloud setup surface and supports query parameters:
  - `cloudServer`
  - `cloudTemplate`
  - `runtimeProfile`
  - `modPack`
- `Advanced.razor` applies those query parameters through `ApplyCloudSetupQuerySelectionAsync`.
- The Phase 57 cloud submit should open the created server workspace first, with a prominent `Continue cloud setup` action that links to the advanced page and carries those same query defaults.
- Because runtime basics currently is not a supported query parameter, the executor must either add a non-persistent query/default mechanism for it or keep runtime basics visible in review only. Any carried value must not be persisted as setup state before an explicit advanced action.

### User-Facing Language

- Existing English strings use `Record only`, `Local install`, `Cloud planning`, `Create cloud planning server`, and `Open cloud operations`.
- Phase 57 decisions require the normal path to use:
  - `Server record`
  - `Local server`
  - `Cloud server`
  - `Create server`
  - `Continue cloud setup`
  - `Review cloud setup`
- Internal terms such as lifecycle jobs, dry-run plans, policy gates, runtime profiles, templates, and generated route names can remain in details, errors, diagnostics, or advanced setup.
- `Billing owner` is already the field label and should remain.

### Existing Tests

- API tests already cover guided cloud entry routes, authorization, owner scoping, selector availability, no provider side effects, and readiness validation in `tests/Api.Tests/CloudPlanning/GuidedCloudEntryApiTests.cs`.
- WebApp boundary tests already cover creation endpoint usage and old mode markers in `tests/Api.Tests/WebApp/GameServerCreationBoundaryTests.cs`.
- Generic boundary tests already prevent returning to Arma-3-only public repository methods.
- UI tests already cover create page record/local/cloud paths and the mock backend in `tests/Ui.Tests/CloudAgentUiTests.cs` and `tests/Ui.Tests/GameServerUiMockBackend.cs`.
- The UI tests currently expect old labels and direct advanced-page navigation, so Phase 57 must update them to the wizard journey and workspace handoff.

---

## Recommended Plan Split

### 57-01 - Creation journey information architecture and state model

Create a non-persistent wizard state model in `Create.razor`:

- Step enum and derived state for Choose game, Choose hosting, Server details, First setup, and Review.
- Hosting choices based on generic creation capability and feature flags.
- Centralized validation results with step targets.
- Final-submit routing model that chooses record, local, or cloud creation only from Review.

### 57-02 - Game-first creation UI implementation

Replace the mode-first toolbar surface with the wizard UI:

- Game-first step body and mode selection labels.
- Task-focused copy and localized resources.
- Inline validation, review summary, and disabled option reasons.
- CSS that matches the UTF command-center surfaces and the UI-SPEC.

### 57-03 - Review/provision/run continuation flow and validation messaging

Make cloud continuation discoverable after creation:

- Cloud final submit opens the server workspace, not the advanced page directly.
- Workspace shows `Continue cloud setup` for newly created or eligible cloud servers.
- The link carries non-persistent query defaults into advanced operations.
- Advanced-page labels and stale/policy blocker copy use `Review cloud setup` for user-facing actions.

### 57-04 - Creation journey component and repository tests

Update automated coverage:

- WebApp boundary tests for wizard labels, no intermediate server save calls, neutral public methods, and disabled-state copy.
- UI tests for record, local, cloud, missing owner/options, provider metadata missing, stale review/policy blocker copy, and successful workspace handoff.
- Existing API guided-entry tests remain the no-provider-side-effect guard.

---

## Security Research

### Assets

- Billing owner identity and assignment.
- Server creation payloads and lifecycle job creation.
- Cloud template/profile/mod-pack/runtime defaults.
- Provider metadata and policy gate state.
- Steam credentials, provider tokens, worker credentials, bearer headers, cookies, and raw secret-bearing payloads.

### Risks

- A wizard implementation could accidentally create durable server/setup state on step changes or validation actions.
- A direct cloud submit could create dry-run or review records before the user explicitly starts `Review cloud setup`.
- Query defaults could leak or persist runtime setup details that should remain local until advanced setup actions.
- User-facing copy could expose implementation nouns as the primary path and make users think dry-run review is a saved server.
- Validation could silently hide unavailable cloud/billing/template choices and leave users with no fix path.

### Required Mitigations

- Step transitions mutate only component fields and never call repository save methods.
- Final submit is the only server creation repository call.
- Billing owner creation remains an explicit separate action with separate copy.
- Cloud defaults are navigation/query defaults only; no dry-run, policy review, or readiness record is created from the initial wizard.
- Disabled options stay visible with blocker text and next action copy where available.
- All frontend models remain hand-written repository models or allowlisted generated DTOs already in use; do not expose raw provider or worker payloads.

---

## Validation Architecture

Phase 57 requires automated and browser validation because it changes the main creation entry point, user-facing task labels, and navigation across create, workspace, and advanced operations.

Required validation layers:

- WebApp boundary tests for route/method names, wizard step labels, task-worded copy, and no Arma-only public method regression.
- API guided cloud entry tests to retain no-provider-side-effect and readiness-validation behavior.
- UI mock tests for record creation, local install, cloud creation handoff to workspace, and advanced continuation link.
- UI mock tests or boundary tests for empty options, unsupported hosting mode, missing owner, missing metadata, stale review copy, and policy gate next action copy.
- Source scans for old primary labels such as `Record only`, `Local install`, `Cloud planning`, `Create cloud planning server`, and normal-path `dry-run` wording in `Create.razor`.
- `dotnet build X39.UnitedTacticalForces.slnx` and focused `dotnet test` filters after implementation.

---

## Open Implementation Choices

- The exact wizard state type may stay private inside `Create.razor` or move to a small local helper class if Razor complexity becomes difficult to maintain.
- If carrying runtime basics through URL query is too noisy, the executor may keep it review-only during initial creation and let advanced setup ask for it again. It still must not persist setup draft state.
- The workspace `Continue cloud setup` action can be a command button in overview/cloud summary, a route alert, or both. It must be prominent enough that users do not need to discover the admin cloud page manually.
- Advanced operations may retain internal dry-run terminology in details, diagnostics, and existing plan lists, but the main next action from the creation journey must say `Review cloud setup`.

---

## RESEARCH COMPLETE
