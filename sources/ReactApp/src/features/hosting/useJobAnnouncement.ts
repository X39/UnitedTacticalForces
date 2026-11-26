import { useState } from "react";
import { useTranslation } from "react-i18next";
import { jobKindLabelKey } from "@/features/hosting/activityLabels";
import { jobTransitionAnnouncement } from "@/features/hosting/jobProgress";
import type { LifecycleJob } from "@/features/hosting/types";

/** What the hook remembers between renders: the snapshot the last message was derived from. */
interface AnnouncementState {
  /** The data timestamp of the snapshot; a different incoming timestamp means a newer snapshot. */
  updatedAt: number;
  /** The jobs of the snapshot; undefined until the first load completed, so nothing is announced at load. */
  jobs: readonly LifecycleJob[] | undefined;
  /** The current announcement; empty when the newest snapshot changed no job outcome. */
  message: string;
}

/**
 * Derives the polite announcement of a job that finished or failed while the workspace is open.
 *
 * The hook compares each new snapshot of the jobs with the previous one. The state is adjusted during render when
 * `dataUpdatedAt` differs from the stored value, so no effect and no synchronous state update in an effect is
 * needed. The first snapshot has nothing to compare with, which keeps a page that loads with a finished or failed
 * job silent. A snapshot without a transition clears the message, so at most one message exists per refetch and an
 * identical outcome later is announced again.
 *
 * @param jobs The loaded jobs of the server, or undefined while they have not loaded.
 * @param dataUpdatedAt The time of the newest successful fetch in epoch milliseconds; 0 when none.
 * @returns The localized message to place in a polite live region, or an empty string.
 */
export function useJobAnnouncement(
  jobs: readonly LifecycleJob[] | undefined,
  dataUpdatedAt: number,
): string {
  const { t } = useTranslation();
  // Starting at 0 stores even an already cached first snapshot silently, so the next refetch has a base to compare.
  const [state, setState] = useState<AnnouncementState>({
    updatedAt: 0,
    jobs: undefined,
    message: "",
  });
  if (state.updatedAt !== dataUpdatedAt) {
    const transition = jobs === undefined ? null : jobTransitionAnnouncement(state.jobs, jobs);
    const message =
      transition === null
        ? ""
        : t(
            transition.kind === "finished"
              ? "Server_Workspace_Announce_JobFinished"
              : "Server_Workspace_Announce_JobFailed",
            { action: t(jobKindLabelKey(transition.job.kind)) },
          );
    setState({ updatedAt: dataUpdatedAt, jobs: jobs ?? state.jobs, message });
  }
  return state.message;
}
