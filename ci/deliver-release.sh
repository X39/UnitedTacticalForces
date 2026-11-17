#!/bin/sh
set -eu
: "${MOZAIC_ATTEMPT_ID:?The runner must supply this exact requesting attempt}"
mode=$(cat ci/oci-mode)
case "$mode" in receive|activate) ;; *) exit 1 ;; esac
archive_limit=1073741824
reader=/run/mozaic/secrets/deployment_oci_read
release=$(python3 ci/oci_download.py --credential "$reader" \
  --origin https://x39.mozaic.space --attempt "$MOZAIC_ATTEMPT_ID" \
  --max-archive-bytes "$archive_limit")
sh ci/deploy.sh --target amsel --manifest deploy/production.json \
  --release-job checked-artifacts --attempt "$MOZAIC_ATTEMPT_ID" \
  "--$mode-oci" "$release" --attempt-evidence-file "$reader" \
  --max-archive-bytes "$archive_limit" --wait --timeout 600
