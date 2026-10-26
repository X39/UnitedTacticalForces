#!/bin/sh
# Keep required checks and the four publication inputs in the same job workspace.
set -eu
cd /workspace
helper=/workspace/.mozaic/oci-layout/run.sh

sh .mozaic/check-ordinary.sh react-tests
sh .mozaic/check-browser.sh dotnet-release
sh .mozaic/check-browser.sh react-browser

# The root canary uses the same checked sources with a different public base path.
(
    cd sources/ReactApp
    UTF_REACT_BASE_PATH=/ npm run build:assets
)

# Preserve the worker's pinned SDK family and test its actual Release binaries.
sh "$helper" stage-dotnet /workspace/.dotnet-worker 302
mkdir -p /workspace/.worker-source
# Exclude the SDK201 build products so SDK302 must compile the worker afresh.
tar --exclude=bin --exclude=obj -cf /workspace/.worker-source/source.tar \
    sources/HostingContracts sources/GameServerImplementations sources/CloudWorker tests/CloudWorker.Tests
tar -xf /workspace/.worker-source/source.tar -C /workspace/.worker-source
printf '{"sdk":{"version":"10.0.302","rollForward":"disable"}}\n' \
    > /workspace/.worker-source/global.json
(
    cd /workspace/.worker-source
    export DOTNET_ROOT=/workspace/.dotnet-worker
    export PATH="$DOTNET_ROOT:$PATH"
    test "$(dotnet --version)" = 10.0.302
    dotnet restore tests/CloudWorker.Tests/CloudWorker.Tests.csproj --disable-parallel
    dotnet build tests/CloudWorker.Tests/CloudWorker.Tests.csproj --configuration Release \
        --no-restore --maxcpucount:1
    # Match the worker's fixed credential-run-script boundary while retaining disk scratch for builds/browsers.
    TMPDIR=/tmp TEMP=/tmp TMP=/tmp dotnet test tests/CloudWorker.Tests/CloudWorker.Tests.csproj --configuration Release \
        --no-build --no-restore --maxcpucount:1 --logger trx \
        --results-directory "$UTF_PROOF_DIRECTORY/worker-test-results"
    sh "$helper" python /workspace/.mozaic/verify-test-results.py \
        "$UTF_PROOF_DIRECTORY/worker-test-results" CloudWorker.Tests
    dotnet publish sources/CloudWorker/CloudWorker.csproj --configuration Release \
        --no-build --no-restore --no-self-contained --maxcpucount:1 \
        --output "$UTF_CHECK_PUBLISH_DIRECTORY/worker"
)

mkdir -p /workspace/images
for component in api web worker aska; do
    sh "$helper" assemble "$component" /workspace "/workspace/images/$component.tar"
done
