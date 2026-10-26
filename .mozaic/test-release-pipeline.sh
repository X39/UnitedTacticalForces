#!/bin/sh
# Exercise orchestration with inert commands; no application build or network access.
set -eu
repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
temporary=$(mktemp -d)
trap 'rm -rf "$temporary"' EXIT HUP INT TERM

for failure in none react-tests dotnet-release react-browser worker-test worker-evidence; do
    workspace=$temporary/$failure
    mkdir -p "$workspace/.mozaic" "$workspace/.oci-target/release" "$workspace/bin" \
        "$workspace/sources/ReactApp" "$workspace/sources/HostingContracts" \
        "$workspace/sources/GameServerImplementations" "$workspace/sources/CloudWorker" \
        "$workspace/tests/CloudWorker.Tests"
    sed "s|/workspace|$workspace|g" "$repo_root/.mozaic/check-and-package-release.sh" \
        > "$workspace/release.sh"
    cat > "$workspace/.mozaic/check-ordinary.sh" <<'CHECK'
set -eu
printf '%s\n' "$1" >> "$TRACE"
test "$FAILURE" != "$1"
CHECK
    cp "$workspace/.mozaic/check-ordinary.sh" "$workspace/.mozaic/check-browser.sh"
    cat > "$workspace/.oci-target/release/utf-oci-layout" <<'HELPER'
#!/bin/sh
set -eu
printf '%s\n' "$*" >> "$TRACE"
if [ "$1" = assemble ]; then printf 'checked archive\n' > "$4"; fi
if [ "$1" = python ]; then test "$FAILURE" != worker-evidence; fi
HELPER
    cat > "$workspace/bin/dotnet" <<'DOTNET'
#!/bin/sh
set -eu
printf 'dotnet %s\n' "$*" >> "$TRACE"
if [ "$1" = --version ]; then
    grep -q '"version":"10.0.302"' global.json
    printf '10.0.302\n'
fi
if [ "$1" = test ]; then test "$FAILURE" != worker-test; fi
DOTNET
    cat > "$workspace/bin/npm" <<'NPM'
#!/bin/sh
printf 'npm %s\n' "$*" >> "$TRACE"
NPM
    chmod +x "$workspace/bin/"* "$workspace/.oci-target/release/utf-oci-layout"
    export TRACE=$workspace/trace FAILURE=$failure
    export UTF_PROOF_DIRECTORY=$workspace/proof UTF_CHECK_PUBLISH_DIRECTORY=$workspace/.publish
    if PATH="$workspace/bin:$PATH" sh "$workspace/release.sh"; then
        test "$failure" = none
        for component in api web worker aska; do test -s "$workspace/images/$component.tar"; done
        grep -q '^dotnet publish .*--no-build --no-restore' "$TRACE"
        test "$(grep -c '^assemble ' "$TRACE")" = 4
    else
        test "$failure" != none
        test ! -d "$workspace/images"
        if grep -q '^assemble ' "$TRACE"; then exit 1; fi
    fi
done
printf 'Release orchestration passed: four outputs; every required suite fails closed.\n'
