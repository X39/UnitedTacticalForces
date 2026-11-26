import type { ActionControlVariant } from "@/components/ActionControl";
import type { TranslationKey } from "@/app/localizationTypes";
import type { components } from "@/generated/api";

/** Cost impact values the API reports for a server action. */
type ECostImpact = components["schemas"]["ECostImpact"];

/** Translation keys of one known server action. */
interface ActionVocabularyEntry {
  /** Key of the button label that names the action. */
  readonly label: TranslationKey;
  /** Key of the outcome-specific label of the button that declines the action. */
  readonly dismiss: TranslationKey;
  /** Key of the label shown while the action request is pending. */
  readonly pending: TranslationKey;
}

/** How an action changes the cost of a running or stopped server. */
export type ActionCostCategory = "starts" | "stops" | "keeps" | "none" | "unknown";

/** Dedicated confirmation copy keys, or the generic ones for actions without dedicated copy. */
export interface ConfirmationCopyKeys {
  readonly titleKey: TranslationKey;
  readonly bodyKey: TranslationKey;
  /** True only when the action has its own title and body copy. */
  readonly dedicated: boolean;
}

/** Closed list of the action ids this client has copy for, keyed by lowercase API action id. */
const actionKeys = {
  start: {
    label: "Server_Action_Start_Label",
    dismiss: "Server_Action_Start_Dismiss",
    pending: "Server_Action_Start_Pending",
  },
  stop: {
    label: "Server_Action_Stop_Label",
    dismiss: "Server_Action_Stop_Dismiss",
    pending: "Server_Action_Stop_Pending",
  },
  terminate: {
    label: "Server_Action_Terminate_Label",
    dismiss: "Server_Action_Terminate_Dismiss",
    pending: "Server_Action_Terminate_Pending",
  },
  restart: {
    label: "Server_Action_Restart_Label",
    dismiss: "Server_Action_Restart_Dismiss",
    pending: "Server_Action_Restart_Pending",
  },
  install: {
    label: "Server_Action_Install_Label",
    dismiss: "Server_Action_Install_Dismiss",
    pending: "Server_Action_Install_Pending",
  },
  update: {
    label: "Server_Action_Update_Label",
    dismiss: "Server_Action_Update_Dismiss",
    pending: "Server_Action_Update_Pending",
  },
  "collect-logs": {
    label: "Server_Action_CollectLogs_Label",
    dismiss: "Server_Action_CollectLogs_Dismiss",
    pending: "Server_Action_CollectLogs_Pending",
  },
} as const satisfies Record<string, ActionVocabularyEntry>;

/** A Map, never a property lookup: action ids come from the URL and the API. */
const actionVocabulary: ReadonlyMap<string, ActionVocabularyEntry> = new Map(
  Object.entries(actionKeys),
);

const dedicatedCopyKeys: ReadonlyMap<string, ConfirmationCopyKeys> = new Map([
  [
    "start",
    {
      titleKey: "Server_Confirm_Title_Start",
      bodyKey: "Server_Confirm_Body_Start",
      dedicated: true,
    },
  ],
  [
    "stop",
    {
      titleKey: "Server_Confirm_Title_Stop",
      bodyKey: "Server_Confirm_Body_Stop",
      dedicated: true,
    },
  ],
  [
    "terminate",
    {
      titleKey: "Server_Confirm_Title_Terminate",
      bodyKey: "Server_Confirm_Body_Terminate",
      dedicated: true,
    },
  ],
]);

const otherCopyKeys: ConfirmationCopyKeys = {
  titleKey: "Server_Confirm_Title_Other",
  bodyKey: "Server_Confirm_Body_Other",
  dedicated: false,
};

/** Keys indexed by the exact `ECostImpact` strings; anything else is classified as unknown. */
const costCategories: ReadonlyMap<ECostImpact, ActionCostCategory> = new Map<
  ECostImpact,
  ActionCostCategory
>([
  ["NoCostImpact", "none"],
  ["StartsCost", "starts"],
  ["StopsCost", "stops"],
  ["KeepsCostRunning", "keeps"],
  ["Unknown", "unknown"],
]);

const confirmVariants: ReadonlyMap<string, ActionControlVariant> = new Map([
  ["start", "gold"],
  ["stop", "danger"],
  ["terminate", "danger"],
]);

function normalizeActionId(actionId: string | null | undefined): string | undefined {
  return typeof actionId === "string" ? actionId.toLowerCase() : undefined;
}

/**
 * Finds the copy keys of a known action id.
 *
 * @param actionId The action id from the API or the URL, in any letter case.
 * @returns The keys for the seven known actions, otherwise nothing; prototype names such as
 * `constructor` and `__proto__` resolve to nothing.
 */
export function lookupActionVocabulary(
  actionId: string | null | undefined,
): ActionVocabularyEntry | undefined {
  const id = normalizeActionId(actionId);
  return id === undefined ? undefined : actionVocabulary.get(id);
}

/**
 * Classifies the cost impact of an action as reported by the API.
 *
 * @param action The action, or any object carrying its `costImpact`.
 * @returns `unknown` for a missing, unrecognised or non-string impact; never `none` by default.
 */
export function actionCostCategory(
  action: { costImpact?: unknown } | null | undefined,
): ActionCostCategory {
  const impact: unknown = action?.costImpact;
  if (typeof impact !== "string") return "unknown";
  return costCategories.get(impact as ECostImpact) ?? "unknown";
}

/** Whether the action starts or stops provider cost and so must go through the cost dialog. */
export function isCostBearingAction(action: { costImpact?: unknown } | null | undefined): boolean {
  const category = actionCostCategory(action);
  return category === "starts" || category === "stops";
}

/**
 * Chooses the confirm button variant: danger for the cost-stop actions and gold for start and for
 * every other action, including unknown ids.
 */
export function confirmVariantFor(actionId: string | null | undefined): ActionControlVariant {
  const id = normalizeActionId(actionId);
  return (id === undefined ? undefined : confirmVariants.get(id)) ?? "gold";
}

/**
 * Chooses the variant of an action trigger in the More actions grid of the Overview: danger for the cost-stop
 * actions and the neutral primary variant for every other action, including start and unknown ids.
 *
 * The grid never uses gold. The workspace header carries the one gold main action of the view, and the grid lists the
 * same actions again, so a gold button here would give the view a second one.
 */
export function gridVariantFor(actionId: string | null | undefined): ActionControlVariant {
  return confirmVariantFor(actionId) === "danger" ? "danger" : "primary";
}

/** Key of the outcome-specific dismiss label; unknown ids use the generic action wording. */
export function dismissKeyFor(actionId: string | null | undefined): TranslationKey {
  return lookupActionVocabulary(actionId)?.dismiss ?? "Server_Action_Other_Dismiss";
}

/** Key of the pending label shown while the request runs; unknown ids use the generic wording. */
export function pendingKeyFor(actionId: string | null | undefined): TranslationKey {
  return lookupActionVocabulary(actionId)?.pending ?? "Server_Action_Other_Pending";
}

/**
 * Chooses the title and body keys of the confirmation dialog.
 *
 * The caller uses `Server_Confirm_Title_Unknown` instead of the returned title when
 * `lookupActionVocabulary` finds nothing for the id.
 */
export function confirmationCopyKeysFor(actionId: string | null | undefined): ConfirmationCopyKeys {
  const id = normalizeActionId(actionId);
  return (id === undefined ? undefined : dedicatedCopyKeys.get(id)) ?? otherCopyKeys;
}

/** Key of the toast shown after the API accepted an action request. */
export function toastKeyFor(actionId: string | null | undefined): TranslationKey {
  const id = normalizeActionId(actionId);
  if (id === "start") return "Server_Toast_StartRequested";
  if (id === "stop" || id === "terminate") return "Server_Toast_ShutdownRequested";
  return "Server_Toast_ActionRequested";
}
