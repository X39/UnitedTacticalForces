import { useState } from "react";
import { useTranslation } from "react-i18next";
import { formatAccruedAmount } from "@/features/hosting/accruedModel";
import { deriveServerState } from "@/features/hosting/serverState";
import type { GameServer, GameServerAccruedCost } from "@/features/hosting/types";

/** What the hook remembers between renders. */
interface StoppedState {
  /** Whether the server was costing in the last render; undefined until a server has loaded. */
  costing: boolean | undefined;
  /** Whether a stop was seen and its final amount has not been announced yet. */
  pending: boolean;
  /** The current announcement; empty until a stop with a known final amount was seen. */
  message: string;
}

/**
 * Derives the polite announcement that a session stopped costing while the workspace is open.
 *
 * The state is adjusted during render, so no ref is read and no effect sets state. The first loaded server has
 * nothing to compare with, which keeps a page that opens on an already stopped session silent. When the derived
 * cost turns from costing to `none`, the stop is remembered and the message "Cost has stopped. This session cost
 * {{amount}}." is produced once the accrued value is final with a known amount, so the announced total is the
 * final one and not a lower value that was cached a moment earlier. A later start clears the message.
 *
 * @param server The loaded server, or null or undefined while it has not loaded.
 * @param accrued The accrued cost of the server, or undefined while it has not loaded.
 * @returns The localized message for a polite live region, or undefined when there is nothing to announce.
 */
export function useAccruedStoppedAnnouncement(
  server: GameServer | null | undefined,
  accrued: GameServerAccruedCost | undefined,
): string | undefined {
  const { t } = useTranslation();
  const [state, setState] = useState<StoppedState>({
    costing: undefined,
    pending: false,
    message: "",
  });
  const costing = server == null ? undefined : deriveServerState(server).cost !== "none";
  let next = state;
  if (state.costing !== costing) {
    next = {
      costing,
      pending: state.costing === true && costing === false,
      message: "",
    };
  }
  const session = accrued?.session;
  if (next.pending && session && session.isFinal && session.amount !== null) {
    next = {
      ...next,
      pending: false,
      message: t("Server_Workspace_Cost_AccruedStoppedAnnounce", {
        amount: formatAccruedAmount(session.amount, session.currency, false, t),
      }),
    };
  }
  if (next !== state) setState(next);
  return next.message === "" ? undefined : next.message;
}
