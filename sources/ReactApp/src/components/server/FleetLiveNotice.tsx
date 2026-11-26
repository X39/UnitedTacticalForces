import { PlugDisconnected20Regular } from "@fluentui/react-icons";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { UpdateStreamStatus } from "@/app/updateStream";
import { ActionButton } from "@/components/ActionControl";
import { StatePanel } from "@/components/primitives";
import "@/components/server/fleet-toolbar.css";

/** Continuous disconnection in milliseconds after which the notice appears, so a short blip never flickers. */
const defaultDelayMs = 3000;

/** Props of {@link FleetLiveNotice}. */
export interface FleetLiveNoticeProps {
  /** Connection state of the live fleet subscription. */
  status: UpdateStreamStatus;
  /** Disconnect epoch of the subscription; it grows once whenever a new disconnection episode begins. */
  epoch: number;
  /** Starts an immediate reconnect attempt. */
  onReconnect(): void;
  /** Continuous disconnection in milliseconds before the notice appears. Defaults to 3 seconds. */
  delayMs?: number;
}

/**
 * Tells the viewer that live fleet updates are paused or unavailable.
 *
 * The notice appears only after the connection has been lost continuously for the delay. Its visibility is keyed by
 * the disconnect epoch: a timer started for one episode records that epoch, so a new episode (a larger epoch) waits
 * for the full delay again and a recovered connection hides the notice without a toast. It uses the info tone, is a
 * status region and never blocks other controls.
 */
export function FleetLiveNotice({
  status,
  epoch,
  onReconnect,
  delayMs = defaultDelayMs,
}: FleetLiveNoticeProps) {
  const { t } = useTranslation();
  const [shownEpoch, setShownEpoch] = useState<number | null>(null);
  const disconnected = status === "reconnecting" || status === "unavailable";

  useEffect(() => {
    if (!disconnected) return undefined;
    const timer = window.setTimeout(() => setShownEpoch(epoch), delayMs);
    return () => window.clearTimeout(timer);
  }, [disconnected, epoch, delayMs]);

  if (!disconnected || shownEpoch !== epoch) return null;

  const unavailable = status === "unavailable";
  return (
    <div className="fleet-live-notice">
      <StatePanel
        kind="info"
        icon={<PlugDisconnected20Regular />}
        title={
          unavailable
            ? t("Server_Fleet_Live_Unavailable_Title")
            : t("Server_Fleet_Live_Reconnecting_Title")
        }
        action={
          <ActionButton variant="ghost" onClick={onReconnect}>
            {t("Server_Fleet_Live_Reconnect")}
          </ActionButton>
        }
      >
        <p>
          {unavailable
            ? t("Server_Fleet_Live_Unavailable_Body")
            : t("Server_Fleet_Live_Reconnecting_Body")}
        </p>
      </StatePanel>
    </div>
  );
}
