# Stack Guidance: v1.8 Cloud Server Creation Experience

**Project:** United Tactical Forces  
**Milestone:** v1.8 Cloud Server Creation Experience  
**Researched:** 2026-06-19  
**Confidence:** HIGH for repo-derived recommendations; MEDIUM for external package version guidance.

## Summary

Use the existing Blazor WebAssembly, Fluent UI, Kiota, repository facade, and API control-plane stack. v1.8 does not need a new frontend framework, state-management library, cloud SDK, payment dependency, or backend orchestration dependency. The missing stack work is a targeted generated-client refresh, repository facade coverage for the guided workflow, and a small Blazor component test project.

The current WebApp already has Fluent UI wizard/form primitives, generated Kiota clients, cookie-aware `HttpClient` wiring, and cloud admin/server repository facades. Productizing cloud creation should be implemented as a server-oriented Blazor workflow over those facades, not as a separate browser HTTP bridge.

## Existing Stack To Reuse

| Area | Reuse | Why |
|------|-------|-----|
| Runtime | `net9.0`, Blazor WebAssembly, ASP.NET Core API | Matches all active projects and keeps Aspire/PostgreSQL/API/WebApp local development intact. |
| UI library | `Microsoft.FluentUI.AspNetCore.Components` 4.11.8 plus Icons/Emoji 4.11.8 | Already registered in `sources/WebApp/Program.cs`; existing pages use `FluentWizard`, `FluentEditForm`, `FluentDataGrid`, `FluentMessageBar`, dialogs, icons, inputs, and progress controls. |
| Generated API client | `Microsoft.Kiota.Bundle` 1.22.2 and `sources/WebApp/UtfApi/**` | `RepositoryBase` already creates a Kiota `ApiClient` with the app's cookie-aware `HttpClient`. Keep generated code isolated under `UtfApi`. |
| Client abstraction | `IGameServerRepository` and `ICloudAdminRepository` | These are the correct integration boundaries for server-scoped owner/runtime/cost/policy actions and admin cloud metadata/inventory actions. |
| Cloud backend | Existing `CloudPlanningController` and `CloudInventoryController` | Server-scoped Arma 3 dry-run planning, dry-run details, provisioning, teardown, cost visibility, metadata, policy, and reconciliation endpoints already exist. |
| Form pattern | Existing `/events/create` wizard | It proves the app already supports `FluentWizard` + `FluentEditForm` + DataAnnotations-style validation without adding a forms package. |

## Needed Additions Or Changes

### 1. Refresh The Generated Kiota Client

Regenerate `sources/WebApp/UtfApi/**` from the current API OpenAPI document before implementing the v1.8 UI. The current generated client has useful cloud routes, including:

- `Client.GameServers[gameServerId].CloudDryRunPlans.Arma3.PostAsync(...)`
- `Client.CloudInventory.DryRunPlans[planId].Provision.PostAsync(...)`
- `Client.CloudInventory.Sessions[sessionId].DeleteAsync(...)`

But the checked generated client does **not** currently expose:

- `CloudSpendingPolicyGateResultDto`
- `acknowledgedDecisionId` / `approvalRequestId` query parameters on provisioning or teardown request builders

Those are required for a guided review/provision/teardown workflow that can show policy gate results and then retry with acknowledgement or approval. If regeneration still omits them, fix the API OpenAPI metadata before writing UI code. Do not patch generated files by hand.

### 2. Extend Repository Facades, Not Pages With Raw HTTP

Add methods to existing facades:

- `IGameServerRepository.CreateArma3CloudDryRunPlanAsync(...)`
- `ICloudAdminRepository.ProvisionDryRunPlanAsync(planId, acknowledgedDecisionId, approvalRequestId, ...)`
- `ICloudAdminRepository.TeardownSessionAsync(sessionId, acknowledgedDecisionId, approvalRequestId, ...)`

Use a small WebApp-owned result shape for policy-gated actions, for example `CloudActionResult<TSuccess>`, with `Success`, `PolicyGate`, `BadRequest`, and `NotFound` states. This keeps pages simple and avoids leaking Kiota exceptions or generated DTOs into workflow logic.

Continue mapping generated DTOs into `CloudAdminModels` inside `CloudAdminRepositoryImpl`. Pages should consume stable WebApp model types except for short-lived creation payloads where the repository method owns the conversion.

### 3. Build The Workflow With Existing Fluent UI Components

Recommended page/component shape:

- Add a focused route such as `/servers/cloud/create` or a selected-server creation panel from `/servers`.
- Use a linear `FluentWizard` for: server/owner, provider shape, runtime inputs, dry-run review, policy/approval, provision/operate.
- Use `FluentEditForm`, `EditContext`, `DataAnnotationsValidator`, and `ValidationMessageStore` for step-local validation and API validation failures.
- Use `FluentSelect`/`FluentOption` or existing autocomplete services for server type, location/datacenter, image, billing owner, modpack, and terrain/runtime selections.
- Use `FluentNumberField` for ports/storage, `FluentCheckbox` for SSH/exposure flags, `FluentMessageBar` for validation/policy/cost warnings, `FluentProgress` for async actions, and `FluentDialogService` for provision/teardown confirmations.
- Reuse existing status-badge and table patterns from `Pages/Servers/Index.razor` and `Pages/Servers/Cloud.razor`; do not introduce a second design system.

## Generated Client Impact

Required verification after regeneration:

| Check | Expected Result |
|-------|-----------------|
| `rg "CloudSpendingPolicyGateResult" sources/WebApp/UtfApi` | Finds generated model and request/error mapping support, or the API OpenAPI metadata needs repair. |
| `rg "acknowledgedDecisionId|approvalRequestId" sources/WebApp/UtfApi` | Finds query parameters for provision/teardown request builders. |
| `rg "CloudDryRunPlans.*Arma3|Arma3CloudDryRunPlanRequestPayload" sources/WebApp/UtfApi` | Confirms server-scoped dry-run planning remains generated. |
| `dotnet build X39.UnitedTacticalForces.slnx` | Confirms regenerated client and facade mappings compile. |

Do not upgrade to `Microsoft.Kiota.Bundle` 2.0.0 as part of v1.8 unless regeneration or compilation requires it. The current 1.22.2 package is already newer than older project notes and is sufficient for the existing generated client pattern.

## Testing/Verification

Add a small WebApp component test project only if v1.8 implements the guided workflow as planned:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="bunit" Version="2.7.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\sources\WebApp\WebApp.csproj" />
  </ItemGroup>
</Project>
```

Recommended component tests:

- Wizard disables dry-run/provision until required owner, server, provider, port, and runtime inputs are valid.
- Dry-run review renders planned resources, validation failures, metadata freshness, estimates, assumptions, and cleanup warnings.
- Policy gate result renders as warning/blocked/approval-required without provisioning success text.
- Acknowledged/approved retry passes the expected ids into the repository facade.
- Cost labels distinguish estimated, reconciled, and adjusted values.
- Rendered output does not include provider tokens, Steam credentials, worker credentials, bearer/session secrets, or raw secret-like failure details.

Keep existing `tests/Api.Tests` coverage for API/controller/service behavior. Add API tests only where the WebApp contract depends on OpenAPI shape, especially policy gate response metadata and query parameters.

For browser verification, use Aspire plus an authenticated manual/browser smoke path for v1.8 evidence. Do not add Playwright by default. If the milestone explicitly requires automated browser E2E, add a separate `tests/WebApp.E2E` project with `Microsoft.Playwright.Xunit` 1.60.0 so browser-driver setup does not slow normal unit/component tests.

## Explicit Non-Additions

| Do Not Add | Rationale | Use Instead |
|------------|-----------|-------------|
| React, Angular, or another SPA shell | The app is already Blazor WebAssembly and has existing auth/client/service wiring. | Blazor components and existing repositories. |
| MudBlazor, Radzen, Bootstrap wizard packages, or custom JS widgets | Fluent UI is already installed, registered, and used for wizard/data/form surfaces. | Existing Fluent UI components. |
| Fluxor/Redux/global client-state library | The workflow is linear and server-backed; durable truth lives in API/PostgreSQL. | Component-local view model plus repository refreshes. |
| Browser-side Hetzner SDK or provider calls | Provider tokens and mutation must remain server-side. | Existing API provider services and DTOs. |
| Raw browser HTTP bridge for cloud actions | v1.2+ deliberately moved cloud visibility to generated Kiota/repository patterns. | Regenerated Kiota client and repository facade methods. |
| Payment, invoicing, or checkout packages | CR-0001 billing is internal attribution, not payment collection. | Existing billing owner/cost/reconciliation views. |
| Fluent UI 4.14.2 upgrade as a v1.8 prerequisite | NuGet shows it exists and supports net9.0, but current 4.11.8 already has needed wizard/form/grid components. Upgrading Microsoft UI packages should be separate dependency hygiene. | Stay on 4.11.8 unless a verified component bug blocks the workflow. |
| Kiota 2.0.0 upgrade as a v1.8 prerequisite | NuGet shows 2.0.0 exists, but the current WebApp uses 1.22.2 successfully. Major generator/runtime churn is avoidable unless required by regeneration. | Regenerate with the current repo toolchain first. |
| Fluent DataGrid EF adapter | WebApp receives materialized DTO collections from API; EF adapter is for server-side EF query integration. | Existing `Items="@rows.AsQueryable()"` or plain tables where the page already uses them. |

## Sources/Evidence

Repo evidence:

- `sources/WebApp/WebApp.csproj` currently targets `net9.0` and references Fluent UI 4.11.8, Icons/Emoji 4.11.8, and `Microsoft.Kiota.Bundle` 1.22.2.
- `sources/WebApp/Program.cs` registers `AddFluentUIComponents`, localization, auth core, cookie-aware named `HttpClient`, and attributed services.
- `sources/WebApp/Services/RepositoryBase.cs` wraps the generated Kiota `ApiClient` over the existing `HttpClient`.
- `sources/WebApp/Pages/Events/Create/Index.razor` uses `FluentWizard`, `FluentEditForm`, validators, autocomplete, number, checkbox, and upload controls.
- `sources/WebApp/Pages/Servers/Index.razor` and `sources/WebApp/Pages/Servers/Cloud.razor` already render lifecycle, cloud status, cost, policy, reconciliation, and cleanup risk using Fluent UI and repository facades.
- `sources/Api/Controllers/CloudPlanningController.cs` exposes server-scoped Arma 3 dry-run planning.
- `sources/Api/Controllers/CloudInventoryController.cs` exposes dry-run inventory, details, provisioning, teardown, cost visibility, reconciliation, and policy-gated mutation endpoints.
- `rg "CloudSpendingPolicyGateResult" sources/WebApp/UtfApi` returned no generated WebApp model on 2026-06-19; this is the main generated-client gap for v1.8.

External/current evidence:

- NuGet lists `Microsoft.FluentUI.AspNetCore.Components` 4.14.2 and marks net9.0 compatible: https://www.nuget.org/packages/Microsoft.FluentUI.AspNetCore.Components/
- Fluent UI Blazor docs include `FluentWizard` for step-based UI: https://fluentui-blazor.azurewebsites.net/Wizard
- Microsoft Learn says Kiota .NET clients need `Microsoft.Kiota.Bundle`: https://learn.microsoft.com/en-us/openapi/kiota/dependencies
- NuGet lists `Microsoft.Kiota.Bundle` 2.0.0 as current, but v1.8 does not require a major upgrade: https://www.nuget.org/packages/Microsoft.Kiota.Bundle/
- Microsoft Learn identifies bUnit as the common Blazor component testing approach, while noting it is third-party: https://learn.microsoft.com/en-us/aspnet/core/blazor/test
- NuGet lists `bunit` 2.7.2: https://www.nuget.org/packages/bunit/
- Playwright .NET supports xUnit/MSTest/NUnit templates, but should be separate if automated browser E2E becomes explicit: https://playwright.dev/dotnet/docs/intro
