import {
  Briefcase20Regular,
  ChevronDown20Regular,
  ChevronUp20Regular,
  DocumentText20Regular,
  ErrorCircle20Regular,
  History20Regular,
  Warning20Regular,
} from "@fluentui/react-icons";
import type { ComponentType, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { formatRelativeTime } from "@/app/format";
import type { TranslationKey } from "@/app/localizationTypes";
import { ActionAnchor, ActionButton } from "@/components/ActionControl";
import { CopyButton } from "@/components/CopyButton";
import { ApiText } from "@/components/server/ApiText";
import "@/components/server/activity.css";
import { actorLabelKey, type ActorDisplay } from "@/features/hosting/activityLabels";
import type {
  AuditTimelineEntry,
  JobTimelineEntry,
  LogTimelineEntry,
  TimelineEntry,
  TimelineEntryType,
} from "@/features/hosting/activityModel";
import { HostingAuditEvidence } from "@/features/hosting/HostingAuditFeed";
import { formatDate } from "@/features/hosting/model";
import { buildCommandProtocol, textDownloadUrl } from "@/features/hosting/workspaceModel";

/** An invisible separator that stands in for the actor name while the translated sentence is split around it. */
const actorMarker = String.fromCodePoint(0x2063);

/** The separator between the protocols of the commands in one job download. */
const protocolSeparator = "\n\n---\n\n";

/** The decorative icon of each entry source. */
const typeIcons = {
  job: Briefcase20Regular,
  log: DocumentText20Regular,
  audit: History20Regular,
} as const satisfies Record<TimelineEntryType, ComponentType>;

/** The localization key of the type tag of each entry source. */
const typeTagKeys = {
  job: "Server_Activity_Type_Job",
  log: "Server_Activity_Type_Log",
  audit: "Server_Activity_Type_Audit",
} as const satisfies Record<TimelineEntryType, TranslationKey>;

/** Returns the icon and its tone modifier: a problem or an active job replaces the neutral look of the type icon. */
function iconFor(entry: TimelineEntry): { Icon: ComponentType; tone: string } {
  if (entry.tone === "danger") return { Icon: ErrorCircle20Regular, tone: "danger" };
  if (entry.tone === "warning") return { Icon: Warning20Regular, tone: "warning" };
  return { Icon: typeIcons[entry.type], tone: entry.active ? "active" : "neutral" };
}

/**
 * The actor of an entry: a name inside a `bdi` element, or the localized label of the platform, a hidden member or
 * the server. A job entry words it as "Requested by ...".
 */
function ActorText({ actor, requestedBy }: { actor: ActorDisplay; requestedBy: boolean }) {
  const { t } = useTranslation();
  if (actor.kind !== "name") {
    const label = t(actorLabelKey(actor.kind));
    return <>{requestedBy ? t("Server_Activity_RequestedBy", { actor: label }) : label}</>;
  }
  if (!requestedBy) return <bdi>{actor.name}</bdi>;
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

/** One label and value row of a details fact list. */
function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="activity-details__fact">
      <dt>{label}</dt>
      <dd>{children}</dd>
    </div>
  );
}

/**
 * The diagnostic output of a command in a focusable box that scrolls inside itself.
 *
 * The output is machine text the worker wrote in English, so the box declares that language (WCAG 3.1.2) and a
 * German page neither shows it as untranslated interface text nor has a screen reader read it with a German voice.
 */
function OutputBox({ output, labelledBy }: { output: string; labelledBy: string }) {
  return (
    <pre tabIndex={0} lang="en" aria-labelledby={labelledBy} className="activity-details__output">
      {output}
    </pre>
  );
}

/**
 * The expanded details of a job entry: its facts, every command with status and output, the protocol download
 * and the retry and skip actions the API allows.
 */
function JobDetails({
  entry,
  jobBusy,
  onRetryJob,
  onSkipJob,
}: {
  entry: JobTimelineEntry;
  jobBusy: boolean;
  onRetryJob: ((jobId: string) => void) | undefined;
  onSkipJob: ((jobId: string) => void) | undefined;
}) {
  const { t } = useTranslation();
  const { job } = entry;
  const commands = [...job.commands].sort((left, right) => left.sequence - right.sequence);
  const sending = jobBusy ? t("Server_Failure_Sending") : undefined;
  return (
    <>
      <dl className="activity-details__facts">
        <Fact label={t("React_ServerWorkspace_JobId")}>
          <code>{job.primaryKey}</code>
        </Fact>
        <Fact label={t("Status")}>
          <ApiText>{job.status}</ApiText>
        </Fact>
        <Fact label={t("Hosting_Common_Stage")}>
          <ApiText>{job.stage}</ApiText>
        </Fact>
        <Fact label={t("Attempts")}>{job.attemptCount}</Fact>
        <Fact label={t("React_ServerWorkspace_Created")}>{formatDate(job.timeStampCreated)}</Fact>
        <Fact label={t("React_ServerWorkspace_Started")}>{formatDate(job.timeStampStarted)}</Fact>
        <Fact label={t("React_ServerWorkspace_Completed")}>
          {formatDate(job.timeStampCompleted)}
        </Fact>
        {job.failureCode ? (
          <Fact label={t("React_ServerWorkspace_FailureCode")}>
            <ApiText>{job.failureCode}</ApiText>
          </Fact>
        ) : null}
      </dl>
      {job.failureReason ? (
        <p className="activity-details__failure">
          <ApiText>{job.failureReason}</ApiText>
        </p>
      ) : null}
      {commands.length > 0 ? (
        <ol className="activity-details__commands">
          {commands.map((command) => {
            const labelId = `${entry.id}-command-${command.primaryKey}`;
            return (
              <li key={command.primaryKey} className="activity-details__command">
                <p id={labelId} className="activity-details__command-title">
                  <strong>
                    #{command.sequence} <ApiText>{command.actionId}</ApiText>
                  </strong>{" "}
                  <ApiText>{command.status}</ApiText>
                </p>
                {command.failureReason ? (
                  <p className="activity-details__failure">
                    <ApiText>{command.failureReason}</ApiText>
                  </p>
                ) : null}
                {command.diagnosticOutput ? (
                  <OutputBox output={command.diagnosticOutput} labelledBy={labelId} />
                ) : (
                  <p className="activity-details__muted">
                    {t("React_ServerWorkspace_NoDiagnostic")}
                  </p>
                )}
              </li>
            );
          })}
        </ol>
      ) : null}
      <div className="activity-details__actions">
        {commands.length > 0 ? (
          <ActionAnchor
            variant="ghost"
            size="compact"
            href={textDownloadUrl(
              commands
                .map((command) => buildCommandProtocol({ job, command }))
                .join(protocolSeparator),
            )}
            download={`utf-lifecycle-${job.primaryKey}.txt`}
          >
            {t("Server_Activity_DownloadLog")}
          </ActionAnchor>
        ) : null}
        {job.canRetry ? (
          <ActionButton
            variant="primary"
            size="compact"
            unavailableReason={sending}
            onClick={() => onRetryJob?.(job.primaryKey)}
          >
            {t("Server_Failure_Retry")}
          </ActionButton>
        ) : null}
        {job.canSkip ? (
          <ActionButton
            variant="ghost"
            size="compact"
            unavailableReason={sending}
            onClick={() => onSkipJob?.(job.primaryKey)}
          >
            {t("Server_Failure_Skip")}
          </ActionButton>
        ) : null}
      </div>
    </>
  );
}

/**
 * The expanded details of a log entry: the command facts, the diagnostic output with Copy output, and the
 * download of the command protocol.
 */
function LogDetails({ entry, sentenceId }: { entry: LogTimelineEntry; sentenceId: string }) {
  const { t } = useTranslation();
  const { job, command } = entry;
  const output = command.diagnosticOutput;
  return (
    <>
      <dl className="activity-details__facts">
        <Fact label={t("React_ServerWorkspace_JobId")}>
          <code>{job.primaryKey}</code>
        </Fact>
        <Fact label={t("React_ServerWorkspace_CommandId")}>
          <code>{command.primaryKey}</code>
        </Fact>
        <Fact label={t("React_ServerWorkspace_FailureCode")}>
          {command.failureCode ? <ApiText>{command.failureCode}</ApiText> : "—"}
        </Fact>
        <Fact label={t("React_ServerWorkspace_Created")}>
          {formatDate(command.timeStampCreated)}
        </Fact>
        <Fact label={t("React_ServerWorkspace_Started")}>
          {formatDate(command.timeStampStarted)}
        </Fact>
        <Fact label={t("React_ServerWorkspace_Completed")}>
          {formatDate(command.timeStampCompleted)}
        </Fact>
      </dl>
      {command.failureReason ? (
        <p className="activity-details__failure">
          <ApiText>{command.failureReason}</ApiText>
        </p>
      ) : null}
      {output ? (
        <OutputBox output={output} labelledBy={sentenceId} />
      ) : (
        <p className="activity-details__muted">{t("React_ServerWorkspace_NoDiagnostic")}</p>
      )}
      <div className="activity-details__actions">
        {output ? (
          <CopyButton
            value={output}
            label={t("Server_Activity_CopyOutput")}
            copiedMessage={t("Server_Activity_OutputCopied")}
          />
        ) : null}
        <ActionAnchor
          variant="ghost"
          size="compact"
          href={textDownloadUrl(buildCommandProtocol({ job, command }))}
          download={`utf-command-${command.primaryKey}.txt`}
        >
          {t("Server_Activity_DownloadLog")}
        </ActionAnchor>
      </div>
    </>
  );
}

/** The expanded details of an audit entry: the API summary and the technical evidence. */
function AuditDetails({ entry }: { entry: AuditTimelineEntry }) {
  const { operation } = entry;
  return (
    <>
      {operation.summary ? (
        <p className="activity-details__summary">
          <ApiText>{operation.summary}</ApiText>
        </p>
      ) : null}
      <HostingAuditEvidence operation={operation} />
    </>
  );
}

/**
 * Renders one entry of the activity timeline as a plain-language sentence with its actor and relative time.
 *
 * The anchored variant carries the entry id and a programmatic tab stop so the Open activity details links can land
 * on it. The compact variant has neither and derives a different sentence id, so it can sit on a page that also
 * lists the same entry, for example the Overview recent activity. The icon is decorative: a problem is carried by
 * the words of the sentence, not by colour alone. API prose appears only as React text inside `ApiText`.
 *
 * The anchored variant can expand in place: a disclosure button shows the facts, the raw command output with copy
 * and download, the retry and skip actions of a job, or the technical evidence of an audit record. The compact
 * variant ignores every expansion prop and renders no disclosure.
 *
 * @param props.entry The entry from `buildActivityEntries`.
 * @param props.now The current time in milliseconds, used for the relative time.
 * @param props.compact Renders the variant without anchor id, tab stop and disclosure.
 * @param props.expanded Whether the details are shown.
 * @param props.onToggle Called when the viewer activates the disclosure; the disclosure is rendered only when it
 * is given.
 * @param props.jobBusy Whether a retry or skip request is in flight; both actions are then unavailable.
 * @param props.onRetryJob Called with the job id when the viewer asks to try a job again.
 * @param props.onSkipJob Called with the job id when the viewer asks to skip a job.
 */
export function ActivityEntry({
  entry,
  now,
  compact = false,
  expanded = false,
  onToggle,
  jobBusy = false,
  onRetryJob,
  onSkipJob,
}: {
  entry: TimelineEntry;
  now: number;
  compact?: boolean;
  expanded?: boolean;
  onToggle?: () => void;
  jobBusy?: boolean;
  onRetryJob?: (jobId: string) => void;
  onSkipJob?: (jobId: string) => void;
}) {
  const { t } = useTranslation();
  const sentenceId = compact ? `${entry.id}-compact-sentence` : `${entry.id}-sentence`;
  const detailsId = `${entry.id}-details`;
  const { Icon, tone } = iconFor(entry);
  const anchor = compact ? {} : { id: entry.id, tabIndex: -1 };
  const showDisclosure = !compact && onToggle !== undefined;
  const ToggleIcon = expanded ? ChevronUp20Regular : ChevronDown20Regular;
  return (
    <article
      {...anchor}
      aria-labelledby={sentenceId}
      className={`activity-entry${entry.problem ? " activity-entry--problem" : ""}`}
    >
      <span aria-hidden="true" className={`activity-entry__icon activity-entry__icon--${tone}`}>
        <Icon />
      </span>
      <div className="activity-entry__content">
        <p id={sentenceId} className="activity-entry__sentence">
          {t(entry.sentenceKey, { action: entry.actionKey ? t(entry.actionKey) : "" })}
          {entry.apiText !== null ? (
            <>
              {" "}
              <ApiText>{entry.apiText}</ApiText>
            </>
          ) : null}
        </p>
        <p className="activity-entry__meta">
          <span className="activity-entry__tag">{t(typeTagKeys[entry.type])}</span>
          <span className="activity-entry__actor">
            {entry.type === "job" && entry.trigger ? (
              t(entry.trigger.key, { count: entry.trigger.count ?? undefined })
            ) : (
              <ActorText actor={entry.actor} requestedBy={entry.type === "job"} />
            )}
          </span>
          <time dateTime={entry.at}>{formatRelativeTime(entry.at, now)}</time>
        </p>
        {showDisclosure ? (
          <div className="activity-entry__disclosure">
            <ActionButton
              variant="ghost"
              size="compact"
              aria-expanded={expanded}
              aria-controls={detailsId}
              onClick={onToggle}
            >
              <ToggleIcon aria-hidden="true" />
              {expanded ? t("Server_Activity_HideDetails") : t("Server_Activity_ShowDetails")}
            </ActionButton>
          </div>
        ) : null}
        {showDisclosure && expanded ? (
          <div id={detailsId} className="activity-entry__details">
            {entry.type === "job" ? (
              <JobDetails
                entry={entry}
                jobBusy={jobBusy}
                onRetryJob={onRetryJob}
                onSkipJob={onSkipJob}
              />
            ) : null}
            {entry.type === "log" ? <LogDetails entry={entry} sentenceId={sentenceId} /> : null}
            {entry.type === "audit" ? <AuditDetails entry={entry} /> : null}
          </div>
        ) : null}
      </div>
    </article>
  );
}
