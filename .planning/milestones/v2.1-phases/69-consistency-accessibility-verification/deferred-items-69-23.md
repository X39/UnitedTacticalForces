# Deferred items from 69-23

- `tests/e2e/public-shell.spec.ts` "filters the event administration ledger on canonical and legacy routes" fails at
  `getByRole('heading', { name: 'Event Administration' })` (line 479) on the plan base `33900a1e`, before any 69-23
  change. The event administration page was converted by the events-admin plan (69-16); its heading or the spec query
  no longer match. Owner: 69-16 or the closure plan 69-33.
- A wiki article whose Markdown starts with `# Heading` renders a second h1 inside the article. Markdown rendering is a
  closed non-goal of this phase; content owners should start articles at level 2.
- `Pages_Wiki_Index_Cancel`, `Pages_Wiki_Index_NoPagesMatchSearch`, `Pages_Public_Media_NoReportsMatchCategory` and
  `Pages_Public_Media_NoReportsPublished` have no reader left in `src`; retire them in 69-33.
