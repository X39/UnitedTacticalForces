---
phase: 33-guided-entry-ownership-and-scope-boundaries
plan: 03b
type: execute
wave: 4
depends_on:
  - 33-03a
files_modified:
  - sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs
  - sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
autonomous: true
requirements:
  - CREATE-01
  - CREATE-02
  - CREATE-03
  - CREATE-04
user_setup: []
must_haves:
  truths:
    - "The /servers UI can consume guided entry through IGameServerRepository, not raw browser HTTP."
    - "Repository methods preserve typed success, validation, and not-found behavior without exposing secret-bearing fields."
    - "Repository facade methods depend on generated Kiota artifacts from Plan 33-03a."
  artifacts:
    - path: "sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs"
      provides: "Repository facade methods for guided entry"
    - path: "sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs"
      provides: "Generated-client-backed implementation of guided entry calls"
  key_links:
    - from: "sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs"
      to: "sources/WebApp/UtfApi/GameServers/CloudEntry/Options/OptionsRequestBuilder.cs"
      via: "Client.GameServers.CloudEntry.Options.GetAsync"
      pattern: "CloudEntry.*Options"
---

<objective>
Expose the Phase 33 generated guided-entry client through `IGameServerRepository` methods, so the `/servers` UI can implement CREATE-01 through CREATE-04 without raw HTTP calls or direct Razor dependencies on generated builders.
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
@.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-03a-SUMMARY.md
@sources/WebApp/UtfApi/GameServers/GameServersRequestBuilder.cs
@sources/WebApp/UtfApi/GameServers/Item/GameServerItemRequestBuilder.cs
@sources/WebApp/UtfApi/GameServers/Item/CloudRuntime/CloudRuntimeRequestBuilder.cs
@sources/WebApp/UtfApi/Models/GuidedCloudEntryOptionsDto.cs
@sources/WebApp/UtfApi/Models/GuidedCloudEntryReadinessPayload.cs
@sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs
@sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
</context>

## Artifacts This Phase Produces

- `IGameServerRepository.GetGuidedCloudEntryOptionsAsync`
- `IGameServerRepository.CreateArma3GuidedCloudPlanningServerAsync`
- `IGameServerRepository.AssignGuidedCloudEntryBillingOwnerAsync`
- `IGameServerRepository.ValidateGuidedCloudEntryReadinessAsync`
- `GameServerRepositoryImpl.GetGuidedCloudEntryOptionsAsync`
- `GameServerRepositoryImpl.CreateArma3GuidedCloudPlanningServerAsync`
- `GameServerRepositoryImpl.AssignGuidedCloudEntryBillingOwnerAsync`
- `GameServerRepositoryImpl.ValidateGuidedCloudEntryReadinessAsync`

<tasks>

<task type="auto">
  <name>Task 1: Add guided entry repository facade methods</name>
  <files>sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs, sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs</files>
  <read_first>
    @sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs
    @sources/WebApp/Services/GameServerRepository/GameServerRepositoryImpl.cs
    @sources/WebApp/UtfApi/GameServers/Item/CloudRuntime/CloudRuntimeRequestBuilder.cs
    @sources/WebApp/UtfApi/Models/GuidedCloudEntryOptionsDto.cs
    @sources/WebApp/UtfApi/Models/GuidedCloudEntryReadinessPayload.cs
    @.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-PATTERNS.md
  </read_first>
  <action>Add repository interface methods `GetGuidedCloudEntryOptionsAsync(CancellationToken)`, `CreateArma3GuidedCloudPlanningServerAsync(GuidedCloudPlanningServerCreationPayload payload, CancellationToken)`, `AssignGuidedCloudEntryBillingOwnerAsync(long gameServerId, GuidedCloudEntryBillingOwnerSelectionPayload payload, CancellationToken)`, and `ValidateGuidedCloudEntryReadinessAsync(long gameServerId, GuidedCloudEntryReadinessPayload payload, CancellationToken)`. Implement them in `GameServerRepositoryImpl` by calling the generated Kiota request builders from Plan 33-03a through `Client.GameServers...`, returning the generated `GuidedCloudEntryOptionsDto`, `GuidedCloudEntryServerOptionDto`, `GameServerBillingAssignmentDto`, and `GuidedCloudEntryReadinessDto` types. Follow existing null handling: throw `InvalidOperationException` or use existing required-result semantics when a generated POST/GET unexpectedly returns null, and translate 404 to null only if the interface method explicitly returns nullable. Do not add `HttpClient.GetFromJsonAsync`, `PostAsJsonAsync`, `PostJsonRequiredAsync`, `BaseUrl.ResolveApiUrl`, or browser raw string paths for any guided-entry route.</action>
  <verify>
    <automated>dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal</automated>
  </verify>
  <acceptance_criteria>
    - Source assertions: `IGameServerRepository` exposes the four guided-entry methods and `GameServerRepositoryImpl` implements them through generated `Client.GameServers` request builders.
    - Boundary assertions: `rg -n "cloud-entry|GuidedCloudEntry" sources/WebApp/Services/GameServerRepository` shows facade methods, and a filtered check over matching lines shows no `GetFromJsonAsync`, `PostAsJsonAsync`, `PostJsonRequiredAsync`, or `BaseUrl.ResolveApiUrl`.
    - Behavior assertions: repository callers can load options, create an Arma 3 planning server, assign an owner, and validate readiness without importing generated builders directly into Razor.
    - Test command: WebApp project build passes.
  </acceptance_criteria>
  <done>WebApp guided entry calls are available through `IGameServerRepository` and use generated Kiota clients, with no new raw browser HTTP bridge.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Generated client to repository facade | Generated request builders are wrapped before Razor consumption. |
| Repository facade to Razor UI | UI code receives only typed repository outputs, not raw HTTP responses. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-33-12 | Information Disclosure | Razor access to generated client internals | mitigate | Expose all Phase 33 calls through `IGameServerRepository`; Plan 04 must not import generated request builders. |
| T-33-13 | Tampering | Repository route construction | mitigate | Use generated request builders instead of raw string paths so route shape stays tied to OpenAPI output. |
| T-33-14 | Information Disclosure | Repository errors | mitigate | Preserve existing expected API exception handling without surfacing secret-bearing payloads or headers. |
</threat_model>

<verification>
Run `dotnet build sources/WebApp/WebApp.csproj --no-restore --verbosity minimal`.
Run `rg -n "GuidedCloudEntry|GuidedCloudPlanning" sources/WebApp/Services/GameServerRepository`.
Run a filtered source check that fails if Phase 33 guided-entry repository methods use `GetFromJsonAsync`, `PostAsJsonAsync`, `PostJsonRequiredAsync`, `BaseUrl.ResolveApiUrl`, or raw `cloud-entry` strings outside generated client code.
Manual, authenticated browser, live Hetzner, Steam, Arma, and Phase 30 UAT are deferred evidence per 33-CONTEXT.md and must not block this plan.
</verification>

<success_criteria>
The WebApp repository facade exposes typed Phase 33 guided-entry operations backed by generated Kiota clients, preserving the no-raw-browser-HTTP boundary.
</success_criteria>

<output>
Create `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-03b-SUMMARY.md` when done.
</output>
