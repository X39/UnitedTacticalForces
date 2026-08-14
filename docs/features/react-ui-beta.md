# React UI beta

The React user interface is a side-by-side beta served at `/next/`. The Blazor WebAssembly interface remains the
default at `/`, and the React deployment does not replace or redirect the classic application automatically.

## Release boundary

`sources/Common/Ui/routes.json` is the neutral route manifest. It contains every canonical and legacy Blazor route
pattern and records whether a route has completed the React parity checklist. Both clients consume the same file:

- Blazor only renders **Try new UI** when the current route is certified.
- React renders every known route. Unknown paths remain a React 404 and are not redirected to an unrelated classic
  route.

Certification is deliberately conservative. The beta now certifies all 76 canonical and legacy route patterns. That
includes the public and member workflows, events and slotting, Wiki and published content, account and Verein flows,
the complete permission-filtered Command Center, event/mod-pack/terrain/content/member/role administration,
Kassenbuch and document governance, server fleet and creation, configuration profiles, hosting finance, and the full
server workspace. The classic application remains deployed as the manual rollback variant rather than as a route-level
fallback.

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

The existing English and German RESX files remain the localization source of truth. `npm run locales:generate`
deterministically exports the typed React resources. `npm run contracts:generate` creates TypeScript declarations from
the complete checked-in `openapi/utf-api.json` snapshot. React pages use project-owned domain services backed by
generated path, method, and schema types.

The API supports a metadata-only `--export-openapi <absolute-path>` mode that skips database migration, seeding, hosted
workers, and external providers. Before committing an API contract update, export that document and run
`npm run generate`. CI exports the current ASP.NET contract and compares it byte-for-byte with the snapshot, then fails
when either localization files or TypeScript declarations drift from their source.

## Validation

The React CI job runs `npm ci`, generation drift checks, Prettier, ESLint, TypeScript, Vitest, a production build, and
Playwright Chromium tests, then publishes the distinct `Frontend-React` artifact. Scheduled, manual, and release runs
also execute the journeys in Firefox and WebKit. The existing .NET job remains independent, and the `Frontend` and
`Backend` artifacts retain their existing names and contents. The Docker image is the integrated side-by-side
deployment; archive-based installations deploy the distinct React artifact at `/next/` beside the classic frontend.
Tests and local end-to-end environments must use seeded
data, fake runtime providers, and the isolated development worker; they must never call real Hetzner or Steam services.

Promoting a route requires updating its manifest flag only after the complete parity checklist in the feature plan is
satisfied. Percentage cohorts and behavioral analytics are intentionally not part of this beta.

Production installations that mount a server-managed nginx file must copy the `/next`, `/next/`, and
`/next/assets/` locations from `deploy/nginx.conf`; the mount replaces the configuration bundled into the image.
Their public WebApp JSON must also carry the `Ui` section shown above before the beta switches are enabled.
