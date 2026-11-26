import type { ReactNode, RefObject } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";

/** Props of {@link PendingChangesBar}. */
export interface PendingChangesBarProps {
  /** The number of staged changes; the bar renders nothing for zero. */
  count: number;
  /** Called when the member wants to review the staged changes. */
  onReview(): void;
  /** Called when the member wants to throw the staged changes away. */
  onDiscard(): void;
  /** Receives the Review changes button so the section can return focus to it. */
  reviewRef?: RefObject<HTMLButtonElement | null> | undefined;
  /** A message shown above the actions, for example the invalid-fields alert. */
  alert?: ReactNode | undefined;
}

/**
 * The sticky region of the Settings section that tells how many changes are staged and offers Review changes
 * and Discard changes.
 *
 * The bar holds no live region of its own; the section announces count transitions in its single polite region.
 *
 * @param props The count, the two callbacks and the optional focus ref and alert.
 */
export function PendingChangesBar({
  count,
  onReview,
  onDiscard,
  reviewRef,
  alert,
}: PendingChangesBarProps) {
  const { t } = useTranslation();
  if (count <= 0) return null;
  return (
    <div className="pending-changes-bar" role="region" aria-label={t("Server_Change_Bar_Label")}>
      <p className="pending-changes-bar__status">{t("Server_Change_Bar_Status", { count })}</p>
      {alert}
      <div className="pending-changes-bar__actions">
        <ActionButton ref={reviewRef} variant="gold" onClick={onReview}>
          {t("Server_Change_Review")}
        </ActionButton>
        <ActionButton variant="ghost" onClick={onDiscard}>
          {t("Server_Change_Discard")}
        </ActionButton>
      </div>
    </div>
  );
}
