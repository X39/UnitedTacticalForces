# Mozaic CI handoff

## Current cloud job placement (2026-10-25)

Pipeline selectors request `host-profile: fedora-44-cloud-v1` and
`workspace-storage: bounded-disk-v1`. Existing checks, immutable image pins,
resource limits and trusted-branch-only publication are preserved. These
selectors establish placement requirements; they do not establish image
admission or native Dockerfile support. The current Fedora worker rejects
Debian-only native actions; retain that failure and do not claim publication
until a compatible build path and actual cloud run pass. No host provisioning,
SDK build, runtime credential or deployment change is included here.

This definition follows the deployed Mozaic `1d8d568` static pilot and native
package-ownership fixture. Its historical Debian runner configuration was
`f464e4f4-e627-4be1-abda-fa23f63c5bf7`, revision 1, with the
`debian-13-trusted-organization-v1` profile. That profile makes no per-container
AppArmor claim. Jobs are serialized and require no runtime credentials.

## Registration

- Project: `01a166ad-9024-7b30-a9bc-cd03ac37f2e8`.
- Source repository: `01a166ad-93f3-7fa2-a4f0-e57014752726`.
- Trusted branch: `main`; definition path: `.mozaic/pipeline.yml`.
- Native applications registry: `01a166b4-bc34-7973-a711-bf840c866313`.
- OCI repository: `utf/web` (`01a166b4-bcbc-7403-b21e-46de55ee811f`).
- Publication alias: `pilot`. Deployment must consume the accepted publication
  receipt and immutable manifest digest, never infer approval from that alias.

The live owner registers the definition and performs the governed push. The
source checkout does not contain transport credentials or deployment authority.
All job and Dockerfile base references are selected from the supplied seven-image
allowlist; the Debian image is not needed by this definition.

## Acceptance boundary

React lint, generated contracts/locales, types, unit tests and asset build precede
the Blazor build and parser tests. Two native jobs exercise both targets in the
repository-owned fixture: apt and app-directory ownership, then apt, pip,
useradd and chown. Both targets assert process restrictions and write access as
the resulting nonroot user. The web image uses the existing root Dockerfile and
the supervisor-owned native build/publication action.

Local checks do not establish native runner acceptance. Retain successful trusted
run/attempt IDs, bounded resource and cleanup observations, both fixture results,
publication digest and a real web asset smoke check from the live owner. Investigate
any denied package host in bounded logs before reviewing allowlist changes.

## Full release continuation

The separate `release.yml` definition and `RELEASE.md` now contain the full
component pipeline, registry bindings and activation gates. The original static
definition remains unchanged.

The static acceptance pipeline is the first handoff, not full application release
approval. Keep API, web, native CloudWorker and ASKA images in the full release.
CloudWorker and ASKA are per-deployment release dependencies; they must not become
permanent services on the shared application host.

The two worker Dockerfiles currently use SDK digest
`3dae2f7699441af56216ff64d5c9b6dfce7cd7dc7f4f71d353d29662b10a384f`
and runtime digest
`ca17638b4cbab2063771e8943dbe887ea6c0ae00c5fede3498d5938758c85dfe`,
which are absent from the approved seven-image profile. Operator approval and
preloading, exact component repository IDs, package download host review, and
native builder compatibility/capacity acceptance are required before enabling
their publication jobs. Do not silently substitute unrelated bases or enlarge limits.

Full release checks still include solution build/tests, API and Blazor publishing,
React CI and Chromium tests, OpenAPI snapshot comparison, actual component OCI
builds and PostgreSQL 18 migration/startup acceptance. The small static runner
profile has not established capacity for those broader checks.

Protected activation also needs immutable component receipts, registry pull access,
protected database inputs, durable API media/hosting storage and an absolute durable
key-ring path. Preserve API DNS alias `api:8080` for the bundled web proxy. Production
has no `/health` or `/alive` route; agree a functional readiness check. Steam and
Discord can be explicitly disabled for initial boot; authorization bypass remains
disabled. Runtime input activation and deployment stay with the live owner and
must never use public CI variables as a secret channel.

## Local verification for the initial handoff

- Release Blazor build with `--no-restore --maxcpucount:1`: passed, 42 existing
  warnings and no errors.
- Release parser tests with `--no-restore --maxcpucount:1`: 93 passed, no skips.
- Independent review against the pinned pipeline schema/native action: passed;
  `git diff --check`: passed.
- React checks were attempted but not completed locally: the existing dependency
  directory lacked executable tools, and `npm ci` failed when its Windows msw
  postinstall hook could not resolve `node`. These checks remain mandatory in CI.
- Native builds/publication and runtime acceptance have not been run locally.
