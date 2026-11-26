# Phase 55: Generic Game Hosting Capability Contracts - Research

## RESEARCH COMPLETE

**Date:** 2026-07-03
**Mode:** Inline research because subagent spawning was not authorized in this Codex session.

## Scope

Phase 55 replaces Arma-3-specific public creation, cloud-entry, and cloud-planning contracts with generic supported-game hosting capability contracts. The clean-cut decision in `55-CONTEXT.md` means plans do not preserve legacy API routes, WebApp repository methods, DTO names, or persisted contract names for compatibility.

## Existing Implementation Seams

- `sources/Api/Services/GameServerController/IGameServerControllerCreatable.cs` already gives each controller a stable identifier and factory method. It can be extended or wrapped to expose a generic game capability projection.
- `sources/Api/Services/GameServerController/GameServerControllerFactory.cs` currently discovers controller identifiers only. It is the central seam for supported-game capability discovery.
- `sources/Api/Controllers/GameServersController.cs` owns current creation, cloud-entry, cloud-dry-run, lifecycle, cost, policy, and selected-server actions. It contains Arma-specific route segments that Phase 55 must remove or replace.
- `sources/Api/DTO/GameServerCreationControllerOptionDto.cs` and `sources/WebApp/Services/GameServerRepository/GameServerCreationControllerInfo.cs` currently expose `SupportsArma3GuidedCloud`. This is a direct target for layered generic capability data.
- `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs` and `GameServerRepositoryImpl.cs` expose Arma-specific public methods and a mix of generated Kiota calls plus custom route helpers. New generic API surface must be regenerated into `sources/WebApp/UtfApi` and consumed through repository facades.

## Contract Findings

- The capability contract should have stable core fields plus extension sections. Core fields should include game/controller identifier, display label, explicit hosting modes, and action descriptors.
- Hosting modes should be explicit values: `RecordOnly`, `LocalManaged`, and `CloudManaged`.
- Game actions should be server-specific descriptors behind a generic interface. The descriptor should contain only identifier, label, and risk marker. Unsupported actions are omitted rather than represented as unavailable.
- Server-scoped cloud planning should use generic routes such as `/game-servers/{id}/cloud-dry-run-plans`. The API derives the game type from the server record/capability; clients do not supply game identifiers for server-scoped actions.

## Clean-Cut Findings

- No deployed migration cost exists, so preserving Arma-specific API/WebApp/persisted contract names is counterproductive.
- Arma 3 remains the first supported capability implementation, but public contracts and active data shapes must be generic.
- Arma-specific branching is acceptable only inside genuine game implementation/adapters, not in generic API/WebApp contracts.
- Arma-specific runtime/profile/template types may be transformed aggressively when touched, even if that overlaps Phase 56.

## Testing Findings

- `tests/Api.Tests` already contains API contract, service, redaction, and WebApp source-boundary tests. It is the right place for capability projection, route, source-scan, and generated-client boundary tests.
- Existing tests currently assert old Arma-specific behavior, such as `SupportsArma3GuidedCloud`, `cloud-entry/arma3-server`, and `cloud-dry-run-plans/arma3`. Phase 55 must replace those with generic assertions.
- Generated-client verification should check that the Kiota regeneration produced request builders/models for the new generic API surface and that WebApp repositories use generated builders rather than handwritten route extensions.
- Source scans should fail old public Arma-specific route/method/DTO/persisted contract names in active API/WebApp code, with explicit allowlist entries only for genuine game implementation internals such as `Arma3GameServerController`.

## Risks

- The clean cut can expand the phase if runtime/profile/template names are deeply coupled to Arma DTOs and persisted entities.
- Kiota regeneration can produce broad generated-file churn; plans should isolate regeneration and verify unrelated generated paths are not accidentally broken.
- Existing UI tests and mock backend routes currently encode Arma-specific route names and will need updates or replacement.
- Aggressive source scans must be precise enough to allow implementation internals while rejecting public contract leakage.

## Recommendations

1. Start with backend capability contracts and controller/service projection, including route removal and generic replacement.
2. Regenerate Kiota after backend routes are stable, then update WebApp repository models and methods to use generated generic builders.
3. Finish with compatibility-cleanup and regression guards: API route tests, repository boundary tests, generated-client presence checks, and strict source scans.

