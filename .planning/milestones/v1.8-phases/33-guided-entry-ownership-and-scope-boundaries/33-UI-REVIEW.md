---
phase: 33
status: warning
overall_score: 16
pillar_scores:
  copywriting: 3
  visuals: 3
  color: 3
  typography: 2
  spacing: 2
  experience_design: 3
findings_total: 12
reviewed_at: 2026-06-19T00:00:00Z
---

# Phase 33 - UI Review

**Audited:** 2026-06-19
**Baseline:** `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-UI-SPEC.md`
**Screenshots:** not captured - user explicitly requested code/spec review only; manual browser/authenticated UAT deferred

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Required core CTA and empty/error copy is present, but no-owner and deferred-UAT contract copy is missing. |
| 2. Visuals | 3/4 | The grid-first guided panel and step rail match the contract, but secondary guided commands are visually under-specified. |
| 3. Color | 3/4 | Accent usage is controlled, but status colors use named color mixes instead of the declared status palette. |
| 4. Typography | 2/4 | New guided typography mostly follows the contract, while surrounding page styles still use rem and extra sizes. |
| 5. Spacing | 2/4 | The guided layout uses the required grid shape, but several spacing values are outside the declared 4px scale. |
| 6. Experience Design | 3/4 | Client validation and server readiness handling are strong, but touch targets and explicit deferred-evidence UX are incomplete. |

**Overall: 16/24**

---

## Top 3 Priority Fixes

1. **Normalize typography and spacing tokens in the touched route CSS** - avoids contract drift and inconsistent density - replace rem font sizes and 3px/6px/7px/10px/12px spacing with the declared 13/14/15/22px type scale and 4/8/16/24px spacing.
2. **Add the missing no-owner and deferred-UAT contract states** - prevents users from seeing only generic validation when owner eligibility is unavailable - render the exact no-owner copy and the deferred UAT note where the guided panel summarizes readiness evidence.
3. **Bring guided actions and mobile controls up to component/accessibility contract** - secondary commands are less scannable and checkbox targets are small - add Fluent icons to `Cancel setup`, consider a clearer icon for `Save owner`, and make guided checkbox touch targets at least 44px on mobile.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)

- **WARNING:** Required boundary CTA labels are present: `Create cloud server`, `Plan cloud hosting`, and `Continue to dry run` appear in `sources/WebApp/Pages/Servers/Index.razor:61`, `sources/WebApp/Pages/Servers/Index.razor:189`, and `sources/WebApp/Pages/Servers/Index.razor:501`.
- **WARNING:** The specified empty and validation summary copy is present at `sources/WebApp/Pages/Servers/Index.razor:263` and `sources/WebApp/Pages/Servers/Index.razor:275`.
- **WARNING:** The contract's no-owner copy, `No active billing owner is available for this server. Select or create an active owner before continuing.`, is not rendered. The UI falls back to field-level messages such as `Owner is required.` and `Owner assignment must be saved before continuing.` at `sources/WebApp/Pages/Servers/Index.razor:1582` and `sources/WebApp/Pages/Servers/Index.razor:1587`.
- **WARNING:** The required deferred-UAT note is absent from the implemented source. This matters because the phase explicitly defers live Hetzner, Steam, Arma, and authenticated browser UAT.
- **WARNING:** Boundary scan found no forbidden Phase 33 terms in the guided controls: no payment, invoice, checkout, tax, `/servers/cloud` normal creation link, dry-run creation call, raw `HttpClient`, or request-builder use in the guided flow.

### Pillar 2: Visuals (3/4)

- **WARNING:** The main workspace keeps the server grid first and renders `section.detail-section.guided-cloud-panel` afterward at `sources/WebApp/Pages/Servers/Index.razor:245`, matching the route and layout contract.
- **WARNING:** The two-column guided layout and step rail exist at `sources/WebApp/Pages/Servers/Index.razor:281` through `sources/WebApp/Pages/Servers/Index.razor:305`, with matching CSS grid rules in `sources/WebApp/Pages/Servers/Index.razor.css:215`.
- **WARNING:** The active step indicator is visually clear via the accent border and inset bar at `sources/WebApp/Pages/Servers/Index.razor.css:252`.
- **WARNING:** Secondary guided actions are weaker than the component contract. `Cancel setup` has no icon at `sources/WebApp/Pages/Servers/Index.razor:383`, while the contract calls for icon-backed Fluent commands.
- **WARNING:** The guided panel uses a bordered tool surface and avoids nested cards, but `.guided-summary` and `.guided-empty` add another bordered frame inside the panel at `sources/WebApp/Pages/Servers/Index.razor.css:358`; this is acceptable for summary/empty states but close to the contract's "do not nest cards" boundary.

### Pillar 3: Color (3/4)

- **WARNING:** Accent use is mostly reserved for declared elements: `Create cloud server` and `Continue to dry run` use `Appearance.Accent` at `sources/WebApp/Pages/Servers/Index.razor:57` and `sources/WebApp/Pages/Servers/Index.razor:498`, and the active step uses `#2563eb` at `sources/WebApp/Pages/Servers/Index.razor.css:253`.
- **WARNING:** Secondary backgrounds use the declared `#f7f9fb` in guided steps and summaries at `sources/WebApp/Pages/Servers/Index.razor.css:231` and `sources/WebApp/Pages/Servers/Index.razor.css:360`.
- **WARNING:** Status colors do not use the declared status palette. They use `dodgerblue`, `goldenrod`, `seagreen`, and `crimson` through `color-mix()` at `sources/WebApp/Pages/Servers/Index.razor.css:123`, `sources/WebApp/Pages/Servers/Index.razor.css:129`, `sources/WebApp/Pages/Servers/Index.razor.css:135`, and `sources/WebApp/Pages/Servers/Index.razor.css:141`.
- **WARNING:** Field validation does use the declared danger value `#b42318` at `sources/WebApp/Pages/Servers/Index.razor.css:337` and `sources/WebApp/Pages/Servers/Index.razor.css:351`.

### Pillar 4: Typography (2/4)

- **WARNING:** Guided-specific CSS follows the new contract in key places: step labels use 13px/600, guided body uses 14px/400, and the panel heading uses 15px/600 at `sources/WebApp/Pages/Servers/Index.razor.css:240`, `sources/WebApp/Pages/Servers/Index.razor.css:246`, and `sources/WebApp/Pages/Servers/Index.razor.css:271`.
- **WARNING:** The route CSS still defines non-contract sizes for touched page elements: `1.35rem`, `0.85rem`, `0.95rem`, `0.82rem`, `0.8rem`, and `0.88rem` appear at `sources/WebApp/Pages/Servers/Index.razor.css:16`, `sources/WebApp/Pages/Servers/Index.razor.css:33`, `sources/WebApp/Pages/Servers/Index.razor.css:45`, `sources/WebApp/Pages/Servers/Index.razor.css:90`, `sources/WebApp/Pages/Servers/Index.razor.css:109`, and `sources/WebApp/Pages/Servers/Index.razor.css:188`.
- **WARNING:** The page title should be the only Display role at 22px, but it uses viewport-independent rem sizing rather than the declared `22px` at `sources/WebApp/Pages/Servers/Index.razor.css:16`.

### Pillar 5: Spacing (2/4)

- **WARNING:** The page shell meets the 16px padding/gap contract at `sources/WebApp/Pages/Servers/Index.razor.css:4` and `sources/WebApp/Pages/Servers/Index.razor.css:5`.
- **WARNING:** The guided form grid uses the required `repeat(auto-fit, minmax(180px, 1fr))` with 8px gaps at `sources/WebApp/Pages/Servers/Index.razor.css:286`.
- **WARNING:** Several values violate the declared multiples-of-4 spacing scale: `6px` margin at `sources/WebApp/Pages/Servers/Index.razor.css:19`, `12px` padding/gaps at `sources/WebApp/Pages/Servers/Index.razor.css:40` and `sources/WebApp/Pages/Servers/Index.razor.css:59`, `10px` margin at `sources/WebApp/Pages/Servers/Index.razor.css:47`, `3px` gap/padding at `sources/WebApp/Pages/Servers/Index.razor.css:101` and `sources/WebApp/Pages/Servers/Index.razor.css:112`, and `7px` grid/table spacing at `sources/WebApp/Pages/Servers/Index.razor.css:148` and `sources/WebApp/Pages/Servers/Index.razor.css:195`.
- **WARNING:** Mobile collapse is implemented at `sources/WebApp/Pages/Servers/Index.razor.css:366`, but button full-width behavior only targets `fluent-button` descendants of `.guided-actions`; non-Fluent rendered internals may need browser verification.

### Pillar 6: Experience Design (3/4)

- **WARNING:** Local validation is comprehensive and blocks readiness for server eligibility, title length, active owner, saved assignment, modpack/runtime basics, required cloud inputs, port ranges, port uniqueness, storage minimum, and CIDR shape at `sources/WebApp/Pages/Servers/Index.razor:1565`.
- **WARNING:** Readiness is server-authoritative: `Continue to dry run` calls `ValidateGuidedCloudReadinessAsync` at `sources/WebApp/Pages/Servers/Index.razor:500`, and the ready summary renders only when `_guidedCloudReadiness?.IsReadyForDryRun is true` at `sources/WebApp/Pages/Servers/Index.razor:505`.
- **WARNING:** Server-returned blockers are rendered inline in a validation list at `sources/WebApp/Pages/Servers/Index.razor:486`, and local validation summary is rendered at `sources/WebApp/Pages/Servers/Index.razor:476`.
- **WARNING:** The readiness summary uses `dl.meta-grid` and includes owner assignment, controller, size, location, storage, ports, SSH/CIDR, modpack, and runtime basics at `sources/WebApp/Pages/Servers/Index.razor:509`.
- **WARNING:** The guided checkbox touch target is only 24px by default at `sources/WebApp/Pages/Servers/Index.razor.css:327`; the accessibility contract requires at least 44px touch targets on mobile for icon-only actions and practical touch controls.
- **WARNING:** No screenshot or authenticated interaction evidence was captured by design. Manual browser/authenticated UAT remains deferred, so text fit, actual Fluent component rendering, focus rings, and mobile wrapping still need browser verification later.

---

## Registry Safety

Registry audit skipped: `components.json` is not present, the UI-SPEC declares shadcn/third-party registries not applicable, and this is a Blazor/Fluent UI implementation.

---

## Files Audited

- `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-UI-SPEC.md`
- `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-CONTEXT.md`
- `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-04-PLAN.md`
- `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-04-SUMMARY.md`
- `.planning/phases/33-guided-entry-ownership-and-scope-boundaries/33-REVIEW.md`
- `AGENTS.md`
- `sources/WebApp/Pages/Servers/Index.razor`
- `sources/WebApp/Pages/Servers/Index.razor.css`
