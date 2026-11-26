# Deferred items from 69-30

Found while running the whole existing chromium suite next to the new journeys (private-port config, 452 passed, 77
skipped, 5 failed). None of the five touches the cost confirmation provider or a journey file; each also fails when run
alone. They are logged, not fixed, because they live in files other plans own.

- `tests/e2e/hosting-fleet.spec.ts:558` "the Command Center nav and the fleet page read Servers" fails on
  `toHaveTitle(/Servers/)` (title is "Server fleet"). Already logged by 69-05 in `deferred-items.md`; it is the page
  title the fleet plan changed.
- `tests/e2e/public-shell.spec.ts:406` "filters the event administration ledger on canonical and legacy routes" looks
  for the h1 "Event Administration" (already logged by 69-21 as XPF-69-21-2 and by 69-23). Owner: 69-16 or 69-33.
- `tests/e2e/public-shell.spec.ts:809` "manages reusable gallery assets and their tracked usage" looks for the heading
  "Image gallery" at `command/content/gallery` (line 854) and does not find it. The gallery page was converted by a
  Wave 2 plan; the heading text or the spec query no longer match. Owner: the plan that converted the gallery page, or
  69-33.
- `tests/e2e/public-shell.spec.ts:1367` "creates a terrain with a registered game and gallery image" times out on the
  Next button (line after the terrain create step): the gallery picker `dialog--gallery-picker` stays open and
  intercepts the click. Same owner area as the previous item (69-20 touched this spec and the terrain pages).
- `tests/e2e/hosting-files-secrets.spec.ts:794` "credential staging layout" fails at 320x568 in both cultures: a
  button moves by 2 px between staging states (limit 1 px, line 785). The 390x844 variants pass. The spec does not
  load the vendored test fonts, so the label font fallback on a machine without network access is a possible cause.
  Owner: 69-10 (credentials panel) or 69-34.
