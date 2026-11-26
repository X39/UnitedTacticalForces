import { Info20Regular } from "@fluentui/react-icons";
import { useEffect, useRef, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { ChangeDiffItem } from "@/components/server/ChangeDiffItem";
import "@/components/server/change.css";
import { ConfirmDialog } from "@/components/primitives";
import {
  summarizeResults,
  type ItemResult,
  type ReviewGroup,
  type ReviewItem,
  type ReviewItemId,
} from "@/features/hosting/serverChangeDraft";

/** Props of {@link ReviewChangesDialog}. */
export interface ReviewChangesDialogProps {
  /** Whether the dialog is open. */
  open: boolean;
  /** The title of the server, shown above the dialog title and used in the take-effect notice. */
  serverTitle: string;
  /** Whether the server is running or starting, which selects the take-effect wording. */
  running: boolean;
  /** The review groups in their fixed order. */
  groups: readonly ReviewGroup[];
  /** The number of staged changes. */
  count: number;
  /** The per-item results of the last apply run, or null before a run. */
  results: readonly ItemResult[] | null;
  /** Whether an apply run is in progress; dismissal and confirmation are blocked meanwhile. */
  busy: boolean;
  /** Visible reason Apply is unavailable, for example while a mod pack comparison loads. */
  applyBlockedReason?: ReactNode | undefined;
  /** Renders extra content below one row, for example the contents diff of a mod pack. */
  renderItemExtra?: ((item: ReviewItem) => ReactNode) | undefined;
  /** Called when the member confirms the apply or the retry. */
  onApply(): void;
  /** Called when the member keeps editing. */
  onDismiss(): void;
  /** Called when the member removes one row from the staged changes. */
  onRemoveItem(id: ReviewItemId): void;
}

/**
 * The review step of the Settings section: what will differ, when it takes effect, the per-item outcome of an
 * apply run and the Apply or retry action.
 *
 * The dialog uses the shared dialog shell; its class only sets the width. The body is one compact note and one
 * section per group with a caption heading and compact rows, so a long list scrolls inside the body while the title
 * and the actions stay in place.
 *
 * After a partial run the applied rows are gone (they left the draft), the failed and not-tried rows stay with
 * their state and a reason, the results line takes focus and the confirm becomes "Try n changes again". The
 * dialog never prints the text of an error.
 *
 * @param props The open state, the groups, the results and the callbacks.
 */
export function ReviewChangesDialog({
  open,
  serverTitle,
  running,
  groups,
  count,
  results,
  busy,
  applyBlockedReason,
  renderItemExtra,
  onApply,
  onDismiss,
  onRemoveItem,
}: ReviewChangesDialogProps) {
  const { t } = useTranslation();
  const bodyMarker = useRef<HTMLSpanElement>(null);
  const afterRemoval = useRef<{ nextId: ReviewItemId | null } | null>(null);

  // After a removal the rows re-render; then focus moves to the remove control of the item that took the
  // place of the removed one, or to the dialog title when the last row was removed.
  useEffect(() => {
    const pending = afterRemoval.current;
    if (!pending) return;
    afterRemoval.current = null;
    const dialog = bodyMarker.current?.closest("dialog");
    if (!dialog) return;
    if (pending.nextId !== null) {
      const controls = dialog.querySelectorAll<HTMLElement>("[data-remove-item]");
      for (const control of controls) {
        if (control.dataset["removeItem"] === pending.nextId) {
          control.focus();
          return;
        }
      }
    }
    const title = dialog.querySelector<HTMLElement>("h2");
    if (title) {
      title.tabIndex = -1;
      title.focus();
    }
  }, [groups]);

  const remove = (id: ReviewItemId) => {
    const ids = groups.flatMap((group) => group.items.map((item) => item.id));
    const index = ids.indexOf(id);
    afterRemoval.current = { nextId: ids[index + 1] ?? null };
    onRemoveItem(id);
  };

  const summary = results ? summarizeResults(results) : null;
  const partial = summary !== null && summary.remaining > 0 ? summary : null;
  const confirmLabel = busy
    ? t("Server_Change_Apply_Pending")
    : partial
      ? t("Server_Change_Retry", { count })
      : t("Server_Change_Apply", { count });
  const headings = {
    modPack: () => t("Server_Change_Group_ModPack"),
    profile: () => t("Server_Change_Group_Profile"),
    settings: (size: number) => t("Server_Change_Group_Settings", { count: size }),
    credentials: (size: number) => t("Server_Change_Group_Credentials", { count: size }),
  };

  return (
    <ConfirmDialog
      open={open}
      className="dialog--review"
      initialFocus="dismiss"
      confirmVariant="gold"
      kicker={serverTitle}
      title={t("Server_Change_Review")}
      description={t("Server_Change_ReviewDialog_Description")}
      bodyLabel={t("Server_Change_ReviewDialog_Body")}
      cancelLabel={t("Server_Change_KeepEditing")}
      confirmLabel={confirmLabel}
      confirmUnavailableReason={applyBlockedReason}
      error={
        partial
          ? t("Server_Change_Results_Partial", { applied: partial.applied, total: partial.total })
          : undefined
      }
      busy={busy}
      onCancel={onDismiss}
      onConfirm={onApply}
    >
      <span ref={bodyMarker} hidden />
      <p className="review-note">
        <Info20Regular aria-hidden="true" />
        <span>
          {t(running ? "Server_Change_Effect_Running" : "Server_Change_Effect_Off", {
            name: serverTitle,
          })}
        </span>
      </p>
      {groups.map((group) => (
        <section key={group.kind} className="review-group">
          <h3 className="review-group__heading">{headings[group.kind](group.items.length)}</h3>
          <ul className="review-group__list">
            {group.items.map((item) => (
              <ChangeDiffItem
                key={item.id}
                item={item}
                state={results?.find((result) => result.id === item.stepId)}
                onRemove={() => remove(item.id)}
              >
                {renderItemExtra?.(item)}
              </ChangeDiffItem>
            ))}
          </ul>
        </section>
      ))}
    </ConfirmDialog>
  );
}
