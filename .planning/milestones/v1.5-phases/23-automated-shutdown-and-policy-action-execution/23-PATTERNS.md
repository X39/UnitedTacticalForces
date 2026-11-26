# Phase 23 Pattern Map

## Entity Patterns

- Follow `CloudSpendingPolicyDecision` and `CloudSpendingPolicyApprovalRequest` for Hosting entities.
- Use `[Index]` attributes plus explicit `ApiDbContext` restrict-delete relationships.
- Keep raw provider payloads out of persisted action records.

## Service Patterns

- Place automatic-action services under `sources/Api/Services/CloudPolicies`.
- Use `[Scoped<Implementation, Interface>]`.
- Depend on `ApiDbContext`, `IGameServerLifecycleJobService`, and `ICloudTeardownService` only in the action execution service, not in evaluation/gate services.
- Use `LifecycleFailureReasonSanitizer` for persisted failure details.

## Controller Patterns

- Admin policy controller can host a manual processing endpoint if needed.
- Return DTOs from `Transform.cs`; do not expose secret fields.

## Test Patterns

- Use SQLite in-memory contexts for restart-safe persistence tests.
- Use fake lifecycle/teardown services to assert exactly what gets called.
- Keep direct provider mutation tests fake-only; do not call live provider APIs.
