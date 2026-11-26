import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { useAppToast } from "@/components/AppToasts";
import { isActiveJobStatus } from "@/features/hosting/jobProgress";
import type { GameServer, LifecycleJob, ServerPendingStop } from "@/features/hosting/types";

/** What {@link useAutoStopNotice} returns. */
export interface AutoStopNoticeState {
  /** The announced automatic stop of the server, or null when none is pending or the server is not loaded. */
  pendingStop: ServerPendingStop | null;
}

/** The pending stop that was last seen together with the server it belonged to. */
interface SeenPendingStop {
  /** The id of the server the stop belongs to. */
  serverId: string;
  /** The stop as the API sent it. */
  stop: ServerPendingStop;
}

/**
 * Returns whether the server has started to stop: an active Stop job exists, or the server reports a pending job
 * that the jobs query may not have delivered yet.
 *
 * @param server The loaded server.
 * @param jobs The loaded lifecycle jobs of the server.
 */
function isStopping(server: GameServer, jobs: readonly LifecycleJob[]): boolean {
  if ((server.pendingJobCount ?? 0) > 0) return true;
  return jobs.some((job) => job.kind === "Stop" && isActiveJobStatus(job.status));
}

/**
 * Returns a function that words why an automatic stop was announced, shared by the workspace notice and the profile
 * menu row so both say the same thing.
 *
 * The reason map is closed: an unknown reason falls back to the schedule-neutral sentence (T-68-37), and an idle or
 * maximum runtime stop without its limit snapshot has no sentence, because the count would be invented.
 *
 * @returns A function from the reason and limit snapshot to the localized sentence, or null when no honest sentence
 * exists.
 */
export function useAutoStopReason(): (
  reason: string | null | undefined,
  limit: number | null | undefined,
) => string | null {
  const { t } = useTranslation();
  return (reason, limit) => {
    if (reason === "Idle") {
      return limit == null ? null : t("Server_Automation_Notice_Reason_Idle", { count: limit });
    }
    if (reason === "MaxRuntime") {
      return limit == null ? null : t("Server_Automation_Notice_Reason_Max", { count: limit });
    }
    return t("Server_Automation_Notice_Reason_Schedule");
  };
}

/**
 * Reads the pending automatic stop of a server and tells the viewer when it was cancelled.
 *
 * The stop comes from the API projection only: `automation.status.pendingStop` with the instant the API recorded.
 * When a stop that was present while this hook was mounted disappears and the server did not start stopping, one
 * info toast names the cause: someone joined for the reason `Idle`, changed settings for every other reason. A stop
 * that disappears because a Stop job started is not a cancellation and shows nothing, and neither does the first
 * render nor a change to another server. The hook has no timer and no query of its own.
 *
 * @param server The server of the workspace, or undefined while it is not loaded.
 * @param jobs The lifecycle jobs of the server.
 * @returns The pending stop, or null when there is none.
 */
export function useAutoStopNotice(
  server: GameServer | undefined,
  jobs: readonly LifecycleJob[],
): AutoStopNoticeState {
  const { t } = useTranslation();
  const { showToast } = useAppToast();
  const seen = useRef<SeenPendingStop | null>(null);
  const pendingStop = server?.automation?.status.pendingStop ?? null;
  const serverId = server?.primaryKey;
  const serverName = server?.title ?? "";
  const stopping = server === undefined ? false : isStopping(server, jobs);

  useEffect(() => {
    if (serverId === undefined) return;
    const previous = seen.current;
    seen.current = pendingStop === null ? null : { serverId, stop: pendingStop };
    if (previous === null || previous.serverId !== serverId || pendingStop !== null || stopping) {
      return;
    }
    showToast(
      previous.stop.reason === "Idle"
        ? t("Server_Automation_Toast_Cancelled_Idle", { name: serverName })
        : t("Server_Automation_Toast_Cancelled_Settings", { name: serverName }),
      "info",
    );
  }, [serverId, serverName, pendingStop, stopping, showToast, t]);

  return { pendingStop };
}
