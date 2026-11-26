import { useState } from "react";
import { useTranslation } from "react-i18next";
import { ActionAnchor, ActionButton } from "@/components/ActionControl";
import { Disclosure } from "@/components/Disclosure";
import { StatusBadge } from "@/components/primitives";
import { EmptyState } from "@/components/StateViews";
import {
  ActionUnavailableReason,
  actionIsUnavailable,
} from "@/components/server/ActionUnavailableReason";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import { formatDate, humanizeToken } from "@/features/hosting/model";
import type { GameServer, LifecycleJob, ServerAction } from "@/features/hosting/types";
import {
  buildCommandProtocol,
  type ProtocolEntry,
  protocolEntries,
  textDownloadUrl,
} from "@/features/hosting/workspaceModel";

/**
 * One worker command protocol behind a shared disclosure with its status and time on the summary line.
 *
 * The disclosure state is local to the entry, so a refresh of the job list never collapses a protocol the viewer
 * opened.
 *
 * @param props.entry The job and command pair to show.
 * @param props.initiallyOpen Whether the protocol starts expanded.
 */
function ProtocolDisclosure({
  entry,
  initiallyOpen,
}: {
  entry: ProtocolEntry;
  initiallyOpen: boolean;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(initiallyOpen);
  const command = entry.command;
  const failed = ["failed", "cancelledbycoststop"].includes(command.status.toLowerCase());
  return (
    <Disclosure
      open={open}
      onOpenChange={setOpen}
      title={
        <>
          <strong>{humanizeToken(command.actionId)}</strong>
          <small>
            #{command.sequence} · {humanizeToken(command.stage)}
          </small>
        </>
      }
      summary={
        <>
          <StatusBadge tone={failed ? "danger" : "success"}>
            {humanizeToken(command.status)}
          </StatusBadge>
          <time>
            {formatDate(
              command.timeStampCompleted ?? command.timeStampStarted ?? command.timeStampCreated,
            )}
          </time>
        </>
      }
    >
      <div className="next-hosting-protocol-body">
        <ActionAnchor
          variant="ghost"
          download={`utf-worker-command-${command.primaryKey}.txt`}
          href={textDownloadUrl(buildCommandProtocol(entry))}
        >
          {t("React_ServerWorkspace_DownloadProtocol")}
        </ActionAnchor>
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
    </Disclosure>
  );
}

/**
 * Shows the worker command protocols of a server and the control that collects fresh logs.
 *
 * The collect-logs control requests the shared confirmation; it never executes the action itself.
 *
 * @param props.server The server whose logs are shown; gives the unavailable-reason context.
 * @param props.jobs The lifecycle jobs whose command protocols are listed.
 * @param props.collectLogsAction The projected collect-logs action, when the server offers one.
 */
export function ServerLogsPanel({
  server,
  jobs,
  collectLogsAction,
}: {
  server: GameServer;
  jobs: LifecycleJob[];
  collectLogsAction: ServerAction | undefined;
}) {
  const { t } = useTranslation();
  const { request } = useCostConfirmation();
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
            <ActionAnchor
              variant="ghost"
              download="utf-server-protocols.txt"
              href={textDownloadUrl(all)}
            >
              {t("Hosting_Detail_DownloadAllProtocols")}
            </ActionAnchor>
          )}
          {collectLogsAction && (
            <ActionButton
              variant="primary"
              unavailableReason={
                actionIsUnavailable(collectLogsAction) ? (
                  <ActionUnavailableReason server={server} action={collectLogsAction} />
                ) : undefined
              }
              onClick={() =>
                request({
                  serverId: server.primaryKey,
                  actionId: collectLogsAction.id,
                  source: "workspace-action",
                })
              }
            >
              {t("Hosting_Detail_RefreshLogs")}
            </ActionButton>
          )}
        </div>
      </header>
      {entries.length === 0 ? (
        <EmptyState title={t("Hosting_Detail_NoLogsTitle")}>
          {t("Hosting_Detail_NoLogsDescription")}
        </EmptyState>
      ) : (
        <div className="next-hosting-protocol-list">
          {entries.map((entry, index) => (
            <ProtocolDisclosure
              key={entry.command.primaryKey}
              entry={entry}
              initiallyOpen={index === 0}
            />
          ))}
        </div>
      )}
    </section>
  );
}
