#!/bin/sh
# Use the ordinary job image's native Python and matching system libraries.
set -eu
if [ "${1:-}" = python ]; then
    shift
    exec python3 "$@"
fi
exec python3 /workspace/.mozaic/oci-layout/assemble.py "$@"
