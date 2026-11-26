import {
  ArrowSync16Regular,
  CheckmarkCircle16Regular,
  CircleOff16Regular,
  DismissCircle16Regular,
  Warning16Regular,
} from "@fluentui/react-icons";
import type { ComponentType } from "react";
import { useTranslation } from "react-i18next";
import { StatusBadge } from "@/components/primitives";
import "@/components/server/server.css";
import type { DerivedServerState, ServerState } from "@/features/hosting/serverState";

const glyphs = {
  off: { name: "CircleOff16Regular", Icon: CircleOff16Regular },
  starting: { name: "ArrowSync16Regular", Icon: ArrowSync16Regular },
  running: { name: "CheckmarkCircle16Regular", Icon: CheckmarkCircle16Regular },
  stopping: { name: "ArrowSync16Regular", Icon: ArrowSync16Regular },
  working: { name: "ArrowSync16Regular", Icon: ArrowSync16Regular },
  attention: { name: "DismissCircle16Regular", Icon: DismissCircle16Regular },
  unconfirmed: { name: "Warning16Regular", Icon: Warning16Regular },
  unknown: { name: "Warning16Regular", Icon: Warning16Regular },
} as const satisfies Record<ServerState, { name: string; Icon: ComponentType }>;

/**
 * Shows the derived state of a server as an icon plus a visible text label.
 *
 * The label is rendered as a React text child, never as HTML.
 */
export function ServerStateBadge({ derived }: { derived: DerivedServerState }) {
  const { t } = useTranslation();
  const { name, Icon } = glyphs[derived.state];
  return (
    <StatusBadge
      tone={derived.tone}
      className={`server-state-host server-state-host--${derived.tone}`}
    >
      <span className={`server-state-badge server-state-badge--${derived.state}`}>
        <span aria-hidden="true" data-glyph={name} className="server-state-badge__icon">
          <Icon />
        </span>
        {t(derived.labelKey)}
      </span>
    </StatusBadge>
  );
}
