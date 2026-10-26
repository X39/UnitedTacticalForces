#!/bin/sh
# Use staged Jammy browser dependencies with the admitted Bookworm worker libc.
set -eu
cd /workspace
mode=${1:?A browser check name is required}
case "$mode" in
    dotnet-release) version=1.61.0 ;;
    react-browser)
        version=1.62.1
        # Firefox's nested chroot is unavailable in the job; retain its seccomp-bpf fallback.
        # https://bugzilla.mozilla.org/show_bug.cgi?id=1884347#c8
        export MOZ_ASSUME_USER_NS=0
        ;;
    *) printf 'Usage: %s dotnet-release|react-browser\n' "$0" >&2; exit 2 ;;
esac
helper=/workspace/.mozaic/oci-layout/run.sh
prefix=/workspace/.browser-deps-$version
sh "$helper" python .mozaic/stage-browser-deps.py "$version" "$prefix"
. "$prefix/environment.sh"
sh .mozaic/check-ordinary.sh "$mode"
