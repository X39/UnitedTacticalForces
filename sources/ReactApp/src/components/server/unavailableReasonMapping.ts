import type { TranslationKey } from "@/app/localizationTypes";

/** Exact API reason: the caller lacks the Server Operate permission. */
export const operatePermissionReason = "Server Operate permission is required.";

/** Exact API reason: another lifecycle operation is still pending. */
export const operationPendingReason = "Another lifecycle operation is pending.";

/** Exact API reason: server launch is disabled by the server configuration. */
export const launchDisabledReason = "Server launch is disabled.";

/** Exact API reason: the action does not apply to the current deployment state. */
export const deploymentStateReason = "This action is unavailable in the current deployment state.";

/** Exact API reason: termination of the deployment is already in progress. */
export const terminationPendingReason = "Termination is already in progress.";

/** Exact API reason: lifecycle execution is paused by the platform operator. */
export const pausedReason = "Hosting lifecycle execution is paused by the operator.";

/** Localization keys a disabled-action reason can resolve to. */
export type UnavailableReasonKey = Extract<
  TranslationKey,
  | "Server_Reason_Operate"
  | "Server_Reason_Pending"
  | "Server_Reason_LaunchDisabled"
  | "Server_Reason_State"
  | "Server_Reason_StateGeneric"
  | "Server_Reason_Terminating"
  | "Server_Reason_Paused"
  | "Server_Reason_Other"
>;

/** Optional facts that refine how a reason text is mapped. */
export interface UnavailableReasonContext {
  /** Localized label of the current server state, substituted into the deployment-state reason. */
  stateLabel?: string | undefined;
  /** Whether the server is known to be launch-disabled, so any custom reason text is the launch reason. */
  launchDisabled?: boolean | undefined;
}

/** The resolved presentation of one reason: a localization key, its values and optional quoted API text. */
export interface ResolvedUnavailableReason {
  /** Localization key of the sentence to show. */
  key: UnavailableReasonKey;
  /** Interpolation values of the sentence, when it takes any. */
  values?: { state: string };
  /** Original API text to quote after the sentence because it has no localized equivalent. */
  quoted?: string;
}

const exactReasonKeys = new Map<string, UnavailableReasonKey>([
  [operatePermissionReason, "Server_Reason_Operate"],
  [operationPendingReason, "Server_Reason_Pending"],
  [launchDisabledReason, "Server_Reason_LaunchDisabled"],
  [terminationPendingReason, "Server_Reason_Terminating"],
  [pausedReason, "Server_Reason_Paused"],
]);

/**
 * Maps an API-provided reason text to localized copy.
 *
 * An exact known reason always wins. Otherwise a launch-disabled hint maps custom text to the launch-disabled
 * copy, empty text maps to the generic sentence, and any other text maps to the generic sentence with the
 * original text kept for quoting.
 *
 * @param reason The reason text sent by the API.
 * @param context Optional state label and launch-disabled hint.
 * @returns The key, values and optional quoted text to render.
 */
export function resolveUnavailableReason(
  reason: string | null | undefined,
  context: UnavailableReasonContext = {},
): ResolvedUnavailableReason {
  if (reason === deploymentStateReason) {
    return context.stateLabel
      ? { key: "Server_Reason_State", values: { state: context.stateLabel } }
      : { key: "Server_Reason_StateGeneric" };
  }
  const exactKey = reason ? exactReasonKeys.get(reason) : undefined;
  if (exactKey) return { key: exactKey };
  if (context.launchDisabled && reason) return { key: "Server_Reason_LaunchDisabled" };
  if (!reason) return { key: "Server_Reason_Other" };
  return { key: "Server_Reason_Other", quoted: reason };
}
