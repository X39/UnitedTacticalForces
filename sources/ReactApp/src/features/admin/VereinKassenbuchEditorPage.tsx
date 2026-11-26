import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useNavigate, useParams } from "react-router";
import { z } from "zod";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatCurrency, formatDate } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageVereinLedger } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { SelectField, TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import { ConfirmDialog, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { financeTones, toneOf } from "@/components/statusTones";
import { useLeaveGuard } from "@/components/useLeaveGuard";
import {
  createKassenbuchEntry,
  createKassenbuchPayload,
  deleteKassenbuchEntry,
  findKassenbuchEntry,
  kassenbuchFormValues,
  kassenbuchQueryKeys,
  todayInputValue,
  updateKassenbuchEntry,
  validateKassenbuchForm,
  type KassenbuchEntry,
  type KassenbuchFormValues,
  type KassenbuchValidationError,
} from "@/features/admin/vereinKassenbuch";
import "@/features/admin/vereinKassenbuch.css";

function defaultValues(): KassenbuchFormValues {
  return {
    bookedOn: todayInputValue(),
    direction: "Income",
    amount: "",
    currency: "EUR",
    category: "",
    description: "",
    reference: "",
    documentNumber: "",
  };
}

/** Renders the page header above a state view, so every state of the editor route keeps one `h1`. */
function EditorStateFrame({ title, children }: { title: string; children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <PageStack>
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_VereinKassenbuch")}
        title={title}
        actions={
          <ActionLink variant="ghost" to="/command/verein/kassenbuch">
            {t("Pages_Command_Verein_KassenbuchBackToLedger")}
          </ActionLink>
        }
      />
      {children}
    </PageStack>
  );
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { entryId: rawEntryId } = useParams();
  const editing = rawEntryId !== undefined;
  const entryId = editing ? Number(rawEntryId) : null;
  const validEntryId = entryId !== null && Number.isSafeInteger(entryId) && entryId >= 0;
  const authorized = canManageVereinLedger(user);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [allowNavigation, setAllowNavigation] = useState(false);
  const [completedAction, setCompletedAction] = useState<"saved" | "deleted" | null>(null);
  const validationMessages = useMemo<Record<KassenbuchValidationError, string>>(
    () => ({
      date: t("Pages_Command_Verein_LedgerFormDate"),
      amount: t("Pages_Command_Verein_KassenbuchValidationAmount"),
      currency: t("Pages_Command_Verein_KassenbuchValidationCurrency"),
      category: t("Pages_Command_Verein_KassenbuchValidationCategory"),
      description: t("Pages_Command_Verein_KassenbuchValidationDescription"),
      reference: t("Pages_Command_Verein_KassenbuchValidationReference"),
    }),
    [t],
  );
  const schema = useMemo(
    () =>
      z
        .object({
          bookedOn: z.string(),
          direction: z.enum(["Income", "Expense"]),
          amount: z.string(),
          currency: z.string(),
          category: z.string(),
          description: z.string(),
          reference: z.string(),
          documentNumber: z.string(),
        })
        .superRefine((values, context) => {
          const error = validateKassenbuchForm(values);
          if (!error) return;
          const path = error === "reference" ? "reference" : error === "date" ? "bookedOn" : error;
          context.addIssue({
            code: "custom",
            path: [path],
            message: validationMessages[error],
          });
        }),
    [validationMessages],
  );
  const form = useForm<KassenbuchFormValues>({
    resolver: zodResolver(schema),
    defaultValues: defaultValues(),
    mode: "onSubmit",
  });
  const values = useWatch({ control: form.control });
  const leaveGuard = useLeaveGuard({ dirty: form.formState.isDirty && !allowNavigation });
  const entryQuery = useQuery({
    queryKey: kassenbuchQueryKeys.entry(entryId ?? -1),
    queryFn: ({ signal }) => findKassenbuchEntry(entryId!, signal),
    enabled: authorized && editing && validEntryId,
    retry: false,
  });
  const entry = entryQuery.data;

  useEffect(() => {
    if (entry) form.reset(kassenbuchFormValues(entry));
  }, [entry, form]);

  useEffect(() => {
    if (!completedAction) return;
    navigate("/command/verein/kassenbuch", {
      replace: true,
      state:
        completedAction === "deleted" ? { kassenbuchDeleted: true } : { kassenbuchSaved: true },
    });
  }, [completedAction, navigate]);

  const finish = async (saved: KassenbuchEntry | undefined, deleted: boolean) => {
    if (saved)
      queryClient.setQueryData(
        kassenbuchQueryKeys.entry(saved.primaryKey),
        deleted ? undefined : saved,
      );
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: kassenbuchQueryKeys.all }),
      queryClient.invalidateQueries({ queryKey: kassenbuchQueryKeys.member }),
    ]);
    setAllowNavigation(true);
    form.reset(form.getValues());
    setCompletedAction(deleted ? "deleted" : "saved");
  };

  const save = useMutation({
    mutationFn: (formValues: KassenbuchFormValues) => {
      const payload = createKassenbuchPayload(formValues);
      return editing && entryId !== null
        ? updateKassenbuchEntry(entryId, payload)
        : createKassenbuchEntry(payload);
    },
    onSuccess: (saved) => void finish(saved, false),
  });
  const remove = useMutation({
    mutationFn: () => deleteKassenbuchEntry(entryId!),
    onSuccess: (deleted) => void finish(deleted, true),
    onSettled: () => setDeleteOpen(false),
  });
  const titleKey = editing
    ? "Pages_Command_Verein_KassenbuchEditPageTitle"
    : "Pages_Command_Verein_KassenbuchCreatePageTitle";
  useDocumentTitle(t(titleKey));

  if (!authorized)
    return (
      <EditorStateFrame title={t(titleKey)}>
        <ForbiddenState title={t("Pages_Command_Verein_KassenbuchAccessRequiredTitle")}>
          {t("Pages_Command_Verein_KassenbuchAccessRequiredDescription")}
        </ForbiddenState>
      </EditorStateFrame>
    );

  if (editing && (!validEntryId || (!entryQuery.isPending && !entryQuery.isError && !entry)))
    return (
      <EditorStateFrame title={t(titleKey)}>
        <EmptyState title={t("Pages_Command_Verein_KassenbuchEntryNotFoundTitle")}>
          {t("Pages_Command_Verein_KassenbuchEntryNotFoundDescription")}
        </EmptyState>
      </EditorStateFrame>
    );

  const direction = values.direction ?? "Income";
  const amount = Number(values.amount ?? 0);
  const currencyInput = values.currency?.trim().toUpperCase() ?? "";
  const currency = /^[A-Z]{3}$/.test(currencyInput) ? currencyInput : "EUR";
  const signedAmount = direction === "Expense" ? -amount : amount;
  const mutationError = save.error ?? remove.error;

  return (
    <PageStack>
      <PageHeader
        kicker={t(
          editing
            ? "Pages_Command_Verein_KassenbuchEditorKickerEdit"
            : "Pages_Command_Verein_KassenbuchEditorKickerNew",
        )}
        title={t(titleKey)}
        description={t(
          editing
            ? "Pages_Command_Verein_KassenbuchEditPageDescription"
            : "Pages_Command_Verein_KassenbuchCreatePageDescription",
        )}
        actions={
          <ActionLink variant="ghost" to="/command/verein/kassenbuch">
            {t("Pages_Command_Verein_KassenbuchBackToLedger")}
          </ActionLink>
        }
      />

      {editing && entryQuery.isPending ? (
        <LoadingState label={t("Pages_Command_Verein_Loading")} />
      ) : editing && entryQuery.isError ? (
        <ErrorState onRetry={() => void entryQuery.refetch()}>
          {formatResource(t("Pages_Command_Verein_LoadError"), entryQuery.error.message)}
        </ErrorState>
      ) : !editing || entry ? (
        <div className="verein-kassenbuch-editor-shell">
          <form
            className="command-panel verein-kassenbuch-editor-form-panel"
            aria-label={t("Pages_Command_Verein_KassenbuchEditorAria")}
            noValidate
            onSubmit={form.handleSubmit((formValues) => save.mutate(formValues))}
          >
            <header className="command-panel__header">
              <div>
                <span className="kicker">
                  {t(
                    editing
                      ? "Pages_Command_Verein_KassenbuchEditorKickerEdit"
                      : "Pages_Command_Verein_KassenbuchEditorKickerNew",
                  )}
                </span>
                <h2>
                  {t(
                    editing
                      ? "Pages_Command_Verein_KassenbuchEditorTitleEdit"
                      : "Pages_Command_Verein_KassenbuchEditorTitleNew",
                  )}
                </h2>
              </div>
              {editing && (
                <StatusBadge>{t("Pages_Command_Verein_KassenbuchEditingStatus")}</StatusBadge>
              )}
            </header>
            <div className="verein-kassenbuch-editor-form">
              <TextField
                label={t("Pages_Command_Verein_LedgerFormDate")}
                type="date"
                error={form.formState.errors.bookedOn?.message}
                {...form.register("bookedOn")}
              />
              <SelectField
                label={t("Pages_Command_Verein_LedgerFormDirection")}
                options={[
                  { value: "Income", label: t("Pages_Command_Verein_LedgerDirectionIncome") },
                  { value: "Expense", label: t("Pages_Command_Verein_LedgerDirectionExpense") },
                ]}
                {...form.register("direction")}
              />
              <TextField
                label={t("Pages_Command_Verein_LedgerFormAmount")}
                type="number"
                min="0"
                step="0.01"
                inputMode="decimal"
                error={form.formState.errors.amount?.message}
                {...form.register("amount")}
              />
              <TextField
                label={t("Pages_Command_Verein_LedgerFormCurrency")}
                maxLength={3}
                autoCapitalize="characters"
                error={form.formState.errors.currency?.message}
                {...form.register("currency")}
              />
              <TextField
                label={t("Pages_Command_Verein_LedgerFormCategory")}
                maxLength={96}
                error={form.formState.errors.category?.message}
                {...form.register("category")}
              />
              <TextField
                label={t("Pages_Command_Verein_LedgerFormDocumentNo")}
                maxLength={128}
                {...form.register("documentNumber")}
              />
              <div className="is-wide">
                <TextField
                  label={t("Pages_Command_Verein_LedgerFormDescription")}
                  maxLength={512}
                  error={form.formState.errors.description?.message}
                  {...form.register("description")}
                />
              </div>
              <div className="is-wide">
                <TextField
                  label={t("Pages_Command_Verein_LedgerFormReference")}
                  maxLength={128}
                  error={form.formState.errors.reference?.message}
                  {...form.register("reference")}
                />
              </div>

              {mutationError && (
                <div className="is-wide">
                  <StatePanel kind="error">{mutationError.message}</StatePanel>
                </div>
              )}
              <footer className="is-wide page-header__actions">
                <ActionButton
                  variant="gold"
                  type="submit"
                  disabled={save.isPending || remove.isPending}
                >
                  {save.isPending
                    ? t("Pages_Command_Verein_LedgerSaving")
                    : t(
                        editing
                          ? "Pages_Command_Verein_LedgerUpdateEntry"
                          : "Pages_Command_Verein_LedgerAddEntry",
                      )}
                </ActionButton>
                <ActionLink variant="ghost" to="/command/verein/kassenbuch">
                  {t("Pages_Command_Verein_LedgerEditorLeave_Dismiss")}
                </ActionLink>
              </footer>
            </div>
          </form>

          <aside className="command-panel verein-kassenbuch-preview-panel">
            <header className="command-panel__header">
              <div>
                <span className="kicker">{t("Pages_Command_Verein_KassenbuchPreviewKicker")}</span>
                <h2>{t("Pages_Command_Verein_KassenbuchPreviewTitle")}</h2>
              </div>
              <StatusBadge
                tone={toneOf(financeTones, direction === "Expense" ? "expense" : "income")}
              >
                {formatCurrency(Number.isFinite(signedAmount) ? signedAmount : 0, currency)}
              </StatusBadge>
            </header>
            <dl className="verein-kassenbuch-facts">
              <div>
                <dt>{t("Pages_Command_Verein_LedgerFormDate")}</dt>
                <dd>{values.bookedOn ? formatDate(`${values.bookedOn}T12:00:00`) : "—"}</dd>
              </div>
              <div>
                <dt>{t("Pages_Command_Verein_LedgerFormDirection")}</dt>
                <dd>
                  {t(
                    direction === "Expense"
                      ? "Pages_Command_Verein_LedgerDirectionExpense"
                      : "Pages_Command_Verein_LedgerDirectionIncome",
                  )}
                </dd>
              </div>
              <div>
                <dt>{t("Pages_Command_Verein_LedgerFormCategory")}</dt>
                <dd>
                  {values.category?.trim() || t("Pages_Command_Verein_KassenbuchPreviewEmpty")}
                </dd>
              </div>
              <div>
                <dt>{t("Pages_Command_Verein_KassenbuchHeaderReference")}</dt>
                <dd>
                  {values.documentNumber?.trim() ||
                    values.reference?.trim() ||
                    t("Pages_Command_Verein_KassenbuchPreviewEmpty")}
                </dd>
              </div>
            </dl>
            <div className="verein-kassenbuch-summary">
              <strong>{t("Pages_Command_Verein_LedgerFormDescription")}</strong>
              <p>
                {values.description?.trim() || t("Pages_Command_Verein_KassenbuchPreviewEmpty")}
              </p>
            </div>

            {editing && entry && (
              <section className="verein-kassenbuch-delete-panel">
                <div>
                  <strong>{t("Pages_Command_Verein_KassenbuchDeleteTitle")}</strong>
                  <p>{t("Pages_Command_Verein_KassenbuchDeleteDescription")}</p>
                </div>
                <ActionButton
                  variant="danger"
                  disabled={save.isPending || remove.isPending}
                  onClick={() => setDeleteOpen(true)}
                >
                  {t("Pages_Command_Verein_KassenbuchActionDelete")}
                </ActionButton>
              </section>
            )}
          </aside>
        </div>
      ) : null}

      <ConfirmDialog
        open={deleteOpen}
        title={t("Pages_Command_Verein_KassenbuchDeleteTitle")}
        description={`${t("Pages_Command_Verein_KassenbuchDeleteDescription")} ${formatResource(
          t("Pages_Command_Verein_KassenbuchDeleteEntrySummary"),
          values.bookedOn ? formatDate(`${values.bookedOn}T12:00:00`) : "—",
          values.category?.trim() || t("Pages_Command_Verein_KassenbuchPreviewEmpty"),
        )}`}
        confirmLabel={t("Pages_Command_Verein_KassenbuchDeleteConfirm")}
        cancelLabel={t("Pages_Command_Verein_KassenbuchDelete_Dismiss")}
        initialFocus="dismiss"
        onConfirm={() => {
          if (!remove.isPending) remove.mutate();
        }}
        onCancel={() => setDeleteOpen(false)}
      />

      <ConfirmDialog
        open={leaveGuard.blocked}
        title={t("Server_Alerts_Leave_Title")}
        description={t("React_UnsavedChanges")}
        cancelLabel={t("Server_Change_KeepEditing")}
        confirmLabel={t("Pages_Command_Verein_LedgerEditorLeave_Dismiss")}
        initialFocus="dismiss"
        onCancel={leaveGuard.stay}
        onConfirm={leaveGuard.proceed}
      />
    </PageStack>
  );
}
