import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import { StatePanel } from "@/components/primitives";
import "@/components/server/change.css";
import { isDeploymentActive } from "@/features/hosting/serverState";
import type { GameServer, ServerChangeArea } from "@/features/hosting/types";

/** Copy keys of the areas the banner can name; unknown API values are skipped (T-65-25). */
const areaLabelKeys: ReadonlyMap<
  ServerChangeArea,
  | "Server_Change_Area_ModPack"
  | "Server_Change_Area_Configuration"
  | "Server_Change_Area_Credentials"
> = new Map([
  ["ModPack", "Server_Change_Area_ModPack"],
  ["Configuration", "Server_Change_Area_Configuration"],
  ["Credentials", "Server_Change_Area_Credentials"],
] as const);

/** Props of {@link PendingStartNotice}. */
export interface PendingStartNoticeProps {
  /** The server whose API projection carries the pending-start signal. */
  server: GameServer;
}

/**
 * Tells every member that saved changes wait for the next start (CHG-04, D-09).
 *
 * The banner follows the API signal only: it renders when `hasChangesPendingStart` is true and disappears when the
 * API clears it, without a dismiss control. The wording depends on whether the deployment may still be active, and
 * a muted line names the waiting areas when the API sends any. The server title and the areas render as React text
 * only.
 *
 * @returns The status panel, or null when nothing waits for a start.
 */
export function PendingStartNotice({ server }: PendingStartNoticeProps) {
  const { t } = useTranslation();
  if (server.hasChangesPendingStart !== true) return null;
  const labels = (server.changesPendingStartAreas ?? []).flatMap((area) => {
    const key = areaLabelKeys.get(area);
    return key === undefined ? [] : [t(key)];
  });
  const areas = new Intl.ListFormat(currentLocale(), { type: "conjunction" }).format(labels);
  return (
    <StatePanel kind="info">
      {isDeploymentActive(server)
        ? t("Server_Change_Pending_Running", { name: server.title })
        : t("Server_Change_Pending_Off", { name: server.title })}
      {labels.length > 0 ? (
        <div className="pending-start-notice__areas">
          {t("Server_Change_Pending_Areas", { areas })}
        </div>
      ) : null}
    </StatePanel>
  );
}
