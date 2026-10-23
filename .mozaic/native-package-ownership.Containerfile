# Operator binds both aliases to reviewed preloaded digests in the native action.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS utf-ownership
USER root
RUN set -eu; test "$(id -u)" = 0; \
    grep -Eq '^NoNewPrivs:[[:space:]]+1$' /proc/self/status; \
    grep -Eq '^Seccomp:[[:space:]]+2$' /proc/self/status; \
    grep -Eq '^CapEff:[[:space:]]+00000000800000db$' /proc/self/status; \
    test ! -S /run/podman/podman.sock; test ! -S /var/run/docker.sock; \
    test ! -e /etc/mozaic/app.env; test ! -e /var/lib/postgresql
RUN apt-get update && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /proof/utf-data /proof/utf-keyring \
    && id -u app > /proof/utf-app-uid \
    && chown -R app:app /proof
USER app
RUN test -w /proof/utf-data && test -w /proof/utf-keyring \
    && test "$(id -u)" = "$(cat /proof/utf-app-uid)" \
    && printf 'UTF package and ownership proof passed\n' > /proof/result

FROM python:3.11-slim-bookworm AS media-ownership
USER root
RUN set -eu; test "$(id -u)" = 0; \
    grep -Eq '^NoNewPrivs:[[:space:]]+1$' /proc/self/status; \
    grep -Eq '^Seccomp:[[:space:]]+2$' /proc/self/status; \
    grep -Eq '^CapEff:[[:space:]]+00000000800000db$' /proc/self/status; \
    test ! -S /run/podman/podman.sock; test ! -S /var/run/docker.sock; \
    test ! -e /etc/mozaic/app.env; test ! -e /var/lib/postgresql
RUN apt-get update && apt-get install --yes --no-install-recommends fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/* \
    && pip install --no-cache-dir --no-deps packaging==25.0 \
    && useradd --system --uid 10001 worker \
    && mkdir -p /proof/media-data && chown -R worker:worker /proof
USER worker
RUN test "$(id -u)" = 10001 && test -w /proof/media-data \
    && python -c 'import packaging; assert packaging.__version__ == "25.0"' \
    && printf 'Media package and ownership proof passed\n' > /proof/result
EXPOSE 8091
