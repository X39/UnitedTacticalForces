---
phase: 18-provider-reconciliation-runs-and-findings
plan: 01
subsystem: api
tags: [hetzner, provider-client, reconciliation, pagination, tests]

requires:
  - phase: 18-provider-reconciliation-runs-and-findings
    provides: Phase 18 plan context for read-only provider reconciliation.
provides:
  - Paged read-only Hetzner list methods for servers, floating IPs, volumes, and firewalls.
  - Normalized live resource models with provider id, kind, status, relationships, timestamps, and filtered labels.
  - Fake HTTP coverage for live resource pagination, mapping, bearer-header confinement, and GET-only calls.
  - Provider boundary reflection coverage including the new explicit read methods.
affects: [phase-18, provider-reconciliation, cloud-inventory, hetzner-provider-boundary]

tech-stack:
  added: []
  patterns: [paged provider reads, normalized provider resources, fake-http provider tests, filtered labels]

key-files:
  created:
    - sources/Api/Services/CloudProviders/Hetzner/Models/HetznerCloudLiveResourceModels.cs
  modified:
    - sources/Api/Services/CloudProviders/Hetzner/IHetznerCloudClient.cs
    - sources/Api/Services/CloudProviders/Hetzner/HetznerCloudClient.cs
    - tests/Api.Tests/CloudProviders/HetznerCloudClientTests.cs

key-decisions:
  - "Live provider resources are normalized before reconciliation rather than exposing raw provider payloads."
  - "Provider labels are filtered by key and value before entering live resource models."
  - "The list methods reuse the existing paged GET helper and stay separate from create/delete/detach methods."

patterns-established:
  - "Provider list methods should be tested with fake paged responses and explicit GET-only request assertions."
  - "Live resource models should carry provider ids as both numeric ids and stable string ids for inventory matching."

requirements-completed: [HETZ-09]

duration: not measured
completed: 2026-06-18
---

# Phase 18 Plan 01 Summary

**Hetzner live resource read boundary**

## Accomplishments

- Added `GetServersAsync`, `GetFloatingIpsAsync`, `GetVolumesAsync`, and `GetFirewallsAsync` to the Hetzner provider boundary.
- Implemented the four list methods through the existing paged GET helper against `servers`, `floating_ips`, `volumes`, and `firewalls`.
- Added normalized live resource records for server, IP address, volume, and firewall reconciliation inputs.
- Captured safe provider fields: ids, resource kind, display name/description, status, location/datacenter, IP/device details, attached server ids, creation time, and filtered labels.
- Extended provider tests with two-page fake responses for every list method and assertions for mapping, GET-only requests, no action/delete/detach paths, and bearer-token confinement.
- Updated provider boundary reflection coverage so the new read methods are explicit and expected.

## Files Created/Modified

- `sources/Api/Services/CloudProviders/Hetzner/Models/HetznerCloudLiveResourceModels.cs` - New normalized live resource records.
- `sources/Api/Services/CloudProviders/Hetzner/IHetznerCloudClient.cs` - Added read-only live resource list methods.
- `sources/Api/Services/CloudProviders/Hetzner/HetznerCloudClient.cs` - Implemented paged list methods, safe mapping, and label filtering.
- `tests/Api.Tests/CloudProviders/HetznerCloudClientTests.cs` - Added live list pagination/mapping/no-mutation coverage.

## Deviations from Plan

None.

## Issues Encountered

- SDK verification could not reach compilation in this environment. `dotnet test` and `dotnet build` both failed with `NU1301` while trying to load the NuGet service index for `https://api.nuget.org/v3/index.json`, with socket access denied for `api.nuget.org:443`. The same failure persisted when retried with escalated command permissions and with `NuGetAudit=false`.

## Verification

- `rg "GetServersAsync|GetFloatingIpsAsync|GetVolumesAsync|GetFirewallsAsync" sources/Api/Services/CloudProviders/Hetzner/IHetznerCloudClient.cs sources/Api/Services/CloudProviders/Hetzner/HetznerCloudClient.cs tests/Api.Tests/CloudProviders/HetznerCloudClientTests.cs` - passed.
- `rg "servers\\?page=1|floating_ips\\?page=1|volumes\\?page=1|firewalls\\?page=1|HttpMethod\\.Get|DoesNotContain\\(handler\\.Requests" tests/Api.Tests/CloudProviders/HetznerCloudClientTests.cs` - passed.
- `rg -i "password|secret|authorization|api key|apikey|user data|userdata" sources/Api/Services/CloudProviders/Hetzner/Models/HetznerCloudLiveResourceModels.cs` - passed with no matches.
- `git diff --check -- sources/Api/Services/CloudProviders/Hetzner/IHetznerCloudClient.cs sources/Api/Services/CloudProviders/Hetzner/HetznerCloudClient.cs sources/Api/Services/CloudProviders/Hetzner/Models/HetznerCloudLiveResourceModels.cs tests/Api.Tests/CloudProviders/HetznerCloudClientTests.cs` - passed with only repository LF-to-CRLF warnings.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter FullyQualifiedName~HetznerCloudClientTests --no-restore --verbosity minimal` - did not reach compilation because NuGet source/socket access returned `NU1301`.
- `dotnet build sources\Api\Api.csproj --no-restore --verbosity minimal` - did not reach compilation because NuGet source/socket access returned `NU1301`.
- `dotnet test tests\Api.Tests\Api.Tests.csproj --filter FullyQualifiedName~HetznerCloudClientTests --no-restore -p:NuGetAudit=false --verbosity minimal` - did not reach compilation because NuGet source/socket access returned `NU1301`.
- `dotnet build sources\Api\Api.csproj --no-restore -p:NuGetAudit=false --verbosity minimal` - did not reach compilation because NuGet source/socket access returned `NU1301`.

## User Setup Required

NuGet source access is required before SDK build/test commands can execute compilation in this environment.

## Next Plan Readiness

Plan 18-02 can add durable reconciliation run and finding storage using the new normalized provider read models as future comparison input.

## Self-Check: PASSED

Implementation matches plan scope and acceptance text checks. SDK verification awaits NuGet source access.

---
*Phase: 18-provider-reconciliation-runs-and-findings*
*Completed: 2026-06-18*
