import { useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { formatResource } from "@/app/i18n";
import { ConfirmDialog, StatePanel, StatusBadge } from "@/components/primitives";
import { useHostingCopy } from "@/features/hosting/copy";
import {
  deploymentStatusKey,
  formatDate,
  humanizeToken,
  serverMessageKey,
} from "@/features/hosting/model";
import type { GameServer, ServerAction } from "@/features/hosting/types";

export function HostingAccessBoundary({
  allowed,
  children,
}: {
  allowed: boolean;
  children: ReactNode;
}) {
  const c = useHostingCopy();
  return allowed ? children : <StatePanel kind="forbidden">{c.forbidden}</StatePanel>;
}

export function HostingLoading() {
  const c = useHostingCopy();
  return <StatePanel kind="loading">{c.loading}</StatePanel>;
}

export function HostingError({ onRetry, message }: { onRetry(): void; message?: string }) {
  const c = useHostingCopy();
  return (
    <StatePanel
      kind="error"
      title={c.loadFailed}
      action={
        <button className="button button--ghost" type="button" onClick={onRetry}>
          {c.retry}
        </button>
      }
    >
      {message ?? c.loadFailed}
    </StatePanel>
  );
}

export function serverTone(server: GameServer): "neutral" | "warning" | "info" {
  if (server.deployment?.possibleCost) return "warning";
  if (server.pendingJobCount > 0) return "info";
  return "neutral";
}

export function ServerStatus({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const status = server.deployment?.status;
  const key = deploymentStatusKey(status);
  return (
    <StatusBadge tone={serverTone(server)}>
      {status ? (key ? t(key) : humanizeToken(status)) : t("Hosting_Servers_NoDeployment")}
    </StatusBadge>
  );
}

export function ServerCard({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const terminateAction = server.actions.find(
    (action) => action.id.toLocaleLowerCase() === "terminate",
  );
  const unavailableReasonKey = serverMessageKey(terminateAction?.unavailableReason);
  const unavailableReason = unavailableReasonKey
    ? t(unavailableReasonKey)
    : terminateAction?.unavailableReason;
  const titleId = `server-${server.primaryKey}-title`;
  return (
    <article className="next-hosting-server-card" aria-labelledby={titleId}>
      <header>
        <div>
          <span className="kicker">{server.gameIdentifier}</span>
          <h2 id={titleId}>{server.title}</h2>
        </div>
        <ServerStatus server={server} />
      </header>
      <dl className="next-hosting-facts next-hosting-facts--compact">
        <div>
          <dt>{t("Hosting_Common_Runtime")}</dt>
          <dd>{server.runtimeProviderIdentifier}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Common_CostOwner")}</dt>
          <dd>{server.costOwnerName}</dd>
        </div>
        <div>
          <dt>{t("Hosting_Common_Stage")}</dt>
          <dd>
            {server.deployment?.stage
              ? humanizeToken(server.deployment.stage)
              : t("Hosting_Common_NotApplicable")}
          </dd>
        </div>
        <div>
          <dt>{t("Hosting_Common_Queue")}</dt>
          <dd>{formatResource(t("Hosting_Servers_JobCount"), server.pendingJobCount)}</dd>
        </div>
      </dl>
      {server.deployment?.possibleCost && (
        <p className="next-hosting-cost-warning">{t("Hosting_Servers_CostWarning")}</p>
      )}
      <footer>
        <small>
          {t("Hosting_Common_Updated")} {formatDate(server.timeStampUpdated)}
        </small>
        <div className="next-hosting-server-card-actions">
          {terminateAction &&
            (terminateAction.enabled && !terminateAction.pending ? (
              <Link
                className="button button--danger"
                to={`/command/servers/${server.primaryKey}?action=${encodeURIComponent(terminateAction.id)}`}
              >
                {t("Hosting_Servers_TerminateNow")}
              </Link>
            ) : (
              <button
                className="button button--danger"
                type="button"
                disabled
                title={unavailableReason ?? undefined}
              >
                {terminateAction.pending
                  ? t("Hosting_Detail_ActionPending")
                  : t("Hosting_Servers_TerminateNow")}
              </button>
            ))}
          <Link className="button button--ghost" to={`/command/servers/${server.primaryKey}`}>
            {t("Hosting_Common_Open")}
          </Link>
        </div>
      </footer>
    </article>
  );
}

export function ActionConfirmation({
  action,
  busy,
  error,
  onCancel,
  onExecute,
}: {
  action: ServerAction | null;
  busy: boolean;
  error?: string | undefined;
  onCancel(): void;
  onExecute(confirmation?: string): void;
}) {
  const c = useHostingCopy();
  const description = action
    ? [action.confirmationText, action.description, action.costDescription]
        .filter(Boolean)
        .join(" ")
    : "";
  return (
    <>
      <ConfirmDialog
        open={action !== null}
        title={action?.label || c.confirmAction}
        description={description || c.confirmAction}
        confirmLabel={busy ? c.actionPending : action?.label || c.execute}
        cancelLabel={c.cancel}
        {...(action?.confirmationChallenge ? { challenge: action.confirmationChallenge } : {})}
        onCancel={onCancel}
        onConfirm={() => onExecute(action?.confirmationChallenge || undefined)}
      />
      {error && (
        <p className="next-hosting-inline-error" role="alert">
          {error}
        </p>
      )}
    </>
  );
}

export function ServerActions({
  actions,
  busy,
  onExecute,
}: {
  actions: ServerAction[];
  busy: boolean;
  onExecute(action: ServerAction, confirmation?: string): Promise<void>;
}) {
  const c = useHostingCopy();
  const [selected, setSelected] = useState<ServerAction | null>(null);
  const [error, setError] = useState<string>();
  const execute = async (confirmation?: string) => {
    if (!selected) return;
    setError(undefined);
    try {
      await onExecute(selected, confirmation);
      setSelected(null);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : c.loadFailed);
    }
  };
  if (actions.length === 0) return <p className="next-hosting-muted">{c.noActions}</p>;
  return (
    <>
      <div className="next-hosting-action-grid">
        {[...actions]
          .sort((left, right) => left.order - right.order)
          .map((action) => (
            <article key={action.id} className="next-hosting-action-card">
              <div>
                <strong>{action.label || humanizeToken(action.id)}</strong>
                <p>{action.description}</p>
                {action.unavailableReason && !action.enabled && (
                  <small>
                    {action.unavailableReason ===
                    "Hosting lifecycle execution is paused by the operator."
                      ? c.executionPaused
                      : action.unavailableReason}
                  </small>
                )}
              </div>
              <button
                className={action.dangerous ? "button button--danger" : "button button--primary"}
                type="button"
                disabled={busy || !action.enabled || action.pending}
                onClick={() => setSelected(action)}
              >
                {action.pending ? c.actionPending : action.label || c.execute}
              </button>
            </article>
          ))}
      </div>
      <ActionConfirmation
        action={selected}
        busy={busy}
        {...(error ? { error } : {})}
        onCancel={() => {
          if (!busy) setSelected(null);
        }}
        onExecute={(confirmation) => void execute(confirmation)}
      />
    </>
  );
}
