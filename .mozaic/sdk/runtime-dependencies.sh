#!/bin/sh
set -eu
profile=${1:?Expected api, worker or aska}
test "$(dpkg --print-architecture)" = amd64
sh /tmp/utf-preparation/capture-apt.sh begin
case "$profile" in
  api) packages='libgssapi-krb5-2' ;;
  worker|aska)
    dpkg --add-architecture i386
    packages='bash ca-certificates curl git libc6:i386 libgcc-s1 libgcc-s1:i386 libstdc++6 libstdc++6:i386 tar tini util-linux'
    if [ "$profile" = aska ]; then packages="$packages fonts-liberation wine wine64 winbind xauth xvfb"; fi
    ;;
  *) exit 2 ;;
esac
apt-get update
# Intentionally use the normal authenticated package manager and maintainer hooks.
# shellcheck disable=SC2086
DEBIAN_FRONTEND=noninteractive apt-get install --yes --no-install-recommends $packages
if [ "$profile" = api ]; then
  mkdir -p /app/App_Data /home/app/.aspnet/DataProtection-Keys /var/lib/united-tactical-forces/hosting
  chown -R app:app /app/App_Data /home/app/.aspnet /var/lib/united-tactical-forces
else
  mkdir -p /opt/steamcmd
  curl --fail --location --silent --show-error --proto '=https' --proto-redir '=https' \
    --max-time 120 --max-filesize 67108864 \
    https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz -o /tmp/steamcmd.tar.gz
  printf '%s  %s\n' cebf0046bfd08cf45da6bc094ae47aa39ebf4155e5ede41373b579b8f1071e7c /tmp/steamcmd.tar.gz | sha256sum --check --strict
  cp /tmp/steamcmd.tar.gz /usr/share/utf-preparation/steamcmd-original.tar.gz
  tar --extract --gzip --file /tmp/steamcmd.tar.gz --directory /opt/steamcmd
  rm /tmp/steamcmd.tar.gz
  groupadd --system --gid 10001 utfruntime
  useradd --system --uid 10002 --gid utfruntime --no-create-home --home-dir /srv/united-tactical-forces/runtime utfgame
  useradd --system --uid 10003 --gid utfruntime --no-create-home --home-dir /srv/united-tactical-forces/runtime utfsteam
  mkdir -p /app /var/lib/united-tactical-forces/worker /srv/united-tactical-forces/runtime
  chown -R root:root /var/lib/united-tactical-forces
  chmod 0700 /var/lib/united-tactical-forces/worker
  chown -R root:utfruntime /srv/united-tactical-forces/runtime
  chmod 2770 /srv/united-tactical-forces/runtime
  find /opt/steamcmd -type f -exec sha256sum '{}' ';' > /usr/share/utf-preparation/steamcmd-files-sha256.txt
  find /opt/steamcmd -type f \( -iname '*license*' -o -iname '*copyright*' \) -print > /usr/share/utf-preparation/steamcmd-license-paths.txt
fi
getent passwd > /usr/share/utf-preparation/passwd.txt
getent group > /usr/share/utf-preparation/group.txt
stat -c '%n %u %g %a' /app /var/lib/united-tactical-forces /home/app > /usr/share/utf-preparation/ownership.txt
if [ "$profile" != api ]; then
  stat -c '%n %u %g %a' /var/lib/united-tactical-forces/worker /srv/united-tactical-forces/runtime >> /usr/share/utf-preparation/ownership.txt
fi
sh /tmp/utf-preparation/capture-apt.sh finish
