---
phase: 20
slug: labeling-safety-and-verification-hardening
status: draft
created: 2026-06-18
---

# Phase 20 - Patterns

## Provider Boundary

- Keep Hetzner API request/response records internal under `sources/Api/Services/CloudProviders/Hetzner/Models`.
- Use immutable record constructor parameters for create requests.
- Build JSON request bodies explicitly in `HetznerCloudClient` and remove null values before posting.
- Keep bearer tokens in request headers only; never copy them into request bodies, DTOs, audit records, or exception text.
- Filter provider label data with deny terms before exposing it through live-resource models.

## Provisioning

- Add labels at the `CloudProvisioningService` call site where session/resource correlation is known.
- Generate labels from ids and enum values only.
- Avoid display names, usernames, token references, bootstrap user-data, and free-form details JSON in labels.
- Continue saving inventory records after each successful provider create so partial failure cleanup has durable ids.

## Tests

- Extend existing fake clients and fake HTTP handlers rather than introducing new test infrastructure.
- Prefer assertions on exact provider request bodies/calls for safety-sensitive behavior.
- Use reflection/static guards for negative safety claims such as "reconciliation does not call delete/detach".
- Keep redaction tests broad enough to cover token, bearer, authorization, password, secret, bootstrap, and user-data terms.

## Planning And Closeout

- Keep OPS-02 as operator-gated evidence, not an automated pass.
- Record exact commands and whether failures are environmental `NU1301` restore failures.
- Update requirements only when the implementation and verification artifacts exist.
