# Deferred Items - Phase 66

## From plan 66-06

- `tests/Api.Tests/Security/ApiSecurityConfigurationTests.ApiPipeline_EnablesWebSocketsBeforeAuthenticationAndControllers`
  fails on HEAD independent of this plan: it searches `sources/Api/Program.cs` for the exact text
  `app.UseWebSockets();`, but Program.cs line 104 calls `app.UseWebSockets(new WebSocketOptions { ... })`. Out of
  scope for 66-06 (Program.cs untouched); the test assertion should match the call with options.
  status: acknowledged

## From plan 66-12

- `npm --prefix sources/ReactApp run format:check` reports 31 files, matching the known pre-existing drift (create
  page files, Disclosure, several create tests). One of them, `src/features/hosting/CostAlertSettingsPanel.tsx`
  (plan 66-10/66-11), differs from Prettier output on every line, which points to a CRLF working-copy line-ending
  artifact (`core.autocrlf`) rather than a style defect; it was not touched here (production code is out of scope for
  66-12). The unit, lint, typecheck, locale and contract checks all pass.
- `ApiSecurityConfigurationTests.ApiPipeline_EnablesWebSocketsBeforeAuthenticationAndControllers` still fails (see
  plan 66-06); it is the only failing test of `dotnet test X39.UnitedTacticalForces.slnx`.
  status: acknowledged
