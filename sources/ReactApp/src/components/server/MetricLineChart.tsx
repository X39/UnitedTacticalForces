import {
  useEffect,
  useId,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
  type PointerEvent,
} from "react";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import {
  buildSegments,
  formatBucketTime,
  formatMetricValue,
  formatXTick,
  hasOpenGap,
  highestPoint,
  metricSeries,
  rangeSpanMs,
  rangeTextKeys,
  xTicks,
  yDomain,
  yTicks,
  type ChartMetric,
  type ChartPoint,
} from "@/features/hosting/metricsModel";
import type { GameServerMetricsHistory, ServerMetricsRange } from "@/features/hosting/types";
import { useMeasuredWidth } from "@/features/hosting/useMeasuredWidth";
import "@/components/server/metrics.css";

/** The height of the plot in pixels; it equals `--utf-metric-chart-height` (10rem) at the default font size. */
const plotHeight = 160;
/** The width used before the first measurement and where no `ResizeObserver` exists. */
const fallbackWidth = 480;
/** The width in pixels of `--utf-metric-chart-min` (22rem); below it the x axis shows three ticks. */
const chartMinimumWidth = 352;
/** The inset above the plot in pixels. */
const insetTop = 8;
/** The inset right of the plot in pixels. */
const insetRight = 8;
/** The inset below the plot in pixels, which holds the x tick labels. */
const insetBottom = 24;
/** The space between the widest y label and the plot in pixels. */
const gutterPadding = 8;
/** The estimated width of one character of a 12px tick label in pixels. */
const tickCharacterWidth = 7;
/** The radius of the marker of an isolated point in pixels (an 8px marker). */
const markerRadius = 4;
/** The maximum width of the tooltip in pixels; it equals `--utf-metric-tooltip-width` (14rem) at the default size. */
const tooltipMaxWidth = 224;
/** The distance between the crosshair and the tooltip in pixels. */
const tooltipOffset = 8;
/** The width of the key stroke in the tooltip in pixels. */
const tooltipKeyWidth = 16;

/** Props of {@link MetricLineChart}. */
export interface MetricLineChartProps {
  /** The metric the chart shows. */
  metric: ChartMetric;
  /** The localized chart title. */
  title: string;
  /** The history the chart draws. */
  history: GameServerMetricsHistory;
  /** The range the history covers, which sets the x domain and the tick format. */
  range: ServerMetricsRange;
  /** The right edge of the plot in epoch milliseconds. */
  nowMs: number;
  /** True when the current readings say the game does not report player counts; only used for players. */
  playersNotReported?: boolean;
}

/** The scales and ticks of one plot, derived from the measured width. */
interface PlotGeometry {
  /** The left inset: the widest y label plus the padding, a multiple of 4. */
  left: number;
  /** The width of the plotting area. */
  plotWidth: number;
  /** The height of the plotting area. */
  plotHeightInner: number;
  /** Maps a time to an x coordinate, clamped to the plot. */
  x: (t: number) => number;
  /** Maps a value to a y coordinate, clamped to the plot. */
  y: (value: number) => number;
}

/** Clamps a number into an inclusive interval. */
function clamp(value: number, low: number, high: number): number {
  return Math.min(high, Math.max(low, value));
}

/** Formats a coordinate with one decimal so long series stay small. */
function coordinate(value: number): string {
  return value.toFixed(1);
}

/**
 * Places the tooltip next to the crosshair: after it, flipped before it near the right edge, and always inside the plot.
 *
 * @param x The x coordinate of the crosshair.
 * @param boxWidth The width of the tooltip box.
 * @param width The width of the plot.
 * @returns Whether the box sits before the crosshair and its left offset.
 */
function placeTooltip(
  x: number,
  boxWidth: number,
  width: number,
): { flipped: boolean; left: number } {
  const flipped = x + tooltipOffset + boxWidth > width;
  const wanted = flipped ? x - tooltipOffset - boxWidth : x + tooltipOffset;
  return { flipped, left: clamp(wanted, 0, Math.max(width - boxWidth, 0)) };
}

/**
 * Builds the scales of one plot.
 *
 * @param width The measured width of the plot box.
 * @param labelCharacters The length of the widest y label.
 * @param from The start of the x domain.
 * @param to The end of the x domain.
 * @param domain The y domain.
 */
function geometryOf(
  width: number,
  labelCharacters: number,
  from: number,
  to: number,
  domain: [number, number],
): PlotGeometry {
  const left = Math.ceil((labelCharacters * tickCharacterWidth + gutterPadding) / 4) * 4;
  const plotWidth = Math.max(width - left - insetRight, 1);
  const plotHeightInner = plotHeight - insetTop - insetBottom;
  const span = to - from;
  const [low, high] = domain;
  return {
    left,
    plotWidth,
    plotHeightInner,
    x: (t) => left + clamp((t - from) / span, 0, 1) * plotWidth,
    y: (value) => insetTop + (1 - clamp((value - low) / (high - low), 0, 1)) * plotHeightInner,
  };
}

/**
 * Builds the path of one value of a run: straight segments, or a step that holds each value until the next point.
 *
 * @param segment The run of points.
 * @param geometry The scales.
 * @param value Selects the value to draw.
 * @param stepped True for the step-after interpolation of the players chart.
 */
function linePath(
  segment: readonly ChartPoint[],
  geometry: PlotGeometry,
  value: (point: ChartPoint) => number,
  stepped: boolean,
): string {
  const first = segment[0];
  if (first === undefined) return "";
  let path = `M${coordinate(geometry.x(first.t))} ${coordinate(geometry.y(value(first)))}`;
  for (const point of segment.slice(1)) {
    const x = coordinate(geometry.x(point.t));
    const y = coordinate(geometry.y(value(point)));
    path += stepped ? `H${x}V${y}` : `L${x} ${y}`;
  }
  return path;
}

/**
 * Builds the closed path of the band between the highest and the lowest value of a run, following the line's own
 * interpolation.
 *
 * @param segment The run of points, at least two.
 * @param geometry The scales.
 * @param stepped True for the step-after interpolation of the players chart.
 */
function bandPath(
  segment: readonly ChartPoint[],
  geometry: PlotGeometry,
  stepped: boolean,
): string {
  const last = segment[segment.length - 1];
  if (last === undefined) return "";
  let path = linePath(segment, geometry, (point) => point.max, stepped);
  path += `L${coordinate(geometry.x(last.t))} ${coordinate(geometry.y(last.min))}`;
  for (let index = segment.length - 1; index > 0; index -= 1) {
    const point = segment[index];
    const before = segment[index - 1];
    if (point === undefined || before === undefined) continue;
    const x = coordinate(geometry.x(before.t));
    const y = coordinate(geometry.y(before.min));
    path += stepped ? `V${y}H${x}` : `L${x} ${y}`;
  }
  return `${path}Z`;
}

/**
 * One accessible line chart of a retained metric: a 2px average line with a min-max band behind it, drawn as hand
 * built SVG from the tokens `--utf-chart-line`, `--utf-chart-band` and `--utf-chart-grid`.
 *
 * The line breaks wherever the history has no reading: at a null value and at a gap wider than twice the bucket
 * length, decided only by `buildSegments`. Nothing is interpolated, no zero is drawn, an isolated point is a marker,
 * and a stale right edge stays empty. Points outside the fixed y domain are clamped into the plot. The plot is a
 * focusable group; its SVG is an image named "{metric}, {range}" and described by the summary of the highest value.
 * Without a single reading the plot keeps its height and shows the empty sentences instead of the axes.
 *
 * Values can be read by pointer and by keyboard. The pointer, or Left, Right, Home and End while the plot has focus,
 * places a crosshair on the nearest selectable point and shows an `aria-hidden` tooltip with the bucket time, the
 * average and, when it differs, the lowest and highest value. Only points of the segments are selectable, so a
 * point inside a gap never is. The tooltip is decoration: every value in it is also in the table view, and it is no
 * live region. Arrow keys are handled only while the plot has focus. Touch keeps the tooltip until a tap outside.
 *
 * @param props The chart props.
 */
export function MetricLineChart({
  metric,
  title,
  history,
  range,
  nowMs,
  playersNotReported = false,
}: MetricLineChartProps) {
  const { t } = useTranslation();
  const headingId = useId();
  const summaryId = useId();
  const [plotRef, width] = useMeasuredWidth<HTMLDivElement>(fallbackWidth);
  const locale = currentLocale();
  const bucketMs = history.bucketSeconds * 1000;
  const segments = useMemo(
    () => buildSegments(metricSeries(metric, history), bucketMs),
    [metric, history, bucketMs],
  );
  const points = useMemo(() => segments.flat(), [segments]);
  const peak = highestPoint(segments);
  const [selectedTime, setSelectedTime] = useState<number | null>(null);
  const [touchSelection, setTouchSelection] = useState(false);
  const tooltipRef = useRef<HTMLDivElement>(null);
  const stepped = metric === "players";
  const name = t("Server_Metrics_Chart_Aria", { metric: title, range: t(rangeTextKeys[range]) });

  const chart = useMemo(() => {
    if (peak === null) return null;
    const from = nowMs - rangeSpanMs(range);
    const domain = yDomain(metric, history);
    const yLabels = yTicks(domain).map((value) => ({
      value,
      text: formatMetricValue(metric, value, locale),
    }));
    const geometry = geometryOf(
      width,
      Math.max(...yLabels.map((label) => label.text.length)),
      from,
      nowMs,
      domain,
    );
    const xLabels = xTicks(from, nowMs, width >= chartMinimumWidth ? 4 : 3);
    const runs = segments.map((segment) =>
      segment.length === 1
        ? { marker: segment[0] ?? null, line: "", band: "" }
        : {
            marker: null,
            line: linePath(segment, geometry, (point) => point.avg, stepped),
            band: segment.some((point) => point.max > point.min)
              ? bandPath(segment, geometry, stepped)
              : "",
          },
    );
    return { geometry, yLabels, xLabels, runs, from };
  }, [peak, nowMs, range, metric, history, locale, width, segments, stepped]);

  const selected = useMemo(
    () =>
      selectedTime === null ? null : (points.find((point) => point.t === selectedTime) ?? null),
    [points, selectedTime],
  );

  // The tooltip shrinks to its content, so its real width is only known after layout. The flip and the clamp are
  // refined on the element itself (an external system), which avoids a second render.
  useLayoutEffect(() => {
    const box = tooltipRef.current;
    if (box === null || selected === null || chart === null || box.offsetWidth <= 0) return;
    const placement = placeTooltip(chart.geometry.x(selected.t), box.offsetWidth, width);
    box.style.left = `${placement.left}px`;
    box.dataset.side = placement.flipped ? "before" : "after";
  }, [selected, chart, width]);

  useEffect(() => {
    if (!touchSelection || selectedTime === null) return undefined;
    const hideOutside = (event: globalThis.PointerEvent) => {
      const plot = plotRef.current;
      if (plot !== null && event.target instanceof Node && plot.contains(event.target)) return;
      setSelectedTime(null);
      setTouchSelection(false);
    };
    document.addEventListener("pointerdown", hideOutside);
    return () => document.removeEventListener("pointerdown", hideOutside);
  }, [touchSelection, selectedTime, plotRef]);

  /** Selects the point nearest in x to the pointer, so the pointer never has to land on the line. */
  const selectNearest = (event: PointerEvent<HTMLDivElement>) => {
    if (chart === null) return;
    const pointerX = event.clientX - event.currentTarget.getBoundingClientRect().left;
    let nearest: ChartPoint | null = null;
    let distance = Infinity;
    for (const point of points) {
      const away = Math.abs(chart.geometry.x(point.t) - pointerX);
      if (away < distance) {
        distance = away;
        nearest = point;
      }
    }
    setTouchSelection(event.pointerType === "touch");
    setSelectedTime(nearest?.t ?? null);
  };

  const hideAfterPointer = (event: PointerEvent<HTMLDivElement>) => {
    if (event.pointerType === "touch") return;
    setSelectedTime(null);
  };

  /** Left, Right, Home, End and Escape; handled only while the plot itself has focus. */
  const readByKeyboard = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.target !== event.currentTarget) return;
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    if (event.key === "Escape") {
      setSelectedTime(null);
      return;
    }
    if (points.length === 0) return;
    const index = selected === null ? -1 : points.indexOf(selected);
    let next: number;
    switch (event.key) {
      case "ArrowRight":
        next = index < 0 ? 0 : Math.min(index + 1, points.length - 1);
        break;
      case "ArrowLeft":
        next = index < 0 ? points.length - 1 : Math.max(index - 1, 0);
        break;
      case "Home":
        next = 0;
        break;
      case "End":
        next = points.length - 1;
        break;
      default:
        return;
    }
    event.preventDefault();
    setTouchSelection(false);
    setSelectedTime(points[next]?.t ?? null);
  };

  /** The tooltip side and left offset: after the crosshair, flipped before it near the right edge, inside the plot. */
  const tooltipPlacement = (() => {
    if (selected === null || chart === null) return null;
    const x = chart.geometry.x(selected.t);
    return placeTooltip(x, Math.min(tooltipMaxWidth, width), width);
  })();

  return (
    <section className="metric-chart" aria-labelledby={headingId}>
      <h3 id={headingId} className="metric-chart__title">
        {title}
      </h3>
      {peak !== null && (
        <p id={summaryId} className="metric-chart__summary">
          {t("Server_Metrics_Chart_Highest", {
            value: formatMetricValue(metric, peak.max, locale),
            time: formatBucketTime(peak.t, range),
          })}
        </p>
      )}
      <div
        ref={plotRef}
        className="metric-chart__plot"
        role="group"
        tabIndex={0}
        aria-label={t("Server_Metrics_Chart_Group", { metric: title })}
        onKeyDown={readByKeyboard}
        onBlur={() => setSelectedTime(null)}
        onPointerDown={selectNearest}
        onPointerMove={selectNearest}
        onPointerLeave={hideAfterPointer}
        onPointerCancel={hideAfterPointer}
      >
        {chart === null ? (
          <div className="metric-chart__empty">
            {metric === "players" && playersNotReported ? (
              <p>{t("Server_Metrics_Reason_NoPlayers")}</p>
            ) : (
              <>
                <p>{t("Server_Metrics_Chart_Empty")}</p>
                <p className="metric-chart__empty-hint">{t("Server_Metrics_Chart_EmptyHint")}</p>
              </>
            )}
          </div>
        ) : (
          <svg
            className="metric-chart__svg"
            role="img"
            aria-label={name}
            aria-describedby={summaryId}
            viewBox={`0 0 ${width} ${plotHeight}`}
            width="100%"
            height="100%"
          >
            {chart.yLabels.map((label) => (
              <g key={label.value}>
                <line
                  className="metric-chart__gridline"
                  x1={chart.geometry.left}
                  x2={chart.geometry.left + chart.geometry.plotWidth}
                  y1={chart.geometry.y(label.value)}
                  y2={chart.geometry.y(label.value)}
                  strokeWidth={1}
                />
                <text
                  className="metric-chart__tick"
                  x={chart.geometry.left - gutterPadding}
                  y={chart.geometry.y(label.value)}
                  textAnchor="end"
                  dominantBaseline="middle"
                >
                  {label.text}
                </text>
              </g>
            ))}
            {chart.xLabels.map((tick, index) => (
              <text
                key={tick}
                className="metric-chart__tick"
                x={chart.geometry.x(tick)}
                y={plotHeight - insetBottom / 2}
                textAnchor={
                  index === 0 ? "start" : index === chart.xLabels.length - 1 ? "end" : "middle"
                }
                dominantBaseline="middle"
              >
                {formatXTick(tick, range)}
              </text>
            ))}
            {chart.runs.map((run, index) => (
              <g key={index}>
                {run.band !== "" && <path className="metric-chart__band" d={run.band} />}
                {run.line !== "" && (
                  <path
                    className="metric-chart__line"
                    d={run.line}
                    fill="none"
                    strokeWidth={2}
                    strokeLinejoin="round"
                    strokeLinecap="round"
                  />
                )}
                {run.marker !== null && (
                  <circle
                    className="metric-chart__marker"
                    cx={chart.geometry.x(run.marker.t)}
                    cy={chart.geometry.y(run.marker.avg)}
                    r={markerRadius}
                    strokeWidth={2}
                  />
                )}
              </g>
            ))}
            {selected !== null && (
              <g aria-hidden="true">
                <line
                  className="metric-chart__crosshair"
                  x1={chart.geometry.x(selected.t)}
                  x2={chart.geometry.x(selected.t)}
                  y1={insetTop}
                  y2={insetTop + chart.geometry.plotHeightInner}
                  strokeWidth={1}
                />
                <circle
                  className="metric-chart__cursor"
                  cx={chart.geometry.x(selected.t)}
                  cy={chart.geometry.y(selected.avg)}
                  r={markerRadius}
                  strokeWidth={2}
                />
              </g>
            )}
          </svg>
        )}
        {selected !== null && tooltipPlacement !== null && (
          <div
            ref={tooltipRef}
            className="metric-chart__tooltip"
            aria-hidden="true"
            data-side={tooltipPlacement.flipped ? "before" : "after"}
            style={{ left: tooltipPlacement.left }}
          >
            <span className="metric-chart__tooltip-time">
              {formatBucketTime(selected.t, range)}
            </span>
            <span className="metric-chart__tooltip-average">
              <svg width={tooltipKeyWidth} height={4} aria-hidden="true" focusable="false">
                <line
                  className="metric-chart__tooltip-key"
                  x1={0}
                  x2={tooltipKeyWidth}
                  y1={2}
                  y2={2}
                  strokeWidth={2}
                />
              </svg>
              {t("Server_Metrics_Tooltip_Average", {
                value: formatMetricValue(metric, selected.avg, locale),
              })}
            </span>
            {selected.max > selected.min && (
              <span className="metric-chart__tooltip-range">
                {t("Server_Metrics_Tooltip_Range", {
                  low: formatMetricValue(metric, selected.min, locale),
                  high: formatMetricValue(metric, selected.max, locale),
                })}
              </span>
            )}
          </div>
        )}
      </div>
      {hasOpenGap(segments, bucketMs, nowMs) && (
        <p className="metric-chart__note">{t("Server_Metrics_Chart_GapNote")}</p>
      )}
    </section>
  );
}
