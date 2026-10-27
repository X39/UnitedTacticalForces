# Ordinary UTF OCI assembly

This helper adapts the accepted Jodel registry verifier. The ordinary admitted
job uses its native Python and stages the existing digest-pinned toolchains.
It runs the standard-library assembler without an engine, socket,
chroot, package manager, target executable, or package maintainer script.

Commands are `stage-node PREFIX`, `stage-dotnet PREFIX [201|302]`,
`assemble api|web|worker SOURCE OUTPUT`, `verify COMPONENT ARCHIVE`, and
`python SCRIPT ARGS`. The .NET executable is at `PREFIX/dotnet`; Node is at
`PREFIX/bin/node`. Compiled application inputs are `.publish/api`,
`.publish/web/wwwroot`, `.publish/worker`, and the existing React outputs.

Runtime base manifests, configs, layer digests and uncompressed identities are
verified. Ubuntu Noble package indexes chain to SHA-256-pinned Release documents
at snapshot `20261024T000000Z`; each package payload is SHA-256 verified.
The original snapshot URL and every Release, index and package digest remain
authoritative. CI downloads those same bytes from the existing CI-restricted
`https://app.mozaic.space/mozaic-release-inputs/ubuntu-snapshot-20261024T000000Z/`
mirror because the upstream snapshot service is IPv4-only. The mirror preserves
the upstream relative paths and cannot replace the pinned integrity checks.
The same restricted input service delivers the unchanged SteamCMD installer at
its existing digest-named path; its original upstream URL and SHA-256 remain
in the assembly request. Neither delivery path is an arbitrary proxy.
Required and pre-required dependencies are resolved against the base's installed
package metadata, including i386 worker dependencies. Package maintainer scripts
are deliberately not run. Runtime users, Wine command links and data ownership
are represented directly in deterministic layer metadata. Existing CA bundles
and the loader's standard library directories supply runtime lookup; no target
`ldconfig` runs. This change requires actual CI validation of the resulting Wine,
SteamCMD and managed applications before deployment.

Assembly verifies the serialized archive's launch configuration, payloads,
ownership and modes. Base fetching, diff-ID verification and output tar writing
stream large layers; individual package downloads remain bounded to 1 GiB.
The shared worker includes Wine/Xvfb support for ASKA as well as the Linux game
executors. It contains runtime tooling, not installed game files. Its final size
must satisfy the registry's publication limit;
the helper cannot establish that limit by source inspection.

Focused inert metadata and unit checks may run locally. Real application image
builds, required full release tests and toolchain runtime acceptance run through
the ordinary Mozaic worker. Local checks do not authorize deployment or imports.
