# Mission Tracking Tactical Replay Concept - 2026-06-24

## Purpose

This note anchors the CR-0002 mission tracking implementation direction to the cinematic dual-shell UI system.

Generated concept image:

- `.planning/ui-reviews/concepts/mission-tracking/01-mission-replay-command-center-concept.png`

Related source documents:

- `docs/change-requests/CR-0002-mission-tracking-and-tactical-replay.md`
- `.planning/ui-reviews/2026-06-22-cinematic-dual-shell-ui-contract.md`

## Direction

The mission tracking replay surface belongs in the Command Center shell. It is an operational analysis workspace, not
a Community Shell hero page.

Use the concept image for:

- Command Center left rail plus compact top utility bar.
- Dominant calibrated terrain map as the main workspace.
- Spot and range replay modes.
- Range/path trails over the terrain map.
- Bottom playback timeline with play, scrub, event markers, and speed control.
- Right-side inspector for recording status, event assignment, ingestion completeness, entity filters, and selected
  entity details.
- Dark/charcoal panels, thin gold active indicators, and semantic green/amber/red status accents.

Do not copy from the generated image:

- Generated logo shapes.
- Exact labels, dates, grid values, entity names, or event names.
- Invented icons.
- Any sample data.

## Implementation Notes

- Keep the page in the Command Center shell from the dual-shell contract.
- Do not introduce public top navigation on this workspace.
- Do not treat the concept as live-tracking scope. CR-0002 v1 remains post-mission replay only.
- Keep dense controls on solid panels, not over background imagery.
- Make the terrain map dominant, with controls arranged around it rather than above it as a normal form.
- Preserve the dark/gold visual system while keeping status semantics distinct: gold for active selection, green for
  healthy/complete, amber for attention, and red for failed/destructive states.
- Use real HTML controls and localizable text during implementation; generated UI text in the image is only visual
  scaffolding.

## Generation Prompt

```text
Use case: ui-mockup
Asset type: implementation orientation concept image for a web app feature
Primary request: Create a high-fidelity desktop UI concept for the United Tactical Forces Mission Tracking tactical replay page, aligned with a dark/gold cinematic operations UI and an explicit Command Center shell.
Scene/backdrop: A single desktop application screen, 16:9 composition, no browser chrome, no marketing hero, no public top navigation.
Subject: Command Center mission replay workspace. Include a persistent dark left navigation rail, a compact utility top bar, and a main operational workspace. The central area is a calibrated Arma-style terrain map with tactical grid/contour texture, player/NPC/vehicle markers, a selected unit marker, muted faction colors, thin gold active outlines, and path trails for range mode. Add a bottom playback timeline with play/pause control, scrubber, tick marks, and speed control. Add a right-side inspector panel with recording status, event assignment, entity filters, selected entity details, and ingestion completeness indicators. Include a small mode switch for Spot and Range.
Style/medium: polished product UI mockup, cinematic operations interface, realistic but not photorealistic, crisp modern Blazor/Fluent-inspired web application surface.
Composition/framing: full-screen dashboard screenshot, dense but organized, central map dominant, left rail narrow, right inspector medium width, bottom timeline integrated into the map area. Stable 8px-or-less card/panel radius, no cards inside cards.
Lighting/mood: dark tactical atmosphere, high readability, quiet operational confidence.
Color palette: near-black background, charcoal panels, muted white text, restrained gold accents, semantic green/amber/red status chips, small blue only for focus/link hints.
Text: Use only short generic UI labels such as Mission Replay, Spot, Range, Entities, Recording, Event, Complete, 2x. Do not include paragraphs or marketing copy.
Constraints: match the cinematic dual-shell direction: Command Center shell, dark/gold shared visual system, operational management surface. Keep controls plausible and implementation-oriented. Use map imagery as an illustrative tactical terrain, not a real copyrighted game map. No generated logos, no provider names, no exact sample dates, no decorative blobs, no blue enterprise theme, no background images behind dense tables, no watermark.
```
