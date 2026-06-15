# Aspire development cloud worker

The AppHost can optionally build and start one isolated `CloudWorker` container for local integration testing. It
uses the production worker Dockerfile and the normal outbound worker protocol. It is not a runtime provider, and the
API still does not execute game-server processes.

The resource is disabled by default and is rejected outside the `Development` environment. Enable it with AppHost
user secrets so a machine-specific bootstrap path is never committed:

```powershell
dotnet user-secrets --project sources/AppHost set "Hosting:DevelopmentWorker:Enabled" "true"
dotnet user-secrets --project sources/AppHost set "Hosting:DevelopmentWorker:BootstrapPath" "C:\secrets\utf-worker-bootstrap.json"
```

`BootstrapPath` may be absolute or relative to `sources/AppHost`. It must point to a fresh, control-plane-issued
bootstrap JSON for exactly one deployment. The AppHost does not read the file contents; it binds the file read-only
at `/run/secrets/utf-worker-bootstrap.json`. Keep the file outside the repository and restrict it to its owner (Unix
mode `0400` or `0600`). On native Linux, the container's root-owned worker agent must be able to read the bind-mounted
file; the unprivileged game-process identity must not be able to read it. The worker credential and command journal
are retained in a dedicated Docker volume across container restarts.

For an HTTP-only local API endpoint, explicitly permit insecure transport for this development resource:

```powershell
dotnet user-secrets --project sources/AppHost set "Hosting:DevelopmentWorker:AllowInsecureHttp" "true"
```

The bootstrap document's `apiBaseUrl` must be reachable from the container. With Docker Desktop, use an exposed API
endpoint on `host.docker.internal`; do not use `localhost`, because that refers to the worker container itself. HTTPS
remains the default whenever `AllowInsecureHttp` is not enabled.
