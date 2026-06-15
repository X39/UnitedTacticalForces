#!/usr/bin/env sh
set -eu

state_directory="${UTF_WORKER_STATE_DIRECTORY:-/var/lib/united-tactical-forces/worker}"
runtime_directory="${UTF_WORKER_RUNTIME_ROOT:-/srv/united-tactical-forces/runtime}"
steamcmd_directory="${runtime_directory}/.utf-tooling/steamcmd"
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
mkdir -p "${runtime_directory}/.utf-tooling" "${steamcmd_directory}"
chown root:root "${runtime_directory}/.utf-tooling"
chmod 0755 "${runtime_directory}/.utf-tooling"
chown "${steam_uid}:${game_gid}" "${steamcmd_directory}"
chmod 0750 "${steamcmd_directory}"

# SteamCMD updates its own files when it starts. The container root filesystem is
# read-only in production, so seed it into the game-writable runtime mount before
# starting the agent.
if [ ! -x "${steamcmd_directory}/steamcmd.sh" ]; then
    "${privilege_drop}" \
        --reuid="${steam_uid}" \
        --regid="${game_gid}" \
        --clear-groups \
        --no-new-privs \
        /bin/sh -c 'umask 0027; cp -a /opt/steamcmd/. "$1"/' sh "${steamcmd_directory}"
fi

exec "${privilege_drop}" \
    --regid="${game_gid}" \
    --keep-groups \
    dotnet X39.UnitedTacticalForces.CloudWorker.dll "$@"
