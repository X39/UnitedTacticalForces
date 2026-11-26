# Phase 22 Validation Strategy

## Automated Gates

- `dotnet build sources/Api/Api.csproj --no-restore --verbosity minimal`
- Targeted `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CloudPolicy"` or narrower filters when the test project compiles.
- `node "$HOME/.codex/gsd-core/bin/gsd-tools.cjs" verify schema-drift 22`

## Required Coverage

- COST-15: Tests show a billable action receives a persisted policy decision before provisioning, cloud runtime start/restart/update, or future resize execution.
- COST-16: Tests cover allow, warn, approval-required, and deny gate outcomes.
- COST-17: Tests cover pending, approved, denied, expired, and wrong-scope approval requests.
- RELY-18: Tests prove direct provisioning/lifecycle/approval endpoint calls cannot bypass gate or authorization checks.

## Manual Or Operator-Gated Items

None expected for this backend phase. Live provider/browser UAT remains operator-gated future evidence.

## Known Caveat

The repository currently has unrelated `tests/Api.Tests` compile drift outside Phase 22. If targeted tests cannot run because those existing files fail first, record the exact drift and rely on API build plus code inspection until the unrelated test project drift is cleared.

