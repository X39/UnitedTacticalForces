#!/bin/sh
# Copy this ordinary script and its Python sibling into ci/ in the application.
set -eu
exec python3 "$(dirname "$0")/invoke-protected-deploy.py" "$@"
