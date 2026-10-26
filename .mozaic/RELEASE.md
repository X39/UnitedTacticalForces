# UTF trusted release

The active trusted definition is `.mozaic/release.yml`. Its ordinary
`checked-artifacts` job checks and packages all four components in one workspace.
`.mozaic/pipeline.yml` retains the same check suites for pull-request feedback.

The release runs React contract/localization generation checks, formatting,
lint, TypeScript, unit/component tests, the full Release .NET solution suite,
the exported OpenAPI comparison, and Chromium/Firefox/WebKit journeys. API and
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

All four archives are assembled only after these checks succeed. Terminal
publication declarations bind distinct `images/api.tar`, `images/web.tar`,
`images/worker.tar`, and `images/aska.tar` inputs to `utf/api`, `utf/web`,
`utf/cloud-worker`, and `utf/cloud-worker-aska` in registry
`01a166b4-bc34-7973-a711-bf840c866313`. No dependency transfers workspace files.
The mutable `release` tag is only an alias: acceptance requires all four exact
manifest receipts from this source and run.

The existing job resource and timeout limits are retained. Actual cloud timing,
browser execution, image runtime compatibility, archive-size admission and
publication remain unverified until a required CI attempt succeeds. Local
syntax/orchestration checks cannot replace those checks.

The trusted default branch is `master`. The verified canonical destination is
`https://x39.mozaic.space/git/x39/united-tactical-forces.git`; its existing refs are
ancestors of the prepared source. The coordinator reconciled default selection
and governed push policy through the audited operator path.

The ordinary `activate-release` job requires `checked-artifacts` and invokes
the generic SSH client with its exact requesting attempt. Its protected key
reference is `utf_target_invoke`; endpoint and host-key bindings are checked in
in `deploy/production.json` and `deploy/known_hosts`. The target must accept all four same-run publication
receipts, activate their exact worker dependency pins and finish its coherent
stateful backup/startup/health checks before the command succeeds. Runtime and
registry credentials stay on the target.

The coordinator must verify target/input readiness, bind the actual server plan
and release the UTF CI slot before the trusted source update. Prepared invocation
configuration does not establish an installed target or successful deployment.

See [runtime requirements](DEPLOYMENT.md). Superseded native-builder and emergency
release instructions remain in Git history and are not activation instructions.
