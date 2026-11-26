import { createContext, useContext, type RefObject } from "react";

/**
 * Where a confirmation request comes from; used for focus return and test assertions only.
 *
 * `alert-banner` is the cost-stop button of the workspace cost alert banner and `alert-notification` is the
 * cost-stop button of a cost alert row in the profile menu.
 */
export type CostConfirmationSource =
  | "workspace-primary"
  | "workspace-action"
  | "workspace-header"
  | "workspace-failure"
  | "workspace-machine"
  | "fleet-card"
  | "notification"
  | "alert-banner"
  | "alert-notification"
  | "deep-link"
  | "create";

/** One request to confirm a server action. The request never executes the action. */
export interface CostConfirmationRequest {
  /** Primary key of the server the action runs on. */
  serverId: string;
  /** Id of the action, matched case-insensitively against the server's actions. */
  actionId: string;
  /** The entry point that raised the request. */
  source: CostConfirmationSource;
  /** Element that receives focus when the dialog closes; the invoking element is the default. */
  returnFocus?: RefObject<HTMLElement | null> | undefined;
  /**
   * Called once after the API accepted the confirmed action, before the toast and the query invalidations.
   * It is never called when the dialog is dismissed or the request fails, and an exception it throws does not
   * stop the toast, the invalidations or the dialog close. A repeated request for the action that is already
   * being confirmed keeps the first request's callback.
   */
  onSubmitted?: (() => void) | undefined;
}

/** The value provided by `CostConfirmationProvider`. */
export interface CostConfirmationContextValue {
  /**
   * Opens the cost confirmation for a server action. A repeated request for the server and action that are
   * already being confirmed does nothing.
   */
  request(request: CostConfirmationRequest): void;
}

/** Context of the confirmation entry point; null outside the provider. */
export const CostConfirmationContext = createContext<CostConfirmationContextValue | null>(null);

/**
 * Returns the confirmation entry point.
 *
 * @throws Error when used outside `CostConfirmationProvider`.
 */
export function useCostConfirmation(): CostConfirmationContextValue {
  const context = useContext(CostConfirmationContext);
  if (!context)
    throw new Error("useCostConfirmation must be used inside CostConfirmationProvider.");
  return context;
}
