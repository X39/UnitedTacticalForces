#!/bin/sh
set -eu
sh /tmp/utf-preparation/capture-apt.sh begin
apt-get update
DEBIAN_FRONTEND=noninteractive apt-get install --yes --no-install-recommends build-essential pkg-config git curl ca-certificates python3
printf '#!/bin/sh\nexec /usr/local/bin/node /usr/local/lib/node_modules/npm/bin/npm-cli.js "$@"\n' > /usr/local/bin/npm
printf '#!/bin/sh\nexec /usr/local/bin/node /usr/local/lib/node_modules/npm/bin/npx-cli.js "$@"\n' > /usr/local/bin/npx
chmod 0755 /usr/local/bin/npm /usr/local/bin/npx
test "$(node --version)" = v24.11.1
cp /tmp/utf-preparation/select-dotnet.sh /usr/local/bin/utf-dotnet-201
cp /tmp/utf-preparation/select-dotnet.sh /usr/local/bin/utf-dotnet-302
chmod 0755 /usr/local/bin/utf-dotnet-201 /usr/local/bin/utf-dotnet-302
for profile in dotnet react; do
  if [ "$profile" = dotnet ]; then version=1.61.0; browsers=chromium; else version=1.62.1; browsers='chromium firefox webkit'; fi
  prefix=/opt/utf-playwright-$profile
  npm install --prefix "$prefix" --save-exact --no-audit --no-fund "playwright@$version"
  export PLAYWRIGHT_BROWSERS_PATH=/opt/utf-playwright-browsers/$profile
  # Use each pinned vendor installer and its normal apt dependency installation.
  # shellcheck disable=SC2086
  "$prefix/node_modules/.bin/playwright" install --with-deps $browsers
  mkdir -p "/usr/share/utf-preparation/playwright/$profile"
  cp "$prefix/package-lock.json" "$prefix/node_modules/playwright-core/browsers.json" "/usr/share/utf-preparation/playwright/$profile/"
  npm ls --prefix "$prefix" --all --json > "/usr/share/utf-preparation/playwright/$profile/npm-tree.json"
  find "$prefix/node_modules" -type f \( -iname 'license*' -o -iname 'notice*' \) -exec sha256sum '{}' ';' > "/usr/share/utf-preparation/playwright/$profile/license-sha256.txt"
  find "$PLAYWRIGHT_BROWSERS_PATH" -type f -exec sha256sum '{}' ';' > "/usr/share/utf-preparation/playwright/$profile/browser-files-sha256.txt"
done
unset PLAYWRIGHT_BROWSERS_PATH
cd /tmp
utf-dotnet-201 --version
utf-dotnet-302 --version
rustc --version
cargo --version
printf 'fn main() { println!("dependency compiler probe"); }\n' > /tmp/compiler-probe.rs
rustc /tmp/compiler-probe.rs -o /tmp/compiler-probe
/tmp/compiler-probe
rm /tmp/compiler-probe /tmp/compiler-probe.rs
{ node --version; npm --version; rustc --version; cargo --version; utf-dotnet-201 --info; utf-dotnet-302 --info; } > /usr/share/utf-preparation/toolchains.txt
find /usr/share/dotnet /opt/utf-dotnet-302 /usr/local/rustup /usr/local/lib/node_modules/npm -maxdepth 3 -type f \( -iname '*license*' -o -iname '*notice*' \) -exec sha256sum '{}' ';' > /usr/share/utf-preparation/toolchain-license-sha256.txt
chmod -R a+rX /opt/utf-dotnet-302 /opt/utf-playwright-* /usr/local/rustup /usr/local/cargo
sh /tmp/utf-preparation/capture-apt.sh finish
