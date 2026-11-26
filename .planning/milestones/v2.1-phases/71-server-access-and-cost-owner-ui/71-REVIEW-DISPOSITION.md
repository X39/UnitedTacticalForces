---
phase: 71
review: 71-REVIEW.md
titles: json
findings:
  - id: WR-01
    severity: warning
    disposition: fixed
    title: "Clearing toggledSubject in runToggle's finally reads the optimistic cache, so the focus move to the list heading never fires against a real server"
---

# Phase 71 — Code Review Disposition

The `--fix --all --auto` loop ran its three iterations. The frontmatter ledger covers the findings of the final
`71-REVIEW.md` (iteration 3). Earlier iterations are recorded below, because each re-review rewrites REVIEW.md.

| Iteration | Review | Findings | Disposition | Commits |
|-----------|--------|----------|-------------|---------|
| 1 | 0 critical, 5 warning, 7 info | WR-01 touch scroll chose an option; WR-02 Retry unreachable by keyboard; WR-03 reason lines shifted layout; WR-04 failed refetch replaced cached list; WR-05 focus lost after last permission removed; IN-01 hover stole active option; IN-02 IME Enter; IN-03 stale candidates; IN-04 silent truncation at 20; IN-05 duplicated constants and LikePattern; IN-06 tests asserting copies of the query; IN-07 sliced query key | all 12 fixed. Accepted remainders with reasons: matching 400 body shapes would change the OpenAPI contract and the legacy Blazor client; harmless extra LikePattern escapes on PostgreSQL; no Postgres-backed search test (no Testcontainers setup) | dc39d60f..26d21efe (13 commits) |
| between | orchestrator gate | the IN-05 constant rendered as type text in the OpenAPI take descriptions, failing the CI snapshot comparison | fixed | c53c5ea2 |
| 2 | 0 critical, 0 warning, 5 info | IN-01 remembered subject never cleared; IN-02 awaited candidate invalidation delayed Remove all; IN-03 Escape inert on Retry; IN-04 public docs cite internal constant; IN-05 dead pointerType guard | all 5 fixed | efde7d2c, 3d11434a, d2f9ea88 |
| 3 | 0 critical, 1 warning, 0 info | WR-01 (above) — regression of iteration-1 WR-05 introduced by iteration-2 IN-01 | fixed: the last toggle's onSettled awaits the settle step; tests cover a late refetch and a subject that stays listed | 726d7558 |

Final gate on the fixed tree: Vitest full suite green (3,806 passed, 2 skipped; hosting and picker suites re-run after
726d7558: 2,554 passed), `dotnet test` of the solution green (93, 364, 2, 1,794), OpenAPI export identical to the
snapshot, `hosting-access.spec.ts` and `hosting-cost-owner.spec.ts` on chromium 41/41, prettier, lint, typecheck,
locales, contracts and build:assets clean.

Human verification carried to milestone end (fixer marked these "requires human verification"): the 521–800 px
reason layout in English and German (iteration-1 WR-03), and the focus behavior of the access toggles against a real
API (iterations 2 and 3).
