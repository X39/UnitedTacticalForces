import { Warning16Regular } from "@fluentui/react-icons";
import { useId, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import { useNow } from "@/app/useNow";
import { ActionButton } from "@/components/ActionControl";
import { FilterChipGroup } from "@/components/FilterChipGroup";
import { MetricLineChart } from "@/components/server/MetricLineChart";
import { MetricTable } from "@/components/server/MetricTable";
import { MetricTile } from "@/components/server/MetricTile";
import { useDelayedDisconnect } from "@/components/server/useDelayedDisconnect";
import { StatePanel } from "@/components/primitives";
import {
  chartMetrics,
  chartTitleKeys,
  metricRanges,
  metricTiles,
  parseRange,
  rangeBucketMs,
  rangeLabelKeys,
  rangeTextKeys,
} from "@/features/hosting/metricsModel";
import type { GameServer, ServerMetricsRange } from "@/features/hosting/types";
import { useServerMetrics, useServerMetricsHistory } from "@/features/hosting/useServerMetrics";
import "@/components/server/metrics.css";

/** How often the held reading is checked against its stale threshold, in milliseconds. */
const reageIntervalMs = 5000;

/**
 * How often the right edge of the charts moves, in milliseconds: one bucket of the finest range. A coarser clock
 * would leave a stale edge, and a false gap note, after switching from a long range back to one hour.
 */
const chartClockIntervalMs = rangeBucketMs("1h");

/** The two views of the history. */
type MetricsView = "charts" | "tables";

/**
 * The metrics panel of the Overview: the current readings of one server as four tiles in the order Running since,
 * Players, CPU, Memory, and below them the retained history as three charts or three tables.
 *
 * Each tile shows the value exactly as the control plane sent it, or "Unavailable" with a plain reason. While the
 * first load runs one loading panel replaces the tiles; when the first load fails one error panel with "Try again"
 * does. A failed background refetch with data held changes nothing. The panel owns a status region that is always
 * mounted so later announcements have a stable target; its only announcement is the range change. The tiles, the
 * charts and the paused notice are not live regions.
 *
 * The toolbar scopes the views below it: a range chip group (1 hour, 24 hours, 7 days; one hour by default, held in
 * React state only) and a toggle between charts and tables that keeps focus on itself. While a new range loads the
 * previous range's views stay at reduced opacity with `aria-busy`, so nothing jumps; when that load fails the views
 * region shows the history error with "Try again" for the same range while the tiles and the toolbar remain.
 *
 * A held reading is re-aged by the shared `useNow` clock every 5 seconds against the control plane's
 * `staleAfterSeconds`, so a tile turns Unavailable while the page stays open without any request. After 3 seconds of
 * socket disconnect the paused notice appears and the tiles keep their last values.
 *
 * @param props.server The server whose readings are shown.
 */
export function ServerMetricsPanel({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const headingId = useId();
  const viewsId = useId();
  const [range, setRange] = useState<ServerMetricsRange>("1h");
  const [view, setView] = useState<MetricsView>("charts");
  const [announcement, setAnnouncement] = useState("");
  const metrics = useServerMetrics(server.primaryKey);
  const history = useServerMetricsHistory(server.primaryKey, range);
  const shownRange = history.data === undefined ? range : parseRange(history.data.range, range);
  const chartClock = useNow(chartClockIntervalMs);
  const paused = useDelayedDisconnect();
  const tick = useNow(reageIntervalMs);
  // The tick only schedules the re-check; the age itself is measured on the monotonic performance.now() timeline.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const nowMs = useMemo(() => performance.now(), [tick]);

  function chooseRange(next: ServerMetricsRange): void {
    if (next === range) return;
    setRange(next);
    setAnnouncement(t("Server_Metrics_Range_Announce", { range: t(rangeTextKeys[next]) }));
  }

  return (
    <section className="workspace-panel" data-slot="metrics" aria-labelledby={headingId}>
      <h2 id={headingId}>{t("Server_Overview_MetricsTitle")}</h2>
      <p className="workspace-panel__muted">{t("Server_Metrics_Description")}</p>
      {paused && (
        <p className="server-metrics__paused">
          <Warning16Regular aria-hidden="true" />
          {t("Server_Live_StaleReason_Disconnected")}
        </p>
      )}
      <p className="sr-only" role="status">
        {announcement}
      </p>
      {metrics.data !== undefined ? (
        <>
          <dl className="server-metrics__tiles">
            {metricTiles(metrics.data, nowMs, currentLocale()).map((tile) => (
              <MetricTile key={tile.id} tile={tile} />
            ))}
          </dl>
          <div className="server-metrics__toolbar">
            <FilterChipGroup
              label={t("Server_Metrics_Range_Label")}
              items={metricRanges.map((id) => ({ id, label: t(rangeLabelKeys[id]) }))}
              value={range}
              onChange={chooseRange}
            />
            <ActionButton
              variant="ghost"
              aria-controls={viewsId}
              onClick={() => setView(view === "charts" ? "tables" : "charts")}
            >
              {view === "charts" ? t("Server_Metrics_View_Table") : t("Server_Metrics_View_Charts")}
            </ActionButton>
          </div>
          <div
            id={viewsId}
            className={[
              "server-metrics__views",
              view === "tables" ? "server-metrics__views--tables" : "",
              history.isPlaceholderData ? "server-metrics__views--loading" : "",
            ]
              .filter((name) => name !== "")
              .join(" ")}
            aria-busy={history.isPlaceholderData}
          >
            {history.data !== undefined ? (
              chartMetrics.map((metric) => {
                const common = {
                  metric,
                  title: t(chartTitleKeys[metric]),
                  history: history.data,
                  range: shownRange,
                  nowMs: chartClock,
                  playersNotReported:
                    metrics.data.playersUnavailableReason === "game-does-not-report",
                };
                return view === "charts" ? (
                  <MetricLineChart key={metric} {...common} />
                ) : (
                  <MetricTable key={metric} {...common} />
                );
              })
            ) : history.isError ? (
              <StatePanel
                kind="error"
                action={
                  <ActionButton variant="ghost" onClick={() => void history.refetch()}>
                    {t("Server_Settings_Retry")}
                  </ActionButton>
                }
              >
                {t("Server_Metrics_HistoryLoadError")}
              </StatePanel>
            ) : (
              <StatePanel kind="loading">{t("Server_Metrics_Loading")}</StatePanel>
            )}
          </div>
        </>
      ) : metrics.isError ? (
        <StatePanel
          kind="error"
          action={
            <ActionButton variant="ghost" onClick={() => void metrics.refetch()}>
              {t("Server_Settings_Retry")}
            </ActionButton>
          }
        >
          {t("Server_Metrics_LoadError")}
        </StatePanel>
      ) : (
        <StatePanel kind="loading">{t("Server_Metrics_Loading")}</StatePanel>
      )}
    </section>
  );
}
