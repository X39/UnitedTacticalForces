import { useMemo, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import { useNow } from "@/app/useNow";
import { ActionLink } from "@/components/ActionControl";
import { ApiText } from "@/components/server/ApiText";
import { ActivityEntry } from "@/components/server/ActivityEntry";
import { ServerMetricsPanel } from "@/components/server/ServerMetricsPanel";
import "@/components/server/activity.css";
import "@/components/server/sections.css";
import { lookupActionVocabulary } from "@/features/hosting/actionVocabulary";
import { buildActivityEntries, filterEntries } from "@/features/hosting/activityModel";
import { formatWallTime } from "@/features/hosting/automationModel";
import { ServerActions } from "@/features/hosting/components";
import { gameLabel } from "@/features/hosting/ConfigurationProfileFields";
import { formatDate } from "@/features/hosting/model";
import type {
  GameServer,
  HostingOperation,
  LifecycleJob,
  ServerAutomation,
} from "@/features/hosting/types";
import { primaryServerAction } from "@/features/hosting/workspaceModel";
import { sectionTo } from "@/features/hosting/workspaceSections";

/** How many of the newest merged entries the recent activity panel lists. */
const recentEntryCount = 5;

/** How often the relative times of the recent entries are refreshed. */
const clockIntervalMs = 30_000;

/** The id of the projected action that asks the worker to collect fresh logs; it lives in the Activity section. */
const collectLogsActionId = "collect-logs";

/** One label and value row of the server details. */
function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="workspace-facts__fact">
      <dt>{label}</dt>
      <dd>{children}</dd>
    </div>
  );
}

/** The id of the automation panel in the Settings section; the Overview link lands on it. */
const automationAnchor = "#server-automation";

/** The wall-clock format of the next scheduled action: weekday, day, month and time. */
const nextScheduledFormat: Intl.DateTimeFormatOptions = {
  weekday: "short",
  day: "numeric",
  month: "short",
  hour: "numeric",
  minute: "2-digit",
};

/** Whether idle shutdown, the maximum runtime or at least one schedule is set. */
function isAutomationConfigured(automation: ServerAutomation): boolean {
  const { settings } = automation;
  return (
    settings.idleShutdownEnabled || settings.maxRuntimeEnabled || settings.schedules.length > 0
  );
}

/**
 * The Automation fact of the server details: up to three short lines (idle shutdown, maximum runtime, next
 * scheduled action) and the link to the automation settings.
 *
 * Every line is omitted when its setting is off, and nothing renders when the server has no automation block or
 * nothing is configured. The next action is shown in the saved time zone and is omitted when its name or time
 * cannot be shown. The link is shown to every viewer; the panel itself explains what the viewer may change.
 */
function AutomationFact({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const automation = server.automation;
  if (!automation || !isAutomationConfigured(automation)) return null;
  const { settings, status } = automation;
  const next = status.nextScheduled;
  const nextLabel = next ? lookupActionVocabulary(next.action)?.label : undefined;
  const nextWhen =
    next && nextLabel
      ? formatWallTime(
          next.at,
          settings.timeZone ?? Intl.DateTimeFormat().resolvedOptions().timeZone,
          currentLocale(),
          nextScheduledFormat,
        )
      : "";
  return (
    <Fact label={t("Server_Overview_Automation")}>
      {settings.idleShutdownEnabled && (
        <div>{t("Server_Overview_Automation_Idle", { count: settings.idleShutdownMinutes })}</div>
      )}
      {settings.maxRuntimeEnabled && (
        <div>{t("Server_Overview_Automation_Max", { count: settings.maxRuntimeHours })}</div>
      )}
      {nextLabel && nextWhen !== "" && (
        <div>{t("Server_Overview_Automation_Next", { action: t(nextLabel), when: nextWhen })}</div>
      )}
      <ActionLink
        variant="ghost"
        size="compact"
        to={{ ...sectionTo(server.primaryKey, "settings"), hash: automationAnchor }}
      >
        {t("Server_Overview_Automation_Link")}
      </ActionLink>
    </Fact>
  );
}

/**
 * The Overview section: the server details, the metrics panel, the five newest merged activity entries and the
 * remaining actions.
 *
 * The details end with an Automation row, shown only when idle shutdown, a maximum runtime or a schedule is set.
 * Cost owner and cost are not repeated here because the cost strip of the workspace shows them. Terrain and
 * scenario appear only when set. The metrics panel follows the details and shows the current readings of the
 * server as the control plane sent them, or why a reading is unavailable. The recent activity reuses the compact activity entry (no disclosure, no anchor) and links to
 * the Activity section. More actions lists every projected action except collect-logs and the primary action of
 * the workspace header, and the panel is omitted when nothing is left.
 *
 * @param props.server The server whose overview is shown.
 * @param props.jobs The loaded lifecycle jobs of the server.
 * @param props.operations The loaded hosting operations of the server.
 */
export function OverviewSection({
  server,
  jobs,
  operations,
}: {
  server: GameServer;
  jobs: LifecycleJob[];
  operations: HostingOperation[];
}) {
  const { t } = useTranslation();
  const now = useNow(clockIntervalMs);
  const recent = useMemo(
    () =>
      filterEntries(buildActivityEntries(jobs, operations), "all", false).slice(
        0,
        recentEntryCount,
      ),
    [jobs, operations],
  );
  const primary = primaryServerAction(server.actions, server.deployment?.possibleCost ?? false);
  const moreActions = server.actions.filter(
    (action) => action.id !== collectLogsActionId && action.id !== primary?.id,
  );
  return (
    <div className="workspace-section">
      <div className="workspace-overview__row">
        <section className="workspace-panel">
          <h2>{t("Server_Overview_DetailsTitle")}</h2>
          <dl className="workspace-facts">
            <Fact label={t("Server_Overview_Game")}>{gameLabel(server.gameIdentifier)}</Fact>
            <Fact label={t("Server_Overview_Provider")}>
              <ApiText>{server.runtimeProviderIdentifier}</ApiText>
            </Fact>
            <Fact label={t("Hosting_Detail_CloudProfile")}>
              {server.runtimeProfileDisplayName}{" "}
              <small>
                <code>{server.runtimeProfileIdentifier}</code>
              </small>
            </Fact>
            <Fact label={t("React_ServerWorkspace_ConfigurationProfile")}>
              {server.selectedConfigurationProfileTitle ?? t("React_ServerWorkspace_GameDefaults")}
            </Fact>
            <Fact label={t("Hosting_Detail_ModPackTitle")}>
              {server.selectedModPackDefinitionTitle ?? t("React_ServerWorkspace_None")}
            </Fact>
            {server.selectedTerrainTitle && (
              <Fact label={t("Server_Overview_Terrain")}>{server.selectedTerrainTitle}</Fact>
            )}
            {server.selectedScenarioTitle && (
              <Fact label={t("Server_Overview_Scenario")}>{server.selectedScenarioTitle}</Fact>
            )}
            <Fact label={t("Hosting_Detail_LaunchPolicy")}>
              {server.isLaunchDisabled
                ? t("Hosting_Detail_LaunchDisabled")
                : t("Hosting_Detail_LaunchEnabled")}
            </Fact>
            <Fact label={t("React_ServerWorkspace_Created")}>
              {formatDate(server.timeStampCreated)}
            </Fact>
            <Fact label={t("Server_Overview_Updated")}>{formatDate(server.timeStampUpdated)}</Fact>
            <AutomationFact server={server} />
          </dl>
        </section>
        <ServerMetricsPanel server={server} />
      </div>
      <section className="workspace-panel">
        <h2>{t("Server_Overview_RecentTitle")}</h2>
        {recent.length === 0 ? (
          <p className="workspace-panel__muted">{t("Server_Activity_Empty_Body")}</p>
        ) : (
          <>
            <ol className="activity-timeline__list">
              {recent.map((entry) => (
                <li key={entry.id} className="activity-timeline__item">
                  <ActivityEntry entry={entry} now={now} compact />
                </li>
              ))}
            </ol>
            <ActionLink variant="ghost" to={sectionTo(server.primaryKey, "activity")}>
              {t("Server_Activity_ShowAll")}
            </ActionLink>
          </>
        )}
      </section>
      {moreActions.length > 0 && (
        <section className="workspace-panel">
          <h2>{t("Server_Overview_ActionsTitle")}</h2>
          <ServerActions server={server} actions={moreActions} />
        </section>
      )}
    </div>
  );
}
