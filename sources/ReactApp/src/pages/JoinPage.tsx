import { zodResolver } from "@hookform/resolvers/zod";
import {
  ArrowRight20Regular,
  CheckmarkCircle24Regular,
  PeopleAdd24Regular,
  ShieldLock24Regular,
} from "@fluentui/react-icons";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { useEffect, type CSSProperties, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { z } from "zod";
import { identityService, queryKeys, vereinService } from "@/api/services";
import type { VereinMembershipRequestPayload } from "@/api/types";
import { formatDate } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { isOpenMembershipStatus, membershipStatusTranslationKey } from "@/app/membership";
import { useSiteImage } from "@/app/siteImages";
import { useAuth } from "@/auth/auth";
import { isVereinMember } from "@/auth/claims";
import { PageHeader, StatePanel, StatusBadge, useUnsavedChanges } from "@/components/primitives";

export const membershipApplicationSchema = z.object({
  firstName: z.string().trim().min(1).max(256),
  lastName: z.string().trim().min(1).max(256),
  dateOfBirth: z
    .string()
    .regex(/^\d{4}-\d{2}-\d{2}$/)
    .refine((value) => new Date(`${value}T00:00:00Z`).getTime() < Date.now()),
  email: z.email().max(1024),
  phoneNumber: z.string().trim().min(1).max(64),
  addressLine1: z.string().trim().min(1).max(512),
  addressLine2: z.string().trim().max(512),
  postalCode: z.string().trim().min(1).max(32),
  city: z.string().trim().min(1).max(256),
  country: z.string().trim().min(1).max(128),
  motivation: z.string().trim().min(1).max(4096),
  acknowledgesMembershipSeparation: z.boolean().refine(Boolean),
  acknowledgesDataProcessing: z.boolean().refine(Boolean),
  acknowledgesSatzung: z.boolean().refine(Boolean),
});
type MembershipForm = z.infer<typeof membershipApplicationSchema>;

export function Component() {
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const heroImage = useSiteImage("join-hero", "/media/generated/join-recruitment-staging-hero.jpg");
  const queryClient = useQueryClient();
  const request = useQuery({
    queryKey: queryKeys.membershipRequest,
    queryFn: ({ signal }) => vereinService.myMembershipRequest(signal),
    enabled: auth.isAuthenticated && !isVereinMember(auth.user),
  });
  const settings = useQuery({
    queryKey: queryKeys.vereinSettings,
    queryFn: ({ signal }) => vereinService.settings(signal),
  });
  const profile = useQuery({
    queryKey: queryKeys.profileSettings,
    queryFn: ({ signal }) => identityService.profileSettings(signal),
    enabled: auth.isAuthenticated && !isVereinMember(auth.user),
  });
  const form = useForm<MembershipForm>({
    resolver: zodResolver(membershipApplicationSchema),
    defaultValues: {
      firstName: "",
      lastName: "",
      dateOfBirth: "",
      email: auth.user?.eMail ?? "",
      phoneNumber: "",
      addressLine1: "",
      addressLine2: "",
      postalCode: "",
      city: "",
      country: t("React_Join_DefaultCountry"),
      motivation: "",
      acknowledgesMembershipSeparation: false,
      acknowledgesDataProcessing: false,
      acknowledgesSatzung: false,
    },
  });
  useEffect(() => {
    if (!profile.data || form.formState.isDirty) return;
    form.reset({
      firstName: profile.data.firstName ?? "",
      lastName: profile.data.lastName ?? "",
      dateOfBirth: profile.data.dateOfBirth ?? "",
      email: profile.data.eMail ?? auth.user?.eMail ?? "",
      phoneNumber: profile.data.phoneNumber ?? "",
      addressLine1: profile.data.addressLine1 ?? "",
      addressLine2: profile.data.addressLine2 ?? "",
      postalCode: profile.data.postalCode ?? "",
      city: profile.data.city ?? "",
      country: profile.data.country || t("React_Join_DefaultCountry"),
      motivation: "",
      acknowledgesMembershipSeparation: false,
      acknowledgesDataProcessing: false,
      acknowledgesSatzung: false,
    });
  }, [auth.user?.eMail, form, profile.data, t]);
  useUnsavedChanges(form.formState.isDirty && !form.formState.isSubmitSuccessful);
  const create = useMutation({
    mutationFn: (payload: VereinMembershipRequestPayload) =>
      vereinService.createMembershipRequest(payload),
    onSuccess: (data) => {
      queryClient.setQueryData(queryKeys.membershipRequest, data);
      form.reset();
    },
  });
  const withdraw = useMutation({
    mutationFn: (requestId: number) => vereinService.withdrawMembershipRequest(requestId),
    onSuccess: (data) => queryClient.setQueryData(queryKeys.membershipRequest, data),
  });
  const onSubmit = form.handleSubmit((values) =>
    create.mutate({ ...values, addressLine2: values.addressLine2 || null }),
  );
  return (
    <div className="join-page">
      <section
        className="editorial-hero editorial-hero--join"
        style={{ "--hero-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
      >
        <div>
          <span className="kicker">{t("Pages_Public_Join_Kicker")}</span>
          <h1>{t("Pages_Public_Join_Title")}</h1>
          <p>{t("Pages_Public_Join_Subtitle")}</p>
          <Link className="button button--glass" to="/events/upcoming">
            {t("Pages_Public_Join_ViewOperations")}
          </Link>
        </div>
      </section>
      <section className="content-section join-paths">
        <article>
          <PeopleAdd24Regular />
          <span className="kicker">{t("React_Join_CommunityKicker")}</span>
          <h2>{t("Pages_Public_Join_StartHere")}</h2>
          <p>{t("Pages_Public_Join_OperationsBoardBody")}</p>
          <Link className="text-link" to="/events/upcoming">
            {t("Pages_Public_Join_Access")}
            <ArrowRight20Regular />
          </Link>
        </article>
        <article>
          <ShieldLock24Regular />
          <span className="kicker">{t("React_Join_IdentityKicker")}</span>
          <h2>{t("Pages_Public_Join_AccountAccessTitle")}</h2>
          <p>{t("Pages_Public_Join_LoginHint")}</p>
          {auth.isAuthenticated ? (
            <StatusBadge tone="success">
              {t("Pages_Public_Join_LoggedInAs")} {auth.user?.nickname}
            </StatusBadge>
          ) : (
            <div className="login-options">
              {auth.providers.steam && (
                <button
                  type="button"
                  className="identity-button identity-button--steam"
                  onClick={() => auth.login("steam")}
                >
                  {t("Pages_Public_Join_UseSteamIdentity")}
                </button>
              )}
              {auth.providers.discord && (
                <button
                  type="button"
                  className="identity-button identity-button--discord"
                  onClick={() => auth.login("discord")}
                >
                  {t("Pages_Public_Join_UseDiscordIdentity")}
                </button>
              )}
            </div>
          )}
        </article>
      </section>
      <section className="content-section membership-application">
        <PageHeader
          kicker={t("React_Join_Form_Kicker")}
          title={t("React_Join_Form_Title")}
          description={t("React_Join_Form_Description")}
        />
        <div className="join-contribution">
          <StatusBadge
            tone={
              settings.data?.membershipContributionMonthlyAmount != null &&
              settings.data.membershipContributionAnnualAmount != null
                ? "info"
                : "warning"
            }
          >
            {settings.data?.membershipContributionMonthlyAmount != null &&
            settings.data.membershipContributionAnnualAmount != null
              ? formatResource(
                  t("Pages_Public_Join_ContributionAmountsFormat"),
                  settings.data.membershipContributionMonthlyAmount,
                  settings.data.membershipContributionAnnualAmount,
                  settings.data.membershipContributionCurrency,
                )
              : t("Pages_Public_Join_ContributionAmountPending")}
          </StatusBadge>
          <div>
            <h2>{t("Pages_Public_Join_ContributionTitle")}</h2>
            <p>
              {(settings.data &&
                (i18n.resolvedLanguage?.startsWith("de")
                  ? settings.data.membershipContributionPublicTextDe
                  : settings.data.membershipContributionPublicTextEn)) ||
                t("Pages_Public_Join_ContributionAmountPending")}
            </p>
          </div>
        </div>
        {settings.isError && (
          <StatePanel kind="warning">{t("React_Join_SettingsError")}</StatePanel>
        )}
        {!auth.isAuthenticated && (
          <StatePanel kind="forbidden" title={t("React_Join_SignInRequired_Title")}>
            {t("React_Join_SignInRequired_Body")}
          </StatePanel>
        )}
        {isVereinMember(auth.user) && (
          <StatePanel kind="success" title={t("Pages_Verein_Index_ActiveMember")}>
            <p>{t("Pages_Verein_Index_ActiveMemberDetail")}</p>
          </StatePanel>
        )}
        {request.isLoading && auth.isAuthenticated && (
          <StatePanel kind="loading">{t("Pages_Verein_Index_Loading")}</StatePanel>
        )}
        {request.isError && auth.isAuthenticated && (
          <StatePanel
            kind="error"
            action={
              <button
                className="button button--ghost"
                type="button"
                onClick={() => void request.refetch()}
              >
                {t("React_UI_Retry")}
              </button>
            }
          >
            {t("React_Join_LoadError")}
          </StatePanel>
        )}
        {profile.isError && auth.isAuthenticated && !isVereinMember(auth.user) && (
          <StatePanel kind="warning">{t("React_Join_ProfileError")}</StatePanel>
        )}
        {request.data && isOpenMembershipStatus(request.data.status) && (
          <StatePanel
            kind="info"
            title={t("Pages_Verein_Index_RequestStateOpen")}
            action={
              <button
                className="button button--danger"
                type="button"
                disabled={withdraw.isPending}
                onClick={() => withdraw.mutate(request.data!.primaryKey)}
              >
                {t("React_Join_Withdraw")}
              </button>
            }
          >
            <p>
              {formatResource(
                t("Pages_Verein_Index_RequestUpdated"),
                formatDate(request.data.timeStampUpdated),
              )}
            </p>
            <StatusBadge tone="warning">
              {t(membershipStatusTranslationKey(request.data.status))}
            </StatusBadge>
            {withdraw.isError && <p role="alert">{t("React_Join_WithdrawError")}</p>}
          </StatePanel>
        )}
        {request.data && !isOpenMembershipStatus(request.data.status) && (
          <StatePanel kind="info" title={t(membershipStatusTranslationKey(request.data.status))}>
            <p>
              {formatResource(
                t("Pages_Verein_Index_RequestUpdated"),
                formatDate(request.data.timeStampUpdated),
              )}
            </p>
            {request.data.decisionReason && <p>{request.data.decisionReason}</p>}
          </StatePanel>
        )}
        {auth.isAuthenticated &&
          !isVereinMember(auth.user) &&
          request.isSuccess &&
          !profile.isLoading &&
          (!request.data || !isOpenMembershipStatus(request.data.status)) && (
            <form className="application-form" onSubmit={onSubmit} noValidate>
              <fieldset>
                <legend>{t("React_Join_Identity_Legend")}</legend>
                <div className="form-grid">
                  <FormField
                    label={t("React_Join_FirstName")}
                    error={Boolean(form.formState.errors.firstName)}
                  >
                    <input {...form.register("firstName")} autoComplete="given-name" />
                  </FormField>
                  <FormField
                    label={t("React_Join_LastName")}
                    error={Boolean(form.formState.errors.lastName)}
                  >
                    <input {...form.register("lastName")} autoComplete="family-name" />
                  </FormField>
                  <FormField
                    label={t("React_Join_DateOfBirth")}
                    error={Boolean(form.formState.errors.dateOfBirth)}
                  >
                    <input {...form.register("dateOfBirth")} type="date" />
                  </FormField>
                  <FormField
                    label={t("React_Join_Email")}
                    error={Boolean(form.formState.errors.email)}
                  >
                    <input {...form.register("email")} type="email" autoComplete="email" />
                  </FormField>
                  <FormField
                    label={t("React_Join_Phone")}
                    error={Boolean(form.formState.errors.phoneNumber)}
                  >
                    <input {...form.register("phoneNumber")} type="tel" autoComplete="tel" />
                  </FormField>
                </div>
              </fieldset>
              <fieldset>
                <legend>{t("React_Join_Address_Legend")}</legend>
                <div className="form-grid">
                  <FormField
                    label={t("React_Join_Address1")}
                    error={Boolean(form.formState.errors.addressLine1)}
                  >
                    <input {...form.register("addressLine1")} autoComplete="address-line1" />
                  </FormField>
                  <FormField
                    label={t("React_Join_Address2")}
                    error={Boolean(form.formState.errors.addressLine2)}
                  >
                    <input {...form.register("addressLine2")} autoComplete="address-line2" />
                  </FormField>
                  <FormField
                    label={t("React_Join_PostalCode")}
                    error={Boolean(form.formState.errors.postalCode)}
                  >
                    <input {...form.register("postalCode")} autoComplete="postal-code" />
                  </FormField>
                  <FormField
                    label={t("React_Join_City")}
                    error={Boolean(form.formState.errors.city)}
                  >
                    <input {...form.register("city")} autoComplete="address-level2" />
                  </FormField>
                  <FormField
                    label={t("React_Join_Country")}
                    error={Boolean(form.formState.errors.country)}
                  >
                    <input {...form.register("country")} autoComplete="country-name" />
                  </FormField>
                </div>
              </fieldset>
              <fieldset>
                <legend>{t("React_Join_Motivation_Legend")}</legend>
                <FormField
                  label={t("React_Join_Motivation")}
                  error={Boolean(form.formState.errors.motivation)}
                >
                  <textarea {...form.register("motivation")} rows={6} />
                </FormField>
              </fieldset>
              <fieldset className="acknowledgements">
                <legend>{t("React_Join_Acknowledgements_Legend")}</legend>
                <label>
                  <input type="checkbox" {...form.register("acknowledgesMembershipSeparation")} />
                  {t("React_Join_AcknowledgeSeparation")}
                </label>
                <label>
                  <input type="checkbox" {...form.register("acknowledgesDataProcessing")} />
                  {t("React_Join_AcknowledgeData")}
                </label>
                <label>
                  <input type="checkbox" {...form.register("acknowledgesSatzung")} />
                  {t("React_Join_AcknowledgeStatutes")}
                </label>
                <Link to="/verein/satzung">{t("Pages_Verein_Index_ReadSatzung")}</Link>
              </fieldset>
              {create.isError && (
                <StatePanel kind="error">{t("React_Join_SubmitError")}</StatePanel>
              )}
              <button className="button button--gold" type="submit" disabled={create.isPending}>
                {create.isPending ? t("React_Join_Submitting") : t("React_Join_Submit")}
              </button>
            </form>
          )}
      </section>
      <section className="content-section join-assurance">
        <CheckmarkCircle24Regular />
        <div>
          <h2>{t("Pages_Public_Join_CommunityChannelsTitle")}</h2>
          <p>{t("Pages_Public_Join_CommunityChannelsBody")}</p>
        </div>
      </section>
    </div>
  );
}

function FormField({
  label,
  error,
  children,
}: {
  label: string;
  error?: boolean | undefined;
  children: ReactNode;
}) {
  const { t } = useTranslation();
  return (
    <label className="field">
      <span>{label}</span>
      {children}
      {error && <small role="alert">{t("React_Join_InvalidField")}</small>}
    </label>
  );
}
