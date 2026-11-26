import { Spinner } from "@fluentui/react-components";
import type { Ref } from "react";
import { useTranslation } from "react-i18next";
import type { TranslationKey } from "@/app/localizationTypes";
import { ActionButton } from "@/components/ActionControl";
import type { AfterCreating } from "@/components/server/AfterCreatingChoice";
import type { CreateBlockReason, CreateCostEstimate } from "@/features/hosting/createServerModel";
import { formatHourlyRate, formatMoney } from "@/features/hosting/model";
import "@/components/server/create.css";

/** Translation key of the visible reason for each blocking reason; a new reason fails typecheck until it has copy. */
export const createBlockReasonKeys = {
  "owner-frozen": "Server_Create_Blocked_OwnerFrozen",
  "owner-loading": "Server_Create_Owner_Loading",
  "owner-failed": "Server_Create_Blocked_OwnerFailed",
  "options-loading": "Server_Create_Blocked_OptionsLoading",
  "options-failed": "Server_Create_Blocked_OptionsFailed",
  "no-provider": "Server_Create_Blocked_NoProvider",
  "estimate-running": "Server_Create_Blocked_Checking",
  "estimate-failed": "Server_Create_Blocked_EstimateFailed",
  "storage-misfit": "Server_Create_Blocked_Storage",
} as const satisfies { readonly [Reason in CreateBlockReason]: TranslationKey };

/** Props of {@link CreateSummary}. */
export interface CreateSummaryProps {
  /** The live cost estimate: calculating, unavailable or known. */
  estimate: CreateCostEstimate;
  /** Display label of the selected size, or undefined while none is selected. */
  sizeLabel: string | undefined;
  /** Display label of the selected storage, or undefined while none is selected. */
  storageLabel: string | undefined;
  /** Name of the cost owner who pays, or undefined when it is not known yet. */
  ownerLabel: string | undefined;
  /** The one reason that blocks the submit, or null when the server can be created. */
  reason: CreateBlockReason | null;
  /** Name of the frozen owner, interpolated into the owner-frozen reason. */
  reasonOwnerName: string | undefined;
  /** Whether the server is kept off or started after it is created; names the outcome on the submit. */
  afterCreating: AfterCreating;
  /** Whether the create request is running. */
  pending: boolean;
  /** Number of fields that failed validation on the last submit attempt. */
  invalidCount: number;
  /** Whether the last create request failed. */
  requestFailed: boolean;
  /** Receives the request error paragraph so the page can move focus to it. */
  errorRef?: Ref<HTMLParagraphElement> | undefined;
}

/** Placeholder shown for a size or storage value that is not selected yet. */
const missingValue = "—";

/**
 * Sticky cost summary and create bar of the guided create page.
 *
 * It leads with the honest estimate (never an amount for an unknown price), lists size, storage and cost owner, and
 * ends with the gold submit that names the outcome. While a blocking reason exists the submit stays focusable,
 * shows exactly one visible reason and cannot submit the form by click or by Enter in a text field. The summary
 * owns no live region; the page announces changes through its single polite region.
 */
export function CreateSummary({
  estimate,
  sizeLabel,
  storageLabel,
  ownerLabel,
  reason,
  reasonOwnerName,
  afterCreating,
  pending,
  invalidCount,
  requestFailed,
  errorRef,
}: CreateSummaryProps) {
  const { t } = useTranslation();
  const unavailableReason =
    reason === null ? undefined : t(createBlockReasonKeys[reason], { owner: reasonOwnerName });
  return (
    <aside className="create-summary" aria-labelledby="create-summary-title">
      <h2 id="create-summary-title" className="create-summary__title">
        {t("Server_Create_Cost_Title")}
      </h2>
      {estimate.kind === "calculating" && (
        <p className="create-summary__hourly">{t("Hosting_Create_Calculating")}</p>
      )}
      {estimate.kind === "unavailable" && (
        <p className="create-summary__hourly">{t("Server_Create_Cost_Unavailable")}</p>
      )}
      {estimate.kind === "known" && (
        <>
          <p className="create-summary__hourly">
            {t("Server_Create_Cost_Hourly", { hourly: formatHourlyRate(estimate.hourly) })}
          </p>
          {estimate.monthly !== undefined && (
            <p className="create-summary__monthly">
              {t("Server_Create_Cost_Monthly", { monthly: formatMoney(estimate.monthly) })}
            </p>
          )}
        </>
      )}
      <dl className="create-summary__rows">
        <dt>{t("Server_Create_Size_Label")}</dt>
        <dd>{sizeLabel ?? missingValue}</dd>
        <dt>{t("Server_Create_Storage_Label")}</dt>
        <dd>{storageLabel ?? missingValue}</dd>
        <dt>{t("Hosting_Common_CostOwner")}</dt>
        <dd>{ownerLabel ?? t("Server_Confirm_OwnerUnknown")}</dd>
      </dl>
      <p className="create-summary__while-off">{t("Server_Create_Cost_WhileOff")}</p>
      {invalidCount > 0 && (
        <p className="create-summary__alert" role="alert">
          {t("Server_Create_Invalid", { count: invalidCount })}
        </p>
      )}
      {requestFailed && (
        <p className="create-summary__alert" role="alert" tabIndex={-1} ref={errorRef}>
          {t("Server_Create_Error")}
        </p>
      )}
      <ActionButton
        type="submit"
        variant="gold"
        fullWidth
        disabled={pending}
        aria-busy={pending}
        unavailableReason={unavailableReason}
      >
        {pending ? (
          <>
            <Spinner size="tiny" /> {t("Server_Create_Submit_Pending")}
          </>
        ) : afterCreating === "start" ? (
          t("Server_Create_Submit_Start")
        ) : (
          t("Server_Create_Submit_KeepOff")
        )}
      </ActionButton>
    </aside>
  );
}
