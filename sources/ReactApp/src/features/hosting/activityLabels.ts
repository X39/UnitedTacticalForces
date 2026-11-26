import type { TranslationKey } from "@/app/localizationTypes";

/**
 * How the actor of an activity entry is presented.
 *
 * `name` carries the display name, `platform` is the system actor, `hidden` is a member the viewer may not see and
 * `server` is the dedicated worker that reports log entries.
 */
export type ActorDisplay =
  { kind: "name"; name: string } | { kind: "platform" } | { kind: "hidden" } | { kind: "server" };

/**
 * The placeholder the API sends instead of the display name of a user the viewer may not see, for example a deleted
 * member.
 *
 * This is the mirror of the hidden-member placeholder that the API projects on job and operation actors; both sides
 * use the same literal, as `hiddenCostOwnerName` does for the cost owner.
 */
export const hiddenActorName = "Hidden member";

/**
 * Maps the actor name of a job or operation to its presentation.
 *
 * The UI never infers an actor from any other field: `null` is the platform itself, the hidden-member placeholder
 * (or a blank name) is a member the viewer may not see, and any other value is shown as the name.
 *
 * @param name The actor display name from the API, or null or undefined for the platform.
 * @returns `platform` for a missing name, `hidden` for the placeholder or a blank name, otherwise `name` with the
 *   value unchanged.
 */
export function actorFor(name: string | null | undefined): ActorDisplay {
  if (name === null || name === undefined) return { kind: "platform" };
  if (name === hiddenActorName || name.trim() === "") return { kind: "hidden" };
  return { kind: "name", name };
}

const actorLabelKeys = new Map<Exclude<ActorDisplay["kind"], "name">, TranslationKey>([
  ["platform", "Server_Activity_Actor_Platform"],
  ["hidden", "Server_Activity_Actor_Hidden"],
  ["server", "Server_Activity_Actor_Server"],
]);

/**
 * Returns the localization key of an actor that is not shown by name.
 *
 * @param kind The platform, hidden or server actor kind.
 * @returns The key of the actor label, for example `Server_Activity_Actor_Platform`.
 */
export function actorLabelKey(kind: Exclude<ActorDisplay["kind"], "name">): TranslationKey {
  return actorLabelKeys.get(kind) ?? "Server_Activity_Actor_Platform";
}

const jobKindLabelKeys = new Map<string, TranslationKey>([
  ["Start", "Server_Action_Start_Label"],
  ["Stop", "Server_Action_Stop_Label"],
  ["Terminate", "Server_Action_Terminate_Label"],
  ["Restart", "Server_Action_Restart_Label"],
  ["Install", "Server_Action_Install_Label"],
  ["Update", "Server_Action_Update_Label"],
  ["CollectLogs", "Server_Action_CollectLogs_Label"],
  ["Custom", "Server_Activity_Kind_Custom"],
]);

/**
 * Returns the localization key of a lifecycle job kind label.
 *
 * Progress, failure and activity surfaces share this map so a job is named the same everywhere. The kind is an
 * API-originated string and is only ever looked up in a `Map`, never used to index an object.
 *
 * @param kind The job kind from the API, which may be a value this client does not know yet.
 * @returns The `Server_Action_*_Label` key for a known action kind, `Server_Activity_Kind_Custom` for a custom
 *   action and `Server_Activity_Kind_Unknown` for every other value.
 */
export function jobKindLabelKey(kind: string | null | undefined): TranslationKey {
  if (kind === null || kind === undefined) return "Server_Activity_Kind_Unknown";
  return jobKindLabelKeys.get(kind) ?? "Server_Activity_Kind_Unknown";
}

/** The localization keys of the trigger line; the two plural keys are given without their `_one` or `_other` suffix. */
export type TriggerKey =
  | "Server_Activity_Trigger_Idle"
  | "Server_Activity_Trigger_Max"
  | "Server_Activity_Trigger_Schedule";

/** The trigger line of an automatic job: the localization key and the `count` of a plural key. */
export interface TriggerDisplay {
  /** The key of the line, for example `Server_Activity_Trigger_Max`. */
  key: TriggerKey;
  /** Minutes for the idle line and hours for the maximum runtime line; null for the schedule line. */
  count: number | null;
}

/**
 * Maps the trigger of a lifecycle job to its trigger line.
 *
 * The trigger is an API-originated string and is only compared against the closed list below. An absent or `Manual`
 * trigger, a value this client does not know yet, and an idle or maximum runtime trigger without a usable limit all
 * return null, so the actor line stays and nothing is invented.
 *
 * @param trigger The job trigger from the API, which may be absent on an older API.
 * @param limit The minutes of an idle trigger or the hours of a maximum runtime trigger.
 * @returns The trigger line, or null when the job shows its actor instead.
 */
export function triggerFor(
  trigger: string | null | undefined,
  limit: number | null | undefined,
): TriggerDisplay | null {
  const count = typeof limit === "number" && Number.isFinite(limit) ? limit : null;
  switch (trigger) {
    case "Idle":
      return count === null ? null : { key: "Server_Activity_Trigger_Idle", count };
    case "MaxRuntime":
      return count === null ? null : { key: "Server_Activity_Trigger_Max", count };
    case "Schedule":
      return { key: "Server_Activity_Trigger_Schedule", count: null };
    default:
      return null;
  }
}
