import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { z } from "zod";
import { queryKeys } from "@/api/services";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageVereinMemberships } from "@/auth/claims";
import { PageHeader, StatePanel, StatusBadge, useUnsavedChanges } from "@/components/primitives";
import {
  createVereinSettingsPayload,
  loadVereinAdminSettings,
  parseContributionAmount,
  updateVereinAdminSettings,
  vereinAdminQueryKeys,
  type VereinAdminSettings,
} from "@/features/admin/vereinSettingsAdmin";

interface VereinSettingsForm {
  monthlyAmount: string;
  annualAmount: string;
  currency: string;
  publicTextDe: string;
  publicTextEn: string;
}

function toFormValues(settings: VereinAdminSettings): VereinSettingsForm {
  return {
    monthlyAmount: settings.membershipContributionMonthlyAmount?.toString() ?? "",
    annualAmount: settings.membershipContributionAnnualAmount?.toString() ?? "",
    currency: settings.membershipContributionCurrency || "EUR",
    publicTextDe: settings.membershipContributionPublicTextDe,
    publicTextEn: settings.membershipContributionPublicTextEn,
  };
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const canManage = canManageVereinMemberships(user);
  const schema = useMemo(
    () =>
      z
        .object({
          monthlyAmount: z.string(),
          annualAmount: z.string(),
          currency: z
            .string()
            .trim()
            .length(3, t("Pages_Command_Verein_SettingsValidationCurrency")),
          publicTextDe: z.string().max(4096, t("Pages_Command_Verein_SettingsValidationText")),
          publicTextEn: z.string().max(4096, t("Pages_Command_Verein_SettingsValidationText")),
        })
        .superRefine((values, context) => {
          if (createVereinSettingsPayload(values)) return;
          const message = t("Pages_Command_Verein_SettingsValidationAmounts");
          context.addIssue({ code: "custom", path: ["monthlyAmount"], message });
          context.addIssue({ code: "custom", path: ["annualAmount"], message });
        }),
    [t],
  );
  const form = useForm<VereinSettingsForm>({
    resolver: zodResolver(schema),
    defaultValues: {
      monthlyAmount: "",
      annualAmount: "",
      currency: "EUR",
      publicTextDe: "",
      publicTextEn: "",
    },
  });
  const query = useQuery({
    queryKey: vereinAdminQueryKeys.settings,
    queryFn: ({ signal }) => loadVereinAdminSettings(signal),
    enabled: canManage,
  });
  useEffect(() => {
    if (query.data && !form.formState.isDirty) form.reset(toFormValues(query.data));
  }, [form, query.data]);
  useUnsavedChanges(form.formState.isDirty);
  const save = useMutation({
    mutationFn: (values: VereinSettingsForm) => {
      const payload = createVereinSettingsPayload(values);
      if (!payload) throw new Error(t("Pages_Command_Verein_SettingsValidationAmounts"));
      return updateVereinAdminSettings(payload);
    },
    onSuccess: async (settings) => {
      queryClient.setQueryData(vereinAdminQueryKeys.settings, settings);
      queryClient.setQueryData(queryKeys.vereinSettings, settings);
      form.reset(toFormValues(settings));
      await queryClient.invalidateQueries({ queryKey: queryKeys.vereinSettings });
    },
  });
  const values = useWatch({ control: form.control });
  const monthly = parseContributionAmount(values.monthlyAmount ?? "");
  const annual = parseContributionAmount(values.annualAmount ?? "");
  const amountsReady = typeof monthly === "number" && typeof annual === "number";
  const amountLabel = amountsReady
    ? formatResource(
        t("Pages_Public_Join_ContributionAmountsFormat"),
        monthly,
        annual,
        values.currency?.trim().toLocaleUpperCase() || "EUR",
      )
    : t("Pages_Public_Join_ContributionAmountPending");

  if (!canManage)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Command_Verein_SettingsAccessRequiredTitle")}>
        {t("Pages_Command_Verein_SettingsAccessRequiredDescription")}
      </StatePanel>
    );

  return (
    <section className="verein-settings-admin-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_VereinSettings")}
        title={t("Pages_Command_Verein_SettingsPageTitle")}
        description={t("Pages_Command_Verein_SettingsPageDescription")}
        actions={
          <>
            <Link className="button button--ghost" to="/join">
              {t("Pages_Command_Verein_SettingsOpenJoin")}
            </Link>
            <button
              className="button button--ghost"
              type="button"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Common_Refresh")}
            </button>
          </>
        }
      />
      {query.isPending ? (
        <StatePanel kind="loading">{t("Pages_Command_Verein_Loading")}</StatePanel>
      ) : query.isError ? (
        <StatePanel
          kind="error"
          action={
            <button className="button button--ghost" onClick={() => void query.refetch()}>
              {t("Common_Refresh")}
            </button>
          }
        >
          {formatResource(t("Pages_Command_Verein_LoadError"), query.error.message)}
        </StatePanel>
      ) : (
        <>
          {save.isSuccess && (
            <StatePanel kind="success">{t("Pages_Command_Verein_SettingsSavedToast")}</StatePanel>
          )}
          {save.isError && <StatePanel kind="error">{save.error.message}</StatePanel>}
          <div className="verein-settings-admin-workspace">
            <form
              className="command-panel verein-settings-admin-form"
              onSubmit={form.handleSubmit((formValues) => save.mutate(formValues))}
            >
              <header className="command-panel__header">
                <span className="kicker">{t("Pages_Command_Verein_SettingsFormKicker")}</span>
                <h2>{t("Pages_Command_Verein_SettingsFormTitle")}</h2>
              </header>
              <div className="verein-settings-admin-form__fields">
                <label>
                  <span>{t("Pages_Command_Verein_SettingsMonthlyAmount")}</span>
                  <input inputMode="decimal" {...form.register("monthlyAmount")} />
                  {form.formState.errors.monthlyAmount && (
                    <small role="alert">{form.formState.errors.monthlyAmount.message}</small>
                  )}
                </label>
                <label>
                  <span>{t("Pages_Command_Verein_SettingsAnnualAmount")}</span>
                  <input inputMode="decimal" {...form.register("annualAmount")} />
                  {form.formState.errors.annualAmount && (
                    <small role="alert">{form.formState.errors.annualAmount.message}</small>
                  )}
                </label>
                <label>
                  <span>{t("Pages_Command_Verein_SettingsCurrency")}</span>
                  <input maxLength={3} {...form.register("currency")} />
                  {form.formState.errors.currency && (
                    <small role="alert">{form.formState.errors.currency.message}</small>
                  )}
                </label>
                <label className="is-wide">
                  <span>{t("Pages_Command_Verein_SettingsTextDe")}</span>
                  <textarea rows={6} maxLength={4096} {...form.register("publicTextDe")} />
                  {form.formState.errors.publicTextDe && (
                    <small role="alert">{form.formState.errors.publicTextDe.message}</small>
                  )}
                </label>
                <label className="is-wide">
                  <span>{t("Pages_Command_Verein_SettingsTextEn")}</span>
                  <textarea rows={6} maxLength={4096} {...form.register("publicTextEn")} />
                  {form.formState.errors.publicTextEn && (
                    <small role="alert">{form.formState.errors.publicTextEn.message}</small>
                  )}
                </label>
              </div>
              <footer>
                <button className="button button--primary" type="submit" disabled={save.isPending}>
                  {save.isPending ? t("Common_Saving") : t("Pages_Command_Verein_SettingsSave")}
                </button>
                <button
                  className="button button--ghost"
                  type="button"
                  disabled={save.isPending || !query.data}
                  onClick={() => query.data && form.reset(toFormValues(query.data))}
                >
                  {t("Common_Reset")}
                </button>
              </footer>
            </form>

            <aside className="command-panel verein-settings-admin-preview">
              <header className="command-panel__header">
                <span className="kicker">{t("Pages_Command_Verein_SettingsPreviewKicker")}</span>
                <h2>{t("Pages_Command_Verein_SettingsPreviewTitle")}</h2>
                <StatusBadge tone={amountsReady ? "info" : "warning"}>{amountLabel}</StatusBadge>
              </header>
              <div>
                <strong>{t("Pages_Command_Verein_SettingsPreviewGerman")}</strong>
                <p>
                  {values.publicTextDe?.trim() || t("Pages_Command_Verein_SettingsPreviewDefault")}
                </p>
                <strong>{t("Pages_Command_Verein_SettingsPreviewEnglish")}</strong>
                <p>
                  {values.publicTextEn?.trim() || t("Pages_Command_Verein_SettingsPreviewDefault")}
                </p>
              </div>
            </aside>
          </div>
        </>
      )}
    </section>
  );
}
