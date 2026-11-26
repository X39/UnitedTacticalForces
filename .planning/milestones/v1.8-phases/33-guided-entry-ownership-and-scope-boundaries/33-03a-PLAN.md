---
phase: 33-guided-entry-ownership-and-scope-boundaries
plan: 03a
type: execute
wave: 3
depends_on:
  - 33-02
files_modified:
  - .planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-03-openapi-guided-entry.json
  - sources/WebApp/UtfApi/GameServers/**
  - sources/WebApp/UtfApi/Models/GuidedCloud*
autonomous: true
requirements:
  - CREATE-01
  - CREATE-02
  - CREATE-03
  - CREATE-04
user_setup: []
must_haves:
  truths:
    - "The WebApp has generated Kiota request builders for every Phase 33 guided-entry API route."
    - "Generated guided-entry models mirror the API DTOs from Plan 01 without secret-bearing, payment, or admin-only fields."
    - "Generated client output remains isolated from hand-written repository behavior."
  artifacts:
    - path: "sources/WebApp/UtfApi/GameServers/CloudEntry/Options/OptionsRequestBuilder.cs"
      provides: "Generated GET client for guided entry options"
    - path: "sources/WebApp/UtfApi/GameServers/Item/CloudEntry/Readiness/ReadinessRequestBuilder.cs"
      provides: "Generated POST client for readiness validation"
  key_links:
    - from: "sources/WebApp/UtfApi/GameServers/CloudEntry/Options/OptionsRequestBuilder.cs"
      to: "sources/Api/Controllers/GameServersController.cs"
      via: "GET /game-servers/cloud-entry/options"
      pattern: "CloudEntry.*Options"
---

<objective>
Generate the Phase 33 Kiota client artifacts for guided cloud entry routes and DTOs, without mixing generated-code work with hand-written repository changes.

Purpose: preserve the project's generated-client boundary before `/servers` UI work consumes the new guided-entry API.
Output: generated request builders and generated models for guided-entry options, planning-server creation, billing-owner assignment, and readiness validation.
</objective>

<execution_context>
@C:/Users/msili/.codex/gsd-core/workflows/execute-plan.md
@C:/Users/msili/.codex/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/REQUIREMENTS.md
@.planning/STATE.md
@.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-CONTEXT.md
@.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-RESEARCH.md
@.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-PATTERNS.md
@.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-VALIDATION.md
@.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-02-SUMMARY.md
@sources/WebApp/UtfApi/GameServers/GameServersRequestBuilder.cs
@sources/WebApp/UtfApi/GameServers/Item/GameServerItemRequestBuilder.cs
@sources/WebApp/UtfApi/GameServers/Item/CloudRuntime/CloudRuntimeRequestBuilder.cs
@sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanRequestPayload.cs
</context>

## Artifacts This Phase Produces

- `sources/WebApp/UtfApi/GameServers/CloudEntry/CloudEntryRequestBuilder`
- `sources/WebApp/UtfApi/GameServers/CloudEntry/Options/OptionsRequestBuilder`
- `sources/WebApp/UtfApi/GameServers/CloudEntry/Arma3Server/Arma3ServerRequestBuilder`
- `sources/WebApp/UtfApi/GameServers/Item/CloudEntry/CloudEntryRequestBuilder`
- `sources/WebApp/UtfApi/GameServers/Item/CloudEntry/BillingOwner/BillingOwnerRequestBuilder`
- `sources/WebApp/UtfApi/GameServers/Item/CloudEntry/Readiness/ReadinessRequestBuilder`
- `sources/WebApp/UtfApi/Models/GuidedCloudEntryBillingOwnerDto`
- `sources/WebApp/UtfApi/Models/GuidedCloudEntryBillingOwnerSelectionPayload`
- `sources/WebApp/UtfApi/Models/GuidedCloudEntryOptionsDto`
- `sources/WebApp/UtfApi/Models/GuidedCloudEntryReadinessDto`
- `sources/WebApp/UtfApi/Models/GuidedCloudEntryReadinessPayload`
- `sources/WebApp/UtfApi/Models/GuidedCloudEntryServerOptionDto`
- `sources/WebApp/UtfApi/Models/GuidedCloudPlanningServerCreationPayload`

<tasks>

<task type="auto">
  <name>Task 1: Generate guided entry Kiota models and request builders</name>
  <files>.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-03-openapi-guided-entry.json, sources/WebApp/UtfApi/GameServers/**, sources/WebApp/UtfApi/Models/GuidedCloud*</files>
  <read_first>
    @sources/WebApp/UtfApi/GameServers/GameServersRequestBuilder.cs
    @sources/WebApp/UtfApi/GameServers/Item/GameServerItemRequestBuilder.cs
    @sources/WebApp/UtfApi/GameServers/Item/CloudRuntime/CloudRuntimeRequestBuilder.cs
    @sources/WebApp/UtfApi/Models/Arma3CloudDryRunPlanRequestPayload.cs
    @sources/WebApp/UtfApi/kiota-lock.json
    @sources/Api/Controllers/GameServersController.cs
  </read_first>
  <action>Generate Kiota artifacts for the Phase 33 endpoints implemented in Plan 02: `GET /game-servers/cloud-entry/options`, `POST /game-servers/cloud-entry/arma3-server`, `POST /game-servers/{gameServerId}/cloud-entry/billing-owner`, and `POST /game-servers/{gameServerId}/cloud-entry/readiness`. Prefer capturing the current API OpenAPI document into `33-03-openapi-guided-entry.json` and running the repository's existing Kiota generation workflow. If local hosting is blocked, create a minimal temporary OpenAPI document for only these Phase 33 routes, run Kiota into a temporary output directory, and copy only generated Phase 33 models/builders plus required property additions into `sources/WebApp/UtfApi`; do not manually invent request-builder code. Preserve unrelated generated client files and existing compatibility aliases. The generated client must expose `Client.GameServers.CloudEntry.Options.GetAsync`, `Client.GameServers.CloudEntry.Arma3Server.PostAsync`, `Client.GameServers[gameServerId].CloudEntry.BillingOwner.PostAsync`, and `Client.GameServers[gameServerId].CloudEntry.Readiness.PostAsync` or the exact Kiota-equivalent property names generated from those routes.</action>
  <verify>
    <automated>pwsh -NoProfile -Command "rg -n \"CloudEntry|GuidedCloud\" sources/WebApp/UtfApi/GameServers sources/WebApp/UtfApi/Models; if ($LASTEXITCODE -ne 0) { exit 1 }"</automated>
  </verify>
  <acceptance_criteria>
    - Source assertions: generated files have the Kiota auto-generated header/pattern and live under `sources/WebApp/UtfApi/**`; hand-written behavior remains outside generated files.
    - Behavior assertions: generated builders cover all four Phase 33 endpoints and generated models include all DTO/payload fields from Plan 01.
    - Boundary assertions: generated models do not introduce provider token, Steam credential, worker credential, authorization header, bearer token, raw metadata JSON, payment, invoice, checkout, tax, multi-provider, or multi-session-worker fields.
    - CLI output: `rg -n "CloudEntry|GuidedCloud" sources/WebApp/UtfApi/GameServers sources/WebApp/UtfApi/Models` prints Phase 33 generated builders/models.
  </acceptance_criteria>
  <done>Generated Kiota artifacts exist for all Phase 33 guided-entry API routes and models.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| API OpenAPI to generated client | Public API contracts become browser-consumable generated types. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-33-10 | Information Disclosure | Generated models | mitigate | Generated models mirror Plan 01 allowlisted DTOs only; verify no secret-bearing or payment/invoicing fields. |
| T-33-11 | Tampering | Generated route shape | mitigate | Use Kiota output from the OpenAPI route definitions rather than hand-written raw string paths. |
| T-33-SC | Tampering | Package supply chain | accept | No package-manager install is planned; Kiota generation uses existing tooling/patterns and no new dependency is added. |
</threat_model>

<verification>
Run `rg -n "CloudEntry|GuidedCloud" sources/WebApp/UtfApi/GameServers sources/WebApp/UtfApi/Models`.
Run a source check that generated models do not contain provider token, Steam credential, worker credential, authorization header, bearer token, payment, invoice, checkout, tax, multi-provider, or multi-session-worker fields.
Manual, authenticated browser, live Hetzner, Steam, Arma, and Phase 30 UAT are deferred evidence per 33-CONTEXT.md and must not block this plan.
</verification>

<success_criteria>
Generated Kiota artifacts are available for all Phase 33 guided-entry API actions, with generated-code changes separated from hand-written repository behavior.
</success_criteria>

<output>
Create `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-03a-SUMMARY.md` when done.
</output>
