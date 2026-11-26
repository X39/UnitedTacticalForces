# 19-03 Summary: Admin And Server-Owner WebApp Visibility

## Status

Completed 2026-06-18.

## What Changed

- Extended `/servers/cloud` with cost and reconciliation summary cards:
  - Estimated runtime cost
  - Accumulated estimate
  - Latest reconciliation status
  - Finding/orphan counts
  - Cleanup risk
- Added cost source, accumulated estimate, and cleanup risk columns to the admin cloud sessions table.
- Added a reconciliation tab with runs, live provider counts, finding counts, orphan counts, and cleanup-needed billable counts.
- Added session detail cost fields, metadata freshness, confidence, billable resources, cleanup risk, and cost snapshot refresh.
- Added selected-server cost and reconciliation visibility to `/servers`, including source label, current/accumulated estimates, freshness, confidence, billable resource count, scoped finding count, cleanup risk, and snapshot refresh.
- Added copy clarifying that provider teardown stops billable resources while runtime cleanup does not delete provider resources.

## Verification

Passed:

- `rg -n "Estimated runtime cost|Reconciled runtime cost|Adjusted runtime cost|Provider teardown|Runtime cleanup|Accumulated estimate|Cost source|Metadata freshness|Confidence|Billable resources|Cleanup risk" sources/WebApp/Pages/Servers`
- `rg -n "Actual cost|Invoice total|Billed amount|Guaranteed cost" sources/WebApp/Pages/Servers` returned no matches.
- `rg -n "HttpClient|GetFromJsonAsync|PostAsJsonAsync|ResolveApiUrl\(.*cost|ResolveApiUrl\(.*reconciliation" sources/WebApp/Pages/Servers/Cloud.razor sources/WebApp/Pages/Servers/Index.razor` returned no matches.
- `dotnet test tests\X39.BI.Config.Parsing.Tests\X39.BI.Config.Parsing.Tests.csproj --no-restore --no-build --verbosity minimal` (89 passed)

Blocked by environment:

- `dotnet build sources\WebApp\WebApp.csproj --no-restore --verbosity minimal -p:NuGetAudit=false`
- Escalated rerun of the same build

Both WebApp build attempts failed before compiling changed WebApp code with `NU1301` socket-denied access to `https://api.nuget.org/v3/index.json`.

## Notes

- Owner `/servers` visibility uses the server-scoped repository endpoint and filters displayed findings to the selected server/session scope.
- Admin `/servers/cloud` shows orphan indicators and finding details from admin-scoped reconciliation data.
- Browser UAT remains operator-gated because authenticated sessions and representative live cloud/runtime data are not available in this environment.

## Handoff

Phase 19 is complete. Next work is Phase 20 planning for provider labeling, safety, and verification hardening.
