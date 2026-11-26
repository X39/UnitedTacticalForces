# Phase 67 - UI Review

**Audited:** 2026-11-22
**Baseline:** 67-UI-SPEC.md (approved 2026-11-21)
**Screenshots:** not captured (code-only audit; the run constraints asked not to bind dev-server ports, and no server was started)
**Interaction captures:** off (workflow.ui_interaction_capture is false). All interaction findings are code-derived.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | All strings go through `Server_Metrics_*` keys, EN and DE keys exist and there are no banned labels. Visual rendering of the German strings is unverified. |
| 2. Visuals | 3/4 | The structure matches the contract. Chart rendering, tooltip placement and crosshair were not seen, so nothing above 3 can be proven. |
| 3. Color | 3/4 | The new tokens are in `tokens.css`. `metrics.css` has no literals, and a forced-colors block is present. Contrast was not re-measured. |
| 4. Typography | 3/4 | Four size tokens and two weight tokens are used throughout. A few details are unproven. |
| 5. Spacing | 3/4 | Space tokens only, plus the three declared layout tokens. One 4px SVG attribute is off the 8px grid. |
| 6. Experience Design | 3/4 | Loading, error, empty, paused, aria-busy and keyboard reading are all implemented. Nothing was observed running. |

**Overall: 18/24**

Scores are capped at 3 across the board. There are no screenshots and no interaction captures, and the contract's own backstop items (live frames from a real worker, 24h/7d refresh) are explicitly UAT-only. I found no 1 or 2 level defect in the code, but I could not verify visual output.

---

## Top 3 Priority Fixes

1. **No rendered evidence for the chart and the 320px layout** - The crosshair, tooltip flip and clamp, the x-tick count rule, German wrapping and the stacked toolbar can only be checked in a browser. The spec defers full Playwright baselines to Phase 69. Capture 1440, 768 and 320 baselines (English and German) with the metrics panel populated, with all tiles unavailable, and with the paused notice, before sign-off.
2. **`nowMs` memo bypasses the hook-dependency lint** - `ServerMetricsPanel.tsx` computes `useMemo(() => performance.now(), [tick])` under an `eslint-disable react-hooks/exhaustive-deps`. This is deliberate and documented, but it is fragile. A tile's stale state depends on a render being triggered by `tick`. Move this into a small `useMonotonicNow(intervalMs)` hook so the exemption lives in one tested place.
3. **Tooltip key SVG is `height={4}`** (`MetricLineChart.tsx`, the `tooltipKeyWidth` svg) - This is a minor deviation from the 4px grid in the spacing contract. The 2px stroke is inside the declared exceptions. Confirm the box does not add vertical jitter in the flex row.

Minor recommendations:
- Confirm that `.server-metrics__toolbar .button` and `> .button` match the class that `actionControlClassName` emits. If it does not emit `button`, the 44px touch height and the full-width toggle at 520px silently fail.
- Note that `chartClockIntervalMs` is 15s. All three charts re-render on that clock in every range, which is acceptable but worth profiling with a 2,016 point series.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)
- `ServerMetricsPanel.tsx`, `MetricTile.tsx` and `MetricTable.tsx` hard-code no English text. All copy resolves through `t("Server_Metrics_*")`, plus the carried keys `Server_Overview_MetricsTitle`, `Server_Live_StaleReason_Disconnected` and `Server_Settings_Retry`, exactly as the contract lists.
- `de.json` holds 48 `Server_Metrics_*` lines, which is consistent with the contract's key table.
- The retry button uses "Try again" and the toggle uses "Show data as tables" / "Show charts". There is no "Submit", "OK" or "Save".
- Not verified: German string lengths in 2-column tiles at 320px, and the plural keys `_one` and `_other`.

### Pillar 2: Visuals (3/4)
- The panel is a `section.workspace-panel[data-slot="metrics"]` with `aria-labelledby`, and the DOM order matches the contract: h2, description, paused notice, status region, tiles, toolbar, views.
- `sections.css` `.workspace-overview__row` is a single column, as required.
- The tiles `dl` holds `div.metric-tile` groups with `dt` and `dd`, and the "Unavailable" value has an `Info16Regular` marked `aria-hidden`.
- The plot is `role="group"` and focusable, the SVG is `role="img"` with `aria-describedby`, and the tooltip is `aria-hidden` with `pointer-events: none`.
- Gap: the contract's own Dimension 2 FLAG recorded no declared visual anchor. The four tiles carry equal weight, so the focal point is unconstrained. Not a code defect, but the page lacks a clear hierarchy cue.

### Pillar 3: Color (3/4)
- `metrics.css` contains zero color literals. `--utf-chart-line` (#3a9fd0), `--utf-chart-band` and `--utf-chart-grid` live in `tokens.css:46-48`.
- Gold is used only through `--utf-focus-ring` on the plot and table regions, and no gold control renders. This matches the contract.
- The "Unavailable" tile text uses `--utf-text-muted`, with no warning or danger color. The paused notice uses `--utf-state-warning-fg`.
- The forced-colors block covers the line, markers, crosshair, gridlines, band, tooltip and focus ring.
- Not verified: whether `tokens-contrast.test.ts` was extended for the chart pair. I did not run the tests.

### Pillar 4: Typography (3/4)
- Only `--utf-text-body`, `--utf-text-label` and `--utf-text-heading` are used, with `--utf-weight-regular` and `--utf-weight-bold` only. Tabular numerals are set on tile values, ticks, the tooltip and table values. Tile terms use `--font-label` uppercase with 0.06em tracking. This matches the contract.
- No `text-overflow: ellipsis` appears. Text wraps with `overflow-wrap: anywhere`.
- Axis tick `fill` and `font-size` come from CSS tokens, but `MetricLineChart.tsx` estimates the tick label width in JS (a per-character constant), which can clip the left gutter for wide German or percent labels. Unverified in a browser.

### Pillar 5: Spacing (3/4)
- All `margin`, `padding` and `gap` values use `--utf-space-*`. The three layout tokens `--utf-metric-chart-min`, `--utf-metric-chart-height` and `--utf-metric-tooltip-width` match the spec.
- Breakpoints 800px and 520px are present: touch height at 800px, stacked toolbar and 2-column tiles at 520px.
- Deviations: the tooltip key svg has `height={4}`. Tile icon `margin-top: var(--utf-space-xs)` is an optical nudge that is on-scale but only verified visually.
- The toolbar uses `justify-content: space-between` and wraps, with no fixed widths.

### Pillar 6: Experience Design (3/4)
- State coverage: panel loading and error `StatePanel`s, a history error with retry, `aria-busy` plus reduced opacity during range changes (`isPlaceholderData`), the empty plot and the table gap rows (`gapRows`), the delayed paused notice (`useDelayedDisconnect`), and a single `role="status"` region for the range announcement only.
- There is no `refetchInterval`, no raw `setInterval`, no chart library, and no `<details>` or `<select>` in the metrics files. Range switching uses `FilterChipGroup`.
- The view toggle never loses focus because it is the same persistent button, and its label swaps with no `aria-pressed`.
- Keyboard chart reading handles Left, Right, Home, End and Escape on the focused plot only.
- No destructive actions exist, so no confirmation is needed.

#### User preference check (React UI dislikes)
- Native `<details>`: none used in the metrics files. Pass.
- Select right padding: no select exists. Not applicable.
- Dialog alignment: no dialog exists. Not applicable.
- Layout shift on staging: no staging concept. The empty plot keeps full height and range changes keep the old views, so no shift. Pass.
- Configuration list filter/search: no configuration list in this phase. Not applicable.

Registry audit: skipped (no `components.json`, no third-party registry).

---

## Files Audited
- sources/ReactApp/src/components/server/ServerMetricsPanel.tsx
- sources/ReactApp/src/components/server/MetricTile.tsx
- sources/ReactApp/src/components/server/MetricLineChart.tsx (structure, ARIA, keyboard handling)
- sources/ReactApp/src/components/server/MetricTable.tsx (structure)
- sources/ReactApp/src/components/server/metrics.css
- sources/ReactApp/src/components/server/sections.css (overview row)
- sources/ReactApp/src/styles/tokens.css (chart tokens)
- sources/ReactApp/src/features/hosting/metricsModel.ts, useServerMetrics.ts (existence and size only)
- .planning/phases/67-server-metrics/67-UI-SPEC.md
