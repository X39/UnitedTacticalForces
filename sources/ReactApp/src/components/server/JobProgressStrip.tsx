import {
  ArrowSync16Regular,
  CheckmarkCircle16Regular,
  Circle16Regular,
  Warning16Regular,
} from "@fluentui/react-icons";
import { useId, type ComponentType, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { formatRelativeTime } from "@/app/format";
import type { TranslationKey } from "@/app/localizationTypes";
import { useNow } from "@/app/useNow";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import {
  ActionUnavailableReason,
  actionIsUnavailable,
} from "@/components/server/ActionUnavailableReason";
import { ApiText } from "@/components/server/ApiText";
import "@/components/server/job-status.css";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import {
  actorFor,
  actorLabelKey,
  jobKindLabelKey,
  triggerFor,
} from "@/features/hosting/activityLabels";
import { ActionLabel } from "@/features/hosting/components";
import { costSentenceKey } from "@/features/hosting/jobFailure";
import {
  activeJobs,
  deriveJobProgress,
  progressStepLabelKey,
  type ProgressStepId,
} from "@/features/hosting/jobProgress";
import { deriveServerState } from "@/features/hosting/serverState";
import type { GameServer, LifecycleJob } from "@/features/hosting/types";
import { costStopAction } from "@/features/hosting/workspaceModel";
import { activityEntryAnchor, activityEntryTo } from "@/features/hosting/workspaceSections";

/** How often the relative start time is refreshed while the strip is mounted. */
const clockIntervalMs = 15_000;

/** An invisible separator that stands in for the actor while the sentence is split around it. */
const actorMarker = String.fromCodePoint(0x2063);

type StepState = "done" | "current" | "upcoming";

/** The decorative marker and the visually hidden state word of each step state. */
const stepStates = {
  done: { Marker: CheckmarkCircle16Regular, wordKey: "Server_Progress_StepDone" },
  current: { Marker: ArrowSync16Regular, wordKey: "Server_Progress_StepCurrent" },
  upcoming: { Marker: Circle16Regular, wordKey: "Server_Progress_StepUpcoming" },
} as const satisfies Record<StepState, { Marker: ComponentType; wordKey: TranslationKey }>;

/** One entry of the step list: a decorative marker, the step label and a visually hidden state word. */
function StepItem({ step, state }: { step: ProgressStepId; state: StepState }) {
  const { t } = useTranslation();
  const { Marker, wordKey } = stepStates[state];
  const content = (
    <>
      <span aria-hidden="true" className="job-progress__marker">
        <Marker />
      </span>
      <span className="job-progress__step-label">{t(progressStepLabelKey(step))}</span>
      <span className="sr-only"> {t(wordKey)}</span>
    </>
  );
  const className = `job-progress__step job-progress__step--${state}`;
  if (state === "current") {
    return (
      <li className={className} aria-current="step">
        {content}
      </li>
    );
  }
  return <li className={className}>{content}</li>;
}

/** The name of whoever asked for the job, inside a `bdi` element when it is a person. */
function RequestedBy({ name }: { name: string | null | undefined }) {
  const { t } = useTranslation();
  const actor = actorFor(name);
  if (actor.kind !== "name") {
    return <>{t("Server_Activity_RequestedBy", { actor: t(actorLabelKey(actor.kind)) })}</>;
  }
  const [before = "", after = ""] = t("Server_Activity_RequestedBy", { actor: actorMarker }).split(
    actorMarker,
  );
  return (
    <>
      {before}
      <bdi>{actor.name}</bdi>
      {after}
    </>
  );
}

/** Whether a job kind is itself a cost-stop: it shuts the server down, so it is the answer to a cost risk. */
function isCostStopKind(kind: string): boolean {
  switch (kind) {
    case "Stop":
    case "Terminate":
      return true;
    default:
      return false;
  }
}

/**
 * The retry-mode body: why the job is waiting, the failure reason, whether the server may cost money and the
 * way out of a cost risk.
 *
 * A job that is not itself a cost-stop offers the shared shut-down confirmation while cost may remain; a
 * cost-stop job instead says that shutting down is retried automatically.
 */
function RetryDetails({ server, job }: { server: GameServer; job: LifecycleJob }) {
  const { t } = useTranslation();
  const { request } = useCostConfirmation();
  const cost = deriveServerState(server).cost;
  const costStopJob = isCostStopKind(job.kind);
  const costStop = costStopJob || cost === "none" ? undefined : costStopAction(server.actions);
  let way: ReactNode = null;
  if (costStop) {
    way = (
      <div className="job-progress__links">
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
      </div>
    );
  } else if (costStopJob) {
    way = <p className="job-progress__stage">{t("Server_Failure_Next_Wait")}</p>;
  }
  return (
    <>
      <p className="job-progress__stage">{t("Server_Progress_RetryBody")}</p>
      {job.failureReason ? (
        <p className="job-progress__stage">
          <ApiText>{job.failureReason}</ApiText>
        </p>
      ) : null}
      <p className="job-progress__stage">{t(costSentenceKey(cost))}</p>
      {way}
    </>
  );
}

/**
 * Shows the newest active lifecycle job of a server with its named stage.
 *
 * The strip names the action, the current step of the job's step list (done, current and upcoming steps carry a
 * visually hidden state word), how long ago the job was requested or started, who requested it, an honest note
 * about the update lag and a link to the job's activity entry. The stage is derived only from API data by
 * `deriveJobProgress`; the milestone percent is never shown, so the strip has no bar, no percent sign and no
 * `progressbar` role. A job whose stage maps to no known step reads "Working" with the raw stage token. A
 * `RetryPending` job switches to the warning retry mode, and more than one active job adds a queue line.
 *
 * @param props.server The server the jobs belong to.
 * @param props.jobs The loaded jobs of the server; the strip renders nothing when none is active.
 */
export function JobProgressStrip({
  server,
  jobs,
}: {
  server: GameServer;
  jobs: readonly LifecycleJob[] | undefined;
}) {
  const { t } = useTranslation();
  const titleId = useId();
  const now = useNow(clockIntervalMs);
  const active = activeJobs(jobs);
  const job = active[0];
  if (!job) return null;

  const progress = deriveJobProgress(job);
  const queued = job.status === "Queued";
  const sinceIso = queued ? job.timeStampCreated : (job.timeStampStarted ?? job.timeStampCreated);
  const relative = formatRelativeTime(sinceIso, now);
  const sinceText = t(queued ? "Server_Progress_Requested" : "Server_Progress_Started", {
    relative,
  });

  const trigger = triggerFor(job.trigger, job.triggerLimit);

  const retrying = progress.mode === "retry";
  let stepLabel: ReactNode = t(
    retrying ? "Server_Progress_Step_RetryWait" : "Server_Progress_Step_Working",
  );
  let body: ReactNode;
  if (progress.mode === "retry") {
    body = <RetryDetails server={server} job={job} />;
  } else if (progress.mode === "steps") {
    const current = progress.steps[progress.currentIndex];
    if (current !== undefined) stepLabel = t(progressStepLabelKey(current));
    body = (
      <ol className="job-progress__steps" aria-label={t("Server_Progress_StepsLabel")}>
        {progress.steps.map((step, index) => {
          let state: StepState = "upcoming";
          if (index < progress.currentIndex) state = "done";
          else if (index === progress.currentIndex) state = "current";
          return <StepItem key={step} step={step} state={state} />;
        })}
      </ol>
    );
  } else {
    const stage = progress.stage;
    body = stage ? (
      <p className="job-progress__stage">
        <ApiText>{stage}</ApiText>
      </p>
    ) : null;
  }

  return (
    <section
      className={retrying ? "job-progress job-progress--warning" : "job-progress"}
      aria-labelledby={titleId}
    >
      <p className="job-progress__kicker">
        <span aria-hidden="true" className="job-progress__kicker-icon">
          {retrying ? <Warning16Regular /> : <ArrowSync16Regular />}
        </span>
        {t("Server_Progress_Kicker")}
      </p>
      <p id={titleId} className="job-progress__title">
        <strong className="job-progress__action">{t(jobKindLabelKey(job.kind))}</strong>
        <span className="job-progress__step-name">{stepLabel}</span>
      </p>
      {body}
      <p className="job-progress__meta">
        {relative !== "" && <time dateTime={sinceIso}>{sinceText}</time>}
        <span>
          {trigger ? (
            t(trigger.key, { count: trigger.count ?? undefined })
          ) : (
            <RequestedBy name={job.requestedByName} />
          )}
        </span>
      </p>
      <p className="job-progress__lag">{t("Server_Progress_Lag")}</p>
      {active.length > 1 && (
        <p className="job-progress__queue">
          {t("Server_Progress_MoreQueued", { count: active.length - 1 })}
        </p>
      )}
      <div className="job-progress__links">
        <ActionLink
          variant="ghost"
          size="compact"
          to={activityEntryTo(server.primaryKey, activityEntryAnchor("job", job.primaryKey))}
        >
          {t("Server_Failure_OpenDetails")}
        </ActionLink>
      </div>
    </section>
  );
}
