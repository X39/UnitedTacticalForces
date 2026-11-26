---
last_mapped_commit: 6c393fc7bef9e4c6086a43628f86979fc5aec7ce
last_mapped_at: 2026-11-18
---
# Testing Patterns

**Analysis Date:** 2026-11-18

## Test Framework

**Runner:**
- .NET: xUnit 2.9.3 + `xunit.runner.visualstudio` 3.1.5, `Microsoft.NET.Test.Sdk` 18.7.0, `coverlet.collector` 10.0.1, all `net10.0`.
- Projects: `tests/Api.Tests/Api.Tests.csproj`, `tests/CloudWorker.Tests/CloudWorker.Tests.csproj`, `tests/Ui.Tests/Ui.Tests.csproj` (Microsoft.Playwright 1.61.0), `tests/X39.BI.Config.Parsing.Tests/X39.BI.Config.Parsing.Tests.csproj`.
- React: Vitest (`sources/ReactApp/vitest.config.ts`, jsdom, globals, setup `tests/setup.ts`) and Playwright (`sources/ReactApp/playwright.config.ts`, e2e under `tests/e2e`).

**Assertion Library:**
- xUnit `Assert.*` (no FluentAssertions). Vitest `expect`.

**Run Commands:**
```bash
dotnet test X39.UnitedTacticalForces.slnx                   # All .NET tests
dotnet test tests/Api.Tests --filter "FullyQualifiedName~LifecycleJobQueueTests"  # Single class
dotnet test X39.UnitedTacticalForces.slnx --collect:"XPlat Code Coverage"         # Coverage
cd sources/ReactApp && npm test                             # Vitest once
cd sources/ReactApp && npm run test:watch                   # Watch mode
cd sources/ReactApp && npm run test:e2e                     # Playwright
cd sources/ReactApp && npm run ci                           # Full frontend gate
```

## Test File Organization

**Location:**
- Separate test projects under `tests/`, one per production project. `Api.Tests` mirrors domain areas in folders (`GameServerHosting/`, `Authorization/`, `Eventing/`, `Wiki/`, `OpenApi/`, `Security/`, ...). `CloudWorker.Tests` is flat.
- React: a few co-located `src/**/*.test.ts`, most under `sources/ReactApp/tests/` (kebab-case, with subfolders `hosting/`, `wiki/`, `events-member/`, `mocks/`, `e2e/`).

**Naming:**
- `<SubjectUnderTest>Tests.cs`, `public sealed class` (or `public class`). Methods: `Subject_ConditionOrScenario_ExpectedOutcome` or `Action_ExpectedBehavior`, e.g. `ClaimNext_ExecutesOneDeploymentInRequestOrderBeforePriority`, `Redelivery_WithChangedAction_IsRejected`, `TerminalOutcome_SurvivesRestartAndPreventsRegression`.

**Structure:**
```
tests/Api.Tests/
├── GlobalUsings.cs                     # NodaTime, Api.Tests, NodaSystemClock alias
├── NodaTimeSqliteApiDbContext.cs       # SQLite-compatible DbContext with NodaTime converters
├── InstantToLongConverter.cs / LocalDateToIntConverter.cs
├── GameServerHosting/
│   ├── HostingTestStore.cs             # in-memory SQLite store
│   ├── HostingTestData.cs / HostingTestSeed.cs
│   └── *Tests.cs
tests/CloudWorker.Tests/
├── TemporaryDirectory.cs               # disposable temp dir
└── *Tests.cs
tests/Ui.Tests/WebAppHostFixture.cs     # starts WebApp + Playwright
```

## Test Structure

**Suite Organization:**
```csharp
namespace Api.Tests.GameServerHosting;

public sealed class LifecycleJobQueueTests
{
    [Fact]
    public async Task ClaimNext_ExecutesOneDeploymentInRequestOrderBeforePriority()
    {
        await using var store = await HostingTestStore.CreateAsync();
        var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);
        var first = CreateJob(seed, sequence: 1, priority: 0);
        store.DbContext.LifecycleJobs.AddRange(first, /* ... */);
        await store.DbContext.SaveChangesAsync();
        store.DbContext.ChangeTracker.Clear();
        var queue = CreateQueue(store.Connection);

        var claimed = await queue.ClaimNextAsync("processor-a", TimeSpan.FromMinutes(1));

        Assert.NotNull(claimed);
        Assert.Equal(ELifecycleJobStatus.Running, claimed.Status);
    }
}
```

**Patterns:**
- Arrange / act / assert separated by blank lines; no shared mutable fixture state, each test builds its own store via `await using`.
- Private static factory helpers at the bottom of the class (`CreateJob`, `CreateQueue`, `CreateCommand`, `CreateDbContext`).
- Call `ChangeTracker.Clear()` after seeding and verify through a fresh `DbContext` on the same connection to prove persistence.
- Restart/durability tests construct a new instance over the same storage (`new FlatFileWorkerCommandJournal(directory.Path)`) to simulate process restarts.
- Use `[Theory]`/`[InlineData]` for tabular cases; Vitest uses `it.each`.
- Optional XML `<summary>`/`<returns>` on test methods describing the guarantee.

## Mocking

**Framework:** None for .NET (no Moq/NSubstitute). Hand-written private nested test doubles implementing the real interfaces. React uses `vi.fn`, `vi.stubGlobal`, and Vitest aliases replacing Fluent UI (`tests/mocks/react-components.tsx`, `tests/mocks/react-icons.tsx`).

**Patterns:**
```csharp
private sealed class RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle)
    : HttpMessageHandler { /* records requests, returns scripted responses */ }

private sealed class FakeExecutor : IWorkerCommandExecutor { /* scripted outcomes */ }
```
```ts
const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ok: true }), { status: 200 }));
vi.stubGlobal("fetch", fetchMock);
afterEach(() => vi.unstubAllGlobals());
```
- Names: `Recording*` (captures calls: `RecordingLogger<T>`, `RecordingAccessService`, `RecordingCostSliceService`, `RecordingProcessController`, `RecordingStorageTransfer`), `Fake*` (scripted behavior), `NoOp*` (shared, e.g. `tests/Api.Tests/GameServerHosting/NoOpGameServerAccessService.cs`).

**What to Mock:**
- External HTTP (Hetzner via `HttpMessageHandler`), process control, storage transfer, loggers (to assert secrets are absent), access/cost services peripheral to the subject.

**What NOT to Mock:**
- `DatabaseContext`: use real EF Core over in-memory SQLite (`HostingTestStore`). Filesystem: use `TemporaryDirectory`. Contract serialization: use real `System.Text.Json`.

## Fixtures and Factories

**Test Data:**
```csharp
await using var store = await HostingTestStore.CreateAsync();   // SQLite :memory:, EnsureCreated
var seed = await HostingTestData.SeedServerAsync(store.DbContext, withActiveDeployment: true);

using var directory = new TemporaryDirectory();                  // CloudWorker.Tests
```

**Location:**
- `tests/Api.Tests/GameServerHosting/HostingTestStore.cs`, `HostingTestData.cs`, `HostingTestSeed.cs`, `tests/Api.Tests/MediaAssetTestData.cs`, `tests/Api.Tests/NodaTimeSqliteApiDbContext.cs`.
- `tests/CloudWorker.Tests/TemporaryDirectory.cs`.
- `tests/X39.BI.Config.Parsing.Tests/Resources/` (config/mission.sqm samples).
- `tests/Ui.Tests/WebAppHostFixture.cs` (`IAsyncLifetime`; honors `UTF_UI_TEST_BASE_URL`, otherwise launches WebApp on a free port).
- React: `sources/ReactApp/tests/setup.ts`, `tests/mocks/`, `scripts/classic-stub.mjs` (e2e backend stub).

Test helper types are `internal sealed` and follow one-type-per-file.

## Coverage

**Requirements:** None enforced. coverlet collector is referenced; Vitest coverage reporters `text`, `json`, `html`.

**View Coverage:**
```bash
dotnet test X39.UnitedTacticalForces.slnx --collect:"XPlat Code Coverage"
cd sources/ReactApp && npx vitest run --coverage
```

## Test Types

**Unit Tests:**
- Pure logic (sanitizers, renderers, options validation, parsers, `formatBytes`).

**Integration Tests:**
- Majority of `Api.Tests`: services and controllers against real EF Core on SQLite (lifecycle queue, provider reconciliation, finance, authorization, migrations such as `CorrectCloudGameServerHostingMigrationTests.cs`, OpenAPI contract checks).
- `CloudWorker.Tests`: journal durability, credential store, storage transfer, game implementations against temp dirs.
- CI also runs `tests/Api.Tests/GameServerHosting/verify_ext4_capacity.py` under `unshare` (`.github/workflows/dotnet.yml`). `.mozaic/` holds release pipeline checks and Python tests (`test_*.py`, `verify-test-results.py`).

**E2E Tests:**
- Playwright: `tests/Ui.Tests` (Blazor WebApp, Chromium headless) and `sources/ReactApp/tests/e2e` (against `vite preview` + classic stub).

Required focus per `AGENTS.md`: provider idempotency, attribution, teardown verification, worker authentication and redelivery, persistence, job transitions, authorization-sensitive actions, recovery.

## Common Patterns

**Async Testing:**
```csharp
[Fact]
public async Task TerminalOutcome_SurvivesRestartAndPreventsRegression()
{
    using var directory = new TemporaryDirectory();
    var first = new FlatFileWorkerCommandJournal(directory.Path);
    await first.RecordReceivedAsync(command);
    var entry = await new FlatFileWorkerCommandJournal(directory.Path).RecordReceivedAsync(command);
    Assert.Equal(EWorkerCommandJournalState.Succeeded, entry.State);
}
```

**Error Testing:**
```csharp
await Assert.ThrowsAsync<HostingConflictException>(() => service.DoAsync(...));
Assert.DoesNotContain("payload", json, StringComparison.OrdinalIgnoreCase); // secret-leak checks
```

---

*Testing analysis: 2026-11-18*
