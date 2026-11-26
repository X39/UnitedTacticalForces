# Phase 41: Cloud Template Domain And Audit Foundation - Context

**Gathered:** 2026-06-21
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Administrators can manage versioned cloud server templates that define the supported Arma 3 cloud shapes users may choose.

Implementation boundaries from ROADMAP:

- Do not run live provider UAT.
- Do not create provider resources as part of template management.
- Do not expose provider tokens, Steam credentials, worker credentials, cookies, authorization headers, bearer tokens, or raw secret-bearing payloads.

Success criteria:

1. Cloud server templates are persisted with stable id, version, game type, provider, enabled state, provider shape defaults, storage/port/runtime bounds, and update history.
2. Template create, update, disable, and inspect operations are permission checked and audited.
3. Disabled or superseded template versions remain available for historical dry-run/provisioning snapshots.
4. Template projections expose only allowlisted fields needed by administrators and authorized creators.
5. Tests cover persistence, versioning, authorization, audit records, and redaction.

</domain>

<decisions>
## Implementation Decisions

### the agent's Discretion
All implementation choices are at the agent's discretion - discuss phase was skipped per user setting. Use ROADMAP phase goal, success criteria, existing codebase conventions, and security constraints to guide decisions.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- API persistence belongs under `sources/Api/Data`, with hosting-related entities under `sources/Api/Data/Hosting`.
- HTTP surfaces belong in API controllers under `sources/Api/Controllers`, with DTOs under `sources/Api/DTO`.
- Authorization-sensitive behavior should reuse the existing policy/requirement pattern under `sources/Api/Authorization`.
- WebApp-facing consumption should later use generated Kiota clients wrapped by repository facades rather than raw browser HTTP.

### Established Patterns
- EF entities use `PrimaryKey` for primary keys and `Fk` suffixes for foreign keys.
- API controllers perform authorization checks, EF projection, and DTO shaping directly in the current brownfield style.
- Secrets and raw credential-bearing data must be excluded from DTOs, logs, audit projections, and planning artifacts.

### Integration Points
- `ApiDbContext` is the persistence registration point for new template entities.
- Existing cloud planning, provisioning, cost, policy, reconciliation, and audit flows are the downstream consumers that later phases will make template-aware.
- Existing GSD boundaries keep live Hetzner, Steam, Arma, authenticated browser, teardown, and cleanup evidence deferred until CR-0001 implementation is otherwise complete.

</code_context>

<specifics>
## Specific Ideas

No specific requirements - discuss phase skipped. Refer to ROADMAP phase description and success criteria.

</specifics>

<deferred>
## Deferred Ideas

None - discuss phase skipped.

</deferred>
