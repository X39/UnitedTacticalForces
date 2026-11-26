import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import {
  formatBucketTime,
  formatMetricValue,
  gapRows,
  metricSeries,
  rangeTextKeys,
  type ChartMetric,
} from "@/features/hosting/metricsModel";
import type { GameServerMetricsHistory, ServerMetricsRange } from "@/features/hosting/types";
import "@/components/server/metrics.css";

/** The number of columns of the table: time, average, lowest and highest. */
const columnCount = 4;

/** Props of {@link MetricTable}. */
export interface MetricTableProps {
  /** The metric the table shows. */
  metric: ChartMetric;
  /** The localized metric name, used in the caption. */
  title: string;
  /** The history the table lists. */
  history: GameServerMetricsHistory;
  /** The range the history covers, which sets the time format. */
  range: ServerMetricsRange;
  /** The current time in epoch milliseconds, which closes a trailing stretch without readings. */
  nowMs: number;
  /** True when the current readings say the game does not report player counts; only used for players. */
  playersNotReported?: boolean;
}

/**
 * The full-fidelity table view of one metric: the same history the chart draws, as a real table.
 *
 * The caption equals the chart's accessible name. Rows are newest first with the time, the average, the lowest and
 * the highest value; each stretch without readings is one row that spans all columns and says what is missing, so a
 * gap is never a zero cell. Without any reading one row carries the two empty sentences. The table sits in a
 * focusable, labelled scroll region with a capped height and a sticky header, so a keyboard user can scroll it and
 * it never widens the page.
 *
 * @param props The table props.
 */
export function MetricTable({
  metric,
  title,
  history,
  range,
  nowMs,
  playersNotReported = false,
}: MetricTableProps) {
  const { t } = useTranslation();
  const locale = currentLocale();
  const bucketMs = history.bucketSeconds * 1000;
  const rows = useMemo(
    () => gapRows(metricSeries(metric, history), bucketMs, nowMs),
    [metric, history, bucketMs, nowMs],
  );
  const caption = t("Server_Metrics_Chart_Aria", { metric: title, range: t(rangeTextKeys[range]) });
  return (
    <div className="metric-table" role="region" tabIndex={0} aria-label={caption}>
      <table className="metric-table__table">
        <caption className="metric-table__caption">{caption}</caption>
        <thead>
          <tr>
            <th scope="col">{t("Server_Metrics_Table_Time")}</th>
            <th scope="col">{t("Server_Metrics_Table_Average")}</th>
            <th scope="col">{t("Server_Metrics_Table_Lowest")}</th>
            <th scope="col">{t("Server_Metrics_Table_Highest")}</th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 ? (
            <tr>
              <td className="metric-table__note" colSpan={columnCount}>
                {metric === "players" && playersNotReported ? (
                  <p>{t("Server_Metrics_Reason_NoPlayers")}</p>
                ) : (
                  <>
                    <p>{t("Server_Metrics_Chart_Empty")}</p>
                    <p>{t("Server_Metrics_Chart_EmptyHint")}</p>
                  </>
                )}
              </td>
            </tr>
          ) : (
            rows.map((row) =>
              row.kind === "gap" ? (
                <tr key={`gap-${row.from}`}>
                  <td className="metric-table__note" colSpan={columnCount}>
                    {t("Server_Metrics_Table_Gap", {
                      from: formatBucketTime(row.from, range),
                      to: formatBucketTime(row.to, range),
                    })}
                  </td>
                </tr>
              ) : (
                <tr key={row.point.t}>
                  <td className="metric-table__value">
                    <time dateTime={new Date(row.point.t).toISOString()}>
                      {formatBucketTime(row.point.t, range)}
                    </time>
                  </td>
                  <td className="metric-table__value">
                    {formatMetricValue(metric, row.point.avg, locale)}
                  </td>
                  <td className="metric-table__value">
                    {formatMetricValue(metric, row.point.min, locale)}
                  </td>
                  <td className="metric-table__value">
                    {formatMetricValue(metric, row.point.max, locale)}
                  </td>
                </tr>
              ),
            )
          )}
        </tbody>
      </table>
    </div>
  );
}
