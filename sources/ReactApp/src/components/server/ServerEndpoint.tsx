import { useTranslation } from "react-i18next";
import { CopyButton } from "@/components/CopyButton";
import "@/components/server/workspace-shell.css";
import type { GameServer } from "@/features/hosting/types";

/**
 * Shows the address players connect to, with a button that copies it.
 *
 * The address appears only when the API reports a connection endpoint for the deployment. Without one the
 * component says the address is available while the server is running. Visibility depends only on the
 * presence of the field, never on the deployment status.
 *
 * @param props.server The server whose connection endpoint is shown.
 */
export function ServerEndpoint({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const endpoint = server.deployment?.connectionEndpoint ?? null;
  return (
    <div className="server-endpoint">
      <span className="server-endpoint__label">{t("Server_Workspace_Endpoint_Label")}</span>
      {endpoint ? (
        <span className="server-endpoint__value">
          <code className="server-endpoint__address">{endpoint}</code>
          <CopyButton
            value={endpoint}
            label={t("Server_Workspace_Endpoint_Copy")}
            copiedMessage={t("Server_Workspace_Endpoint_Copied")}
          />
        </span>
      ) : (
        <span className="server-endpoint__unavailable">
          {t("Server_Workspace_Endpoint_Unavailable")}
        </span>
      )}
    </div>
  );
}
