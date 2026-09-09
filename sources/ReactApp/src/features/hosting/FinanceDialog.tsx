import { useEffect, useRef, type FormEvent, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";

export function FinanceDialog({
  open,
  title,
  submitLabel,
  busy = false,
  children,
  onSubmit,
  onClose,
}: {
  open: boolean;
  title: string;
  submitLabel: string;
  busy?: boolean;
  children: ReactNode;
  onSubmit(event: FormEvent<HTMLFormElement>): void;
  onClose(): void;
}) {
  const { t } = useTranslation();
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    if (open && !dialogRef.current?.open) dialogRef.current?.showModal();
    if (!open && dialogRef.current?.open) dialogRef.current.close();
  }, [open]);

  return (
    <dialog
      ref={dialogRef}
      className="dialog finance-dialog"
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
    >
      <form onSubmit={onSubmit}>
        <h2>{title}</h2>
        <div className="finance-dialog__body">{children}</div>
        <div className="dialog__actions">
          <ActionButton variant="ghost" onClick={onClose} disabled={busy}>
            {t("Common_Cancel")}
          </ActionButton>
          <ActionButton variant="primary" type="submit" disabled={busy}>
            {busy ? t("HostingFinance_Saving") : submitLabel}
          </ActionButton>
        </div>
      </form>
    </dialog>
  );
}
