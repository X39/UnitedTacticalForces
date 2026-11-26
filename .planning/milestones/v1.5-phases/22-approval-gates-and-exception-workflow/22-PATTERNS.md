# Phase 22 Pattern Map

## Entity And Migration Patterns

- Follow `sources/Api/Data/Hosting/CloudSpendingPolicy.cs` and `CloudSpendingPolicyDecision.cs` for one public entity per file, XML docs, `PrimaryKey`, bounded strings, nullable FK navigation pairs, and restrict-delete relationships in `ApiDbContext`.
- Add contract enums under `sources/Contract/GameServer` with stable explicit values, matching Phase 21 enum tests.
- Add migrations under `sources/Api/Data/Migrations` and update `ApiDbContextModelSnapshot` through EF tooling.

## Service Patterns

- Place policy services under `sources/Api/Services/CloudPolicies`.
- Use `[Scoped<Implementation, Interface>]` attribute registration.
- Keep services server-side and provider-mutation-free unless they are explicit provisioning/teardown services.
- Reuse `CloudSpendingPolicyEvaluationService` for persisted decisions instead of reimplementing threshold math.
- Reuse `LifecycleFailureReasonSanitizer` for free-text reason sanitization.

## Controller Patterns

- `CloudSpendingPoliciesController` is the closest policy API analog: class-level `[Authorize]`, per-action `[Authorize(Claims.Administrative.Server)]`, actor lookup with `User.GetDatabaseUserAsync`, and `BadRequest` for validation exceptions.
- `CloudInventoryController` already owns cloud provisioning and teardown endpoints.
- `GameServersController.EnqueueLifecycleJobAsync` is the central helper for runtime lifecycle job creation. Gate there for cloud-session lifecycle actions so direct start/restart/update endpoint calls cannot bypass policy.

## DTO/Payload Patterns

- Add DTOs under `sources/Api/DTO` and payloads under `sources/Api/DTO/Payloads`.
- Add transform methods in `sources/Api/Transform.cs`.
- Keep raw secret/provider payloads out of DTOs.

## Test Patterns

- Existing Phase 21 policy tests under `tests/Api.Tests/CloudPolicies` use SQLite in-memory contexts and reflection checks for provider mutation boundaries.
- Controller tests can use fake service implementations and inspect authorization attributes, as in `CloudSpendingPolicyControllerTests`.
- Service tests should directly assert that blocked gates do not invoke fake provisioning/lifecycle/teardown services.

