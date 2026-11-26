import { actionCostCategory, type ActionCostCategory } from "@/features/hosting/actionVocabulary";
import type { GameServer, ServerAction } from "@/features/hosting/types";

/** The API placeholder a viewer receives when they may not see the cost owner. */
export const hiddenCostOwnerName = "Assigned owner";

/** The API placeholder for a server whose cost owner cannot be resolved. */
export const unknownCostOwnerName = "Unknown owner";

/** What the estimated cost row of the confirmation shows. */
export type CostRowKind = "hidden" | "estimate" | "unavailable" | "keeps-running" | "unknown";

/** How the cost owner is presented: by name, as the hidden placeholder, or as unknown. */
export interface CostOwnerDisplay {
  kind: "name" | "hidden" | "unknown";
  /** The owner name; present only when `kind` is `name`. */
  name?: string;
}

/** Everything the confirmation dialog needs to render its facts, derived without any localization. */
export interface CostConfirmationFacts {
  /** The cost category of the action. */
  category: ActionCostCategory;
  /** Which variant of the estimated cost row to render. */
  costRow: CostRowKind;
  /** Estimated hourly cost in euro, or null when no estimate exists. */
  hourly: number | null;
  /** Estimated monthly cost in euro, or null when no estimate exists. */
  monthly: number | null;
  /** The cost owner presentation. */
  owner: CostOwnerDisplay;
  /** Expected minutes until billing stops, or null when the API gave no duration. */
  minutes: number | null;
  /** Which how-to-stop sentence to render. */
  howToStop: "start-text" | "stop-text";
}

function costRowFor(category: ActionCostCategory, hourly: number | null): CostRowKind {
  switch (category) {
    case "none":
      return "hidden";
    case "keeps":
      return "keeps-running";
    case "unknown":
      return "unknown";
    case "starts":
    case "stops":
      return hourly === null ? "unavailable" : "estimate";
  }
}

/**
 * Maps the cost owner name the API returns to its presentation.
 *
 * The confirmation dialog, the fleet card and the fleet search share this mapping so the API placeholders are
 * interpreted in one place.
 *
 * @param name The API cost owner name, which may be the hidden or the unknown placeholder.
 * @returns `hidden` for the hidden placeholder, `unknown` for the unknown placeholder or a missing or empty
 *   name, and otherwise `name` with the owner name.
 */
export function ownerFor(name: string | null | undefined): CostOwnerDisplay {
  if (name === hiddenCostOwnerName) return { kind: "hidden" };
  if (!name || name === unknownCostOwnerName) return { kind: "unknown" };
  return { kind: "name", name };
}

function minutesFor(seconds: number | null | undefined): number | null {
  if (typeof seconds !== "number" || !Number.isFinite(seconds) || seconds <= 0) return null;
  return Math.max(1, Math.round(seconds / 60));
}

/** The server-level cost facts the confirmation dialog and the workspace cost strip share. */
export interface ServerCostFacts {
  /** The cost owner presentation. */
  owner: CostOwnerDisplay;
  /** Estimated hourly cost in euro, or null when no estimate exists. */
  hourly: number | null;
  /** Estimated monthly cost in euro, or null when no estimate exists. */
  monthly: number | null;
}

/**
 * Derives the cost owner and the hourly and monthly estimates of a server.
 *
 * The confirmation dialog and the workspace cost strip both read these values through this function, so the two
 * can never show different numbers or owners for the same server.
 *
 * @param server The server whose cost facts are needed.
 * @returns The owner presentation, the hourly estimate (deployment before server) and the monthly estimate.
 */
export function serverCostFacts(server: GameServer): ServerCostFacts {
  return {
    owner: ownerFor(server.costOwnerName),
    hourly: server.deployment?.estimatedHourlyCostEur ?? server.estimatedHourlyCostEur ?? null,
    monthly: server.estimatedMonthlyCostEur ?? null,
  };
}

/**
 * Derives the facts shown by the cost confirmation from a server and one of its actions.
 *
 * @param server The server the action runs on.
 * @param action The action that is being confirmed.
 * @returns Plain facts; the dialog maps them to localized sentences.
 */
export function buildCostConfirmationFacts(
  server: GameServer,
  action: ServerAction,
): CostConfirmationFacts {
  const category = actionCostCategory(action);
  const { owner, hourly, monthly } = serverCostFacts(server);
  return {
    category,
    costRow: costRowFor(category, hourly),
    hourly,
    monthly,
    owner,
    minutes: minutesFor(action.expectedDurationSeconds),
    howToStop: category === "stops" ? "stop-text" : "start-text",
  };
}
