# React UI beta

The React user interface is a side-by-side beta served at `/next/`. The Blazor WebAssembly interface remains the
default at `/`, and the React deployment does not replace or redirect the classic application automatically.

## Release boundary

`sources/Common/Ui/routes.json` is the neutral route manifest. It contains every canonical and legacy Blazor route,
plus React-only routes with an explicit classic fallback, and records whether a route has completed the React parity
checklist. Both clients consume the same file:

- Blazor only renders **Try new UI** when the current route is certified.
- React renders every known route. Unknown paths remain a React 404 and are not redirected to an unrelated classic
  route.

Certification is deliberately conservative. The beta now certifies all 76 canonical and legacy Blazor route patterns
and two React-only configuration-profile routes with an explicit classic fallback. That includes the public and member
workflows, events and slotting, Wiki and published content, account and Verein flows, the complete permission-filtered
Command Center, event/mod-pack/terrain/content/member/role administration, Kassenbuch and document governance,
server fleet and creation, configuration profiles, hosting finance, and the full server workspace. The classic
application remains deployed as the manual rollback variant rather than as a route-level fallback.

The manifest remains the release contract. A future route must not be marked certified until reads, mutations,
authorization, aliases, polling, loading/empty/error states, localization, accessibility, and cross-variant data
visibility have passed the parity checklist.

## Configuration and attribution

The public application configuration supports:

```json
{
  "Ui": {
    "ReactVariantEnabled": false,
    "ReactBasePath": "/next"
  }
}
```

`ReactVariantEnabled` controls whether either interface displays its switch; direct `/next/` access and both deployed
artifacts remain available for rollback testing. Selecting an interface writes the functional cookie
`utf_ui_variant=classic|react` for 180 days with `Path=/`, `SameSite=Lax`, and `Secure` on HTTPS. The cookie is not an
authorization signal and is documented in the shared GDPR copy.

Both HTTP clients also send `X-UTF-UI-Variant`. The API accepts only `classic` or `react` and adds a valid value to the
request logging scope and current OpenTelemetry activity. It never changes API behavior or authorization.

## Local development

The Aspire AppHost starts the Vite project as `react-app` and gives it references to the API and classic application.
Vite serves the SPA with the `/next/` base and same-origin proxies API, WebSocket, authentication callback, public
configuration, image, and media requests.

Run the complete distributed application with:

```powershell
dotnet run --project sources/AppHost/AppHost.csproj
```

For isolated frontend work:

```powershell
cd sources/ReactApp
npm ci
npm run dev
```

Node 24 is pinned in `.node-version`; the npm lockfile is committed. The production Docker build compiles React in a
Node 24 stage and copies the result to nginx at `/usr/share/nginx/html/next/`.

## Generated sources

The React app owns its English and German text in `src/locales/en.json` and `src/locales/de.json` (flat keys,
edited by hand). `npm run locales:check` fails the build when a German value is missing, empty, orphaned, or has
different placeholders than the English value. It runs first in `npm run build`, `npm run build:assets`, and
`npm run ci`. German values that legitimately equal English are listed with a reason in
`src/locales/identical-allowed.json`. The classic Blazor build keeps its own RESX files and the React app never reads
them. `npm run contracts:generate` remains the generator for the TypeScript API declarations from the complete
checked-in `openapi/utf-api.json` snapshot. React pages use project-owned domain services backed by generated path,
method, and schema types.

The API supports a metadata-only `--export-openapi <absolute-path>` mode that skips database migration, seeding, hosted
workers, and external providers. Before committing an API contract update, export that document and run
`npm run generate`. CI exports the current ASP.NET contract and compares it byte-for-byte with the snapshot, then fails
when the TypeScript declarations drift from their source.

## Fleet change signal

The server fleet page updates live from a fleet change signal. Each message says that one game server changed and
carries only that server's id (`GameServerFleetChangedMessage.ServerId`). It never carries status, cost, owner,
title or secrets. It is delivered only to users who may view that server, by the same rule as the REST fleet
(D-17). On receipt the client refetches the fleet through the authorized REST endpoint, which applies the per-server
visibility rules.

Subscription. Clients subscribe through the existing update stream endpoint with the path `game-servers/fleet`; the
client request and its frame handling are unchanged. The API registers the socket on `game-servers/fleet/{userId}`,
where the user id (lower-case `D` format) is derived only from the authenticated user. The requested path must have
exactly the two segments `game-servers` and `fleet`. Casing and surrounding slashes are ignored, and a request with
any further segment is rejected. The section gate is unchanged and deliberately wide: it admits a global
administrator (`adm`) and any holder of an `sv`, `sv:view`, `crt:sv`, `adm:sv` or `adm:hosting` claim, bare or
server-bound, which matches the client rule for showing the server section. The gate only decides who may open the
socket. Holders of `crt:sv` or of a bare `sv:view` may connect but are never in any server's audience, so they receive
no frames. Per-user paths exist because the update stream matches subscribers by raw string prefix and keeps no
recipient identity. Claim-scoped delivery therefore needs one path per recipient, and no shared path is ever published
to: a send to the bare `game-servers/fleet` prefix would reach every subscriber.

Audience. For every coalesced batch the notifier asks `HostingAuthorizationService.GetServerViewAudienceAsync` for
the users who may view each changed server. The audience is every user who is neither deleted nor banned and who
holds, directly or through a role, an empty-valued `adm`, `adm:sv` or `adm:hosting` claim, or an `sv` or `sv:view`
claim whose value is the server id. This is exactly `HasServerPermission` with the view permission; a drift test
compares both for every claim class. Archived servers are included, so an archive change reaches their viewers. The
audience is read from the database once per batch and evaluated against the current claims every time, with no cache,
so a revoked claim stops delivery at the next signal. Delivery is designed to fail closed: if the audience cannot be
read, nothing is sent for that batch, one warning is logged, and the notifier never falls back to a broadcast. The
reconnect refetch and the Refresh button are the recovery path (D-15).

Sources. An EF Core SaveChanges interceptor signals committed changes to game servers, deployments and lifecycle
jobs that the fleet displays. Examples are a server being added, retitled, archived, launch-toggled or given a new
cost owner, a deployment changing status or possible cost, and a job being added or changing status. Inside an
explicit transaction the signal is held until the transaction commits and discarded on rollback. The lifecycle
executor makes one explicit call after the possible-cost bulk update, because a bulk `ExecuteUpdate` bypasses the
interceptor.

Deliberately not signaled. Claim and role changes do not touch a hosting entity, so they emit no signal. A revoked
viewer receives no further frames about the revoked server from the next signal on, but their fleet stays as loaded
until the next refetch: a reconnect, the Refresh button, or a later signal for a server they may still view. A newly
granted viewer sees the server at the next refetch or at the next signal for that server. An explicit notification
of the affected users was not added (review finding IN-10): claim and role mutations happen at more than five sites
in four files (the access service, the resource-claim controller, the role controller and the user controller),
including role membership changes that affect arbitrary sets of users. Hooking all of them would broaden shared
administration code, which AGENTS.md forbids without an explicit request.

Delivery. One serialized notifier loop sends every message, so two lifecycle writers never send to the same socket
at the same time. Signals are coalesced over a 250 millisecond window. For each distinct server of a window the loop
sends one frame per viewer, each on that viewer's own path, one after another. Writers only enqueue and never wait
for delivery. A failed send is logged per recipient as a structured warning naming the server and the user, never
rethrown, and the loop continues with the next recipient. This design is required because the shared update stream
service drops any subscriber whose send fails, and concurrent sends to one socket fail.

Process-local limitation (D-14). The update stream keeps its subscribers in memory in the API process, and each API
instance reads the audience for its own batches as well, so the signal only reaches clients connected to the
instance that committed the change. A multi-instance API deployment would need a shared backplane (for example a
message bus or database notifications) before clients on one instance could see changes committed by another.

Keep-alive. The API sends a WebSocket ping every 25 seconds (`WebSocketOptions.KeepAliveInterval`). This keeps an
idle fleet stream open behind nginx, whose `/api/` location sets no `proxy_read_timeout` and so falls back to the
nginx default, assumed to be 60 seconds. The interval applies globally, including to the event update streams, where
the extra ping is harmless. If a global setting is rejected, the fallback is a longer `proxy_read_timeout` on the
update stream location. Manual verification: open the fleet page behind the deployed nginx, leave it idle for more
than 60 seconds, and confirm the socket stays open without reconnecting and that a later server change still
updates the page.

Client behavior. After the socket closes or errors, the React client retries after 1, 2, 4, 8 and 16 seconds and then
every 30 seconds, each delay with plus or minus 20 percent jitter, for as long as the subscription is mounted. It also
retries at once when the browser comes back online, when the page becomes visible again, or when the viewer chooses
Reconnect now. Every successful open, including the first one and every reconnect, refetches the fleet when the cached
data is older than the open, so changes missed while disconnected are picked up. There is deliberately no polling
fallback (D-15). That makes the server-side guarantees above mandatory: a subscriber must never be dropped without its
socket closing.

## Fleet and live updates

The fleet lives at `/command/servers`. Search and filter state is kept in the URL: `q` holds the search text and
`filter` holds one of `all`, `mine`, `running` or `off`. Both are written with history replace on the same pathname,
so a reload or a shared link restores the view and typing never grows the browser history. Typed text filters the
list immediately and reaches the URL after a short debounce. An unknown `filter` value falls back to All.

The shared update stream client is `useUpdateStream` in `src/app/updateStream.ts`. It is resource-agnostic: it opens
`update-stream/subscribe?path=...` relative to the configured API base URL, delivers parsed frames, reports the
connection status (`connecting`, `open`, `reconnecting` or `unavailable`) and reconnects with the backoff described
above. Callers decide what a frame means and refetch from its open callback, which runs on every reconnect. It has no
polling fallback. `FleetLiveUpdatesProvider` is mounted in both route layouts (`CommunityLayout` and
`CommandCenterLayout`) and subscribes to `game-servers/fleet` only when the viewer may access servers. A fleet frame
invalidates the fleet and notification queries, and bursts are coalesced. Because the provider sits at layout level,
the overview and the profile menu, which share the fleet cache, stay fresh as well. The fleet page shows a
non-blocking notice after three seconds of continuous disconnection.

The fleet query of the fleet page and the hosting overview (`useFleetQuery`) no longer polls. The Command Center
overview page (its fleet and notification queries), the notification queries of the hosting overview and the profile
menu still keep their own pollers.

The server workspace follows the same socket. It refetches its server, jobs and operations queries when a fleet frame
carries its server id (coalesced) and once on every reconnect, and it polls those three queries every 30 seconds only
while a lifecycle job is active, because stage changes, command output and audit records do not signal. Without an
active job the workspace never polls.

Proxy requirement. `/api` must forward WebSocket upgrades. The Vite development proxy enables `ws` for `/api`, and the
nginx `/api/` location in `deploy/nginx.conf` uses HTTP/1.1 and sets the `Upgrade` and `Connection` headers. A
server-managed nginx file must keep both headers, otherwise the fleet stays on its last fetched state and shows the notice.

## Validation

The React CI job runs `npm ci`, generation drift checks, Prettier, ESLint, TypeScript, Vitest, a production build, and
Playwright Chromium tests, then publishes the distinct `Frontend-React` artifact. Scheduled, manual, and release runs
use the same Chromium journeys. The existing .NET job remains independent, and the `Frontend` and
`Backend` artifacts retain their existing names and contents. The Docker image is the integrated side-by-side
deployment; archive-based installations deploy the distinct React artifact at `/next/` beside the classic frontend.
Tests and local end-to-end environments must use seeded
data, fake runtime providers, and the isolated development worker; they must never call real Hetzner or Steam services.

Promoting a route requires updating its manifest flag only after the complete parity checklist in the feature plan is
satisfied. Percentage cohorts and behavioral analytics are intentionally not part of this beta.

Production installations that mount a server-managed nginx file must copy the `/next`, `/next/`, and
`/next/assets/` locations from `deploy/nginx.conf`; the mount replaces the configuration bundled into the image.
Their public WebApp JSON must also carry the `Ui` section shown above before the beta switches are enabled.
