#!/bin/sh
# Preserve package-manager evidence without replacing installation or hooks.
set -eu
receipt=/usr/share/utf-preparation
cache=/var/lib/utf-preparation/debs
case "${1:-}" in
  begin)
    mkdir -p "$receipt" "$cache/partial"
    dpkg-query -W -f='${binary:Package}\t${Version}\t${Architecture}\n' > "$receipt/base-packages.tsv"
    # The vendor docker-clean hook clears its usual cache path. A separate cache
    # retains selected archives for inspection; every normal dpkg hook still runs.
    printf 'Dir::Cache::archives "%s";\nDebug::Acquire::gpgv "true";\nAcquire::AllowInsecureRepositories "false";\nAPT::Get::AllowUnauthenticated "false";\n' "$cache" > /etc/apt/apt.conf.d/99-utf-preparation
    ;;
  finish)
    dpkg --audit > "$receipt/dpkg-audit.txt"
    test ! -s "$receipt/dpkg-audit.txt"
    apt-get check
    dpkg-query -W -f='${binary:Package}\t${Version}\t${Architecture}\n' > "$receipt/installed-packages.tsv"
    mkdir -p "$receipt/apt-sources" "$receipt/apt-inrelease" "$receipt/packages" "$receipt/licenses"
    cp -a /etc/apt/sources.list.d/. "$receipt/apt-sources/"
    if [ -f /etc/apt/sources.list ]; then cp /etc/apt/sources.list "$receipt/apt-sources/"; fi
    find /usr/share/keyrings /etc/apt/trusted.gpg.d -type f -exec sha256sum '{}' ';' > "$receipt/apt-keyring-sha256.txt"
    for release in /var/lib/apt/lists/*InRelease; do
      test -f "$release"
      cp "$release" "$receipt/apt-inrelease/"
    done
    : > "$receipt/deb-sha256.txt"
    counter=0
    for archive in "$cache"/*.deb; do
      test -f "$archive"
      counter=$((counter + 1))
      package=$(dpkg-deb -f "$archive" Package)
      version=$(dpkg-deb -f "$archive" Version)
      architecture=$(dpkg-deb -f "$archive" Architecture)
      target="$receipt/packages/$counter"
      mkdir -p "$target"
      dpkg-deb --control "$archive" "$target/control"
      apt-cache show "$package:$architecture=$version" > "$target/authenticated-index.txt"
      observed=$(sha256sum "$archive" | cut -d ' ' -f 1)
      grep -Fx "SHA256: $observed" "$target/authenticated-index.txt" > /dev/null
      printf '%s  %s\n' "$observed" "$(basename "$archive")" >> "$receipt/deb-sha256.txt"
      find "$target/control" -type f -exec sha256sum '{}' ';' > "$target/control-and-hook-sha256.txt"
      if [ -f "/usr/share/doc/$package/copyright" ]; then
        cp -L "/usr/share/doc/$package/copyright" "$receipt/licenses/$counter-copyright"
      else
        printf '%s\t%s\t%s\n' "$package" "$version" "$architecture" >> "$receipt/missing-copyright.tsv"
      fi
    done
    test "$counter" -gt 0
    find /usr/share/doc -name copyright -type f -exec sha256sum '{}' ';' > "$receipt/all-installed-copyright-sha256.txt"
    chmod -R a+rX "$receipt"
    rm -rf "$cache" /var/lib/apt/lists/*
    ;;
  *) echo 'Expected begin or finish.' >&2; exit 2 ;;
esac
