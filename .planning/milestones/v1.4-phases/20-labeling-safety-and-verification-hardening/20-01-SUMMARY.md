# 20-01 Summary: Safe UTF Provider Labels For Hetzner Provisioning

## Status

Completed 2026-06-18.

## What Changed

- Added label dictionaries to Hetzner firewall, server, floating IP, and volume create request models.
- Posted `labels` in all Hetzner create request bodies.
- Added provisioning-time label generation from deterministic non-secret values:
  - provider identifier
  - cloud session id
  - game server id
  - billing account id
  - billing assignment id
  - inventory record id
  - resource kind
- Extended provisioning tests to capture all four provider create requests and assert every resource receives complete labels.
- Extended provider client tests to assert label payloads are posted and do not include tokens, bearer headers, password, secret, or user-data terms.

## Verification

Passed:

- `rg -n 'utf\.managed|utf\.session|utf\.game-server|CreateProviderLabels|\["labels"\] = request\.Labels|AssertProviderLabels' sources\Api\Services tests\Api.Tests\CloudProvisioning tests\Api.Tests\CloudProviders -g "*.cs"`
- `rg -n "new HetznerCloud(Firewall|Server|FloatingIp|Volume)CreateRequest" sources tests -g "*.cs"`
- `git diff --check`
- `dotnet test tests\X39.BI.Config.Parsing.Tests\X39.BI.Config.Parsing.Tests.csproj --no-restore --no-build --verbosity minimal` (89 passed)

Blocked by environment:

- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~HetznerCloudClientTests|FullyQualifiedName~CloudProvisioningServiceTests" --no-restore --verbosity minimal -p:NuGetAudit=false`
- Escalated rerun of the same command

Both targeted API test attempts failed before compiling changed code with `NU1301` socket-denied access to
`https://api.nuget.org/v3/index.json`.

## Notes

- Labels are generated from stable ids and enum values only; they do not include usernames, provider tokens,
  token references, bootstrap credentials, bootstrap user-data, or free-form planning details.
- HETZ-12 implementation is in place, but final requirement status remains pending until Phase 20 closeout records the
  complete verification matrix.

## Handoff

Next work is 20-02 redaction and read-only safety guards.
