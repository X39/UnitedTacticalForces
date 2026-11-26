import "@/components/server/server.css";
import { CostIndicator } from "@/components/server/CostIndicator";
import { ServerStateBadge } from "@/components/server/ServerStateBadge";
import type { DerivedServerState } from "@/features/hosting/serverState";

/**
 * Shows the state badge and the cost indicator of one derived server state together.
 *
 * Every surface that shows server state uses this component so the state and its cost can never be presented
 * from different derivations.
 */
export function ServerStateSummary({ derived }: { derived: DerivedServerState }) {
  return (
    <span className="server-state-summary">
      <ServerStateBadge derived={derived} />
      <CostIndicator derived={derived} />
    </span>
  );
}
