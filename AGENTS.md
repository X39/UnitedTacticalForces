<!-- Repository-specific rules derived from A:/dev/work/SimplifySoft/server/AGENTS.md. -->

# Repository Agent Rules

## Handwritten source standards

These rules are mandatory for all repository-authored, handwritten source code and technical documentation:

- Write all code identifiers, comments, C# XML documentation, and technical documentation in English. Put
  localized end-user text in resource files.
- Name every enum type with an `E` prefix followed by a PascalCase name, regardless of its visibility or purpose.
  Enum members use normal PascalCase.
- Add meaningful C# XML documentation to every C# type and member. Include `<summary>` and every applicable
  `<typeparam>`, `<param>`, `<returns>`, `<value>`, and `<exception>` element. Use `<inheritdoc />` when it
  accurately describes an inherited or implemented contract. Placeholder documentation that merely repeats the
  declaration does not satisfy this requirement.
- Put each top-level type in its own file and name the file after the type. Co-locate types only when there is a
  concrete, documented justification. A same-named generic-arity family such as `Type`, `Type<T>`, and
  `Type<T1, ..., TN>` is an accepted justification.

Generated code is always exempt from every rule in this section, including generated clients such as the Kiota
client under `sources/WebApp/UtfApi` and EF migration designer files. Do not rename, split, translate, or
hand-document generated output to make it comply. Change the generator, schema, template, or configuration and
regenerate when generated output must change. Generator inputs and handwritten partial extensions are not generated
output and must follow the rules above.

## Architecture boundaries

- Keep the API control plane, cloud runtime providers, hosting contracts, external worker, and game-specific runtime
  implementations separated according to the architecture documented below.
- Do not place game-process execution in the API or turn the Aspire development harness into a production local
  runtime provider.
- Adding or expanding a cross-cutting or server-wide feature requires an explicit user request and an architectural
  justification for its placement. Do not broaden the API, shared contracts, or core infrastructure for convenience.

## Entity Framework migrations

Every EF Core migration must be scaffolded with `dotnet ef migrations add`. Never hand-create or copy a complete
migration, its designer metadata, or the model snapshot. Start with tool-generated artifacts, then add necessary
custom SQL or data-migration operations to the generated `Up` and `Down` methods when EF cannot express them.

Run the following command from the repository root with a `dotnet-ef` version compatible with the repository's EF
Core packages:

```text
dotnet ef migrations add <Name> --project sources/Database/Database.csproj --startup-project sources/Api/Api.csproj --context X39.UnitedTacticalForces.Database.DatabaseContext --output-dir Migrations
```

## Commit cadence

- Commit cohesive, related features regularly.
- Commit whenever a cohesive feature, fix, or other meaningful unit of work is complete and verified. Do not wait
  until several features have accumulated.
- Keep each commit limited to related changes. Do not combine unrelated work in one commit.

## Commit messages

Write every commit message using this subject format:

```text
type(scope)!,type(scope): summary
```

The subject prefix may contain one or more `type(scope)` entries. Separate multiple entries with commas, add `!`
only to an entry that introduces a breaking change, and end the complete prefix with a colon and one space before
the summary text.

- Use a change type such as `feat`, `fix`, `chore`, `docs`, `refactor`, `test`, or `build`.
- Use `version-bump` as the type for every version change.
- Use the affected area as the scope, such as `api`, `worker`, `hosting`, `webapp`, `database`, or `parser`.
- Keep the entire subject line under 80 characters.
- Do not use Markdown in the summary.
- State what changed, not how it was changed.
- If any change is breaking, the subject prefix must include `!`.

Use `!` and a breaking-change body for a public method, property, type, interface, contract, or namespace that is
renamed, removed, or incompatibly changed. Do not use `!` for private or internal changes, additive public APIs, or
logic changes that preserve the public API.

Every breaking commit must include a concrete migration note:

```text
BREAKING CHANGE: Explain what changed and what users must update.
```

For non-breaking commits, keep any body short and explain only what changed and why.

## Project

**United Tactical Forces - Cloud Game Server Orchestration**

United Tactical Forces is an existing ASP.NET Core API and Blazor WebAssembly application for managing community operations, including users, roles, events, mod packs, terrain, wiki content, update streams, and cloud game-server hosting. The active hosting work is specified by `docs/features/cloud-based-game-server-hosting/`; older change requests and planning artifacts are historical context only unless that feature pack explicitly reintroduces them.

The hosting implementation is a persistent control plane with one dedicated external worker for each active deployment. Hetzner is the first cloud runtime provider. Provider provisioning, observation, attribution, and teardown remain control-plane responsibilities and must not depend on the worker or an API-hosted game process.

**Core Value:** Authorized users can operate cloud game servers through durable, auditable lifecycle jobs with a clearly assigned cost owner, including reliable recovery and verified provider cleanup after process or machine restarts.

### Constraints

- **Architecture**: Keep provider/runtime and game implementation boundaries provider-neutral. Arma 3 and Hetzner are the first implementations, not hard-coded product concepts.
- **Workers**: Exactly one dedicated external worker exists for each active deployment. It connects outbound with deployment-scoped credentials; the API does not execute game processes.
- **Persistence**: Lifecycle, job, worker, ownership, and audit state must survive API restarts.
- **Billing**: Every cost-generating or hosting session must have an owner from creation until cleanup or transfer completes.
- **Provider**: Implement real Hetzner provisioning, provider-wide discovery/reconciliation, and verified non-billable teardown. Persist provisioning intent before provider mutation and adopt attributable late resources.
- **Security**: Steam credentials, provider tokens, bootstrap credentials, and worker credentials must not be exposed to frontend clients or unredacted logs.
- **Storage**: V1 persistent server data may use the API host's configured local filesystem. This storage decision does not authorize local runtime execution.
- **Development**: Aspire may start an isolated development worker container for end-to-end testing. A development harness must preserve the external-worker boundary and must not become a production local runtime provider.
- **Testing**: Add focused tests around provider idempotency, attribution, teardown verification, worker authentication and redelivery, persistence, job transitions, authorization-sensitive actions, and recovery.

## Technology Stack

## Languages

- C# on .NET 10.0 - All active application, worker, and test projects target `net10.0`.
- Razor / Blazor - Client UI lives in `sources\WebApp\WebApp.csproj` and `sources\WebApp\Program.cs` using `Microsoft.NET.Sdk.BlazorWebAssembly`.
- XML - Project and solution metadata in `X39.UnitedTacticalForces.slnx`, `sources\*\*.csproj`, `.editorconfig`, and `.DotSettings` files.
- JSON with comments - Runtime configuration in `sources\Api\appsettings.json`, `sources\Api\appsettings.Development.json`, `sources\Api\appsettings.Production.json`, `sources\AppHost\appsettings.json`, `sources\AppHost\appsettings.Development.json`, and `sources\WebApp\wwwroot\appsettings.json`.
- YAML - GitHub Actions workflow in `.github\workflows\dotnet.yml`.

## Runtime

- .NET SDK/runtime 10.0 - Required by every project file under `sources\` and `tests\`.
- ASP.NET Core - API runtime from `Microsoft.NET.Sdk.Web` in `sources\Api\Api.csproj`; startup in `sources\Api\Program.cs`.
- Blazor WebAssembly - Browser runtime from `Microsoft.NET.Sdk.BlazorWebAssembly` in `sources\WebApp\WebApp.csproj`; startup in `sources\WebApp\Program.cs`.
- .NET Aspire AppHost - Local orchestration from `Aspire.AppHost.Sdk` in `sources\AppHost\AppHost.csproj`; topology in `sources\AppHost\Program.cs`.
- NuGet via SDK-style `.csproj` package references.
- Lockfile: missing; no `packages.lock.json` detected.

## Frameworks

- ASP.NET Core 10.0.9 - HTTP API, controllers, authentication, OpenAPI, CORS, health checks; configured in `sources\Api\Api.csproj`, `sources\Api\Program.cs`, and `sources\ServiceDefaults\Extensions.cs`.
- Blazor WebAssembly 10.0.9 - Client application shell and browser-side DI; configured in `sources\WebApp\WebApp.csproj` and `sources\WebApp\Program.cs`.
- .NET Aspire 13.4.6 - Development service orchestration, PostgreSQL resource, service discovery, resilience, health checks, and OpenTelemetry defaults; configured in `sources\AppHost\AppHost.csproj`, `sources\AppHost\Program.cs`, and `sources\ServiceDefaults\ServiceDefaults.csproj`.
- Entity Framework Core 10.0.9 with Npgsql 10.0.2 - PostgreSQL data access and migrations; configured in `sources\Api\Api.csproj`, `sources\Api\Program.cs`, `sources\Database\DatabaseContext.cs`, and `sources\Database\Migrations\`.
- Microsoft Fluent UI Blazor 4.14.3 - UI component library; configured in `sources\WebApp\WebApp.csproj` and registered in `sources\WebApp\Program.cs`.
- Microsoft Kiota 2.0.0 - Generated API client dependencies for `sources\WebApp\UtfApi\`.
- xUnit 2.9.3 - Unit tests in `tests\X39.BI.Config.Parsing.Tests\X39.BI.Config.Parsing.Tests.csproj`.
- Microsoft.NET.Test.Sdk 18.7.0 - Test execution host in the active test projects.
- coverlet.collector 10.0.1 - Coverage collector configured in the active test projects.
- .NET CLI - Restore/build/test/publish commands in `.github\workflows\dotnet.yml`.
- Scalar.AspNetCore 2.1.18 - Development OpenAPI UI mapped in `sources\Api\Program.cs` using `app.MapScalarApiReference()`.
- Microsoft.AspNetCore.OpenApi 10.0.9 - OpenAPI document generation in `sources\Api\Api.csproj` and `sources\Api\Program.cs`.
- ReSharper / EditorConfig - Formatting and inspection settings in `.editorconfig`, `X39.UnitedTacticalForces.sln.DotSettings.user`, and `X39.UnitedTacticalForces.WebApp.sln.DotSettings`.

## Key Dependencies

- `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2 - PostgreSQL provider used by `options.UseNpgsql(builder.Configuration.GetConnectionString("database"))` in `sources\Api\Program.cs`.
- `AspNet.Security.OpenId.Steam` 10.0.0 - Steam OpenID authentication configured in `sources\Api\Program.cs`.
- `AspNet.Security.OAuth.Discord` 10.0.0 - Discord OAuth authentication configured in `sources\Api\Program.cs`.
- `Discord.Net` 3.20.1 - Discord bot client and slash commands implemented in `sources\Api\HostedServices\DiscordBot.cs`.
- `SteamWebAPI2` 5.0.0 - Steam Web API profile lookup in `sources\Api\Services\SteamRepository.cs`.
- `Microsoft.FluentUI.AspNetCore.Components` 4.14.3 - Blazor UI components registered with `AddFluentUIComponents` in `sources\WebApp\Program.cs`.
- `Microsoft.Kiota.Bundle` 2.0.0 - Generated client support for API request builders under `sources\WebApp\UtfApi\`.
- `X39.Util.DependencyInjection` 3.0.0.10 - Attribute-driven DI registration used by `AddAttributedServicesFromAssemblyOf` in `sources\Api\Program.cs` and `sources\WebApp\Program.cs`.
- `Aspire.Hosting.AppHost` 13.4.6 - AppHost process in `sources\AppHost\AppHost.csproj`.
- `Aspire.Hosting.PostgreSQL` 13.4.6 - PostgreSQL development resource declared in `sources\AppHost\Program.cs`.
- `Microsoft.Extensions.Http.Resilience` 10.7.0 - Default HTTP resilience handlers in `sources\ServiceDefaults\Extensions.cs`.
- `Microsoft.Extensions.ServiceDiscovery` 10.7.0 - Aspire service discovery configured in `sources\ServiceDefaults\Extensions.cs`.
- `OpenTelemetry.*` 1.15-1.16 - Logging, metrics, tracing, and OTLP exporter support in `sources\ServiceDefaults\ServiceDefaults.csproj` and `sources\ServiceDefaults\Extensions.cs`.
- `HtmlAgilityPack` 1.12.4 - HTML parsing dependency declared by `sources\Api\Api.csproj`.
- `Pidgin` 3.5.1 - Parser combinator dependency for `sources\X39.BI.Config.Parsing\`.
- `Markdig` 1.3.2 - Markdown rendering dependency declared by `sources\WebApp\WebApp.csproj`.

## Configuration

- API configuration uses `appsettings.json` plus optional `appsettings.secret.json` and `appsettings.user.json` loaded in `sources\Api\Program.cs`.
- API base paths and origins use `General:BasePath`, `General:ApiBaseUrl`, `General:ClientBaseUrl`, and optional `Cors:AllowedOrigins` in `sources\Api\appsettings.json` and `sources\Api\appsettings.Production.json`.
- Database connection uses `ConnectionStrings:database` read by `sources\Api\Program.cs`; Aspire supplies the `database` resource from `sources\AppHost\Program.cs`.
- Steam integration uses `Steam:Enabled`, `Steam:ApiKey`, `Steam:SteamCmdPath`, `Steam:DepotDownloaderPath`, `Steam:InstallBasePath`, `Steam:Username`, and `Steam:Password` constants in `sources\Api\Constants.cs`.
- Discord integration uses `Discord:Enabled`, `Discord:OAuth:ClientId`, `Discord:OAuth:ClientSecret`, `Discord:Bot:ApplicationId`, `Discord:Bot:PublicKey`, `Discord:Bot:BotToken`, and `Discord:Bot:EmbedColor` constants in `sources\Api\Constants.cs`.
- TeamSpeak display settings use `TeamSpeak:Host`, `TeamSpeak:Port`, `TeamSpeak:Password`, `TeamSpeak:Channel`, and `TeamSpeak:ChannelPassword` constants in `sources\Api\Constants.cs`.
- WebApp public runtime config uses `Api:BaseUrl`, `Urls:TeamSpeak3`, and `Urls:Discord` in `sources\WebApp\wwwroot\appsettings.json`.
- `sources\Api\appsettings.secret.json` exists and is excluded from build/publish in `sources\Api\Api.csproj`; contents were not read.
- Solution file: `X39.UnitedTacticalForces.slnx`.
- Project files: `sources\Api\Api.csproj`, `sources\WebApp\WebApp.csproj`, `sources\AppHost\AppHost.csproj`, `sources\ServiceDefaults\ServiceDefaults.csproj`, `sources\Common\Common.csproj`, `sources\HostingContracts\HostingContracts.csproj`, `sources\WebSocketContracts\WebSocketContracts.csproj`, `sources\Claims\Claims.csproj`, `sources\X39.BI.Config.Parsing\X39.BI.Config.Parsing.csproj`, and `tests\X39.BI.Config.Parsing.Tests\X39.BI.Config.Parsing.Tests.csproj`.
- Runtime config files: `sources\Api\appsettings.json`, `sources\Api\appsettings.Development.json`, `sources\Api\appsettings.Production.json`, `sources\AppHost\appsettings.json`, `sources\AppHost\appsettings.Development.json`, and `sources\WebApp\wwwroot\appsettings.json`.
- Launch profiles: `sources\Api\Properties\launchSettings.json`, `sources\AppHost\Properties\launchSettings.json`, and `sources\WebApp\Properties\launchSettings.json`.
- CI config: `.github\workflows\dotnet.yml`.
- Formatting config: `.editorconfig`.

## Platform Requirements

- Install .NET SDK 10.0 to build all current `net10.0` projects.
- Use `dotnet restore`, `dotnet build`, and `dotnet test` from the repo root against `X39.UnitedTacticalForces.slnx`.
- Run `sources\AppHost\AppHost.csproj` for Aspire-managed local development with PostgreSQL.
- Provide local secrets through `sources\Api\appsettings.secret.json`, `sources\Api\appsettings.user.json`, .NET user secrets, or environment variables; do not commit secret values.
- Steam game server tooling paths are environment-specific: Windows examples in `sources\Api\appsettings.Development.json`, Linux examples in `sources\Api\appsettings.Production.json`.
- API is an ASP.NET Core service published from `sources\Api\Api.csproj`.
- WebApp is a static Blazor WebAssembly artifact published from `sources\WebApp\WebApp.csproj`.
- PostgreSQL is required for API persistence.
- `ReadMe.md` documents Linux deployment with PostgreSQL, `systemd` for the API, and nginx for the WebApp.
- CI workflow `.github\workflows\dotnet.yml` restores, builds, tests, and publishes the `net10.0` solution with .NET 10.

## Conventions

## Naming Patterns

- Use one public type per `.cs` file and name the file after that type, e.g. `sources/Api/Controllers/EventsController.cs`, `sources/WebApp/Services/EventRepository/EventRepositoryImpl.cs`, `sources/X39.BI.Config.Parsing/ConfigParser.cs`.
- Use feature folders with interface/implementation pairs for WebApp repositories, e.g. `sources/WebApp/Services/EventRepository/IEventRepository.cs` and `sources/WebApp/Services/EventRepository/EventRepositoryImpl.cs`.
- Use `.razor` plus `.razor.cs` partial class files for components with non-trivial backing code, e.g. `sources/WebApp/Components/FluentMarkdown.razor` and `sources/WebApp/Components/FluentMarkdown.razor.cs`.
- Treat `sources/WebApp/UtfApi/**`, `sources/Database/Migrations/**`, `sources/**/obj/**`, and `Properties/Language.Designer.cs` as generated code. Do not use these files as style sources for hand-written code.
- Prefer `sources/` for active projects. `X39.UnitedTacticalForces.slnx` references `sources/Api/Api.csproj`, `sources/WebApp/WebApp.csproj`, `sources/X39.BI.Config.Parsing/X39.BI.Config.Parsing.csproj`, and `tests/X39.BI.Config.Parsing.Tests/X39.BI.Config.Parsing.Tests.csproj`.
- Use PascalCase for methods, including async methods with the `Async` suffix: `GetUpcomingEventsAsync` in `sources/Api/Controllers/EventsController.cs`, `CreateEventAsync` in `sources/WebApp/Services/EventRepository/EventRepositoryImpl.cs`, `ParseOrThrow` in `sources/X39.BI.Config.Parsing/ConfigParser.cs`.
- Use local helper methods for isolated setup or normalization logic inside entry-point classes, e.g. `AddServices`, `AddAuth`, `NormalizeOrigin`, and `MigrateAndSeedDataAsync` in `sources/Api/Program.cs`.
- Use extension method classes named after the target or concern, e.g. `sources/Api/ExtensionMethods/ClaimsPrincipalExtensions.cs`, `sources/WebApp/ExtensionMethods/LifetimeStatusExtensions.cs`.
- Use camelCase for locals and parameters: `cancellationToken`, `eventId`, `query`, `response`, `allowedOrigins` in `sources/Api/Controllers/EventsController.cs` and `sources/Api/Program.cs`.
- Prefix private fields with `_`: `_logger`, `_apiDbContext`, `_discordBot`, `_updateStreamService` in `sources/Api/Controllers/EventsController.cs`; `_webSockets`, `_lock`, `_serializerOptions` in `sources/Api/Services/UpdateStreamService/UpdateStreamService.cs`.
- Use aligned assignments sparingly when several related fields/properties are initialized together. This is configured in `.editorconfig` through ReSharper alignment settings and appears in constructors and object initializers such as `sources/Api/Controllers/EventsController.cs`.
- Use named tuple fields when tuple members carry domain meaning, e.g. `(string key, object? value)` in `tests/X39.BI.Config.Parsing.Tests/MixedConfigClassTests.cs`.
- Use PascalCase for classes, records, interfaces, and enums: `EventRepositoryImpl`, `IEventRepository`, `PlainEventDto`, `EEventAcceptance`.
- Interfaces use `I` prefix, e.g. `sources/Api/Services/UpdateStreamService/IUpdateStreamService.cs`, `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs`.
- Repository implementations use the `Impl` suffix and are usually internal in WebApp feature folders, e.g. `sources/WebApp/Services/EventRepository/EventRepositoryImpl.cs`.
- DTOs use the `Dto` suffix for API response/request transport shapes, e.g. `sources/Api/DTO/PlainEventDto.cs`, `sources/Api/DTO/FullEventDto.cs`.
- Creation and update payloads use `Payload` and `Update` suffixes, e.g. `sources/Api/DTO/Payloads/EventCreationPayload.cs`, `sources/Api/DTO/Updates/EventUpdate.cs`.
- Shared enums use the `E` prefix, e.g. `sources/Common/Event/EEventAcceptance.cs` and `sources/HostingContracts/EDeploymentStatus.cs`.
- Entity primary key properties are named `PrimaryKey`, and foreign key properties use the `Fk` suffix, e.g. `OwnerFk`, `HostedByFk`, and `TerrainFk` in `sources/Database/Eventing/Event.cs`.

## Code Style

- Use `.editorconfig` as the source of truth for hand-written C# formatting. It sets ReSharper and .NET style rules across all files.
- Use C# file-scoped namespaces for new hand-written files, matching `sources/Api/Controllers/EventsController.cs`, `sources/WebApp/Services/RepositoryBase.cs`, and `sources/X39.BI.Config.Parsing/ConfigParser.cs`.
- Use braces on new lines for types, methods, loops, object initializers, and multi-line lambdas. `.editorconfig` sets `csharp_new_line_before_open_brace = all`.
- Omit braces only for simple single-line guard clauses when the existing local style does so, e.g. `if (!User.TryGetUserId(out var userId)) return Unauthorized();` style in `sources/Api/Controllers/EventsController.cs`.
- Keep line length around 120 characters. `.editorconfig` sets `resharper_csharp_max_line_length = 120`.
- Prefer trailing commas in multi-line lists and object initializers. `.editorconfig` sets `resharper_trailing_comma_in_multiline_lists = true`, and examples appear in `sources/Api/Controllers/EventsController.cs`.
- Use target-typed empty arrays/lists where already established: `[]` for empty arrays/collections in `sources/Database/Eventing/Event.cs` and `sources/Api/Program.cs`.
- Use `var` for obvious local types and query chains. Use explicit types when the type clarifies the expression, e.g. `IQueryable<UpcomingEventDto> query;` in `sources/Api/Controllers/EventsController.cs`.
- ReSharper/Rider settings are configured in `.editorconfig`, `X39.UnitedTacticalForces.WebApp.sln.DotSettings`, and user-specific `.DotSettings.user` files.
- No Roslyn analyzer rule set, StyleCop config, or separate formatter config is detected. Use `dotnet build X39.UnitedTacticalForces.slnx` as the primary static verification command.
- Nullable reference types and implicit usings are enabled in active projects such as `sources/Api/Api.csproj`, `sources/WebApp/WebApp.csproj`, `sources/X39.BI.Config.Parsing/X39.BI.Config.Parsing.csproj`, and `tests/X39.BI.Config.Parsing.Tests/X39.BI.Config.Parsing.Tests.csproj`.

## Import Organization

- No C# path aliases are configured. Use project references and namespaces from `.csproj` files.
- Root namespaces are set per project: `X39.UnitedTacticalForces.Api` in `sources/Api/Api.csproj`, `X39.UnitedTacticalForces.WebApp` in `sources/WebApp/WebApp.csproj`, and `X39.BI.Config.Parsing` in `sources/X39.BI.Config.Parsing/X39.BI.Config.Parsing.csproj`.

## Error Handling

- API controllers return HTTP results for user/auth/resource outcomes: `Unauthorized()`, `Forbid()`, `NoContent()`, and `Ok(...)` in `sources/Api/Controllers/EventsController.cs`.
- Validate enum switches with `ArgumentOutOfRangeException`, e.g. `SetMeAcceptanceAsync` in `sources/WebApp/Services/EventRepository/EventRepositoryImpl.cs` and `EventUtils` in `sources/Api/Helpers/EventUtils.cs`.
- Use domain-specific exceptions where they exist, e.g. lifecycle, hosting-validation, worker-protocol, and provider outcome types under `sources/Api/Services/GameServerLifecycle`, `sources/Api/Services/WorkerProtocol`, and `sources/Api/Services/HostingRuntime`.
- Parser code returns a structured `ParseResult` from `Parse(...)` and throws `FormatException` only from `ParseOrThrow(...)` in `sources/X39.BI.Config.Parsing/ConfigParser.cs`.
- Repository wrappers translate expected API null/no-content cases to null or empty collections, e.g. `GetEventAsync` catches Kiota `ApiException` with `204` in `sources/WebApp/Services/EventRepository/EventRepositoryImpl.cs`.
- Avoid introducing broad `throw new Exception()` in new code. Existing broad exceptions appear in `sources/Api/Helpers/ValidationHelper.cs` and `sources/WebApp/Helpers/ConfigToSlots.cs`; prefer specific exception types and messages.
- Cancellation paths may intentionally swallow expected cancellation/socket exceptions in long-lived services, e.g. `OperationCanceledException`, `WebSocketException`, and `ObjectDisposedException` in `sources/Api/Services/UpdateStreamService/UpdateStreamService.cs`.

## Logging

- Inject `ILogger<T>` into API controllers and hosted/service classes, e.g. `_logger` in `sources/Api/Controllers/RolesController.cs`, `sources/Api/Controllers/GameServersController.cs`, and `sources/Api/HostedServices/DiscordBot.cs`.
- Use structured message templates with named properties, e.g. `logger.LogInformation("Scalar URL: {SwaggerUrl} ...", ...)` in `sources/Api/Program.cs` and provider/reconciliation logs under `sources/Api/Services/HostingRuntime`.
- Log externally visible state changes and infrastructure operations. Examples include role/mod-pack changes in `sources/Api/Controllers/RolesController.cs`, provider allocation/cleanup, and external-worker health transitions. Runtime command outcomes are reported by the worker without exposing secrets.
- Do not log secrets or raw configuration values from `appsettings.secret.json`, `appsettings.user.json`, or OAuth/database settings.

## Comments

- Use XML documentation for public API controllers, DTOs, entities, and service contracts where the project already exposes docs or OpenAPI metadata, e.g. `sources/Api/Controllers/EventsController.cs`, `sources/Database/Eventing/Event.cs`, `sources/Api/DTO/PlainEventDto.cs`.
- Use short inline comments only for non-obvious rationale or external references, e.g. the ASP.NET/Npgsql comments in `sources/Api/Program.cs` and safety note in `sources/WebApp/Components/FluentMarkdown.razor.cs`.
- Keep generated XML documentation in designer/generated files unchanged.
- Not applicable for primary code. JavaScript exists only as component support in `sources/WebApp/Components/FluentMarkdown.razor.js`; no JS documentation convention is established.

## Function Design

## Module Design

## Architecture

## System Overview

```text

```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| Aspire AppHost | Local distributed app composition for Postgres, API, and WebApp. | `sources/AppHost/Program.cs` |
| API host | Configures ASP.NET Core, EF Core, auth providers, OpenAPI, CORS, migrations, controllers, and hosted services. | `sources/Api/Program.cs` |
| API controllers | Own HTTP endpoints and most application use-case logic for events, mod packs, terrain, users, roles, wiki, resource claims, game servers, and update streams. | `sources/Api/Controllers` |
| EF Core context | Defines aggregate storage sets for authority, core, eventing, wiki, and hosting data. | `sources/Database/DatabaseContext.cs` |
| Domain entities | Persisted EF entities grouped by bounded area. | `sources/Database/Authority`, `sources/Database/Core`, `sources/Database/Eventing`, `sources/Database/Hosting`, `sources/Database/Wiki` |
| DTOs and payloads | API response, creation, and update shapes projected from EF queries. | `sources/Api/DTO` |
| Authorization abstraction | Maps string policies and custom requirements to claim/resource checks. | `sources/Api/Authorization/Abstraction` |
| Game server definition catalog | Supplies API-safe game metadata, actions, file capabilities, and preservation defaults without executing a game process. | `sources/Api/Services/GameServerDefinition` |
| Cloud runtime providers | Provision, discover, observe, and tear down attributed provider resources independently of worker availability. Hetzner is the first production implementation. | `sources/Api/Services/HostingRuntime` |
| Dedicated cloud worker | Runs outside the API, connects outbound, journals stable commands locally, and owns Arma 3 installation and process operations for one deployment. | `sources/CloudWorker` |
| Update stream service | Maintains path-scoped WebSocket subscribers and broadcasts typed update messages. | `sources/Api/Services/UpdateStreamService/UpdateStreamService.cs` |
| Blazor app bootstrap | Configures root components, Fluent UI, localization, auth core, HTTP client, DI-discovered services, and startup initialization. | `sources/WebApp/Program.cs` |
| Blazor routing/layout | Routes pages through authorization-aware layouts and renders navigation based on the current user. | `sources/WebApp/App.razor`, `sources/WebApp/Shared/MainLayout.razor` |
| Client repositories | Wrap the generated Kiota API client behind feature-specific interfaces. | `sources/WebApp/Services/*Repository` |
| Generated API client | Kiota-generated request builders and models used by WebApp repositories. | `sources/WebApp/UtfApi` |
| Shared application types | Cross-project event, Verein, and public-page types. | `sources/Common` |
| Hosting contracts | Provider-neutral hosting lifecycle types and the API-to-worker protocol. | `sources/HostingContracts` |
| WebSocket contracts | WebSocket wire envelopes and typed update messages only. | `sources/WebSocketContracts` |
| BI config parser | Standalone Pidgin parser for Bohemia Interactive config/class syntax. | `sources/X39.BI.Config.Parsing/ConfigParser.cs` |
| Service defaults | Shared .NET Aspire service discovery, health checks, resilience, and OpenTelemetry wiring. | `sources/ServiceDefaults/Extensions.cs` |

## Pattern Overview

- Keep external HTTP API behavior in `[ApiController]` classes under `sources/Api/Controllers`; controllers directly project EF entities into DTOs from `sources/Api/DTO`.
- Use EF Core entities in `sources/Database/*` only on the server side; expose DTO, payload, update, and contract types to the client.
- Register application services via `X39.Util.DependencyInjection` attributes such as `[Scoped<...>]` and `[Singleton<...>]`; both API and WebApp call `AddAttributedServicesFromAssemblyOf`.
- Use generated Kiota request builders in `sources/WebApp/UtfApi` from repository implementations under `sources/WebApp/Services`.
- Use `sources/Common` for shared application types, `sources/HostingContracts` for API-to-worker hosting types, and `sources/WebSocketContracts` only for WebSocket wire messages.
- Keep API-side game definitions metadata-only. Runtime game implementations and process control belong in the dedicated external worker, never in the API process.
- Implement provider behavior through `IRuntimeProvider`; every production provider must support durable attribution, provider-wide discovery, and verified non-billable teardown.

## Layers

- Purpose: Run the distributed application for development with managed Postgres, API, and WebApp resources.
- Location: `sources/AppHost`
- Contains: Aspire host startup and launch settings.
- Depends on: `sources/Api`, `sources/WebApp`, Postgres resource.
- Used by: Developers running the full app locally.
- Purpose: Configure ASP.NET Core middleware, auth, OpenAPI, CORS, database migration, and controller routing.
- Location: `sources/Api/Program.cs`
- Contains: `AddServices`, `AddAuth`, `MigrateAndSeedDataAsync`, `UseRestrictedCors`, `MapControllers`.
- Depends on: `sources/Database`, `sources/Api/Controllers`, `sources/Api/Services`, `sources/ServiceDefaults`.
- Used by: HTTP clients, WebApp repositories, update stream WebSocket subscribers.
- Purpose: Implement request validation, authorization gates, EF queries, mutations, side effects, and response DTO mapping.
- Location: `sources/Api/Controllers`
- Contains: Controllers for events, event slotting, game servers, mod packs, resource claims, roles, terrain, update streams, users, and wiki.
- Depends on: `DatabaseContext`, DTOs, authorization claims/requirements, lifecycle/control-plane services, `IUpdateStreamService`, and `DiscordBot`.
- Used by: Generated API client in `sources/WebApp/UtfApi` and direct external callers.
- Purpose: Store authority, core, eventing, wiki, and hosting data.
- Location: `sources/Database`
- Contains: `DatabaseContext`, EF entities, metadata interfaces, and migrations.
- Depends on: EF Core and Npgsql.
- Used by: API controllers, authorization requirements, lifecycle/control-plane services, and hosted services.
- Purpose: Encapsulate non-trivial infrastructure operations outside normal CRUD: durable lifecycle coordination, Hetzner provisioning/reconciliation, external-worker authentication, Steam profile integration, Discord bot, WebSocket updates, and URL resolution.
- Location: `sources/Api/Services`, `sources/Api/HostedServices`
- Contains: game-server definitions, lifecycle executor, runtime providers, worker protocol, `UpdateStreamService`, `SteamRepository`, `BaseUrl`, and `DiscordBot`.
- Depends on: EF Core, configuration, logging, provider HTTP APIs, and the worker protocol. API-hosted services do not execute game processes.
- Used by: API controllers, the durable control-plane worker, and scheduled provider reconciliation.
- Purpose: Execute deployment-local game/runtime operations for exactly one externally provisioned deployment.
- Location: `sources/CloudWorker`
- Contains: outbound worker client, owner-only credential store, durable command journal, Arma 3 installer/configuration/process adapter, and container image.
- Depends on: shared worker contracts, SteamCMD, deployment-local writable storage, and outbound control-plane HTTPS.
- Used by: one dedicated Hetzner worker resource per active deployment; optionally by an isolated Aspire development container.
- Purpose: Render routed screens, layouts, panels, and shared components in a Blazor WebAssembly client.
- Location: `sources/WebApp`
- Contains: `Pages`, `Panels`, `Components`, `Shared`, `wwwroot`, service/repository classes.
- Depends on: Fluent UI, localization resources, generated Kiota client, `sources/Common`, and `sources/Claims`.
- Used by: Browser users.
- Purpose: Hide generated Kiota request-builder details behind stable feature repository interfaces.
- Location: `sources/WebApp/Services`
- Contains: `RepositoryBase`, `EventRepository`, `GameServerRepository`, `ModPackRepository`, `TerrainRepository`, `UserRepository`, `WikiRepository`, `SlotRepository`, `RoleRepository`, resource claim services.
- Depends on: `IHttpClientFactory`, `BaseUrl`, `sources/WebApp/UtfApi`.
- Used by: Blazor pages, panels, components, and app services.
- Purpose: Provide stable types shared across API and WebApp without depending on server implementation.
- Location: `sources/Common`, `sources/Claims`, `sources/HostingContracts`, `sources/WebSocketContracts`
- Contains: claim constants, shared application types and helpers, hosting/worker contracts, and WebSocket messages.
- Depends on: Minimal framework packages.
- Used by: API and WebApp projects.
- Purpose: Parse Arma/Bohemia config files into `IConfig` trees independent from the web stack.
- Location: `sources/X39.BI.Config.Parsing`
- Contains: parser combinators, parse result types, config node types.
- Depends on: Pidgin and `X39.Util`.
- Used by: WebApp helpers and parser tests.

## Data Flow

### Primary API Request Path

### WebApp Page Data Flow

### Game Server Operation Flow

### Update Stream Flow

- Database state lives in PostgreSQL through EF Core `DbSet` properties in `sources/Database/DatabaseContext.cs`.
- Client user/session state is loaded by `MeService.InitializeAsync` at WebApp startup and auth cookies are passed by `IncludeCookiesHttpMessageHandler`.
- Provider allocation, deployment, worker, command, and lifecycle state is persisted in PostgreSQL. Runtime process state is observed and reported by the dedicated external worker.
- WebSocket subscribers are held in memory by singleton `UpdateStreamService`; subscriptions are process-local.

## Key Abstractions

- Purpose: Stable WebApp data-access APIs per feature.
- Examples: `sources/WebApp/Services/EventRepository/IEventRepository.cs`, `sources/WebApp/Services/GameServerRepository/IGameServerRepository.cs`, `sources/WebApp/Services/TerrainRepository/ITerrainRepository.cs`
- Pattern: Interface plus `[Scoped<Implementation, Interface>]` implementation inheriting `RepositoryBase`.
- Purpose: Type-safe generated HTTP client for API routes and models.
- Examples: `sources/WebApp/UtfApi/ApiClient.cs`, `sources/WebApp/UtfApi/Events/EventsRequestBuilder.cs`, `sources/WebApp/UtfApi/Models`
- Pattern: Do not hand-edit generated request builders; wrap them in `sources/WebApp/Services`.
- Purpose: Server-side persisted model grouped by domain area.
- Examples: `sources/Database/Authority/User.cs`, `sources/Database/Core/ModPackDefinition.cs`, `sources/Database/Eventing/Event.cs`, `sources/Database/Hosting/GameServer.cs`, `sources/Database/Wiki/WikiPage.cs`
- Pattern: Add entities under the matching domain folder and expose them through `DatabaseContext`.
- Purpose: External API shapes separated from EF entities.
- Examples: `sources/Api/DTO/PlainEventDto.cs`, `sources/Api/DTO/Payloads/EventCreationPayload.cs`, `sources/Api/DTO/Updates/EventUpdate.cs`
- Pattern: Use `Plain*Dto` for compact flat results, `Full*Dto` for expanded detail, `*CreationPayload` for creates, and `*Update` for partial updates.
- Purpose: Implement claim and resource-aware authorization policies.
- Examples: `sources/Api/Authorization/Abstraction/IAppRequirement.cs`, `sources/Api/Authorization/Abstraction/AppAuthorizationHandler.cs`, `sources/Api/Authorization/Requirements/ServerIdRequirement.cs`
- Pattern: Add requirement classes under `sources/Api/Authorization/Requirements` and register through `AddAppAuthorization`.
- Purpose: Metadata abstraction for supported games without API-local process execution.
- Examples: `sources/Api/Services/GameServerDefinition/IGameServerDefinitionCatalog.cs`, `sources/Api/Services/GameServerDefinition/Arma3GameServerDefinitionRegistration.cs`
- Pattern: Register API-safe definitions in the catalog; implement installation and process behavior in the external worker.
- Purpose: Provider-neutral deployment allocation and cost-stop boundary.
- Examples: `sources/Api/Services/HostingRuntime/IRuntimeProvider.cs`, `sources/Api/Services/HostingRuntime/HetznerRuntimeProvider.cs`
- Pattern: Persist create intent before mutation, label every created resource, discover provider-wide reality, inventory every resource, and resolve teardown only after verified non-billable observation.
- Purpose: Server-to-client change notifications over WebSockets.
- Examples: `sources/WebSocketContracts/WebSocketMessage.cs`, `sources/WebSocketContracts/Eventing/EventChangedMessage.cs`
- Pattern: Put only WebSocket wire envelopes and messages in `sources/WebSocketContracts/*` and send them via `IUpdateStreamService`.

## Entry Points

- Location: `X39.UnitedTacticalForces.slnx`
- Triggers: IDE/build tooling.
- Responsibilities: Includes active projects under `sources/` and parser tests under `tests/`.
- Location: `sources/Api/Program.cs`
- Triggers: `dotnet run`, Aspire AppHost, deployment host.
- Responsibilities: Configure server dependencies, auth, EF migrations, CORS, OpenAPI, controllers, health checks, and hosted services.
- Location: `sources/WebApp/Program.cs`
- Triggers: Browser loads Blazor WebAssembly app.
- Responsibilities: Configure root components, HTTP client, Fluent UI, localization, authorization, service DI, and startup state.
- Location: `sources/WebApp/App.razor`
- Triggers: Client-side navigation.
- Responsibilities: Route pages with authorization-aware views and default layout.
- Location: `sources/AppHost/Program.cs`
- Triggers: Aspire local orchestration.
- Responsibilities: Create Postgres, run API and WebApp, and optionally run an isolated external-worker container for development protocol testing.
- Location: `sources/X39.BI.Config.Parsing/ConfigParser.cs`
- Triggers: Direct library calls from app code/tests.
- Responsibilities: Parse config text into `IConfig` results or throw format exceptions.

## Architectural Constraints

- **Durability:** Game-server lifecycle requests enqueue persisted jobs. Provider and worker side effects are resumed from durable intent and observed external reality; HTTP request lifetime is never the execution boundary.
- **Global state:** Singleton `UpdateStreamService` stores in-memory WebSockets in `sources/Api/Services/UpdateStreamService/UpdateStreamService.cs`; durable hosting truth remains in PostgreSQL, and `Program.Main` sets process-wide invariant culture in `sources/Api/Program.cs`.
- **Circular imports:** No explicit circular project references detected in `X39.UnitedTacticalForces.slnx`; shared types flow from focused `Common`, `HostingContracts`, `WebSocketContracts`, and `Claims` projects into their consumers.
- **Generated code:** `sources/WebApp/UtfApi` is Kiota-generated; place hand-written client behavior in `sources/WebApp/Services`, not inside generated request builders.
- **Active tree:** The active solution references `sources/`; the parallel `source/` tree is not referenced by `X39.UnitedTacticalForces.slnx`.
- **Secrets:** API optional secret/user configuration files are referenced by `sources/Api/Program.cs` and `sources/Api/Api.csproj`; do not read or commit secret values.

## Anti-Patterns

### Returning EF Entities From API Endpoints

### Editing Generated Kiota Client Code

### Placing New Active Code Under `source/`

### Bypassing Resource Authorization For Game Server/Event Paths

## Error Handling

- Return `Unauthorized()`, `Forbid()`, `NotFound()`, `NoContent()`, `BadRequest()`, and `Ok(...)` from controller actions in `sources/Api/Controllers`.
- Use `ProducesResponseType` annotations on controller actions for OpenAPI generation.
- Catch expected client API exceptions in WebApp repositories when a status maps to a domain result, such as 204 to `null` in `sources/WebApp/Services/EventRepository/EventRepositoryImpl.cs`.
- Log background game-server operation failures with `ILogger` in `sources/Api/Controllers/GameServersController.cs`.
- Parser failures return `ParseResult` with `ConfigParserError` or throw `FormatException` through `ParseOrThrow` in `sources/X39.BI.Config.Parsing/ConfigParser.cs`.

## Cross-Cutting Concerns

## Project Skills

No project skills found. Add skills to any of: `.claude/skills/`, `.agents/skills/`, `.cursor/skills/`,
`.github/skills/`, or `.codex/skills/` with a `SKILL.md` index file.
