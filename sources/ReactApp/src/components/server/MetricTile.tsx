import { Info16Regular } from "@fluentui/react-icons";
import { useTranslation } from "react-i18next";
import { formatInstant } from "@/features/hosting/costAlertFormat";
import type { MetricTilePresentation } from "@/features/hosting/metricsModel";

/**
 * One metric tile: a term and either the value with an optional detail line, or the word "Unavailable" with the
 * reason sentence.
 *
 * An unavailable tile never shows a number. A point in time renders as a `time` element, and the last-reading line
 * appears only when the presentation carries one.
 *
 * @param props.tile The presentation of the tile.
 */
export function MetricTile({ tile }: { tile: MetricTilePresentation }) {
  const { t } = useTranslation();
  const { value } = tile;
  return (
    <div className="metric-tile" data-tile={tile.id}>
      <dt className="metric-tile__term">{t(tile.termKey)}</dt>
      <dd>
        {value.kind === "value" ? (
          <>
            <p className="metric-tile__value">
              {value.dateTime !== undefined ? (
                <time dateTime={value.dateTime}>{value.text}</time>
              ) : (
                value.text
              )}
            </p>
            {value.detail !== undefined && (
              <p className="metric-tile__detail">{t(value.detail.key, value.detail.options)}</p>
            )}
          </>
        ) : (
          <>
            <p className="metric-tile__value metric-tile__value--unavailable">
              <Info16Regular aria-hidden="true" />
              {t("Server_Metrics_Unavailable")}
            </p>
            <p className="metric-tile__reason">{t(value.reasonKey)}</p>
            {value.lastReading !== undefined && (
              <p className="metric-tile__detail">
                <time dateTime={value.lastReading}>
                  {t("Server_Metrics_LastReading", { time: formatInstant(value.lastReading) })}
                </time>
              </p>
            )}
          </>
        )}
      </dd>
    </div>
  );
}
