#!/bin/sh
# Run inside the matching immutable Playwright job image, with no host packages.
set -eu
cd /workspace
mode=${1:?A browser check name is required}
case "$mode" in
    dotnet-release|react-browser) ;;
    *) printf 'Usage: %s dotnet-release|react-browser\n' "$0" >&2; exit 2 ;;
esac

export HOME=/workspace/.home npm_config_cache=/workspace/.npm-cache
export DOTNET_CLI_HOME=/workspace/.dotnet-home NUGET_PACKAGES=/workspace/.nuget/packages
export DOTNET_PROCESSOR_COUNT=1 CI=true
export UTF_PROOF_DIRECTORY=/workspace/test-results
export UTF_CHECK_PUBLISH_DIRECTORY=/workspace/.publish
export UTF_DOTNET_PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
export UTF_REACT_PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
mkdir -p "$HOME" /workspace/.browser-tools "$UTF_PROOF_DIRECTORY" "$UTF_CHECK_PUBLISH_DIRECTORY"

# Verify the official Node release bytes before extracting into writable scratch.
node_archive=/workspace/.browser-tools/node-v24.11.1-linux-x64.tar.xz
curl --fail --silent --show-error --location --proto '=https' --proto-redir '=https' \
    https://nodejs.org/dist/v24.11.1/node-v24.11.1-linux-x64.tar.xz --output "$node_archive"
printf '%s  %s\n' '60e3b0a8500819514aca603487c254298cd776de0698d3cd08f11dba5b8289a8' \
    "$node_archive" | sha256sum --check --status
mkdir -p /workspace/.node
tar -xJf "$node_archive" --strip-components=1 -C /workspace/.node
export PATH="/workspace/.node/bin:$PATH"

if [ "$mode" = dotnet-release ]; then
    # global.json requires this feature band; the Playwright image has SDK 10.0.301.
    # SHA-512 is pinned from Microsoft's official .NET 10 release metadata.
    sdk_archive=/workspace/.browser-tools/dotnet-sdk-10.0.201-linux-x64.tar.gz
    curl --fail --silent --show-error --location --proto '=https' --proto-redir '=https' \
        https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.201/dotnet-sdk-10.0.201-linux-x64.tar.gz \
        --output "$sdk_archive"
    printf '%s  %s\n' \
        'a33354e3291aa21ea5e1983733f001d45264c31ee3c0dc4a85509d4cb6d4896cfec57fd2143a7b54c93bc42b328e059b16802b4085257a367ef2652f1c8fa424' \
        "$sdk_archive" | sha512sum --check --status
    mkdir -p /workspace/.dotnet201
    tar -xzf "$sdk_archive" -C /workspace/.dotnet201
    export DOTNET_ROOT=/workspace/.dotnet201 PATH="/workspace/.dotnet201:$PATH"
fi

sh .mozaic/check-ordinary.sh "$mode"
