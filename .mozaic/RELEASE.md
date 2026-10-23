# Full UTF release pipeline

Register `.mozaic/release.yml` as a separate named pipeline on trusted `main`
after verifying the applied full-fleet runner configuration. The original
`.mozaic/pipeline.yml` remains the unchanged first static acceptance definition.
The full definition uses the same fixed native action and resource ceilings:
1 CPU, 1536 MiB memory, 1024 MiB scratch, 256 PIDs, 32 MiB logs and 30 minutes
per job. Nine jobs form one serial chain; no companion service is declared.
The dedicated native builder filesystem retains its independent operator limit.

## Actual native registry bindings

All components use applications registry `01a166b4-bc34-7973-a711-bf840c866313`.

| Repository | Native repository ID |
| --- | --- |
| `utf/web` | `01a166b4-bcbc-7403-b21e-46de55ee811f` |
| `utf/api` | `01a166c1-9739-7210-95bf-12f3e61ae8e0` |
| `utf/cloud-worker` | `01a166c1-9796-79b3-80ee-041bb0075417` |
| `utf/cloud-worker-aska` | `01a166c1-97fa-7431-83a3-6dabe264c858` |

The owner returned these bindings. Registry publication uses the literal alias
`release` because deployed pipeline version 1 accepts literal tags only, without
source-SHA interpolation. Immutable release identity is each accepted manifest
digest together with its exact source revision and run/plan receipt. Require all
nine jobs and all four publications from the same successful trusted run; partial
publication cannot authorize deployment. Do not treat the reusable alias as an
immutable source-SHA tag. If source-SHA tags become mandatory, that needs supported
native publication functionality rather than a job-side registry credential.

## Required checks and browser execution

React contracts, localization, formatting, lint, types, unit/component tests and
asset build precede the native release checks. The .NET proof restores and builds
the full solution in Release, compares the exported API OpenAPI snapshot, runs all
solution test projects, and publishes API and Blazor. It explicitly starts a
Release Blazor host for the existing UI fixture; no test authorization bypass or
production service is used. Both package-ownership fixture targets remain gates.

Browser tests execute through the same reviewed native action that permits
namespaced package installation. They receive no host socket or credentials.
`dotnet-release.Containerfile` copies Node 24 from the already approved Bookworm
image into the approved SDK image, installs Chromium through npm Playwright 1.61.0
matching Microsoft.Playwright in Ui.Tests, and runs the .NET release script.
`react-browser.Containerfile` uses the approved Node 24 Bookworm base and locked
React Playwright 1.62.1. Full releases retain the existing workflow's Chromium,
Firefox and WebKit journeys with one worker. Chromium is required for ordinary
frontend CI; the broader matrix was additionally required by scheduled, manual
and published-release runs in the retained GitHub workflow. It is not silently
removed here. The GitHub workflow remains intact during migration.

Both test Dockerfiles end in a small scratch proof image so browser layers and
build caches do not enter the 1024 MiB output archive. Native intermediate layers
still count against the separate bounded builder filesystem; actual acceptance
must establish memory, time and storage fit. Proof archives include test output;
actual pass/fail comes from native RUN exit status and accepted job completion.
Native proof results are not exposed as ordinary workspace TRX declarations.

Exact permitted download hosts include Debian package hosts, npm, NuGet,
`cdn.playwright.dev` and `playwright.download.prss.microsoft.com`; worker builds
add `steamcdn-a.akamaihd.net` and verify the existing SteamCMD SHA256. Investigate
actual denied redirect hosts before reviewing an allowlist addition. Do not
replace controlled proxy access with host networking.

## Runner inputs and activation gates

The worker Dockerfiles preserve their existing complete pinned FROM aliases and
linux/amd64 requirement. Their SDK digest `3dae2f7699441af56216ff64d5c9b6dfce7cd7dc7f4f71d353d29662b10a384f`
and runtime digest `ca17638b4cbab2063771e8943dbe887ea6c0ae00c5fede3498d5938758c85dfe`
need the owner's additional full-fleet allowlist/preload, beyond the original
seven-image profile. The owner is preparing that configuration; this checkout
does not assert it has been applied or accepted. Browser execution, multilib,
Wine/ASKA, worker archive size and builder capacity require real native attempts.
An oversized or denied build fails closed without increasing resource limits.

Worker and ASKA images are per-deployment runtime dependencies, never extra
services on the API's shared host. Protected activation still requires the
runtime inputs, persistent storage/key ring, PostgreSQL 18 startup/migration and
functional readiness checks recorded in README.md and the owner's release
readiness handoff. No CI success substitutes for those gates.

## Local evidence

- CloudWorker Release tests: 114 passed, no skips.
- API GameServerHosting Release tests: 355 passed, no skips.
- Initial handoff: parser tests 93 passed; Blazor Release build passed.
- Pipeline YAML parsing, shell syntax and independent pinned-contract review passed.
- No local native build, browser run, publication or deployment is claimed.
- React checks with bundled Node 24.19.0 direct CLIs: lint/type checks passed;
  unit/component tests 321/321 passed; contract/locale generation matched committed
  outputs; Prettier and production assets passed after correcting formatting in
  event-detail.spec.ts. Local dependency installation used `--ignore-scripts`
  after the Windows npm postinstall PATH failure; this is not a claim that clean
  Linux `npm ci` or any browser/native job has passed.
