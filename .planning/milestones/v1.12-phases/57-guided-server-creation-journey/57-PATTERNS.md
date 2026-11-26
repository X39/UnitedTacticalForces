# Phase 57 Pattern Map

**Phase:** 57-guided-server-creation-journey  
**Date:** 2026-07-05  
**Purpose:** Local implementation patterns to preserve while rebuilding server creation as a guided journey.

---

## WebApp Page Patterns

- Command-center pages use `UtfCommandPageHeader`, `UtfCommandButton`, `UtfCommandToolbar`, `utf-panel`, `utf-state-panel`, and the `server-command-page` CSS family.
- Non-trivial page logic currently lives in `.razor` `@code` blocks for server pages. Keep any new helper types private or local unless reuse is real.
- Razor files bind hand-written repository models and local view models. Generated Kiota DTOs should stay behind repository methods unless the current page already uses an allowlisted DTO.
- Existing page CSS uses UTF variables: `--utf-bg`, `--utf-surface`, `--utf-surface-2`, `--utf-stroke`, `--utf-stroke-strong`, `--utf-gold`, `--utf-danger`, `--utf-text`, and status tokens.

## Wizard Patterns

- Step changes should be pure UI state changes.
- Keep one final submit path that calls the repository only after Review is complete.
- Use derived validation collections rather than ad hoc booleans spread across markup.
- Validation items should carry a step/field target so the Review step can link the user back to the exact fix location.
- Disabled options should remain visible when the user can fix them. Hide only options the current user can never act on or cannot be authorized to see.

## Creation Repository Patterns

- `IGameServerRepository` already exposes creation options, record creation, local managed creation, guided cloud server creation, planning selection options, readiness validation, and cloud workflow actions.
- Record-only creation uses `CreateGameServerRecordAsync`.
- Local managed creation uses `CreateGameServerAsync`.
- Cloud planning server creation uses `CreateGuidedCloudPlanningServerAsync`.
- Guided cloud readiness and dry-run actions must remain post-creation advanced/workspace actions.
- Public repository names must remain generic; do not introduce new `Arma3Cloud` repository methods.

## Workspace And Advanced Operations Patterns

- `Details.razor` is the server workspace and is the correct post-submit destination.
- `Advanced.razor` is the existing cloud setup/review/provision/run surface and already understands query defaults for `cloudServer`, `cloudTemplate`, `runtimeProfile`, and `modPack`.
- Phase 57 should deep-link into `Advanced.razor` rather than duplicating cloud dry-run/review/policy/provisioning implementation.
- Cloud setup copy should present `Review cloud setup` as the next action, while dry-run and policy technical labels remain in detail/error context.

## Localization Patterns

- Server page copy goes through `Language.resx` and `Language.de.resx` where existing keys already exist.
- Generated `Language.Designer.cs` is treated as generated; update it only if the repository's existing localization workflow requires it for build.
- English and German resources should keep task-focused strings aligned even when German copy is not perfect. Avoid leaving old English labels as the primary path.

## Test Patterns

- Boundary tests under `tests/Api.Tests/WebApp` read source files and assert route/method/copy invariants.
- API tests under `tests/Api.Tests/CloudPlanning` cover guided cloud entry and no provider side effects.
- UI tests under `tests/Ui.Tests` use Playwright against `GameServerUiMockBackend`.
- Focused UI tests are preferred over running the entire broad `CloudAgentUiTests` class when iterating, but the plan should leave broad suite commands for final verification.

## Security And Redaction Patterns

- Do not log or display provider tokens, Steam credentials, worker credentials, bearer headers, cookies, raw setup payloads, or secret-bearing details.
- Do not create dry-run, policy review, lifecycle, or audit records from step navigation.
- Preserve authorization checks by continuing to use existing repository/API creation methods.
- Final submit failure must keep the user in the wizard and preserve local input state.
