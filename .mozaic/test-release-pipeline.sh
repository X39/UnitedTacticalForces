#!/bin/sh
# Exercise orchestration with inert commands; no application build or network access.
set -eu
repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
# The runner deliberately mounts /tmp noexec; command doubles need executable scratch.
temporary=$(mktemp -d "$repo_root/.mozaic-release-test.XXXXXX")
trap 'rm -rf "$temporary"' EXIT HUP INT TERM

for failure in none react-tests dotnet-release react-browser worker-test worker-evidence; do
    workspace=$temporary/$failure
    mkdir -p "$workspace/.mozaic/oci-layout" "$workspace/bin" \
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
    cat > "$workspace/.mozaic/oci-layout/run.sh" <<'HELPER'
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
if [ "$1" = test ]; then
    test "$TMPDIR" = /tmp && test "$TEMP" = /tmp && test "$TMP" = /tmp
    test "$FAILURE" != worker-test
fi
DOTNET
    cat > "$workspace/bin/npm" <<'NPM'
#!/bin/sh
printf 'npm %s\n' "$*" >> "$TRACE"
NPM
    chmod +x "$workspace/bin/"* "$workspace/.mozaic/oci-layout/run.sh"
    for command in npm dotnet; do
        test "$(PATH="$workspace/bin:$PATH" command -v "$command")" = "$workspace/bin/$command"
    done
    export TRACE=$workspace/trace FAILURE=$failure
    export UTF_PROOF_DIRECTORY=$workspace/proof UTF_CHECK_PUBLISH_DIRECTORY=$workspace/.publish
    if PATH="$workspace/bin:$PATH" sh "$workspace/release.sh"; then
        test "$failure" = none
        for component in api web worker; do test -s "$workspace/images/$component.tar"; done
        test ! -e "$workspace/images/aska.tar"
        grep -q '^dotnet publish .*--no-build --no-restore' "$TRACE"
        test "$(grep -c '^assemble ' "$TRACE")" = 3
    else
        test "$failure" != none
        test ! -d "$workspace/images"
        if grep -q '^assemble ' "$TRACE"; then exit 1; fi
    fi
done
printf 'Release orchestration passed: three outputs; every required suite fails closed.\n'
