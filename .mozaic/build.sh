#!/bin/sh
# Check and package only inside an ordinary Mozaic worker job.
set -eu
report_resources()
{
    # Best-effort observations from this necessary job, without changing its limits.
    for name in memory.peak memory.events pids.peak pids.events; do
        path=/sys/fs/cgroup/$name
        if [ -r "$path" ]; then
            printf '[utf] cgroup %s\n' "$name"
            head -c 512 "$path" || true
        fi
    done
}
trap 'status=$?; set +e; report_resources; exit "$status"' EXIT
cd /workspace
mode=${1:?A check or component name is required}
export HOME=/workspace/.home
export npm_config_cache=/workspace/.npm-cache NUGET_PACKAGES=/workspace/.nuget/packages
export DOTNET_CLI_HOME=/workspace/.dotnet-home DOTNET_PROCESSOR_COUNT=1
export UTF_PROOF_DIRECTORY=/workspace/test-results
export UTF_CHECK_PUBLISH_DIRECTORY=/workspace/.publish
mkdir -p "$HOME" "$UTF_PROOF_DIRECTORY" "$UTF_CHECK_PUBLISH_DIRECTORY"
# Native Python comes from the admitted job image; no compiler bootstrap is required.
helper=/workspace/.mozaic/oci-layout/run.sh
case "$mode" in
    react-tests|react-browser|web|dotnet-release|release)
        sh "$helper" stage-node /workspace/.node
        export PATH="/workspace/.node/bin:$PATH"
        ;;
esac
case "$mode" in
    dotnet-release|api|web|release)
        sh "$helper" stage-dotnet /workspace/.dotnet 201
        export DOTNET_ROOT=/workspace/.dotnet PATH="/workspace/.dotnet:$PATH"
        ;;
    worker|aska)
        sh "$helper" stage-dotnet /workspace/.dotnet 302
        export DOTNET_ROOT=/workspace/.dotnet PATH="/workspace/.dotnet:$PATH"
        ;;
esac
case "$mode" in
    release)
        sh .mozaic/test-release-pipeline.sh
        sh "$helper" python -B .mozaic/test_verify_test_results.py
        sh "$helper" python -B .mozaic/test_stage_browser_deps.py
        sh .mozaic/check-and-package-release.sh
        ;;
    react-tests)
        sh .mozaic/check-ordinary.sh "$mode"
        ;;
    dotnet-release|react-browser)
        sh .mozaic/check-browser.sh "$mode"
        ;;
    api)
        dotnet restore sources/Api/Api.csproj --disable-parallel
        dotnet publish sources/Api/Api.csproj --configuration Release --no-restore \
            --maxcpucount:1 --output /workspace/.publish/api /p:UseAppHost=false
        sh "$helper" assemble api /workspace /workspace/image.tar
        ;;
    web)
        dotnet restore sources/WebApp/WebApp.csproj --disable-parallel
        dotnet publish sources/WebApp/WebApp.csproj --configuration Release --no-restore \
            --maxcpucount:1 --output /workspace/.publish/web /p:UseAppHost=false
        (
            cd sources/ReactApp
            npm ci
            npm run build
            UTF_REACT_BASE_PATH=/ npm run build:assets
        )
        sh "$helper" assemble web /workspace /workspace/image.tar
        ;;
    worker|aska)
        # The worker recipes build without the repository SDK201 global.json.
        mkdir -p /workspace/.worker-source/sources
        cp -R sources/HostingContracts sources/GameServerImplementations sources/CloudWorker \
            /workspace/.worker-source/sources/
        printf '{"sdk":{"version":"10.0.302","rollForward":"disable"}}\n' \
            > /workspace/.worker-source/global.json
        (
            cd /workspace/.worker-source
            test "$(dotnet --version)" = 10.0.302
            dotnet restore sources/CloudWorker/CloudWorker.csproj --disable-parallel
            dotnet publish sources/CloudWorker/CloudWorker.csproj --configuration Release \
                --output /workspace/.publish/worker --no-restore --no-self-contained --maxcpucount:1
        )
        sh "$helper" assemble "$mode" /workspace /workspace/image.tar
        ;;
    *) printf 'Unsupported ordinary job: %s\n' "$mode" >&2; exit 2 ;;
esac
