import { useTranslation } from "react-i18next";
import { StatePanel, StatusBadge } from "@/components/primitives";
import { formatDate, humanizeToken } from "@/features/hosting/model";
import type { LifecycleJob, ServerAction } from "@/features/hosting/types";
import {
  buildCommandProtocol,
  protocolEntries,
  textDownloadUrl,
} from "@/features/hosting/workspaceModel";

export function ServerLogsPanel({
  jobs,
  collectLogsAction,
  busy,
  onCollect,
}: {
  jobs: LifecycleJob[];
  collectLogsAction: ServerAction | undefined;
  busy: boolean;
  onCollect(action: ServerAction): void;
}) {
  const { t } = useTranslation();
  const entries = protocolEntries(jobs);
  const all = entries
    .map(buildCommandProtocol)
    .join("\n\n========================================\n\n");
  return (
    <section className="next-hosting-panel next-hosting-logs-panel">
      <header className="next-hosting-section-heading">
        <div>
          <span className="kicker">{t("Hosting_Detail_LogsKicker")}</span>
          <h2>{t("React_ServerWorkspace_LogsAndProtocols")}</h2>
          <p>{t("Hosting_Detail_LogsDescription")}</p>
        </div>
        <div className="next-hosting-form-actions">
          {entries.length > 0 && (
            <a
              className="button button--ghost"
              download="utf-server-protocols.txt"
              href={textDownloadUrl(all)}
            >
              {t("Hosting_Detail_DownloadAllProtocols")}
            </a>
          )}
          {collectLogsAction && (
            <button
              className="button button--primary"
              type="button"
              disabled={busy || !collectLogsAction.enabled || collectLogsAction.pending}
              onClick={() => onCollect(collectLogsAction)}
            >
              {t("Hosting_Detail_RefreshLogs")}
            </button>
          )}
        </div>
      </header>
      {entries.length === 0 ? (
        <StatePanel kind="empty" title={t("Hosting_Detail_NoLogsTitle")}>
          {t("Hosting_Detail_NoLogsDescription")}
        </StatePanel>
      ) : (
        <div className="next-hosting-protocol-list">
          {entries.map((entry, index) => {
            const command = entry.command;
            const failed = ["failed", "cancelledbycoststop"].includes(command.status.toLowerCase());
            return (
              <details key={command.primaryKey} open={index === 0}>
                <summary>
                  <span>
                    <strong>{humanizeToken(command.actionId)}</strong>
                    <small>
                      #{command.sequence} · {humanizeToken(command.stage)}
                    </small>
                  </span>
                  <StatusBadge tone={failed ? "danger" : "success"}>
                    {humanizeToken(command.status)}
                  </StatusBadge>
                  <time>
                    {formatDate(
                      command.timeStampCompleted ??
                        command.timeStampStarted ??
                        command.timeStampCreated,
                    )}
                  </time>
                </summary>
                <div className="next-hosting-protocol-body">
                  <a
                    className="button button--ghost"
                    download={`utf-worker-command-${command.primaryKey}.txt`}
                    href={textDownloadUrl(buildCommandProtocol(entry))}
                  >
                    {t("React_ServerWorkspace_DownloadProtocol")}
                  </a>
                  <dl className="next-hosting-facts">
                    <div>
                      <dt>{t("React_ServerWorkspace_JobId")}</dt>
                      <dd>
                        <code>{entry.job.primaryKey}</code>
                      </dd>
                    </div>
                    <div>
                      <dt>{t("React_ServerWorkspace_CommandId")}</dt>
                      <dd>
                        <code>{command.primaryKey}</code>
                      </dd>
                    </div>
                    <div>
                      <dt>{t("React_ServerWorkspace_FailureCode")}</dt>
                      <dd>
                        <code>{command.failureCode ?? "—"}</code>
                      </dd>
                    </div>
                    <div>
                      <dt>{t("React_ServerWorkspace_Created")}</dt>
                      <dd>{formatDate(command.timeStampCreated)}</dd>
                    </div>
                    <div>
                      <dt>{t("React_ServerWorkspace_Started")}</dt>
                      <dd>{formatDate(command.timeStampStarted)}</dd>
                    </div>
                    <div>
                      <dt>{t("React_ServerWorkspace_Completed")}</dt>
                      <dd>{formatDate(command.timeStampCompleted)}</dd>
                    </div>
                  </dl>
                  {command.failureReason && (
                    <p className="next-hosting-inline-error">{command.failureReason}</p>
                  )}
                  {command.diagnosticOutput ? (
                    <pre tabIndex={0}>{command.diagnosticOutput}</pre>
                  ) : (
                    <p>{t("React_ServerWorkspace_NoDiagnostic")}</p>
                  )}
                </div>
              </details>
            );
          })}
        </div>
      )}
    </section>
  );
}
