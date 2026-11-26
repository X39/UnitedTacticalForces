---
phase: 57
slug: guided-server-creation-journey
status: approved
shadcn_initialized: false
preset: none
created: 2026-07-05
---

# Phase 57 - UI Design Contract

> Visual and interaction contract for the guided server creation journey. The contract is grounded in
> `sources/WebApp/Pages/Servers/Create.razor`, `sources/WebApp/Pages/Servers/Advanced.razor`, and the
> phase 57 discussion decisions.

---

## Design System

| Property | Value |
|----------|-------|
| Tool | none |
| Preset | not applicable |
| Component library | Microsoft Fluent UI Blazor plus existing UTF command components |
| Icon library | Microsoft Fluent UI Blazor Icons |
| Font | Inherited application/system font |

Use the existing command-center surface language from `sources/WebApp/Pages/Servers/*.razor.css`: dark UTF
surfaces, compact command controls, fixed-radius panels, status badges, and task-focused action rows. Do not
introduce a marketing landing layout, oversized hero section, decorative illustration, or nested cards.

---

## Spacing Scale

Declared values (must be multiples of 4):

| Token | Value | Usage |
|-------|-------|-------|
| xs | 4px | Label-control gaps, inline icon gaps, field hints |
| sm | 8px | Step list gaps, button groups, compact validation rows |
| md | 16px | Form grid gaps, panel padding, default wizard surface spacing |
| lg | 24px | Wizard body padding, section breaks, mobile stacked step spacing |
| xl | 32px | Desktop wizard column gap and review summary separation |
| 2xl | 48px | Major page separation only when the command header and wizard need breathing room |
| 3xl | 64px | Reserved for page-level spacing outside the wizard; normally unused in this phase |

Exceptions: existing UTF component internals may keep their current tokenized spacing. New page-level CSS must
use the scale above and avoid rem values that obscure the underlying pixel intent.

---

## Typography

| Role | Size | Weight | Line Height |
|------|------|--------|-------------|
| Body | 14px | 400 | 1.5 |
| Label | 12px | 800 | 1.3 |
| Heading | 20px | 800 | 1.2 |
| Display | 28px | 800 | 1.0 |

Display type is allowed only in the existing command page header. Wizard step headings use Heading or smaller.
Letter spacing is always `0`. Do not scale type with viewport width.

---

## Color

| Role | Value | Usage |
|------|-------|-------|
| Dominant (60%) | `var(--utf-bg, #0b1117)` | Page background and deep input surfaces |
| Secondary (30%) | `var(--utf-surface, #101820)` | Wizard surface, step rail, review summary, empty states |
| Accent (10%) | `var(--utf-gold, #f59e0b)` | Active step marker, focused fields, primary final action, selected mode affordance |
| Destructive | `var(--utf-danger, #ef4444)` | Validation blockers, destructive links, failed creation state only |

Accent reserved for: active wizard step, focus outlines, selected mode indicator, `Create server`, and the
workspace continuation action. Do not use gold for every interactive control.

Status color usage:

| Status | Usage |
|--------|-------|
| Info | Non-blocking context, previewed defaults, saved-result links |
| Warning | Missing or stale cloud setup, disabled option with an available fix |
| Success | Successful final submit and created server handoff |
| Danger | Failed final submit, unavailable billing owner, invalid selections |

---

## Copywriting Contract

| Element | Copy |
|---------|------|
| Primary CTA | Create server |
| Secondary navigation | Back to fleet |
| Workspace CTA | Continue cloud setup |
| Cloud continuation CTA | Review cloud setup |
| Empty state heading | No server options available |
| Empty state body | Ask a server administrator to enable at least one creation option. |
| Error state | We could not create the server. Review the highlighted steps and try again. |
| Destructive confirmation | Delete server: type the server title to confirm. |

Mode labels are fixed:

| Mode | Label | Description |
|------|-------|-------------|
| Record | Server record | Create a server record now and set it up later. |
| Local managed | Local server | Create a server and prepare the first local setup action. |
| Cloud | Cloud server | Create a server and prepare cloud setup review. |

Wizard step labels are fixed:

| Step | Label | Purpose |
|------|-------|---------|
| 1 | Choose game | Select supported game/controller. Arma 3 is the only expected runnable option today. |
| 2 | Choose hosting | Pick Server record, Local server, or Cloud server. |
| 3 | Server details | Enter server title and billing owner. |
| 4 | First setup | Collect only the options required for the first runnable next action. |
| 5 | Review | Show every choice, blockers, and the single final save action. |

Internal terms such as controller, runtime profile, template, dry run, and policy gate are allowed only in detail,
review, or error context. The mode chooser and step titles use user-facing language.

---

## Interaction Contract

1. Wizard steps hold local component state only. No server record, billing owner, plan, runtime profile assignment,
   lifecycle job, or audit-side effect is saved before the final Review step action.
2. The final submit is the first persistence boundary. If submission fails, the wizard stays open, preserves all
   choices, and highlights the exact fields or setup choices requiring attention.
3. The final submit creates the server and immediately opens the server workspace. Cloud selections may be passed
   as navigation defaults to the advanced cloud page; they must not be saved as partial setup state before submit.
4. Additional setup wizards are allowed after creation from the server workspace or advanced cloud operations page.
5. Disabled options remain visible when the user can fix them. Each disabled option names the blocker and provides
   the next action where one exists.
6. Stale cloud setup explains what changed and offers `Review cloud setup again`.
7. Missing choices are shown inline on the affected step and summarized in Review.

---

## Component/Layout Contract

Page structure:

| Region | Contract |
|--------|----------|
| Header | Keep `UtfCommandPageHeader` with Back to fleet action. Header copy stays concise and task-focused. |
| Wizard shell | One un-nested `utf-panel` surface containing a step rail and active step body. |
| Step rail | Desktop left rail `minmax(180px, 260px)` plus flexible content column. Mobile stacks above content. |
| Step body | Grid fields use `repeat(auto-fit, minmax(180px, 1fr))`; fields never force horizontal scroll. |
| Review | Summary groups by Game, Hosting, Details, First setup, and Next action. |
| Billing owner helper | Billing owner creation can be offered as an inline helper or adjacent panel, but creation still should not save the server wizard itself. |
| Footer actions | Back, Next, and final `Create server`; final action appears only on Review. |

Controls:

| Control | Contract |
|---------|----------|
| Mode selection | Segmented button/filter row with fixed dimensions; options can be disabled with visible reason text. |
| Game selection | Select/list; Arma 3 option appears first when available. |
| Billing owner | Select with display name and key; inactive owners are disabled and labeled as inactive. |
| Runtime basics | Textarea for first setup notes; max 600 characters. |
| Validation | Inline field messages plus Review summary. |

Use Fluent icons inside buttons when already available:

| Action | Icon |
|--------|------|
| Back to fleet | `ArrowLeft` |
| Create server | `New` or `PlayCircle` based on hosting result |
| Continue cloud setup / Review cloud setup | `CloudFlow` |
| Next | `ArrowRight` |
| Warning or blocker | existing status badge/error styling; no custom SVG |

---

## Responsive And Accessibility Contract

| Concern | Requirement |
|---------|-------------|
| Desktop | Wizard rail and step body render side by side above 860px. |
| Mobile | Rail stacks above content, action buttons stretch to full width, and Review groups remain readable without horizontal scroll. |
| Keyboard | Step navigation, mode selection, fields, and final submit are reachable in logical order. |
| Screen readers | Active step, validation errors, disabled option reasons, and final submit result use existing semantic roles or `aria` labels. |
| Text fit | Mode labels and step labels must wrap cleanly; no button text may overflow at 320px width. |
| Focus | Focus outlines use the accent token and remain visible on dark surfaces. |

---

## State And Persistence Contract

| State | Scope | Persistence |
|-------|-------|-------------|
| Wizard choices | `Create.razor` component state | In memory only until final submit |
| Final record/local creation | API repository call | Saved on final submit |
| Cloud planning server | API repository call | Saved on final submit |
| Cloud setup defaults | Query string/navigation defaults | Not persisted as setup state until advanced workflow action |
| Billing owner creation | Existing billing owner API | May be saved only when user explicitly creates a billing owner; this is separate from saving wizard progress |

If the user creates a billing owner during the wizard, make the boundary explicit with button text and success
feedback. Do not imply the server wizard itself has been saved.

---

## Registry Safety

| Registry | Blocks Used | Safety Gate |
|----------|-------------|-------------|
| shadcn official | none | not required |
| third-party registries | none | not allowed for this phase |

---

## Checker Sign-Off

- [x] Dimension 1 Copywriting: PASS
- [x] Dimension 2 Visuals: PASS
- [x] Dimension 3 Color: PASS
- [x] Dimension 4 Typography: PASS
- [x] Dimension 5 Spacing: PASS
- [x] Dimension 6 Registry Safety: PASS

**Approval:** approved 2026-07-05
