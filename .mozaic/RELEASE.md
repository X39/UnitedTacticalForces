# UTF trusted release

The active trusted definition is `.mozaic/release.yml`. Its ordinary
`checked-artifacts` job checks and packages API, web, and one shared worker in one workspace.
`.mozaic/pipeline.yml` retains the same check suites for pull-request feedback.

The former split came from ASKA's Windows runtime support: commit `1bcf282a`
introduced a dedicated Wine Dockerfile while the same CloudWorker executable
registered Arma 3, ASKA, and CS2 executors. Game metadata and provisioning options
selected a separate ASKA image, and `cd66ab1d` made its publication a mandatory
fourth release artifact. The current contract uses one shared worker with the
combined runtime dependencies and chooses the executor from the bootstrap game
identifier. This changes packaging and image selection, not the external-worker
boundary or game installation commands.

The admitted job image supplies native Python 3.11 and its system libraries.
`build.sh` invokes `oci-layout/run.sh`, which calls that Python directly to stage
the pinned Node 24 and .NET SDK 10.0.201/10.0.302 toolchains and reuse the existing
OCI assembly code. No Rust compilation or transported Python bootstrap is part
of the active workflow. The image's Rust tooling and historical helper sources
are unused; the image remains the already verified ordinary Python/SSH toolbox.

Node 24.11.1 is staged from the official Linux x64 `.tar.xz` distribution,
pinned by SHA-256 and exact size. This transfers 30,747,116 bytes instead of
407,538,358 bytes of Node container layers. Only Node/npm/npx are extracted;
archive paths, entry types and expansion are bounded before the toolchain is
made available. The .NET and runtime OCI inputs retain their existing pins.

The release runs React contract/localization generation checks, formatting,
lint, TypeScript, unit/component tests, the full Release .NET solution suite,
the exported OpenAPI comparison, and Chromium journeys. API and
Blazor publishing uses `--no-build` after the checked Release build. Workers
retain their separately pinned SDK; their test project is rebuilt and tested
under that SDK before publishing its binaries with `--no-build`. SDK201 build
products are excluded from the worker source copy.

Release validation requires nonzero passing TRX results from every .NET test
assembly, including the Blazor Chromium scenarios, and from the separate worker
SDK run. Missing reports, zero execution, failed tests and skipped tests block
packaging. Local inert orchestration and TRX regression checks also run in CI.
Browser API responses are isolated test fixtures; API persistence tests execute
against SQLite. Neither suite proves real PostgreSQL migration/startup or remote
worker operation, which remain explicit acceptance gates.

All three archives are assembled only after these checks succeed. Terminal
publication declarations bind distinct `images/api.tar`, `images/web.tar`,
and `images/worker.tar` inputs to `utf/api`, `utf/web`, and `utf/cloud-worker` in registry
`01a166b4-bc34-7973-a711-bf840c866313`. No dependency transfers workspace files.
The shared worker includes Wine/Xvfb for ASKA and the same executor registry for
Arma 3 and Counter-Strike 2. No game files are installed during image assembly.
Consolidation avoids a duplicate worker artifact; it does not make Wine small or
replace importer size, extraction, and resource limits.
The mutable `release` tag is only an alias: acceptance requires all three exact
manifest receipts from this source and run.

The existing job resource and timeout limits are retained. Actual cloud timing,
browser execution, image runtime compatibility, archive-size admission and
publication remain unverified until a required CI attempt succeeds. Local
syntax/orchestration checks cannot replace those checks.

The trusted default branch is `master`. The verified canonical destination is
`https://x39.mozaic.space/git/x39/united-tactical-forces.git`; its existing refs are
ancestors of the prepared source. The coordinator reconciled default selection
and governed push policy through the audited operator path.

The ordinary `activate-release` job requires `checked-artifacts` and sends
the exact three OCI publications to the pinned Amsel endpoint. It uses the
Amsel-owned SSH key in project secret `managed-utf-ssh` and short-lived
job-side OCI credentials. The UTF-specific receiver independently checks
the active attempt and receipts. Receive-only is the initial mode.

The coordinator must migrate the protected target/input contract described in
`.mozaic/DEPLOYMENT.md` and verify its readiness before an authorized source update.
Ordinary capacity admission remains automatic; there is no per-push slot permit.
Prepared invocation configuration does not establish an installed target or
successful deployment. Source consolidation does not authorize restarting a
stopped host or importing an image before the importer resource issue is resolved.

See [runtime requirements](DEPLOYMENT.md). Superseded native-builder and emergency
release instructions remain in Git history and are not activation instructions.
