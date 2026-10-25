#!/bin/sh
# Compile and package only inside an ordinary Mozaic worker job.
set -eu
cd /workspace
mode=${1:?A check or component name is required}
export HOME=/workspace/.home CARGO_HOME=/workspace/.cargo RUSTUP_HOME=/usr/local/rustup
export RUSTUP_AUTO_INSTALL=0 CARGO_BUILD_JOBS=1 CARGO_INCREMENTAL=0
export CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
export npm_config_cache=/workspace/.npm-cache NUGET_PACKAGES=/workspace/.nuget/packages
export DOTNET_CLI_HOME=/workspace/.dotnet-home DOTNET_PROCESSOR_COUNT=1
export CARGO_TARGET_DIR=/workspace/.oci-target
export UTF_PROOF_DIRECTORY=/workspace/test-results
export UTF_CHECK_PUBLISH_DIRECTORY=/workspace/.publish
mkdir -p "$HOME" "$UTF_PROOF_DIRECTORY" "$UTF_CHECK_PUBLISH_DIRECTORY"
cargo build --locked --release --manifest-path .mozaic/oci-layout/Cargo.toml
helper=/workspace/.oci-target/release/utf-oci-layout
case "$mode" in
    react-tests|react-browser|web|dotnet-release)
        "$helper" stage-node /workspace/.node
        export PATH="/workspace/.node/bin:$PATH"
        ;;
esac
case "$mode" in
    dotnet-release|api|web)
        "$helper" stage-dotnet /workspace/.dotnet 201
        export DOTNET_ROOT=/workspace/.dotnet PATH="/workspace/.dotnet:$PATH"
        ;;
    worker|aska)
        "$helper" stage-dotnet /workspace/.dotnet 302
        export DOTNET_ROOT=/workspace/.dotnet PATH="/workspace/.dotnet:$PATH"
        ;;
esac
case "$mode" in
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
        "$helper" assemble api /workspace /workspace/image.tar
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
        "$helper" assemble web /workspace /workspace/image.tar
        ;;
    worker|aska)
        # The worker recipes build without the repository SDK201 global.json.
        mkdir -p /workspace/.worker-source/sources
        cp -R sources/HostingContracts sources/GameServerImplementations sources/CloudWorker \
            /workspace/.worker-source/sources/
        (
            cd /workspace/.worker-source
            dotnet restore sources/CloudWorker/CloudWorker.csproj --disable-parallel
            dotnet publish sources/CloudWorker/CloudWorker.csproj --configuration Release \
                --output /workspace/.publish/worker --no-restore --no-self-contained --maxcpucount:1
        )
        "$helper" assemble "$mode" /workspace /workspace/image.tar
        ;;
    *) printf 'Unsupported ordinary job: %s\n' "$mode" >&2; exit 2 ;;
esac
