# Cinematic Dual-Shell UI Contract - 2026-06-22

## Purpose

This document archives the chosen UI direction so a fresh implementation session can start without re-litigating the visual concept.

The direction is:

> United Tactical Forces should use one shared dark/gold cinematic design system with two explicit application shells: a Community Shell for public/member-facing pages and a Command Center Shell for management/admin surfaces.

The design should keep the strong landing-page atmosphere while solving the navigation conflict between a public website and an operational management tool.

## Reference Samples

Primary samples:

- Landing desktop reference: `.planning/ui-reviews/concepts/landing-exploration/03-event-first-homepage-variant.png`
- Landing mobile reference: `.planning/ui-reviews/concepts/landing-exploration/04-mobile-homepage-variant.png`
- Events board reference: `.planning/ui-reviews/concepts/cinematic-system/01-events-board-dark-cinematic.png`
- Event detail reference: `.planning/ui-reviews/concepts/cinematic-system/02-event-detail-slots-dark-cinematic.png`
- Asset management reference: `.planning/ui-reviews/concepts/cinematic-system/03-asset-library-dark-cinematic.png`
- Server fleet reference: `.planning/ui-reviews/concepts/cinematic-system/04-server-fleet-dark-cinematic.png`
- Cloud operations reference: `.planning/ui-reviews/concepts/cinematic-system/05-cloud-operations-dark-cinematic.png`
- Operation review reference: `.planning/ui-reviews/concepts/cinematic-system/06-operation-review-dark-cinematic.png`
- Compact admin reference: `.planning/ui-reviews/concepts/cinematic-system/07-compact-admin-dark-cinematic.png`

Context documents:

- UI findings: `.planning/ui-reviews/2026-06-22-ui-review-findings.md`
- Original redesign plan: `.planning/ui-reviews/2026-06-22-ui-redesign-implementation-plan.md`
- Landing exploration: `.planning/ui-reviews/2026-06-22-landing-page-concept-exploration.md`

Important: generated samples are visual direction only. Do not copy text, invented icons, generated logos, dates, provider names, or sample values literally.

## Core Decision

Do not solve public and management navigation with one universal header.

Use two shells:

1. Community Shell
2. Command Center Shell

Both shells share:

- Dark/gold brand language.
- Typography scale.
- Semantic status colors.
- Component styling.
- Button, badge, table, panel, and form primitives.
- Responsive breakpoints.

They do not share navigation structure.

## Why Two Shells

The public top navigation is excellent for the landing page and normal member browsing:

- It is cinematic.
- It preserves the brand-first experience.
- It supports simple routes like Home, Operations, Unit, Members, Media, Forums.
- It works well on mobile as a hamburger menu.

The same top navigation is poor for management:

- Admin workflows need many destinations.
- Server/cloud/billing/audit workflows are not peer routes with Home or Media.
- Management users need persistent context, alerts, filters, and selected-object navigation.
- Long operational workflows need room for a command bar and status context.

The solution is a deliberate mode switch:

- Community Shell: "I am browsing or participating."
- Command Center Shell: "I am operating or managing."

The user should always know which mode they are in.

## Shell 1 - Community Shell

### Purpose

Public and member-facing experience:

- Home
- Upcoming events
- Event detail and slots
- Event archive, if user-facing
- Unit/member pages
- Media/wiki-style public content
- Community links

### Desktop Navigation

Use a dark, cinematic top bar:

```text
[UTF Mark] United Tactical Forces     Home  Operations  Unit  Members  Media  Forums      Search  Sign In / Profile  Join Us
```

When logged in with management permissions, add a single clear management entry:

```text
Command Center
```

Placement options:

- Preferred: profile/account menu contains `Command Center`.
- Also acceptable: gold outlined `Command Center` action near the profile for admins.
- Avoid mixing all admin routes into the public top nav.

### Mobile Navigation

Use the mobile sample as the pattern:

- Top bar with UTF mark and hamburger.
- Full-screen or slide-in menu.
- Public routes first.
- If authorized, a separated `Command Center` section.

Example:

```text
Home
Operations
Unit
Members
Media
Forums

Command Center
Sign out
```

### Community Home Layout

Use the event-first landing sample:

1. Full-bleed cinematic hero.
2. Large operation-first title or UTF title.
3. Real next operation data.
4. Primary action: view slots.
5. Secondary community links: Discord, TeamSpeak.
6. Next operations band visible below the hero.

The home page should not be a static poster. It should consume real data:

- Next event title.
- Date/time.
- Terrain.
- Modpack.
- Slot count.
- User attendance state if authenticated.
- Link to slots.

### Community Event Pages

Events can keep cinematic imagery, but content must remain readable:

- Event cards may use image thumbnails.
- Event detail may use a large mission banner.
- Slot rosters, metadata, and action controls sit on solid charcoal panels.
- Do not put dense roster rows directly over imagery.

## Shell 2 - Command Center Shell

### Purpose

Management and admin workflows:

- Management overview
- Event administration
- Server fleet
- Cloud operations
- Workers and lifecycle jobs
- Mod packs
- Terrains
- Users, roles, claims
- Billing owners
- Audit log
- Settings

### Desktop Navigation

Use a left navigation rail plus utility top bar.

Top bar:

```text
Command Center / current area     Search     Alerts     Current user     Back to site
```

Left rail:

```text
Command Center
- Overview

Operations
- Events
- Servers
- Cloud Operations
- Workers & Jobs

Assets
- Mod Packs
- Terrains
- Wiki / Media

Community
- Members
- Roles
- Claims

Governance
- Billing Owners
- Audit Log
- Settings
```

The left rail must not pretend to be public navigation. It is operational navigation.

### Mobile/Compact Admin

Use the compact admin sample:

- No desktop table squeezed into mobile.
- Use one-column selected-object cards.
- Show attention chips first.
- Keep normal runtime actions as large buttons.
- Put destructive or cost actions behind a review bottom sheet.
- Provide a compact menu drawer for management sections.

### Command Center Overview

This is the management landing page, not a "pre-page" obstacle.

It should show:

- Next event requiring admin action.
- Active lifecycle jobs.
- Stale workers.
- Active cost exposure.
- Pending policy approvals.
- Cleanup risks.
- Recent audit records.
- Quick links to Events, Servers, Cloud, Assets, Members/Roles.

It answers: "What needs my attention now?"

## Visual System

### Design Character

Use the phrase:

> Cinematic operations UI.

The public side may be more cinematic. The management side must be more operational.

Allowed:

- Dark hero imagery.
- Charcoal panels.
- Thin gold active indicators.
- Gold primary calls to action.
- Subtle tactical/header textures.
- Event and terrain thumbnails.

Avoid:

- Background images behind tables, forms, logs, or policy/cost data.
- Gold as warning color.
- Blue-dominated enterprise styling.
- Decorative gradient blobs.
- Cards inside cards.
- Dense admin actions hidden in public navigation.

### Token Palette

Initial CSS token direction:

```css
:root {
    --utf-bg: #070b0f;
    --utf-bg-elevated: #0d131a;
    --utf-surface: #111820;
    --utf-surface-2: #151e27;
    --utf-surface-glass: rgba(12, 17, 23, 0.78);
    --utf-stroke: rgba(255, 255, 255, 0.12);
    --utf-stroke-strong: rgba(255, 255, 255, 0.22);

    --utf-text: #f5f7fa;
    --utf-text-muted: #aeb7c2;
    --utf-text-subtle: #778391;

    --utf-gold: #baa05a;
    --utf-gold-strong: #d4ad54;
    --utf-gold-muted: rgba(186, 160, 90, 0.16);

    --utf-success: #22c55e;
    --utf-success-bg: rgba(34, 197, 94, 0.14);
    --utf-warning: #f59e0b;
    --utf-warning-bg: rgba(245, 158, 11, 0.16);
    --utf-danger: #ef4444;
    --utf-danger-bg: rgba(239, 68, 68, 0.16);
    --utf-info: #60a5fa;
    --utf-info-bg: rgba(96, 165, 250, 0.14);

    --utf-radius-sm: 4px;
    --utf-radius-md: 8px;
    --utf-radius-lg: 12px;

    --utf-shadow-panel: 0 18px 60px rgba(0, 0, 0, 0.34);
}
```

Map Fluent UI tokens to these rather than scattering raw colors:

```css
body {
    background: var(--utf-bg);
    color: var(--utf-text);
}

.utf-panel {
    background: var(--utf-surface);
    border: 1px solid var(--utf-stroke);
    border-radius: var(--utf-radius-md);
}
```

### Semantic Color Rules

Gold:

- Brand.
- Selected nav.
- Primary public CTA.
- Selected row outline.
- Current step in wizard/review.

Green:

- Online.
- Confirmed.
- Running healthy.
- Policy pass.

Amber:

- Attention.
- Low slots.
- Approval required.
- Stale worker.
- Cost still active.
- Partial cleanup.

Red:

- Failed.
- Destructive.
- Teardown.
- Delete.
- Orphan requiring action.

Blue:

- Links/focus only, if needed.
- Do not let blue become the main operational color.

## Component Contracts

### App Shell Components

```text
CommunityShell
- CommunityTopNav
- CommunityMobileMenu
- ProfileMenu
- CommunityFooterRail

CommandCenterShell
- CommandTopBar
- CommandSideNav
- CommandMobileDrawer
- AttentionBar
- BackToSiteAction
```

### Shared Components

```text
UtfPageHeader
UtfHero
UtfPanel
UtfSectionHeader
UtfCommandBar
UtfStatusBadge
UtfAttentionCard
UtfDataTableShell
UtfMasterDetailWorkspace
UtfFormPanel
UtfOperationReviewDialog
UtfEvidenceChecklist
UtfCostSummary
UtfJobTimeline
UtfEmptyState
UtfImageTile
```

### Status Badge Sample

```razor
<span class="utf-status utf-status--success">
    <span class="utf-status__dot"></span>
    Confirmed
</span>
```

```css
.utf-status {
    align-items: center;
    border-radius: 999px;
    display: inline-flex;
    font-size: 0.82rem;
    font-weight: 650;
    gap: 0.45rem;
    min-height: 28px;
    padding: 0 0.7rem;
}

.utf-status__dot {
    border-radius: 999px;
    height: 0.5rem;
    width: 0.5rem;
}

.utf-status--success {
    background: var(--utf-success-bg);
    color: var(--utf-success);
}

.utf-status--success .utf-status__dot {
    background: var(--utf-success);
}
```

### Community Hero Sample

```razor
<section class="utf-landing-hero" style="--hero-image: url('/images/landing/hero.jpg')">
    <div class="utf-landing-hero__overlay"></div>
    <div class="utf-landing-hero__content">
        <p class="utf-eyebrow">Next Operation</p>
        <h1>Silent Watch</h1>

        <div class="utf-landing-hero__meta">
            <span>24 May 2025</span>
            <span>20:00 UTC</span>
            <span>32 / 40 slots</span>
        </div>

        <FluentButton Appearance="Appearance.Accent">
            View Slots
        </FluentButton>
    </div>
</section>
```

```css
.utf-landing-hero {
    background-image:
        linear-gradient(90deg, rgba(7, 11, 15, 0.92), rgba(7, 11, 15, 0.38) 46%, rgba(7, 11, 15, 0.78)),
        var(--hero-image);
    background-position: center;
    background-size: cover;
    min-height: min(820px, calc(100dvh - 72px));
    position: relative;
}

.utf-landing-hero__content {
    display: flex;
    flex-direction: column;
    gap: 1.25rem;
    margin-inline: auto;
    max-width: 1440px;
    padding: clamp(3rem, 8vh, 7rem) clamp(1rem, 5vw, 5rem);
}

.utf-landing-hero h1 {
    color: var(--utf-text);
    font-size: clamp(4rem, 7.5rem, 7.5rem);
    font-weight: 800;
    letter-spacing: 0;
    line-height: 0.92;
    margin: 0;
    max-width: 720px;
    text-transform: uppercase;
}
```

Note: the `clamp()` sample above uses fixed min/max values. Do not use viewport-width-only typography.

### Command Center Layout Sample

```razor
<div class="utf-command-shell">
    <aside class="utf-command-nav">
        <CommandSideNav />
    </aside>

    <div class="utf-command-main">
        <CommandTopBar />

        <main class="utf-command-content">
            @Body
        </main>
    </div>
</div>
```

```css
.utf-command-shell {
    background: var(--utf-bg);
    color: var(--utf-text);
    display: grid;
    grid-template-columns: 280px minmax(0, 1fr);
    min-height: 100dvh;
}

.utf-command-nav {
    background: linear-gradient(180deg, #070b0f, #0c1218);
    border-right: 1px solid var(--utf-stroke);
}

.utf-command-main {
    display: grid;
    grid-template-rows: 64px minmax(0, 1fr);
    min-width: 0;
}

.utf-command-content {
    min-width: 0;
    padding: 1.25rem;
}
```

### Master/Detail Sample

```razor
<div class="utf-master-detail">
    <section class="utf-panel utf-master-detail__list">
        <ServerTable />
    </section>

    <section class="utf-panel utf-master-detail__detail">
        <SelectedServerDetail />
    </section>
</div>
```

```css
.utf-master-detail {
    align-items: start;
    display: grid;
    gap: 1rem;
    grid-template-columns: minmax(460px, 0.95fr) minmax(420px, 1.05fr);
}

@media (max-width: 1100px) {
    .utf-command-shell {
        grid-template-columns: 1fr;
    }

    .utf-command-nav {
        display: none;
    }

    .utf-master-detail {
        grid-template-columns: 1fr;
    }
}
```

## Page Contracts

### Home

Shell: Community Shell.

Must include:

- Cinematic event-first hero.
- Next event from real data.
- Discord and TeamSpeak actions.
- Next operations band.
- "Who we are" section below the fold.

Do not include:

- Management navigation.
- Admin tables.
- Deep cloud/server state.

### Upcoming Events

Shell: Community Shell.

Use:

- Dark event cards.
- Image thumbnails.
- Filters as chips.
- Slot progress.
- Status badges.

Logged-in users should see their attendance state.

### Event Detail

Shell: Community Shell for normal event participation.

Use:

- Mission banner.
- Metadata bar.
- Briefing panel.
- Sticky attendance panel.
- Slot roster on solid panels.

Admin actions can appear in a small event command bar, but complex event administration should move to Command Center if it grows.

### Command Center Overview

Shell: Command Center Shell.

Use:

- Attention cards.
- Recent activity.
- Quick links.
- Summaries, not deep tables.

This page replaces the idea of a confusing management pre-page.

### Event Administration

Shell: Command Center Shell.

Use:

- Event table/list.
- Create/edit event workflow.
- Slot management tools.
- Archive and filters.

Keep the visual connection to community events, but prioritize management efficiency.

### Assets

Shell: Command Center Shell.

Use:

- Tabs or section switcher for Mod Packs and Terrains.
- Search/filter.
- List/detail or table/editor split.
- Right-side create/edit form panel.
- Inline validation.

Do not show dead edit/delete buttons.

### Server Fleet

Shell: Command Center Shell.

Use:

- Attention bar: active jobs, stale workers, billing owner, cleanup risk.
- Server table/list.
- Selected server detail tabs:
  - Overview
  - Runtime
  - Cloud
  - Jobs
  - Ownership
  - Audit
  - Danger Zone

Danger actions must not live as casual row icons.

### Cloud Operations

Shell: Command Center Shell.

Use:

- Attention-first summary.
- Tabs: Sessions, Plans, Resources, Estimates, Reconciliation, Policies, Configurations, Metadata.
- Master/detail layout.
- Policy/cost/cleanup detail panel.
- Review-required actions.

### Operation Review

Shell: Command Center Shell.

Use for:

- Provisioning.
- Provider teardown.
- Server delete.
- Runtime cleanup when cost or resources may remain.
- Ownership transfer.

Review must show:

- Action name.
- Server/session.
- Billing owner.
- Cost estimate/current cost.
- Policy gate.
- Preflight evidence.
- Provider resources affected.
- Cleanup consequence.
- Confirmation checkbox or typed confirmation for destructive actions.

## Interaction Rules

### Navigation

- Public routes stay in Community Shell.
- Management routes stay in Command Center Shell.
- The only bridge from Community to management is `Command Center`.
- The only bridge back is `Back to site`.
- Do not render a hybrid shell with both full public top nav and full management left nav.

### Actions

Normal low-risk actions:

- Can be direct buttons if state and effect are obvious.

High-risk actions:

- Must open operation review.

High-risk includes:

- Creates provider resources.
- Starts live billing.
- Deletes provider resources.
- Deletes server records.
- Changes billing owner.
- Cleans up runtime/provider resources.
- Can leave billable resources behind.

### Forms

- Validate inline.
- Do not close dialogs/panels on invalid submit.
- Show required field errors next to fields.
- Show repository failures in the same panel.

### Loading/Empty/Error

Every page must have:

- Loading state.
- Empty state.
- Failed-load state.
- Unauthorized/no-permission state where applicable.

## Implementation Sequence

### Phase 0 - Protect Current UI

Goal: create a branch and keep changes reviewable.

Tasks:

- Create a branch such as `codex/cinematic-dual-shell-ui`.
- Do not regenerate Kiota clients unless API contracts change.
- Do not touch `.planning/tmp` or unrelated backend work.

### Phase 1 - Tokens and Shared CSS

Goal: establish the dark/gold system without changing routes.

Tasks:

- Add global design tokens.
- Add `utf-*` utility/component CSS classes.
- Map Fluent accent/custom color to UTF gold.
- Replace hard-coded light backgrounds in new components only.

Verification:

- Existing pages still compile.
- Dark background, text, stroke, gold, status colors render consistently.

### Phase 2 - Shell Split

Goal: create explicit Community and Command Center shells.

Tasks:

- Introduce `CommunityLayout`.
- Introduce `CommandCenterLayout`.
- Route public pages through Community Shell.
- Route management/admin pages through Command Center Shell.
- Add `Command Center` link for authorized users.
- Add `Back to site` in Command Center.

Verification:

- Non-authenticated users never see management nav.
- Admin users can move between site and command center intentionally.
- Navigation does not duplicate public and admin route sets.

### Phase 3 - Home and Event Public UX

Goal: implement the most visually important direction first.

Tasks:

- Build cinematic home hero.
- Pull next event data.
- Build next operations band.
- Redesign upcoming event cards.
- Redesign event detail with mission banner and slot panel.

Verification:

- Desktop matches landing/event samples in structure.
- Mobile matches mobile landing sample in behavior.
- Text remains real HTML and localizable.

### Phase 4 - Command Center Overview

Goal: make the management entry useful.

Tasks:

- Create `/command` or `/management` overview route.
- Show attention cards for events, jobs, workers, cost, cleanup, approvals.
- Link to server/cloud/assets/admin sections.

Verification:

- Admin user can answer "what needs attention?" within one page.

### Phase 5 - Assets and Event Admin

Goal: migrate simpler management pages before cloud/server complexity.

Tasks:

- Move mod packs and terrains into Command Center layout.
- Add list/detail or table/editor structure.
- Fix inline validation pattern.
- Remove or implement dead actions.
- Move event archive/create/admin tools into management shell if appropriate.

Verification:

- Create/edit/delete flows show feedback.
- Dialogs do not silently close on invalid input.

### Phase 6 - Server Fleet

Goal: convert the largest current page into a safer workspace.

Tasks:

- Build server fleet master/detail layout.
- Add attention summary.
- Move sections into tabs.
- Separate runtime actions from danger actions.
- Add operation review for delete/cleanup/teardown/provisioning as needed.

Verification:

- Billing owner is visible on every hosting action.
- Runtime stopped and provider billing stopped are visually distinct.
- Stale worker and cleanup states are visible without deep scrolling.

### Phase 7 - Cloud Operations

Goal: make cloud admin safe and coherent.

Tasks:

- Convert cloud page to dark command center system.
- Keep tabbed workspace.
- Use selected-session detail panel.
- Route provision/teardown through operation review.
- Standardize policy/cost/finding cards.

Verification:

- No direct destructive action from a dense table row.
- Cost, owner, policy, and resources are visible before action.

### Phase 8 - Responsive and Accessibility QA

Goal: finish the design responsibly.

Tasks:

- Keyboard navigation pass.
- Accessible labels for icon buttons.
- Meaningful alt text for images.
- Mobile checks for home, events, command overview, server detail, operation review.
- Playwright screenshots for desktop and mobile.

Verification:

- No overlapping text.
- No desktop table squeezed into mobile.
- Focus states are visible on dark backgrounds.

## Fresh Session Prompt

Use this prompt to start implementation in a fresh Codex session:

```text
Read A:\dev\git\UnitedTacticalForces\.planning\ui-reviews\2026-06-22-cinematic-dual-shell-ui-contract.md.

Implement the United Tactical Forces cinematic dual-shell UI direction.

Start with Phase 1 and Phase 2 only:
- Add shared dark/gold design tokens and reusable utf-* CSS primitives.
- Introduce separate Community and Command Center layouts.
- Keep public/member pages in Community Shell.
- Move management/admin routes behind Command Center Shell.
- Add an authorized Command Center entry from the community shell and a Back to site action from the command shell.

Do not redesign every page yet.
Do not modify backend behavior.
Do not regenerate generated API clients unless required.
Preserve existing authorization behavior.
Run dotnet build X39.UnitedTacticalForces.slnx before finishing.
```

## Acceptance Criteria

The implementation is on-direction when:

- The public home page can keep the cinematic top-nav experience.
- Management pages no longer depend on the public top navigation.
- Both shells clearly belong to the same UTF product.
- Admin navigation is persistent, grouped, and efficient.
- Public navigation remains simple and attractive.
- Dark/gold styling does not weaken warning/danger semantics.
- High-risk operations require a review step.
- Mobile uses drawers/cards/bottom sheets instead of compressed desktop tables.

## Non-Goals

- Do not implement Hetzner provisioning as part of UI redesign.
- Do not replace Fluent UI.
- Do not rebuild the app as a separate SPA framework.
- Do not make generated concept art production assets with baked-in text.
- Do not force every page to use large background imagery.
- Do not move authorization logic to the client.

