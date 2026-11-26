import { useRef, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { ConfirmDialog } from "@/components/primitives";
import type { ActionControlVariant } from "@/components/ActionControl";

/** Props of {@link FinanceDialog}. */
export interface FinanceDialogProps {
  /** Whether the dialog is shown. */
  open: boolean;
  /** Heading of the dialog; it names the finance action. */
  title: string;
  /** One sentence that says what the action affects. It is the accessible description. */
  description: string;
  /** Label of the confirm button. While `busy` the saving text replaces it. */
  submitLabel: string;
  /** Outcome-specific label of the dismiss button, for example "Don't record payment". */
  dismissLabel: string;
  /** Marks a request in flight: the dialog cannot be dismissed or confirmed. */
  busy?: boolean;
  /** Visual variant of the confirm button; defaults to the gold variant. */
  confirmVariant?: ActionControlVariant;
  /** Error text shown in an alert region inside the dialog. */
  error?: string | undefined;
  /** The form fields. Native `required` and `name` attributes decide what is submitted. */
  children: ReactNode;
  /**
   * Called with the values of the named fields once the browser accepted the native validation.
   *
   * @param form The submitted values of every named field inside the dialog.
   */
  onSubmit(form: FormData): void;
  /** Called when the viewer dismisses the dialog by button or Escape. */
  onClose(): void;
}

/**
 * The finance form dialog: a {@link ConfirmDialog} on the shared dialog shell that hosts the fields of one finance
 * action.
 *
 * The fields live inside the shell's form, so the native validation and the Enter key keep working, and the submitted
 * `FormData` is read from that form. The dismiss button carries an outcome-specific label instead of a generic cancel.
 *
 * @param props The dialog props.
 * @returns The modal dialog element.
 */
export function FinanceDialog({
  open,
  title,
  description,
  submitLabel,
  dismissLabel,
  busy = false,
  confirmVariant = "gold",
  error,
  children,
  onSubmit,
  onClose,
}: FinanceDialogProps) {
  const { t } = useTranslation();
  const bodyRef = useRef<HTMLDivElement>(null);
  return (
    <ConfirmDialog
      open={open}
      className="finance-dialog"
      kicker={t("HostingFinance_OwnersKicker")}
      title={title}
      description={description}
      confirmLabel={busy ? t("HostingFinance_Saving") : submitLabel}
      cancelLabel={dismissLabel}
      confirmVariant={confirmVariant}
      busy={busy}
      error={error}
      onCancel={onClose}
      onConfirm={() => {
        const form = bodyRef.current?.closest("form");
        if (form) onSubmit(new FormData(form));
      }}
    >
      <div ref={bodyRef} className="finance-dialog__body">
        {children}
      </div>
    </ConfirmDialog>
  );
}
