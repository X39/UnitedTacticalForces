import { Fragment, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import "@/components/server/automation.css";
import { formatInstant } from "@/features/hosting/costAlertFormat";
import { lookupActionVocabulary } from "@/features/hosting/actionVocabulary";
import { formatWallTime, idleStatusKey } from "@/features/hosting/automationModel";
import type { ServerAutomation } from "@/features/hosting/types";

/** The marker that stands in for a time inside a translated sentence until the sentence is split around it. */
const markerPattern = /\[\[(\w+)\]\]/;

/**
 * Builds the marker of a named part of a sentence.
 *
 * @param name The name of the part.
 * @returns The marker text to pass to the translation as the placeholder value.
 */
function marker(name: string): string {
  return `[[${name}]]`;
}

/**
 * Replaces the markers of a translated sentence by elements.
 *
 * @param sentence The translated sentence holding markers.
 * @param parts The element of every marker name; a marker without an element is dropped.
 * @returns The sentence as nodes in reading order.
 */
function fillMarkers(sentence: string, parts: Readonly<Record<string, ReactNode>>): ReactNode[] {
  return sentence.split(markerPattern).map((piece, index) =>
    // An odd index is the captured marker name; an even index is plain text between markers.
    index % 2 === 1 ? (
      <Fragment key={`${piece}-${index}`}>{parts[piece]}</Fragment>
    ) : (
      <Fragment key={`text-${index}`}>{piece}</Fragment>
    ),
  );
}

/**
 * Renders an instant the API sent as a `time` element with its absolute, localized text.
 *
 * @param instant The ISO instant, or null.
 * @returns The element; null when there is no instant.
 */
function renderInstant(instant: string | null | undefined): ReactNode {
  if (!instant) return null;
  return <time dateTime={instant}>{formatInstant(instant)}</time>;
}

/** Props of {@link AutomationStatusList}. */
export interface AutomationStatusListProps {
  /** The saved automation settings and live status of the server; the list never reflects an unsaved draft. */
  automation: ServerAutomation;
  /** The locale the times are written in, for example `en-US`. */
  locale: string;
}

/**
 * The three-row summary of the saved automation state: idle shutdown, maximum runtime and the next scheduled action.
 *
 * Every row always renders, so the list never shifts and never leaves a blank value: a setting that is off reads
 * "Off" and a server without schedules reads "No schedules.". All instants come from the control plane and are only
 * formatted here; the list runs no timers, holds no live region and never invents a countdown. An idle state the
 * client does not know yet falls back to the fixed unknown sentence.
 *
 * @param props The automation block and the locale of the times.
 * @returns A description list labelled "Current automation status".
 */
export function AutomationStatusList({ automation, locale }: AutomationStatusListProps) {
  const { t } = useTranslation();
  const { settings, status } = automation;

  const idleKey = idleStatusKey({
    available: automation.idleShutdown.available,
    enabled: settings.idleShutdownEnabled,
    state: status.idle.state,
  });
  const idleValue: ReactNode = (() => {
    switch (idleKey) {
      case "NotAvailable":
        return t("Server_Automation_Status_Idle_NotAvailable");
      case "Off":
        return t("Server_Automation_Status_Off");
      case "Players":
        return t("Server_Automation_Status_Idle_Players");
      case "Running":
        return fillMarkers(
          t("Server_Automation_Status_Idle_Running", {
            since: marker("since"),
            stops: marker("stops"),
          }),
          { since: renderInstant(status.idle.since), stops: renderInstant(status.idle.stopsAt) },
        );
      case "Armed":
        return t("Server_Automation_Status_Idle_Armed", { count: settings.idleShutdownMinutes });
      case "Unknown":
        return t("Server_Automation_Status_Idle_Unknown");
    }
  })();

  const runtimeValue: ReactNode = !settings.maxRuntimeEnabled
    ? t("Server_Automation_Status_Off")
    : status.maxRuntimeStopsAt
      ? fillMarkers(t("Server_Automation_Status_Max_Running", { time: marker("time") }), {
          time: renderInstant(status.maxRuntimeStopsAt),
        })
      : t("Server_Automation_Status_Max_Armed", { count: settings.maxRuntimeHours });

  const next = status.nextScheduled;
  const nextValue: ReactNode = (() => {
    if (next === null) return t("Server_Automation_Status_Next_None");
    const zone = settings.timeZone ?? "UTC";
    const when =
      formatWallTime(next.at, zone, locale, {
        weekday: "short",
        day: "numeric",
        month: "short",
        hour: "numeric",
        minute: "2-digit",
      }) || formatInstant(next.at);
    const action = lookupActionVocabulary(next.action);
    return (
      <time dateTime={next.at}>
        {t("Server_Automation_Status_Next_Value", {
          action: action ? t(action.label) : t("Server_Automation_Rule_Action"),
          when,
          zone,
        })}
      </time>
    );
  })();

  return (
    <dl className="automation-status" aria-label={t("Server_Automation_Status_Label")}>
      <div className="automation-status__row">
        <dt>{t("Server_Automation_Status_Idle")}</dt>
        <dd>{idleValue}</dd>
      </div>
      <div className="automation-status__row">
        <dt>{t("Server_Automation_Status_Max")}</dt>
        <dd>{runtimeValue}</dd>
      </div>
      <div className="automation-status__row">
        <dt>{t("Server_Automation_Status_Next")}</dt>
        <dd>{nextValue}</dd>
      </div>
    </dl>
  );
}
