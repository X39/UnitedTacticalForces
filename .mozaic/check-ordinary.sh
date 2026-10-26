#!/bin/sh
# Run the existing release checks using toolchains staged by build.sh.
set -eu
repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$repo_root"
export CI=true DOTNET_PROCESSOR_COUNT=1
export HOME="${HOME:-$repo_root/.home}"
export npm_config_cache="${npm_config_cache:-$repo_root/.npm-cache}"
export UTF_PROOF_DIRECTORY="${UTF_PROOF_DIRECTORY:-$repo_root/.mozaic-work/proof}"
mkdir -p "$HOME" "$npm_config_cache" "$UTF_PROOF_DIRECTORY"

case "${1:-}" in
    react-tests)
        # Source transfer excludes .git. Generated-file checks need a baseline.
        if [ ! -d .git ]; then
            git init --quiet
            git -c core.autocrlf=false add -- sources/ReactApp/src/generated
            git -c user.name='Mozaic snapshot' -c user.email=snapshot@mozaic.invalid \
                commit --quiet -m baseline
        fi
        cd sources/ReactApp
        npm ci
        npm run contracts:check
        npm run locales:check
        npm run format:check
        npm run lint
        npm run typecheck
        npm test -- --maxWorkers=1
        npm run build:assets
        printf 'React contracts, locales, formatting, lint, types, tests and assets passed\n' \
            > "$UTF_PROOF_DIRECTORY/react-tests"
        ;;
    dotnet-release)
        # This browser revision matches Microsoft.Playwright in Ui.Tests.
        export PLAYWRIGHT_BROWSERS_PATH="${UTF_DOTNET_PLAYWRIGHT_BROWSERS_PATH:-$repo_root/.mozaic-work/browsers-dotnet}"
        npx --yes playwright@1.61.0 install chromium
        sh .mozaic/check-dotnet-release.sh
        ;;
    react-browser)
        # Playwright's readiness probes must reach the same job's loopback servers.
        export NO_PROXY=localhost,127.0.0.1
        export no_proxy="$NO_PROXY"
        cd sources/ReactApp
        npm ci
        export PLAYWRIGHT_BROWSERS_PATH="${UTF_REACT_PLAYWRIGHT_BROWSERS_PATH:-$repo_root/.mozaic-work/browsers-react}"
        # Shared libraries come from the staged runtime; never install host packages.
        npx --no-install playwright install chromium
        npm run test:e2e -- --workers=1
        printf 'React Chromium journeys passed\n' \
            > "$UTF_PROOF_DIRECTORY/react-browser"
        ;;
    *)
        printf 'Usage: %s react-tests|dotnet-release|react-browser\n' "$0" >&2
        exit 2
        ;;
esac
