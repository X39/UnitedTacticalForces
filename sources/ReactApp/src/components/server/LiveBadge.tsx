import {
  ArrowSync16Regular,
  Live16Regular,
  PlugDisconnected16Regular,
  Warning16Regular,
} from "@fluentui/react-icons";
import type { ComponentType } from "react";
import { useTranslation } from "react-i18next";
import { formatRelativeTime } from "@/app/format";
import { useNow } from "@/app/useNow";
import { ActionButton } from "@/components/ActionControl";
import "@/components/server/activity.css";
import { useDelayedDisconnect } from "@/components/server/useDelayedDisconnect";
import { useFleetLiveUpdates } from "@/features/hosting/useFleetLiveUpdates";
import { deriveFreshness, type WorkspaceFreshness } from "@/features/hosting/workspaceFreshness";

/** How often the relative time and the data age are refreshed while the badge is mounted. */
const clockIntervalMs = 30_000;

/** An invisible separator that stands in for the relative time while the translated sentence is split around it. */
const relativeMarker = String.fromCodePoint(0x2063);

/** The props of {@link LiveBadge}. */
export interface LiveBadgeProps {
  /** The time of the newest successful fetch of the jobs and operations in epoch milliseconds; 0 when none. */
  dataUpdatedAt: number;
  /** Whether a lifecycle job is active; while one is, the age of the data decides, not the socket alone. */
  hasActiveJob: boolean;
  /** Called when the viewer asks to load the jobs and operations again from the old-data state. */
  onRefresh(): void;
}

/** The visual of one freshness state: its modifier class, icon and state word key. */
function visualFor(freshness: WorkspaceFreshness): {
  tone: string;
  Icon: ComponentType;
  wordKey: "Server_Live_Connecting" | "Server_Live_Live" | "Server_Live_Stale";
} {
  if (freshness.state === "connecting") {
    return { tone: "connecting", Icon: ArrowSync16Regular, wordKey: "Server_Live_Connecting" };
  }
  if (freshness.state === "live") {
    return { tone: "live", Icon: Live16Regular, wordKey: "Server_Live_Live" };
  }
  return {
    tone: "stale",
    Icon: freshness.reason === "disconnected" ? PlugDisconnected16Regular : Warning16Regular,
    wordKey: "Server_Live_Stale",
  };
}

/**
 * Shows how current the activity data is: Connecting, Live or Not up to date, with the time of the last update.
 *
 * The state comes from {@link deriveFreshness} over the shared fleet socket and the data age. The stale state for a
 * disconnected socket appears only after the connection has been down for 3 seconds in a row, through
 * `useDelayedDisconnect` (keyed by the disconnect epoch, so a new episode waits again and a recovered connection
 * never shows it); until then the badge renders as for an open socket. Only the state word sits in a polite status region, so a ticking relative
 * time is never announced. A stale state names its reason and offers the matching recovery: Reconnect now for a
 * paused socket, Refresh activity for old data.
 *
 * @param props See {@link LiveBadgeProps}.
 */
export function LiveBadge({ dataUpdatedAt, hasActiveJob, onRefresh }: LiveBadgeProps) {
  const { t } = useTranslation();
  const { status, disconnectEpoch, reconnectNow } = useFleetLiveUpdates();
  const now = useNow(clockIntervalMs);
  const disconnectShown = useDelayedDisconnect();

  const reported = deriveFreshness({ status, disconnectEpoch, dataUpdatedAt, now, hasActiveJob });
  const disconnected = reported.state === "stale" && reported.reason === "disconnected";

  const freshness =
    disconnected && !disconnectShown
      ? deriveFreshness({ status: "open", disconnectEpoch, dataUpdatedAt, now, hasActiveJob })
      : reported;
  const { tone, Icon, wordKey } = visualFor(freshness);

  const relative = dataUpdatedAt > 0 ? formatRelativeTime(dataUpdatedAt, now) : "";
  const [before = "", after = ""] = t("Server_Live_Updated", { relative: relativeMarker }).split(
    relativeMarker,
  );

  return (
    <div className="live-badge">
      <div className="live-badge__row">
        <span role="status" className={`live-badge__pill live-badge__pill--${tone}`}>
          <Icon aria-hidden="true" />
          {t(wordKey)}
        </span>
        {relative !== "" && (
          <span className="live-badge__updated">
            {before}
            <time dateTime={new Date(dataUpdatedAt).toISOString()}>{relative}</time>
            {after}
          </span>
        )}
      </div>
      {freshness.state === "stale" && (
        <div className="live-badge__recovery">
          <p className="live-badge__reason">
            {freshness.reason === "disconnected"
              ? t("Server_Live_StaleReason_Disconnected")
              : t("Server_Live_StaleReason_Old")}
          </p>
          {freshness.reason === "disconnected" ? (
            <ActionButton variant="ghost" size="compact" onClick={reconnectNow}>
              {t("Server_Fleet_Live_Reconnect")}
            </ActionButton>
          ) : (
            <ActionButton variant="ghost" size="compact" onClick={onRefresh}>
              {t("Server_Live_RefreshActivity")}
            </ActionButton>
          )}
        </div>
      )}
    </div>
  );
}
