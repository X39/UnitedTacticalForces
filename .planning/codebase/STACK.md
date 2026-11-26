---
last_mapped_commit: 6c393fc7bef9e4c6086a43628f86979fc5aec7ce
last_mapped_at: 2026-11-18
---
# Technology Stack

**Analysis Date:** 2026-11-18

## Languages

**Primary:**
- C# on .NET 10.0 (`net10.0`) - every project under `sources/` and `tests/` (`sources/Api/Api.csproj`, `sources/CloudWorker/CloudWorker.csproj`, `sources/Database/Database.csproj`, etc.)
- TypeScript 6.0.3 / React 19.2.8 - new frontend in `sources/ReactApp/` (`sources/ReactApp/package.json`)

**Secondary:**
- Razor / Blazor WebAssembly - legacy-but-active client in `sources/WebApp/`
- Shell / Python - Mozaic pipeline scripts in `.mozaic/` (`build.sh`, `check-*.sh`, `stage-browser-deps.py`, `verify-test-results.py`, `test_*.py`)
- PowerShell - host setup scripts in `deploy/host/` (`Initialize-Host.ps1`, `Add-OrgKeyVariable.ps1`, `Set-DeployKeyVariable.ps1`)
- YAML - `.github/workflows/dotnet.yml`, `.mozaic/pipeline.yml`, `.mozaic/release.yml`
- JSON (with comments) - `sources/Api/appsettings*.json`, `sources/AppHost/appsettings*.json`, `sources/WebApp/wwwroot/appsettings.json`, `deploy/production.json`, `deploy/webapp.appsettings.json`

## Runtime

**Environment:**
- .NET 10 SDK/runtime; ASP.NET Core (`Microsoft.NET.Sdk.Web`) for `sources/Api`
- Blazor WebAssembly (`Microsoft.NET.Sdk.BlazorWebAssembly`) for `sources/WebApp`
- .NET Aspire AppHost (`Aspire.AppHost.Sdk`) for `sources/AppHost`
- Node.js `>=24 <25`, npm `>=11` for `sources/ReactApp` (engines in `package.json`)
- Cloud worker container: `mcr.microsoft.com/dotnet/runtime:10.0` (digest-pinned) plus SteamCMD, built from `sources/CloudWorker/Dockerfile`
- Production containers run under rootless Podman with systemd user units (`deploy/host/utf-api.service`, `deploy/host/utf-web.service`)

**Package Manager:**
- NuGet via SDK-style `.csproj`; lockfile: missing (no `packages.lock.json`)
- npm; lockfile: present (`sources/ReactApp/package-lock.json`)

## Frameworks

**Core:**
- ASP.NET Core 10.0.9 - HTTP API, controllers, auth, OpenAPI (`sources/Api/Program.cs`)
- Entity Framework Core 10.0.9 + Npgsql 10.0.2 (+ NodaTime plugin) - persistence (`sources/Database/DatabaseContext.cs`, `sources/Database/Migrations/`)
- NodaTime 3.3.2 + `NodaTime.Serialization.SystemTextJson` 1.4.0 - time types
- Blazor WebAssembly 10.0.9 + Microsoft Fluent UI Blazor 4.14.3 - `sources/WebApp`
- React 19 + Fluent UI React v9 (`@fluentui/react-components` 9.74.6), React Router 7.18, TanStack Query 5.101, react-hook-form 7.85 + zod 4.4, i18next 26 - `sources/ReactApp`
- .NET Aspire 13.4.6 - local orchestration incl. `Aspire.Hosting.JavaScript` (`AddViteApp("react-app", "../ReactApp")` in `sources/AppHost/Program.cs`)
- Pidgin 3.5.1 - BI config parser (`sources/X39.BI.Config.Parsing`)

**Testing:**
- xUnit 2.9.3 + `xunit.runner.visualstudio` 3.1.5, Microsoft.NET.Test.Sdk 18.7.0, coverlet.collector 10.0.1 - `tests/Api.Tests`, `tests/CloudWorker.Tests`, `tests/Ui.Tests`, `tests/X39.BI.Config.Parsing.Tests`
- `Microsoft.EntityFrameworkCore.Sqlite` 10.0.9 + `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3 - in-process DB for tests
- Microsoft.Playwright 1.61.0 - browser UI tests (`tests/Ui.Tests`)
- Vitest 4.1, Testing Library, jsdom 28, msw 2.12, `@playwright/test` 1.62 - `sources/ReactApp`
- Python unittest-style scripts for pipeline/image checks - `.mozaic/test_*.py`

**Build/Dev:**
- .NET CLI; solution `X39.UnitedTacticalForces.slnx`
- Vite 8.2 + `@vitejs/plugin-react`, TypeScript `tsc -b`, ESLint 9, Prettier 3.9 - `sources/ReactApp`
- `openapi-typescript` 7.13 - generates `sources/ReactApp/src/generated/api.ts` from `openapi/utf-api.json`
- Microsoft Kiota 2.0.0 - generated C# client in `sources/WebApp/UtfApi/`
- `Microsoft.EntityFrameworkCore.Tools` 10.0.9 - migrations (`dotnet ef migrations add ...`, see `AGENTS.md`)
- Microsoft.AspNetCore.OpenApi 10.0.9, Microsoft.OpenApi 2.7.5, Scalar.AspNetCore 2.16.5 - OpenAPI doc + dev UI
- Containerfiles: `.mozaic/dotnet-release.Containerfile`, `.mozaic/react-browser.Containerfile`, `.mozaic/native-package-ownership.Containerfile`, `sources/CloudWorker/Dockerfile`

## Key Dependencies

**Critical:**
- `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2 - PostgreSQL provider (`UseNpgsql` in `sources/Api/Program.cs`)
- `AspNet.Security.OpenId.Steam` 10.0.0 / `AspNet.Security.OAuth.Discord` 10.0.0 - login providers (`sources/Api/Program.cs`)
- `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.9 - token auth in the API
- `Discord.Net` 3.20.1 - bot (`sources/Api/HostedServices/DiscordBot.cs`)
- `SteamWebAPI2` 5.0.0 - Steam profile lookup (`sources/Api/Services/SteamRepository.cs`)
- `X39.Util.DependencyInjection` 3.0.0.10 - attribute DI (`AddAttributedServicesFromAssemblyOf`)
- `MessagePack` 3.1.7 - binary serialization (API side)
- `Magick.NET-Q8-AnyCPU` 14.15.0 - image processing (media assets / site images)

**Infrastructure:**
- `Aspire.Hosting.AppHost` / `.PostgreSQL` / `.JavaScript` 13.4.6 - `sources/AppHost`
- `Microsoft.Extensions.Http.Resilience` 10.7.0, `Microsoft.Extensions.ServiceDiscovery` 10.7.0, `OpenTelemetry.*` 1.15-1.16 - `sources/ServiceDefaults/Extensions.cs`
- `HtmlAgilityPack` 1.12.4 - Arma 3 launcher HTML import (`sources/Api/Services/GameServerDefinition/Arma3LauncherHtmlFormatHandler.cs`)
- `Markdig` 1.3.2 - Markdown in WebApp; `react-markdown` + remark/rehype plugins in ReactApp
- `X39.Util` 1.0.0.56, `X39.Util.Blazor.WebAssembly` 1.0.1.7

## Configuration

**Environment:**
- API: `appsettings.json` + environment files + optional `appsettings.secret.json` / `appsettings.user.json` (loaded in `sources/Api/Program.cs`; secret file exists, excluded from publish, never read)
- Config keys are centralized as constants in `sources/Api/Constants.cs`: `General:*`, `Steam:*`, `Discord:*` (OAuth + Bot), `TeamSpeak:*`, `Testing:AuthorizationBypass:*`
- Hosting: `Hosting:Providers:Hetzner` (`sources/Api/Services/HostingRuntime/HetznerRuntimeOptions.cs` - Token, BaseUrl restricted to `api.hetzner.cloud`, ServerType, Image, Location, EstimatedHourlyCostEur, Volume), plus `PersistentServerStorageOptions.cs` and `ProviderReconciliationOptions.cs`
- Database: `ConnectionStrings:database`
- Cloud worker: `CloudWorkerOptions.cs`, `CloudWorkerBootstrapConfig.cs`, `CloudWorkerConnectionConfig.cs`, per-game `Arma3WorkerOptions.cs`, `AskaWorkerOptions.cs` in `sources/CloudWorker/`
- Production: `deploy/production.json`, `deploy/webapp.appsettings.json`; API container reads `api.env` on host (`deploy/host/utf-api.service`)

**Build:**
- `X39.UnitedTacticalForces.slnx`, `.editorconfig`, `sources/ReactApp/package.json` (+ vite/tsconfig/eslint configs), `.mozaic/pipeline.yml`, `.mozaic/release.yml`, `.github/workflows/dotnet.yml`

## Platform Requirements

**Development:**
- .NET 10 SDK, Node 24 + npm 11, Docker/Podman for Aspire PostgreSQL and optional dev worker container
- Run `sources/AppHost/AppHost.csproj` for Postgres + API + WebApp + ReactApp (Vite)

**Production:**
- Single Linux host ("Amsel") with rootless Podman images `localhost/utf/api:release` and `localhost/utf/web:release`, systemd user units, PostgreSQL, nginx (`deploy/nginx.conf`)
- Released by the Mozaic pipeline over SSH (`deploy/host/README.md`, `.mozaic/DEPLOYMENT.md`, `.mozaic/RELEASE.md`)
- Game servers run on Hetzner Cloud machines with one `CloudWorker` container per deployment

---

*Stack analysis: 2026-11-18*
