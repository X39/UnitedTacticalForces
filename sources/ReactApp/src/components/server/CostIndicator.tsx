import { CheckmarkCircle16Regular, Money16Regular, Warning16Regular } from "@fluentui/react-icons";
import type { ComponentType } from "react";
import { useTranslation } from "react-i18next";
import "@/components/server/server.css";
import type { DerivedServerState, ServerCost } from "@/features/hosting/serverState";

const glyphs = {
  live: { name: "Money16Regular", Icon: Money16Regular },
  maybe: { name: "Warning16Regular", Icon: Warning16Regular },
  none: { name: "CheckmarkCircle16Regular", Icon: CheckmarkCircle16Regular },
} as const satisfies Record<ServerCost, { name: string; Icon: ComponentType }>;

/**
 * Shows whether a server is costing money now, may still cost money, or is not costing money.
 *
 * Renders nothing when the derived state says no cost information is available. The glyph is decorative; the
 * cost is always stated in visible text.
 */
export function CostIndicator({ derived }: { derived: DerivedServerState }) {
  const { t } = useTranslation();
  if (!derived.costVisible) return null;
  const { name, Icon } = glyphs[derived.cost];
  return (
    <span
      className={`server-cost-indicator server-cost-indicator--${derived.cost}`}
      data-cost={derived.cost}
    >
      <span aria-hidden="true" data-glyph={name} className="server-cost-indicator__icon">
        <Icon />
      </span>
      {t(derived.costLabelKey)}
    </span>
  );
}
