# Build from the repository root:
#   docker build -f sources/CloudWorker/Dockerfile -t utf-cloud-worker .
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:3dae2f7699441af56216ff64d5c9b6dfce7cd7dc7f4f71d353d29662b10a384f AS build
WORKDIR /src
COPY sources/HostingContracts/HostingContracts.csproj sources/HostingContracts/
COPY sources/CloudWorker/CloudWorker.csproj sources/CloudWorker/
RUN dotnet restore sources/CloudWorker/CloudWorker.csproj
COPY sources/HostingContracts/ sources/HostingContracts/
COPY sources/CloudWorker/ sources/CloudWorker/
RUN dotnet publish sources/CloudWorker/CloudWorker.csproj \
    --configuration Release \
    --output /out \
    --no-restore \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:10.0@sha256:ca17638b4cbab2063771e8943dbe887ea6c0ae00c5fede3498d5938758c85dfe AS runtime
ARG DEBIAN_FRONTEND=noninteractive
ARG STEAMCMD_SHA256=cebf0046bfd08cf45da6bc094ae47aa39ebf4155e5ede41373b579b8f1071e7c
RUN test "$(dpkg --print-architecture)" = "amd64" \
    && dpkg --add-architecture i386 \
    && apt-get update \
    && apt-get install --yes --no-install-recommends \
        bash \
        ca-certificates \
        curl \
        libc6:i386 \
        libgcc-s1 \
        libgcc-s1:i386 \
        libstdc++6 \
        libstdc++6:i386 \
        tar \
        tini \
        util-linux \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /opt/steamcmd \
    && curl --fail --location --silent --show-error \
        https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz \
        --output /tmp/steamcmd.tar.gz \
    && echo "${STEAMCMD_SHA256}  /tmp/steamcmd.tar.gz" | sha256sum --check --strict \
    && tar --extract --gzip --file /tmp/steamcmd.tar.gz --directory /opt/steamcmd \
    && rm /tmp/steamcmd.tar.gz
RUN groupadd --system --gid 10001 utfruntime \
    && useradd --system --uid 10002 --gid utfruntime --no-create-home \
        --home-dir /srv/united-tactical-forces/runtime utfgame \
    && useradd --system --uid 10003 --gid utfruntime --no-create-home \
        --home-dir /srv/united-tactical-forces/runtime utfsteam \
    && mkdir -p /app /var/lib/united-tactical-forces/worker /srv/united-tactical-forces/runtime \
    && chown -R root:root /var/lib/united-tactical-forces \
    && chmod 0700 /var/lib/united-tactical-forces/worker \
    && chown -R root:utfruntime /srv/united-tactical-forces/runtime \
    && chmod 2770 /srv/united-tactical-forces/runtime
WORKDIR /app
COPY --from=build /out .
COPY --chown=root:root sources/CloudWorker/docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod 0555 /app/docker-entrypoint.sh

# The provider bootstrap and agent state are root-only. Arma runs as UID 10002;
# SteamCMD uses UID 10003. Both share runtime-only GID 10001.
# The bootstrap is mounted read-only at UTF_WORKER_BOOTSTRAP_PATH.
# State and runtime mounts remain separate so command outcomes and exact process
# identity survive worker-agent restarts without exposing the worker credential to the game process.
# No inbound control port is required. Game/query ports are declared by the selected game definition and published
# by the provider bootstrap; the worker image itself does not declare provider or game-specific networking.
ENV UTF_WORKER_BOOTSTRAP_PATH=/run/secrets/utf-worker-bootstrap.json \
    UTF_WORKER_STATE_DIRECTORY=/var/lib/united-tactical-forces/worker \
    UTF_WORKER_RUNTIME_ROOT=/srv/united-tactical-forces/runtime \
    UTF_STEAMCMD_PATH=/srv/united-tactical-forces/runtime/.utf-tooling/steamcmd/steamcmd.sh \
    UTF_STEAM_UID=10003 \
    UTF_GAME_UID=10002 \
    UTF_GAME_GID=10001 \
    UTF_GAME_PRIVILEGE_DROP_PATH=/usr/bin/setpriv
VOLUME ["/var/lib/united-tactical-forces/worker", "/srv/united-tactical-forces/runtime"]
USER root
ENTRYPOINT ["/usr/bin/tini", "-g", "--", "/app/docker-entrypoint.sh"]
