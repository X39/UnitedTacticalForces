#!/usr/bin/env sh
set -eu

state_directory="${UTF_WORKER_STATE_DIRECTORY:-/var/lib/united-tactical-forces/worker}"
runtime_directory="${UTF_WORKER_RUNTIME_ROOT:-/srv/united-tactical-forces/runtime}"
steamcmd_directory="${runtime_directory}/.utf-tooling/steamcmd"
steam_home_directory="${UTF_STEAM_HOME:-${runtime_directory}/.utf-tooling/steam-home}"
game_gid="${UTF_GAME_GID:-10001}"
steam_uid="${UTF_STEAM_UID:-10003}"
privilege_drop="${UTF_GAME_PRIVILEGE_DROP_PATH:-/usr/bin/setpriv}"

mkdir -p "${state_directory}" "${runtime_directory}"
chmod 0700 "${state_directory}"

# Named development volumes initially belong to root. Production cloud-init creates
# the runtime bind with the expected owner before this container starts.
runtime_owner="$(stat -c '%u:%g' "${runtime_directory}")"
if [ "${runtime_owner}" != "0:${game_gid}" ]; then
    chown "root:${game_gid}" "${runtime_directory}"
fi
chmod 3770 "${runtime_directory}"

# Keep mutable Steam tooling outside the game identity's write boundary. The sticky
# runtime root prevents the game user from replacing this root-owned directory.
mkdir -p "${runtime_directory}/.utf-tooling" "${steamcmd_directory}" "${steam_home_directory}"
chown root:root "${runtime_directory}/.utf-tooling"
chmod 0755 "${runtime_directory}/.utf-tooling"
chown "${steam_uid}:${game_gid}" "${steamcmd_directory}"
chmod 0750 "${steamcmd_directory}"
chown "${steam_uid}:${game_gid}" "${steam_home_directory}"
chmod 0700 "${steam_home_directory}"

# SteamCMD updates its own files when it starts. The container root filesystem is
# read-only in production, so install it into the game-writable runtime mount before
# starting the agent. The development image seeds it from /opt/steamcmd; the release
# image does not bake it in, so the game host downloads the pinned installer itself.
steamcmd_url="${UTF_STEAMCMD_INSTALLER_URL:-https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz}"
steamcmd_sha256="${UTF_STEAMCMD_INSTALLER_SHA256:-cebf0046bfd08cf45da6bc094ae47aa39ebf4155e5ede41373b579b8f1071e7c}"
if [ ! -x "${steamcmd_directory}/steamcmd.sh" ]; then
    if [ -d /opt/steamcmd ]; then
        install_steamcmd='umask 0027; cp -a /opt/steamcmd/. "$1"/'
    else
        install_steamcmd='set -eu; umask 0027; archive="$1/steamcmd_linux.tar.gz"
            curl --fail --location --silent --show-error --retry 3 --output "$archive" "$2"
            echo "$3  $archive" | sha256sum --check --strict --quiet
            tar --extract --gzip --no-same-owner --file "$archive" --directory "$1"
            rm -f "$archive"'
    fi
    "${privilege_drop}" \
        --reuid="${steam_uid}" \
        --regid="${game_gid}" \
        --clear-groups \
        --no-new-privs \
        /bin/sh -c "${install_steamcmd}" sh "${steamcmd_directory}" "${steamcmd_url}" "${steamcmd_sha256}"
fi

exec "${privilege_drop}" \
    --regid="${game_gid}" \
    --keep-groups \
    dotnet X39.UnitedTacticalForces.CloudWorker.dll "$@"
