import type { TFunction } from "i18next";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import { Disclosure } from "@/components/Disclosure";
import { StatePanel, StatusBadge } from "@/components/primitives";
import { EmptyState } from "@/components/StateViews";
import { ServerStateSummary } from "@/components/server/ServerStateSummary";
import { formatBytes, formatDate, formatMoney, humanizeToken } from "@/features/hosting/model";
import { deriveServerState } from "@/features/hosting/serverState";
import type { GameServer } from "@/features/hosting/types";

/**
 * Shows the facts of the cloud machine of a server: the state summary, technical status, worker status and
 * heartbeat, endpoint, deployment root, storage, compute and storage estimates, the measured disk telemetry, the
 * fault summary, the administrative diagnostics and the provider resources.
 *
 * Hourly amounts are written through the localized per-hour key, never with a hard-coded unit. The possible
 * cost warning is not repeated here: the Machine section shows it once, above this panel, with the shared
 * cost-stop. A server without a deployment shows only the heading and the no-deployment text.
 *
 * @param props.server The server whose deployment is shown.
 */
export function CloudMachinePanel({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const [diagnosticsOpen, setDiagnosticsOpen] = useState(false);
  const deployment = server.deployment;
  if (!deployment)
    return (
      <section className="next-hosting-panel">
        <span className="kicker">{t("Hosting_Detail_DeploymentKicker")}</span>
        <h2>{t("Hosting_Detail_DeploymentTitle")}</h2>
        <EmptyState title={t("Hosting_Detail_NoDeploymentTitle")}>
          {t("Hosting_Detail_NoDeploymentBody")}
        </EmptyState>
      </section>
    );
  const derived = deriveServerState(server);
  const used =
    deployment.diskTotalBytes !== null && deployment.diskAvailableBytes !== null
      ? deployment.diskTotalBytes - deployment.diskAvailableBytes
      : undefined;
  return (
    <section className="next-hosting-panel">
      <header className="next-hosting-section-heading">
        <div>
          <span className="kicker">{t("Hosting_Detail_DeploymentKicker")}</span>
          <h2>{t("Hosting_Detail_DeploymentTitle")}</h2>
        </div>
        <ServerStateSummary derived={derived} />
      </header>
      <dl className="next-hosting-facts">
        {derived.detailKey && (
          <div>
            <dt>{t("Server_State_TechnicalStatus")}</dt>
            <dd>{t(derived.detailKey)}</dd>
          </div>
        )}
        <div>
          <dt>{t("Hosting_Common_Stage")}</dt>
          <dd>{humanizeToken(deployment.stage)}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Detail_Worker")}</dt>
          <dd>{humanizeToken(deployment.workerStatus)}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Detail_LastHeartbeat")}</dt>
          <dd>{formatDate(deployment.lastHeartbeat)}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Detail_ConnectionEndpoint")}</dt>
          <dd>
            <code>{deployment.connectionEndpoint ?? deployment.publicAddress ?? "—"}</code>
            {deployment.connectionProtocol && ` (${deployment.connectionProtocol})`}
          </dd>
        </div>
        <div>
          <dt>{t("React_ServerWorkspace_DeploymentFilesystem")}</dt>
          <dd>
            <code>{deployment.deploymentRoot}</code>
          </dd>
        </div>
        <div>
          <dt>{t("React_ServerWorkspace_ProvisionedStorage")}</dt>
          <dd>
            {formatBytes(
              deployment.selectedExternalStorageBytes ?? deployment.includedStorageBytes,
            )}
          </dd>
        </div>
        <div>
          <dt>{t("React_ServerWorkspace_CloudComputeEstimate")}</dt>
          <dd>
            {t("Server_Machine_ResourceHourly", {
              amount: formatMoney(deployment.estimatedComputeHourlyCostEur),
            })}
          </dd>
        </div>
        <div>
          <dt>{t("React_ServerWorkspace_CloudStorageEstimate")}</dt>
          <dd>
            {t("Server_Machine_ResourceHourly", {
              amount: formatMoney(deployment.estimatedStorageHourlyCostEur),
            })}
          </dd>
        </div>
      </dl>
      {used !== undefined && deployment.diskTotalBytes !== null && (
        <section className="next-hosting-storage-telemetry">
          <h3>{t("React_ServerWorkspace_WorkerDiskTelemetry")}</h3>
          <progress max={deployment.diskTotalBytes} value={used} />
          <dl className="next-hosting-facts">
            <div>
              <dt>{t("Hosting_Detail_DiskUsed")}</dt>
              <dd>{formatBytes(used)}</dd>
            </div>
            <div>
              <dt>{t("Hosting_Detail_DiskFree")}</dt>
              <dd>{formatBytes(deployment.diskAvailableBytes)}</dd>
            </div>
            <div>
              <dt>{t("Hosting_Detail_DiskTotal")}</dt>
              <dd>{formatBytes(deployment.diskTotalBytes)}</dd>
            </div>
            <div>
              <dt>{t("Hosting_Detail_DiskObserved")}</dt>
              <dd>{formatDate(deployment.diskTimeStampObserved)}</dd>
            </div>
          </dl>
        </section>
      )}
      {deployment.faultSummary && <StatePanel kind="error">{deployment.faultSummary}</StatePanel>}
      {deployment.administrativeSshCommand && (
        <Disclosure
          className="next-hosting-diagnostics"
          title={t("React_ServerWorkspace_WorkerDiagnostics")}
          open={diagnosticsOpen}
          onOpenChange={setDiagnosticsOpen}
        >
          <p>
            <code>{deployment.administrativeSshCommand}</code>
          </p>
          <ul>
            <li>
              <code>sudo cloud-init status --long</code>
            </li>
            <li>
              <code>sudo journalctl -u cloud-final --no-pager -n 200</code>
            </li>
            <li>
              <code>sudo systemctl --failed; sudo systemctl list-units 'utf-*'</code>
            </li>
            <li>
              <code>
                sudo docker ps -a; sudo docker logs --tail 200 $(sudo docker ps -aq --filter
                name=utf-cloud-worker)
              </code>
            </li>
          </ul>
        </Disclosure>
      )}
      <h3>{formatResource(t("Hosting_Detail_ProviderResources"), deployment.resources.length)}</h3>
      {deployment.resources.length === 0 ? (
        <p>{t("React_ServerWorkspace_NoProviderResources")}</p>
      ) : (
        <div className="next-hosting-resource-grid">
          {deployment.resources.map((resource) => (
            <article key={resource.primaryKey}>
              <strong>{resourceKindLabel(resource.kind, t)}</strong>
              <StatusBadge tone={resource.possibleCost ? "warning" : "neutral"}>
                {humanizeToken(resource.state)}
              </StatusBadge>
              <code>{resource.providerResourceIdentifier ?? "—"}</code>
              <small>
                <code>{resource.providerState ?? "—"}</code> ·{" "}
                {formatBytes(resource.provisionedCapacityBytes)} ·{" "}
                {t("Server_Machine_ResourceHourly", {
                  amount: formatMoney(resource.estimatedHourlyCostEur),
                })}
              </small>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

/** The locale key of every provider resource kind the control plane reports, keyed by the lowercase kind without separators. */
const resourceKindKeys = {
  firewall: "Hosting_ResourceKind_Firewall",
  machine: "Hosting_ResourceKind_Machine",
  primaryip: "Hosting_ResourceKind_PrimaryIp",
  server: "Hosting_ResourceKind_Server",
  volume: "Hosting_ResourceKind_Volume",
} as const;

/**
 * Names the kind of a provider resource.
 *
 * @param kind The provider resource kind, for example `primary-ip`; unknown kinds fall back to their humanized form.
 * @param t The translation function of the panel.
 * @returns The localized kind label.
 */
function resourceKindLabel(kind: string, t: TFunction): string {
  const normalized = kind.toLowerCase().replace(/[^a-z0-9]/g, "");
  return Object.hasOwn(resourceKindKeys, normalized)
    ? t(resourceKindKeys[normalized as keyof typeof resourceKindKeys])
    : humanizeToken(kind);
}
