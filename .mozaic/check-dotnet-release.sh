#!/bin/sh
# Run only against the isolated source snapshot; no production configuration is needed.
set -eu
export DOTNET_PROCESSOR_COUNT=1
export CI=true
proof_directory="${UTF_PROOF_DIRECTORY:-/proof}"
publish_directory="${UTF_CHECK_PUBLISH_DIRECTORY:-${TMPDIR:-/tmp}/utf-release-check}"
mkdir -p "$proof_directory/test-results" "$publish_directory"
dotnet restore X39.UnitedTacticalForces.slnx --disable-parallel
dotnet build X39.UnitedTacticalForces.slnx --configuration Release --no-restore --maxcpucount:1
dotnet run --project sources/Api/Api.csproj --configuration Release --no-build -- \
    --export-openapi "$proof_directory/utf-api.json"
if ! cmp sources/ReactApp/openapi/utf-api.json "$proof_directory/utf-api.json"; then
    # Preserve a bounded public-schema diagnostic before failed-job scratch is cleaned.
    diff -u sources/ReactApp/openapi/utf-api.json "$proof_directory/utf-api.json" \
        > "$proof_directory/utf-api.diff" || test "$?" = 1
    head -n 160 "$proof_directory/utf-api.diff"
    sha256sum sources/ReactApp/openapi/utf-api.json "$proof_directory/utf-api.json"
    exit 1
fi

# Ui.Tests normally starts a Debug host. Supply a dedicated Release host instead.
export UTF_UI_TEST_BASE_URL=http://127.0.0.1:4183/
export NO_PROXY=localhost,127.0.0.1
export no_proxy="$NO_PROXY"
dotnet run --project sources/WebApp/WebApp.csproj --configuration Release \
    --no-build --no-launch-profile --urls "$UTF_UI_TEST_BASE_URL" > "$proof_directory/blazor-host.log" 2>&1 &
host_pid=$!
trap 'kill "$host_pid" 2>/dev/null || true; wait "$host_pid" 2>/dev/null || true' EXIT HUP INT TERM
ready=false
for attempt in $(seq 1 90); do
    # Staged browser libraries must not replace host curl's matching libcurl.
    if LD_LIBRARY_PATH= curl --fail --silent "$UTF_UI_TEST_BASE_URL" > /dev/null; then
        ready=true
        break
    fi
    if ! kill -0 "$host_pid" 2>/dev/null; then
        cat "$proof_directory/blazor-host.log"
        exit 1
    fi
    sleep 1
done
if [ "$ready" != true ]; then
    cat "$proof_directory/blazor-host.log"
    exit 1
fi
dotnet test X39.UnitedTacticalForces.slnx --configuration Release --no-build \
    --maxcpucount:1 --logger trx --results-directory "$proof_directory/test-results"
sh /workspace/.mozaic/oci-layout/run.sh python .mozaic/verify-test-results.py \
    "$proof_directory/test-results" Api.Tests CloudWorker.Tests Ui.Tests X39.BI.Config.Parsing.Tests
dotnet publish sources/Api/Api.csproj --configuration Release --no-build --no-restore \
    --maxcpucount:1 --output "$publish_directory/api" /p:UseAppHost=false
dotnet publish sources/WebApp/WebApp.csproj --configuration Release --no-build --no-restore \
    --maxcpucount:1 --output "$publish_directory/web" /p:UseAppHost=false
printf 'Release solution, OpenAPI, tests and publishing passed\n' > "$proof_directory/result"
