import { Wallet20Regular } from "@fluentui/react-icons";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useDebouncedValue } from "@/app/useDebouncedValue";
import { formatDate } from "@/app/format";
import { ActionButton } from "@/components/ActionControl";
import { useAppToast } from "@/components/AppToasts";
import { ConfirmDialog, StatePanel, StatusBadge } from "@/components/primitives";
import {
  SearchPicker,
  type PickerOption,
  type SearchPickerStatus,
} from "@/components/SearchPicker";
import { serverCostFacts } from "@/components/server/costConfirmationModel";
import { ReasonReserve } from "@/components/server/ReasonReserve";
import { SettingsPanel } from "@/components/server/SettingsPanel";
import "@/components/server/cost-owner.css";
import { normalizeOwnerKind } from "@/features/hosting/financeModel";
import { limitPickerResults, pickerResultLimit } from "@/features/hosting/pickerLimits";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer } from "@/features/hosting/types";
import {
  costOwnerTransferFailure,
  transferBlocker,
  useCostOwnerTransfer,
  type CostOwnerTransferFailure,
} from "@/features/hosting/useCostOwnerTransfer";

/** The anchor id of the panel; the location hash `#server-cost-owner` opens it. */
const costOwnerPanelId = "server-cost-owner";

/** How long the search text must rest before the owners are requested. */
const searchDebounceMs = 250;

/** The option detail line for each kind of account behind a cost owner. */
const kindKeys = {
  User: "Server_CostOwner_Kind_User",
  Verein: "Server_CostOwner_Kind_Verein",
  ExternalOrganization: "Server_CostOwner_Kind_ExternalOrganization",
} as const;

/** The dialog message for each way a transfer can fail. */
const failureKeys = {
  conflict: "Server_CostOwner_Error_Conflict",
  unavailable: "Server_CostOwner_Error_Unavailable",
  forbidden: "Server_CostOwner_Error_Forbidden",
  generic: "Server_CostOwner_Error_Generic",
} as const satisfies Record<CostOwnerTransferFailure, string>;

/** Props of {@link ServerCostOwnerPanel}. */
export interface ServerCostOwnerPanelProps {
  /** The server whose cost owner is shown. */
  server: GameServer;
}

/**
 * The Cost owner panel of the Settings section.
 *
 * The panel is a collapsible {@link SettingsPanel} with the id `server-cost-owner`. Every viewer of a server that is
 * not archived sees it: the summary names who is billed, the facts show the owner and, when the detail read carries
 * it, since when. The owner reads exactly as the cost strip resolves it through `serverCostFacts`, so a viewer who may
 * not see the owner reads "Assigned owner". A frozen owner shows a warning and a badge in the header. An archived
 * server has no panel, because the API refuses a transfer there; the owner stays visible in the cost strip.
 *
 * A member who may change the server also gets the transfer block. The owners are requested the first time the
 * picker takes focus, not when the panel opens, so a member who only reads the facts costs no request. The Transfer
 * button stays visible and says why it is unavailable: a cloud machine that still exists, pending jobs, or no owner
 * chosen yet. Pressing it opens a confirmation whose dismiss button has the initial focus; confirming sends one
 * request. A success closes the dialog, clears the picker, shows a toast and moves focus to the owner value once the
 * refetched server shows the new owner; a failure keeps the dialog open with the message of its status. The cache
 * refreshes run in `useCostOwnerTransfer`, so this panel adds none of its own.
 *
 * @param props.server The server whose cost owner is shown.
 * @returns The panel, or null for an archived server.
 */
export function ServerCostOwnerPanel({ server }: ServerCostOwnerPanelProps) {
  const { t } = useTranslation();
  const { showToast } = useAppToast();
  const ownerValueRef = useRef<HTMLElement | null>(null);
  const [query, setQuery] = useState("");
  const [selected, setSelected] = useState<PickerOption | null>(null);
  const [engaged, setEngaged] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [focusSerial, setFocusSerial] = useState(0);
  // The owner whose arrival takes focus; written when a transfer succeeds, read only by the focus effect below.
  const focusOwnerId = useRef<string | null>(null);
  const transfer = useCostOwnerTransfer(server.primaryKey);
  const trimmedQuery = query.trim();
  const debouncedQuery = useDebouncedValue(trimmedQuery, searchDebounceMs);
  const owners = useQuery({
    queryKey: hostingQueryKeys.costOwnerOptions(server.primaryKey, debouncedQuery),
    queryFn: ({ signal }) =>
      hostingService.serverCostOwnerOptions(server.primaryKey, debouncedQuery, signal),
    // The first focus of the picker starts the requests; a member who only reads the facts costs none.
    enabled: server.canModify && !server.isArchived && engaged,
    staleTime: 20_000,
    placeholderData: keepPreviousData,
  });
  const { refetch: refetchOwners } = owners;

  // After a successful transfer the facts show the owner of the refetched server; focus moves to the owner value once
  // that owner has arrived. The wanted owner lives in a ref that only the success callback and this effect touch.
  useEffect(() => {
    if (focusOwnerId.current === null || server.costOwnerId !== focusOwnerId.current) return;
    focusOwnerId.current = null;
    ownerValueRef.current?.focus();
  }, [focusSerial, server.costOwnerId]);

  if (server.isArchived) return null;

  const { owner } = serverCostFacts(server);
  const frozen = server.costOwnerIsFrozen === true;
  const since = server.costOwnerAssignedAt;
  // The plain text of the owner as the cost strip resolves it: the name, or the hidden or unknown placeholder text.
  let ownerLabel: string;
  if (owner.kind === "name") ownerLabel = owner.name ?? "";
  else if (owner.kind === "hidden") ownerLabel = t("Server_Confirm_OwnerHidden");
  else ownerLabel = t("Server_Confirm_OwnerUnknown");
  const ownerText: ReactNode = owner.kind === "name" ? <bdi>{ownerLabel}</bdi> : ownerLabel;
  const summary =
    owner.kind === "name" ? t("Server_Create_BilledTo", { owner: ownerLabel }) : ownerLabel;

  const { items: shownOwners, hasMore } = limitPickerResults(owners.data ?? []);
  const options: PickerOption[] = shownOwners.map((option) => ({
    id: option.costOwnerId,
    label: option.displayName,
    detail: option.isCurrentUser
      ? t("Server_CostOwner_YourAccount")
      : t(kindKeys[normalizeOwnerKind(option.kind)]),
    icon: <Wallet20Regular />,
    disabled: option.isFrozen ? true : undefined,
    disabledReason: option.isFrozen ? t("Server_CostOwner_OptionFrozen") : undefined,
  }));

  // The status follows the immediate text, not the debounced copy, so the listbox opens without waiting. An empty
  // answer is `ready` with no options; a failed request is `error`, so the two never look alike. Until the picker took
  // focus nothing was asked, so the picker is idle and not busy.
  let pickerStatus: SearchPickerStatus;
  if (!engaged) pickerStatus = "idle";
  else if (debouncedQuery !== trimmedQuery) pickerStatus = "loading";
  else if (owners.isError) pickerStatus = "error";
  else if (owners.isFetching || owners.data === undefined) pickerStatus = "loading";
  else pickerStatus = "ready";

  const blocker = transferBlocker(server);
  let unavailableReason: string | undefined;
  if (blocker === "running") unavailableReason = t("Server_CostOwner_Unavailable_Running");
  else if (blocker === "jobs") unavailableReason = t("Server_CostOwner_Unavailable_Jobs");
  else if (selected === null) unavailableReason = t("Server_CostOwner_Unavailable_Choose");

  const openConfirm = () => {
    if (selected === null) return;
    transfer.reset();
    setConfirmOpen(true);
  };
  const dismissConfirm = () => {
    if (transfer.isPending) return;
    setConfirmOpen(false);
    transfer.reset();
  };
  const confirmTransfer = () => {
    if (selected === null) return;
    const chosen = selected;
    transfer.mutate(
      { costOwnerId: chosen.id },
      {
        onSuccess: () => {
          setConfirmOpen(false);
          setSelected(null);
          setQuery("");
          showToast(
            t("Server_CostOwner_Toast", { owner: chosen.label, name: server.title }),
            "success",
          );
          focusOwnerId.current = chosen.id;
          setFocusSerial((serial) => serial + 1);
        },
      },
    );
  };

  const newOwnerLabel = selected?.label ?? "";
  return (
    <SettingsPanel
      id={costOwnerPanelId}
      title={t("Server_Settings_CostOwnerTitle")}
      summary={summary}
      badge={
        frozen ? (
          <StatusBadge tone="warning">{t("Server_CostOwner_Badge_Frozen")}</StatusBadge>
        ) : undefined
      }
    >
      <div className="cost-owner">
        <p className="cost-owner__intro">{t("Server_CostOwner_Intro")}</p>
        <dl className="cost-owner-facts">
          <div className="cost-owner-facts__row">
            <dt>{t("Hosting_Common_CostOwner")}</dt>
            <dd ref={ownerValueRef} tabIndex={-1}>
              {ownerText}
            </dd>
          </div>
          {since != null && (
            <div className="cost-owner-facts__row">
              <dt>{t("Server_CostOwner_Since")}</dt>
              <dd>{formatDate(since)}</dd>
            </div>
          )}
        </dl>
        {frozen && (
          <StatePanel kind="warning">
            {t("Server_CostOwner_FrozenCurrent", { owner: ownerLabel })}
          </StatePanel>
        )}
        {server.canModify ? (
          <div className="cost-owner-transfer">
            <div className="cost-owner-transfer__picker">
              <SearchPicker
                label={t("Server_CostOwner_Search")}
                placeholder={t("Server_CostOwner_SearchPlaceholder")}
                query={query}
                onQueryChange={setQuery}
                options={options}
                hasMore={hasMore}
                status={pickerStatus}
                selected={selected}
                onSelect={setSelected}
                onClear={() => setSelected(null)}
                onRetry={() => void refetchOwners()}
                onFocus={() => setEngaged(true)}
                minQueryLength={0}
                texts={{
                  loading: t("Picker_Loading"),
                  error: t("Server_CostOwner_Options_Error"),
                  retry: t("Picker_Retry"),
                  noMatches:
                    trimmedQuery === "" ? t("Server_CostOwner_NoOptions") : t("Picker_NoMatches"),
                  results: (count) => t("Picker_Results", { count }),
                  clear: t("Picker_Clear"),
                  refine: t("Picker_Refine", { count: pickerResultLimit }),
                }}
              />
            </div>
            <div className="cost-owner-transfer__action">
              <ActionButton
                variant="primary"
                unavailableReason={unavailableReason}
                onClick={openConfirm}
              >
                {t("Server_CostOwner_Transfer")}
              </ActionButton>
            </div>
            <ReasonReserve
              reasons={[
                t("Server_CostOwner_Unavailable_Running"),
                t("Server_CostOwner_Unavailable_Jobs"),
                t("Server_CostOwner_Unavailable_Choose"),
              ]}
            />
          </div>
        ) : (
          <p className="cost-owner__readonly">{t("Server_CostOwner_ReadOnly")}</p>
        )}
      </div>
      <ConfirmDialog
        open={confirmOpen && selected !== null}
        title={t("Server_CostOwner_Dialog_Title")}
        description={t("Server_CostOwner_Dialog_Body", {
          new: newOwnerLabel,
          server: server.title,
          old: ownerLabel,
        })}
        confirmLabel={
          transfer.isPending
            ? t("Server_CostOwner_Dialog_Busy")
            : t("Server_CostOwner_Dialog_Confirm")
        }
        cancelLabel={t("Server_CostOwner_Dialog_Dismiss")}
        confirmVariant="gold"
        dismissVariant="primary"
        initialFocus="dismiss"
        busy={transfer.isPending}
        error={
          transfer.isError ? t(failureKeys[costOwnerTransferFailure(transfer.error)]) : undefined
        }
        onCancel={dismissConfirm}
        onConfirm={confirmTransfer}
      >
        <ul className="cost-owner-dialog__list">
          <li>{t("Server_CostOwner_Dialog_Incurred", { old: ownerLabel })}</li>
          <li>{t("Server_CostOwner_Dialog_Alerts", { new: newOwnerLabel })}</li>
        </ul>
      </ConfirmDialog>
    </SettingsPanel>
  );
}
