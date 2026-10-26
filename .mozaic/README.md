# Mozaic CI

The active trusted release definition is [release.yml](release.yml). Its
`checked-artifacts` job runs the required suites and assembles all four publication
inputs in the same workspace. [pipeline.yml](pipeline.yml) retains pull-request
feedback. See [RELEASE.md](RELEASE.md) for checks and immutable publication rules,
and [DEPLOYMENT.md](DEPLOYMENT.md) for data, worker and acceptance requirements.

The ordinary generic deployment client is checked in under [ci](../ci/README.md).
Its target manifest and downstream invocation require the shared owner's reviewed
target/account/known-host values and an executable infrastructure contract.

The canonical source is `https://x39.mozaic.space/git/x39/united-tactical-forces.git`
(repository `01a166ad-93f3-7fa2-a4f0-e57014752726`). The existing `main` and
`master` refs are ancestors of the prepared local `master`; preserve that complete
history through an authorized regular fast-forward. The current default is `main`;
the coordinator must select trusted `master` through the supported policy flow.
GitHub origin remains preserved. Do not create a replacement source repository,
flatten history, force-push, or dispatch an emergency release.

All delivered images must be built and tested on the existing Mozaic cloud worker.
The current placement selectors are `fedora-44-cloud-v1` and `bounded-disk-v1`.
Source pins and resource declarations are requirements, not proof of live admission,
capacity, registry publication, runtime execution or deployment.

Earlier native-builder/static-pilot instructions are retained in Git history.
The SDK preparation directory contains separate work; do not treat it as an
alternate approved release path.
