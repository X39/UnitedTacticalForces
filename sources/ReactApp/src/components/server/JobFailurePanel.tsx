import { ErrorCircle20Regular } from "@fluentui/react-icons";
import { useId } from "react";
import { useTranslation } from "react-i18next";
import { formatRelativeTime } from "@/app/format";
import { useNow } from "@/app/useNow";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { ApiText } from "@/components/server/ApiText";
import {
  ActionUnavailableReason,
  actionIsUnavailable,
} from "@/components/server/ActionUnavailableReason";
import { CostIndicator } from "@/components/server/CostIndicator";
import "@/components/server/job-status.css";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import { jobKindLabelKey } from "@/features/hosting/activityLabels";
import { ActionLabel } from "@/features/hosting/components";
import { costSentenceKey, deriveFailure } from "@/features/hosting/jobFailure";
import { failedStepLabelKey } from "@/features/hosting/jobProgress";
import { deriveServerState } from "@/features/hosting/serverState";
import type { GameServer, LifecycleJob } from "@/features/hosting/types";
import { activityEntryAnchor, activityEntryTo } from "@/features/hosting/workspaceSections";

/** How often the relative failure time is refreshed while the panel is mounted. */
const clockIntervalMs = 30_000;

/** The props of {@link JobFailurePanel}. */
export interface JobFailurePanelProps {
  /** The server the jobs belong to. */
  server: GameServer;
  /** The loaded jobs of the server. */
  jobs: readonly LifecycleJob[] | undefined;
  /** Whether a retry or skip request is in flight; both actions are then unavailable. */
  busy: boolean;
  /** Whether the last retry or skip request failed. */
  requestFailed: boolean;
  /** Called with the job id when the viewer asks to try the failed job again. */
  onRetry: (jobId: string) => void;
  /** Called with the job id when the viewer asks to skip the failed job. */
  onSkip: (jobId: string) => void;
}

/**
 * States what failed, whether the server may still cost money and the next step, with inline actions.
 *
 * The panel renders only when {@link deriveFailure} finds a failure: no job is active and the newest terminal job
 * failed. It is not a live region, so a failure that already exists when the page loads is not announced; the
 * shell announces failures that happen while the page is open. Retry and skip appear only when the API allows them
 * and only call the page callbacks. The cost-stop uses the shared confirmation with the `workspace-failure` source
 * and never executes anything itself.
 *
 * @param props See {@link JobFailurePanelProps}.
 */
export function JobFailurePanel({
  server,
  jobs,
  busy,
  requestFailed,
  onRetry,
  onSkip,
}: JobFailurePanelProps) {
  const { t } = useTranslation();
  const { request } = useCostConfirmation();
  const titleId = useId();
  const now = useNow(clockIntervalMs);
  const failure = deriveFailure(server, jobs);
  if (!failure) return null;

  const { job, cost, costStop, nextStepKey } = failure;
  const failedAt = job.timeStampCompleted ?? job.timeStampUpdated;
  const relative = formatRelativeTime(failedAt, now);
  const sending = busy ? t("Server_Failure_Sending") : undefined;
  return (
    <section className="job-failure" aria-labelledby={titleId}>
      <span aria-hidden="true" className="job-failure__icon">
        <ErrorCircle20Regular />
      </span>
      <div className="job-failure__body">
        <h2 id={titleId} className="job-failure__title">
          {t("Server_Failure_Title", { action: t(jobKindLabelKey(job.kind)) })}
        </h2>
        <dl className="job-failure__facts">
          <div className="job-failure__fact">
            <dt>{t("Server_Failure_WhatFailed")}</dt>
            <dd>
              <p>{t("Server_Failure_FailedAt", { step: t(failedStepLabelKey(job)) })}</p>
              <p>
                {job.failureReason ? (
                  <ApiText>{job.failureReason}</ApiText>
                ) : (
                  t("Server_Failure_NoReason")
                )}
              </p>
              {relative !== "" && (
                <p className="job-failure__time">
                  <time dateTime={failedAt}>{relative}</time>
                </p>
              )}
            </dd>
          </div>
          <div className="job-failure__fact">
            <dt>{t("Server_Failure_CostLabel")}</dt>
            <dd>
              {cost !== "none" && <CostIndicator derived={deriveServerState(server)} />}
              <p>{t(costSentenceKey(cost))}</p>
            </dd>
          </div>
          <div className="job-failure__fact">
            <dt>{t("Server_Failure_NextLabel")}</dt>
            <dd>{t(nextStepKey)}</dd>
          </div>
        </dl>
        <div className="job-failure__actions">
          {costStop && (
            <ActionButton
              variant="danger"
              unavailableReason={
                actionIsUnavailable(costStop) ? (
                  <ActionUnavailableReason server={server} action={costStop} />
                ) : undefined
              }
              onClick={() =>
                request({
                  serverId: server.primaryKey,
                  actionId: costStop.id,
                  source: "workspace-failure",
                })
              }
            >
              {costStop.pending ? t("Server_Action_InProgress") : <ActionLabel action={costStop} />}
            </ActionButton>
          )}
          {job.canRetry && (
            <ActionButton
              variant={costStop ? "ghost" : "gold"}
              unavailableReason={sending}
              onClick={() => onRetry(job.primaryKey)}
            >
              {t("Server_Failure_Retry")}
            </ActionButton>
          )}
          {job.canSkip && (
            <ActionButton
              variant="ghost"
              unavailableReason={sending}
              onClick={() => onSkip(job.primaryKey)}
            >
              {t("Server_Failure_Skip")}
            </ActionButton>
          )}
          <ActionLink
            variant="ghost"
            to={activityEntryTo(server.primaryKey, activityEntryAnchor("job", job.primaryKey))}
          >
            {t("Server_Failure_OpenDetails")}
          </ActionLink>
        </div>
        <p role="alert" className="job-failure__error">
          {requestFailed ? t("Server_Failure_RequestError") : null}
        </p>
      </div>
    </section>
  );
}
