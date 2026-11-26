# Phase 29 Research: Operator Gate Preparation And Live Plan Approval

## Existing Behavior

The project already has a live Hetzner UAT runbook at `docs/operations/hetzner-live-uat.md`. That runbook is intentionally approval-gated because it can create billable resources. It requires a disposable or explicitly approved Hetzner project, a server-side token reference, verified provider configuration, fresh metadata, one active billing owner, and an administrator before provisioning.

The earlier v1.2 acceptance artifact `.planning/v1.2-OPERATOR-GATE-ACCEPTANCE.md` explicitly states that OPS-02 was deferred, not passed. The missing evidence included authenticated administrator and server-owner browser sessions, representative cloud data, live Hetzner approval, controlled create/delete evidence, and final provider-side billable resource count `0`.

The existing v1.2 UAT artifact `.planning/milestones/v1.2-phases/12.2-address-v1-2-audit-tech-debt/12.2-UAT.md` is a useful precedent for recording anonymous browser/API probes, protected API redirects, automated non-mutating test results, live-provider non-execution rationale, and operator checklists without leaking secrets.

The current codebase already exposes the surfaces Phase 29 must prepare for:

- Hetzner provider status, verification, metadata sync, and metadata reads through `HetznerProviderController` and `CloudAdminRepository`.
- Admin cloud operations through `/servers/cloud`.
- Server owner/admin status through `/servers`.
- Billing ownership through game-server billing owner endpoints and UI.
- Dry-run plan creation and details through game-server and cloud-inventory dry-run endpoints.
- Cost, spending policy, reconciliation, and audit visibility through cloud inventory, cloud policies, billing, and game-server APIs.

## Implementation Direction

Phase 29 should create current v1.7 evidence artifacts that can be filled by an operator before Phase 30 performs any real provider mutation. The artifact should be reusable across Phase 30 and must separate:

- approval record,
- local environment and authenticated access,
- provider configuration and metadata freshness,
- test game server and billing owner,
- dry-run plan and cost estimate,
- spending policy and reconciliation status,
- redaction review,
- final pass/fail classification.

The approval artifact must store only token references and IDs safe for planning evidence. It must never store raw Hetzner tokens, Steam credentials, worker bootstrap credential values, worker credential hashes, authorization headers, bearer tokens, session cookies, or unredacted provider error payloads.

The plan should preserve the cost-stop boundary from the runbook: provider deletion, not power-off, is required before OPS-02 can pass.

## Safety Notes

- Autonomous execution may create templates and record missing preconditions, but it must not approve a live run, create provider resources, start provider teardown, or claim live/browser UAT as passed without operator evidence.
- Any browser evidence must describe roles and visible state without storing session cookies, auth headers, or bearer material.
- Any provider evidence must use `TokenReference` only.
- Any blocked status must be explicit. A blocked operator gate is safer than fabricated approval or pass evidence.

## Validation Architecture

Phase 29 is primarily evidence and operational documentation. Automated verification should check file existence and required headings rather than live provider behavior. Manual verification remains required for authenticated browser roles, provider metadata freshness, approval values, and any real UAT readiness.

Recommended automated checks:

- `Test-Path .planning/v1.7-LIVE-UAT-EVIDENCE.md`
- `Select-String -Path .planning/v1.7-LIVE-UAT-EVIDENCE.md -Pattern "Approval Record","Forbidden Evidence Content","Final Classification"`
- `Select-String -Path docs/operations/hetzner-live-uat.md -Pattern "v1.7","provider-side billable resource count"`

Manual checks:

- Administrator and server-owner sessions exist and are authenticated through Steam or Discord.
- Provider configuration is enabled and verified through a token reference only.
- Metadata sync source timestamp is fresh and matches the selected dry-run plan metadata timestamp.
- The selected test game server has exactly one active billing owner.
- The approval record contains approver, approved project, resource shape, budget ceiling, lifetime, cleanup window, game server, billing account, and dry-run plan identifiers before Phase 30 provisioning.

## Research Complete

The phase can be planned as three sequential documentation/evidence waves. The first wave is fully automatable. Later waves may produce a blocked or human-needed verification status if authenticated sessions, provider credentials, approved project, or operator approval are unavailable.
