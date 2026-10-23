#!/bin/sh
# Run only against the isolated source snapshot; no production configuration is needed.
set -eu
export DOTNET_PROCESSOR_COUNT=1
export CI=true
mkdir -p /proof/test-results
dotnet restore X39.UnitedTacticalForces.slnx --disable-parallel
dotnet build X39.UnitedTacticalForces.slnx --configuration Release --no-restore --maxcpucount:1
dotnet run --project sources/Api/Api.csproj --configuration Release --no-build -- \
    --export-openapi /proof/utf-api.json
cmp sources/ReactApp/openapi/utf-api.json /proof/utf-api.json

# Ui.Tests normally starts a Debug host. Supply a dedicated Release host instead.
export UTF_UI_TEST_BASE_URL=http://127.0.0.1:4183/
export NO_PROXY=localhost,127.0.0.1
export no_proxy="$NO_PROXY"
dotnet run --project sources/WebApp/WebApp.csproj --configuration Release \
    --no-build --no-launch-profile --urls "$UTF_UI_TEST_BASE_URL" > /proof/blazor-host.log 2>&1 &
host_pid=$!
trap 'kill "$host_pid" 2>/dev/null || true; wait "$host_pid" 2>/dev/null || true' EXIT HUP INT TERM
ready=false
for attempt in $(seq 1 90); do
    if curl --fail --silent "$UTF_UI_TEST_BASE_URL" > /dev/null; then
        ready=true
        break
    fi
    if ! kill -0 "$host_pid" 2>/dev/null; then
        cat /proof/blazor-host.log
        exit 1
    fi
    sleep 1
done
if [ "$ready" != true ]; then
    cat /proof/blazor-host.log
    exit 1
fi
dotnet test X39.UnitedTacticalForces.slnx --configuration Release --no-build \
    --maxcpucount:1 --logger trx --results-directory /proof/test-results
dotnet publish sources/Api/Api.csproj --configuration Release --no-restore \
    --maxcpucount:1 --output /tmp/utf-api /p:UseAppHost=false
dotnet publish sources/WebApp/WebApp.csproj --configuration Release --no-restore \
    --maxcpucount:1 --output /tmp/utf-web /p:UseAppHost=false
printf 'Release solution, OpenAPI, tests and publishing passed\n' > /proof/result
