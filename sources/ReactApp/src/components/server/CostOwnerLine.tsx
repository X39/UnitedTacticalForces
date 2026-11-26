import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { StatePanel } from "@/components/primitives";
import { effectiveCostOwner } from "@/features/hosting/createServerModel";
import type { ServerCreationCostOwnerOption } from "@/features/hosting/types";
import "@/components/server/create.css";

/**
 * Resolves the name shown as the cost owner of the server being created.
 *
 * @param costOwnerId The chosen cost owner id, or null when the creator keeps their own owner.
 * @param owners The cost owners the member may use, or undefined while they load.
 * @param viewerName The signed-in user's nickname, used when no personal owner exists yet.
 * @returns The chosen owner's name for a known id, undefined for an unknown id, and for a null id the caller's own
 *   owner's name or the viewer's nickname, because the API creates the personal owner on the first create.
 */
export function costOwnerDisplayName(
  costOwnerId: string | null,
  owners: ServerCreationCostOwnerOption[] | undefined,
  viewerName: string | undefined,
): string | undefined {
  const owner = effectiveCostOwner(costOwnerId, owners);
  if (costOwnerId) {
    const selected = owner?.costOwnerId.toLocaleLowerCase() === costOwnerId.toLocaleLowerCase();
    return selected ? owner?.displayName : undefined;
  }
  return owner?.displayName ?? viewerName;
}

/** Props of {@link CostOwnerLine}. */
export interface CostOwnerLineProps {
  /** The cost owners the member may use, or undefined while they load. */
  owners: ServerCreationCostOwnerOption[] | undefined;
  /** The chosen cost owner id, or null when the creator keeps their own owner. */
  costOwnerId: string | null;
  /** The signed-in user's nickname, shown when no personal owner exists yet. */
  viewerName: string | undefined;
  /** Whether the owners are loading. */
  loading: boolean;
  /** Whether loading the owners failed. */
  failed: boolean;
  /** Retries loading the owners. */
  onRetry(): void;
  /** Opens the place where the cost owner can be changed. */
  onChange(): void;
}

/**
 * Read-only Billed to line of the create page.
 *
 * It names who pays, explains a frozen or unchecked owner before any request, and offers exactly one control: the
 * change button, or a retry button while the owners could not be loaded. Owner names render as React text only.
 */
export function CostOwnerLine({
  owners,
  costOwnerId,
  viewerName,
  loading,
  failed,
  onRetry,
  onChange,
}: CostOwnerLineProps) {
  const { t } = useTranslation();
  const changeButton = (
    <ActionButton variant="ghost" size="compact" onClick={onChange}>
      {t("Server_Create_BilledTo_Change")}
    </ActionButton>
  );

  if (loading) {
    return (
      <div className="create-owner-line">
        <p className="create-owner-line__text">{t("Server_Create_Owner_Loading")}</p>
      </div>
    );
  }
  if (failed) {
    return (
      <div className="create-owner-line">
        <StatePanel
          kind="warning"
          action={
            <ActionButton variant="ghost" size="compact" onClick={onRetry}>
              {t("Server_Create_Owner_Retry")}
            </ActionButton>
          }
        >
          {t("Server_Create_Owner_LoadFailed")}
        </StatePanel>
      </div>
    );
  }

  const effective = effectiveCostOwner(costOwnerId, owners);
  if (effective?.isFrozen) {
    return (
      <div className="create-owner-line">
        <StatePanel kind="warning" action={changeButton}>
          {t("Server_Create_Owner_Frozen", { owner: effective.displayName })}
        </StatePanel>
      </div>
    );
  }
  const name = costOwnerDisplayName(costOwnerId, owners, viewerName);
  return (
    <div className="create-owner-line">
      <p className="create-owner-line__text">
        {t("Server_Create_BilledTo", { owner: name ?? t("Server_Confirm_OwnerUnknown") })}
      </p>
      {changeButton}
    </div>
  );
}
