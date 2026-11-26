# Landing Page Concept Exploration - 2026-06-22

## Direction

Treat the first generated image as a separate public landing-page direction, not as a generic UI concept. It works because it does three things the current home page does not yet do strongly:

- Makes the UTF brand the first-viewport signal.
- Uses the current app's dark/gold language in a more deliberate way.
- Connects public community identity directly to the next useful action: view the upcoming operation.

This direction should be called the Cinematic Community Gateway. It should be used for the public home page only. The server, cloud, asset, and admin pages should still use the cleaner operations-console direction from the redesign plan.

## Generated Variants

Four landing-page explorations were generated and saved under `.planning/ui-reviews/concepts/landing-exploration/`.

1. Cinematic homepage variant: `.planning/ui-reviews/concepts/landing-exploration/01-cinematic-homepage-variant.png`
2. Functional preview bands variant: `.planning/ui-reviews/concepts/landing-exploration/02-functional-preview-bands-variant.png`
3. Event-first homepage variant: `.planning/ui-reviews/concepts/landing-exploration/03-event-first-homepage-variant.png`
4. Mobile homepage variant: `.planning/ui-reviews/concepts/landing-exploration/04-mobile-homepage-variant.png`

Treat exact generated text, icons, dates, and logos as placeholders. The useful output is the composition, color balance, hierarchy, and page structure.

## Recommended Combination

Use variant 1 as the main first-viewport direction:

- Dark fixed or sticky header.
- UTF mark and name at the far left.
- Public navigation across the header.
- Search/sign-in/join actions at the far right.
- Full-bleed cinematic background image.
- Large left-aligned title.
- Short gold tagline.
- Discord and TeamSpeak status/action blocks.
- Wide next-operation strip near the bottom of the hero.
- A visible hint of the next section below the fold.

Use variant 3 for the section immediately below the hero:

- A dark full-width "Next Operations" band.
- Three operation cards with image, date, slot progress, and status.
- Link to the full event calendar.
- Community utility links in a compact lower rail.

Use variant 4 for mobile behavior:

- Collapse desktop nav into a hamburger menu.
- Keep the hero image, but crop for vertical readability.
- Turn the desktop next-operation strip into a stacked operation card.
- Keep Discord and TeamSpeak as two large tappable chips.
- Keep the primary action, `View Slots`, visible in the first mobile screen.

Variant 2 is useful for page structure but should be applied more cautiously. It shows how event, mod-pack, and voice/community previews can appear below the hero, but those panels should not overload the first viewport.

## Color Direction

This direction aligns well with the current app theme because it uses:

- Near-black header and page background.
- Charcoal translucent surfaces.
- UTF gold close to the current Fluent custom color `#BAA05A`.
- White hero typography.
- Muted gray borders and dividers.
- Green only for live/confirmed states.
- Amber only for attention and primary landing-page emphasis.

Avoid making the whole app dark/gold. Keep this palette concentrated on the public landing page and potentially event promotional surfaces. Operations pages still need a neutral console palette where warning, danger, and cost states are more important than brand atmosphere.

## Copy Direction

The current generated variants use short, high-impact phrases. For the real UI, prefer copy that is clear and inclusive:

- Primary title: `United Tactical Forces`
- Tagline option: `Realismus. Kooperation. Gemeinschaft.`
- Event label: `Naechste Operation` or localized `Nächste Operation`
- Primary action: `Slots ansehen` or `View Slots`
- Utility actions: `Discord`, `TeamSpeak`

If the app remains German-first on public pages, the landing page should be consistently German. Avoid mixing German labels with English action labels unless that matches the community's actual language practice.

## Functional Requirements

The landing page should not be a static poster. It should pull real app data where possible:

- Next upcoming event title.
- Event date and local time.
- Slot count and capacity.
- Event status.
- Link to event slots.
- Discord link.
- TeamSpeak link.
- Optional: next two or three events below the hero.

If no upcoming event exists, replace the operation strip with a useful empty state:

- `Keine Operation geplant`
- Link to event archive.
- Discord/TeamSpeak join actions.

## Implementation Shape

Keep implementation small and componentized:

- `LandingHero`: background image, headline, tagline, utility links, next-operation summary.
- `CommunityStatusLinks`: Discord and TeamSpeak action chips.
- `NextOperationStrip`: desktop horizontal version.
- `NextOperationCard`: mobile stacked version.
- `NextOperationsBand`: optional below-hero event preview band.
- `LandingSection`: reusable dark section wrapper for "Wer wir sind", gallery, and setup content.

The hero background should be an image asset, not generated through CSS gradients or SVG decoration. Text must remain real HTML over the image for localization, accessibility, and responsive behavior.

## Practical CSS Notes

- Use `min-height: min(820px, calc(100dvh - header-height))` for desktop hero behavior.
- Use a layered overlay: left-to-right black gradient for text readability plus a subtle bottom gradient for the event strip.
- Keep the operation strip inside a constrained max-width container.
- On mobile, use `min-height: auto`, a fixed aspect-ratio image area, and stacked content.
- Do not scale font size with viewport width. Use fixed responsive breakpoints.
- Keep gold borders and hover states subtle. The gold should guide attention, not dominate every element.

## Production Image Recommendation

For implementation, generate or select one final hero background and two or three event thumbnail images. The concept images are not production-ready assets because they contain placeholder UI text and invented marks. The final hero background should have no baked-in UI text, no logos, and enough dark negative space on the left for real HTML text.

