# Phase 56 Pattern Map

**Phase:** 56-generic-runtime-template-and-plan-snapshots  
**Date:** 2026-07-04  
**Purpose:** Local implementation patterns to preserve while genericizing runtime/template/planning snapshots.

---

## Contract Patterns

- API controllers return DTOs and payloads, not EF entities.
- Server-scoped cloud planning derives game behavior from the persisted `GameServer` and capability metadata.
- Public user-facing DTOs, WebApp repository models, mapper names, generated-client expectations, and worker protocol envelopes should be generic.
- Arma-specific names remain valid only where they describe actual Arma implementation internals: the Arma controller, Arma executor, Arma configuration renderer, launch builder, and game-specific recipe body.
- WebApp Razor binds hand-written repository models. Generated Kiota DTOs are mapped inside repository/service layers.

---

## Persistence Patterns

- Hosting entities live under `sources/Api/Data/Hosting`.
- `ApiDbContext` exposes each aggregate as a `DbSet`.
- EF migrations live under `sources/Api/Data/Migrations` and must originate from EF tooling.
- Entity FKs use `DeleteBehavior.Restrict` for hosting/account/server/session references.
- String fields are bounded with `MaxLength`; snapshot JSON currently uses 4096-8192 character bounds.
- Current local-only migration state allows a clean rename/rebuild strategy, but generated migration output still needs review for FKs, indexes, bounds, and delete behavior.

---

## Selector Patterns

- Normal selectors return current enabled options.
- Superseded/disabled options are explicit admin/audit/repair inputs, not default user flow output.
- Option groups should carry stable generic fields: id, label, version, current/enabled state, game type, summary, and validation state.
- Game-specific detail sections are safe typed details, not arbitrary raw JSON.
- Filtering happens on the API side using the server/capability game type.

---

## Dry-Run And Review Patterns

- Dry-run is a provider mutation, billing, cost, policy, and readiness gate.
- Runtime recipe correctness is reviewed separately from provider plan staleness.
- Provider-impacting fields block provisioning until regeneration: provider, cost, owner, policy, template, storage, exposed ports, SSH/admin CIDR, provider metadata, selected game, and hosting mode.
- Runtime-content fields require review/acknowledgement when changed: mission, mod pack revision, config preset, launch recipe, and game-specific execution details that do not affect provider assumptions.
- Provider validation stays in provider services/adapters; do not collapse Hetzner-specific checks into fake generic validation.

---

## Worker Patterns

- `CloudWorkerRuntime` owns bootstrap, credential storage, registration, heartbeat, claim loop, progress/log/status reporting, and recovery.
- The API prepares worker recipes. Workers execute allowlisted game-specific recipes and should not infer plan, policy, cost, or selector state.
- Claim payloads should use a generic recipe envelope with selected game, recipe kind/version, safe labels, and one known game-specific body.
- Arma 3 may remain the only executor in this phase if the generic recipe boundary is explicit and tested.

---

## Redaction And Security Patterns

- Do not expose Steam credentials, provider tokens, worker credentials, bearer headers, cookies, raw request payload dumps, or secret configuration values in DTOs, frontend models, audit text, logs, snapshots, or planning artifacts.
- Use allowlisted DTOs for selector details, review summaries, worker recipes, and debug/operator details.
- Keep generated DTOs out of Razor and fail closed if WebApp redaction guards reject display values.
- Source scans should use narrow allowlists so genuine Arma internals do not hide public contract leaks.

---

## Test Patterns

- Use focused `dotnet test tests/Api.Tests/Api.Tests.csproj --filter ...` commands for API, worker, and WebApp boundary checks.
- Use `dotnet test tests/Ui.Tests/Ui.Tests.csproj --filter ...` only when a plan changes mock backend or browser-level behavior.
- Run `dotnet test X39.UnitedTacticalForces.slnx` before phase verification when feasible.
- Boundary tests are acceptable for generated-client and source-scan assertions, especially around public Arma marker removal.
