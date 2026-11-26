import { zodResolver } from "@hookform/resolvers/zod";
import {
  ArrowRight20Regular,
  CheckmarkCircle24Regular,
  PeopleAdd24Regular,
  ShieldLock24Regular,
} from "@fluentui/react-icons";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { useEffect, type CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { z } from "zod";
import { identityService, queryKeys, vereinService } from "@/api/services";
import type { VereinMembershipRequest, VereinMembershipRequestPayload } from "@/api/types";
import { formatDate } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { isOpenMembershipStatus, membershipStatusTranslationKey } from "@/app/membership";
import { useSiteImage } from "@/app/siteImages";
import { useAuth } from "@/auth/auth";
import { isVereinMember } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { CheckboxField, TextAreaField, TextField } from "@/components/FormFields";
import { PublicHero } from "@/components/PublicHero";
import { PageHeader, StatePanel, StatusBadge, useUnsavedChanges } from "@/components/primitives";
import { ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";

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
  /** The shared invalid message of a field, or undefined while the field is valid. */
  const fieldError = (name: keyof MembershipForm): string | undefined =>
    form.formState.errors[name] ? t("React_Join_InvalidField") : undefined;
  return (
    <div className="join-page public-page">
      <PublicHero
        className="editorial-hero"
        style={{ "--hero-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
        kicker={t("Pages_Public_Join_Kicker")}
        title={t("Pages_Public_Join_Title")}
        description={t("Pages_Public_Join_Subtitle")}
        actions={
          <>
            <ActionLink variant="gold" to="/events/upcoming">
              {t("Pages_Public_Join_ViewOperations")}
            </ActionLink>
            <ActionLink variant="glass" to="/verein">
              {t("Pages_Public_Join_OpenVerein")}
            </ActionLink>
          </>
        }
        asideClassName="join-process-panel"
        asideLabel={t("Pages_Public_Join_ProcessAria")}
        aside={
          <>
            <div className="public-hero__panel-heading">
              <PeopleAdd24Regular />
              <div>
                <span>{t("Pages_Public_Join_ProcessKicker")}</span>
                <strong>{t("Pages_Public_Join_ProcessTitle")}</strong>
              </div>
            </div>
            <ol className="join-mini-steps">
              <li>{t("Pages_Public_Join_MiniStep1")}</li>
              <li>{t("Pages_Public_Join_MiniStep2")}</li>
              <li>{t("Pages_Public_Join_MiniStep3")}</li>
            </ol>
          </>
        }
      />
      <section className="content-section public-section join-path-section">
        <PageHeader
          kicker={t("Pages_Public_Join_StepperKicker")}
          title={t("Pages_Public_Join_StepperTitle")}
          headingLevel={2}
        />
        <div className="join-paths">
          <article>
            <div className="join-paths__heading">
              <PeopleAdd24Regular />
              <StatusBadge tone="success">{t("Pages_Public_Join_GuestPathState")}</StatusBadge>
            </div>
            <h3>{t("Pages_Public_Join_GuestPathTitle")}</h3>
            <p>{t("Pages_Public_Join_GuestPathBody")}</p>
            <ol className="join-stepper">
              {([1, 2, 3] as const).map((step) => (
                <li key={step}>
                  <span>0{step}</span>
                  <div>
                    <strong>{t(`Pages_Public_Join_GuestStep${step}`)}</strong>
                    <p>{t(`Pages_Public_Join_GuestStep${step}Body`)}</p>
                  </div>
                </li>
              ))}
            </ol>
            <Link className="text-link" to="/events/upcoming">
              {t("Pages_Public_Join_Access")}
              <ArrowRight20Regular />
            </Link>
          </article>
          <article>
            <div className="join-paths__heading">
              <ShieldLock24Regular />
              <StatusBadge tone="info">{t("Pages_Public_Join_VereinPathState")}</StatusBadge>
            </div>
            <h3>{t("Pages_Public_Join_VereinPathTitle")}</h3>
            <p>{t("Pages_Public_Join_VereinPathBody")}</p>
            <ol className="join-stepper">
              {([1, 2, 3, 4, 5] as const).map((step) => (
                <li key={step}>
                  <span>0{step}</span>
                  <div>
                    <strong>{t(`Pages_Public_Join_VereinStep${step}`)}</strong>
                    <p>{t(`Pages_Public_Join_VereinStep${step}Body`)}</p>
                  </div>
                </li>
              ))}
            </ol>
          </article>
        </div>
      </section>
      <section className="content-section public-section membership-application">
        <PageHeader
          kicker={t("Pages_Public_Join_RequestKicker")}
          title={t("Pages_Public_Join_RequestTitle")}
          description={t("Pages_Public_Join_FormBody")}
          headingLevel={2}
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
          <div className="join-sign-in-panel">
            <ForbiddenState title={t("Pages_Public_Join_LoginRequiredTitle")}>
              {t("Pages_Public_Join_LoginHint")}
            </ForbiddenState>
            <div className="login-options" aria-label={t("Pages_Public_Join_AccountAccessTitle")}>
              {auth.providers.steam && (
                <ActionButton
                  className="identity-button identity-button--steam"
                  onClick={() => auth.login("steam")}
                >
                  {t("Pages_Public_Join_UseSteamIdentity")}
                </ActionButton>
              )}
              {auth.providers.discord && (
                <ActionButton
                  className="identity-button identity-button--discord"
                  onClick={() => auth.login("discord")}
                >
                  {t("Pages_Public_Join_UseDiscordIdentity")}
                </ActionButton>
              )}
            </div>
          </div>
        )}
        {isVereinMember(auth.user) && (
          <StatePanel kind="success" title={t("Pages_Verein_Index_ActiveMember")}>
            <p>{t("Pages_Verein_Index_ActiveMemberDetail")}</p>
            <div className="inline-actions">
              <Link className="text-link" to="/verein">
                {t("Pages_Public_Join_OpenVerein")}
              </Link>
              <Link className="text-link" to="/settings">
                {t("Pages_Public_Join_OpenSettings")}
              </Link>
            </div>
          </StatePanel>
        )}
        {request.isLoading && auth.isAuthenticated && (
          <LoadingState label={t("Pages_Verein_Index_Loading")} />
        )}
        {request.isError && auth.isAuthenticated && (
          <ErrorState onRetry={() => void request.refetch()}>
            {t("React_Join_LoadError")}
          </ErrorState>
        )}
        {profile.isError && auth.isAuthenticated && !isVereinMember(auth.user) && (
          <StatePanel kind="warning">{t("React_Join_ProfileError")}</StatePanel>
        )}
        {request.data && isOpenMembershipStatus(request.data.status) && (
          <StatePanel
            kind="info"
            title={t("Pages_Verein_Index_RequestStateOpen")}
            action={
              <ActionButton
                variant="danger"
                disabled={withdraw.isPending}
                onClick={() => withdraw.mutate(request.data!.primaryKey)}
              >
                {t("React_Join_Withdraw")}
              </ActionButton>
            }
          >
            <MembershipRequestMeta request={request.data} />
            <StatusBadge tone="warning">
              {t(membershipStatusTranslationKey(request.data.status))}
            </StatusBadge>
            {withdraw.isError && <p role="alert">{t("React_Join_WithdrawError")}</p>}
          </StatePanel>
        )}
        {request.data && !isOpenMembershipStatus(request.data.status) && (
          <StatePanel kind="info" title={t(membershipStatusTranslationKey(request.data.status))}>
            <MembershipRequestMeta request={request.data} />
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
                  <TextField
                    label={t("React_Join_FirstName")}
                    required
                    error={fieldError("firstName")}
                    {...form.register("firstName")}
                    autoComplete="given-name"
                  />
                  <TextField
                    label={t("React_Join_LastName")}
                    required
                    error={fieldError("lastName")}
                    {...form.register("lastName")}
                    autoComplete="family-name"
                  />
                  <TextField
                    label={t("React_Join_DateOfBirth")}
                    required
                    error={fieldError("dateOfBirth")}
                    {...form.register("dateOfBirth")}
                    type="date"
                  />
                  <TextField
                    label={t("React_Join_Email")}
                    required
                    error={fieldError("email")}
                    {...form.register("email")}
                    type="email"
                    autoComplete="email"
                  />
                  <TextField
                    label={t("React_Join_Phone")}
                    required
                    error={fieldError("phoneNumber")}
                    {...form.register("phoneNumber")}
                    type="tel"
                    autoComplete="tel"
                  />
                </div>
              </fieldset>
              <fieldset>
                <legend>{t("React_Join_Address_Legend")}</legend>
                <div className="form-grid">
                  <TextField
                    label={t("React_Join_Address1")}
                    required
                    error={fieldError("addressLine1")}
                    {...form.register("addressLine1")}
                    autoComplete="address-line1"
                  />
                  <TextField
                    label={t("React_Join_Address2")}
                    error={fieldError("addressLine2")}
                    {...form.register("addressLine2")}
                    autoComplete="address-line2"
                  />
                  <TextField
                    label={t("React_Join_PostalCode")}
                    required
                    error={fieldError("postalCode")}
                    {...form.register("postalCode")}
                    autoComplete="postal-code"
                  />
                  <TextField
                    label={t("React_Join_City")}
                    required
                    error={fieldError("city")}
                    {...form.register("city")}
                    autoComplete="address-level2"
                  />
                  <TextField
                    label={t("React_Join_Country")}
                    required
                    error={fieldError("country")}
                    {...form.register("country")}
                    autoComplete="country-name"
                  />
                </div>
              </fieldset>
              <fieldset>
                <legend>{t("React_Join_Motivation_Legend")}</legend>
                <TextAreaField
                  label={t("React_Join_Motivation")}
                  required
                  error={fieldError("motivation")}
                  {...form.register("motivation")}
                  rows={6}
                />
              </fieldset>
              <fieldset className="acknowledgements">
                <legend>{t("React_Join_Acknowledgements_Legend")}</legend>
                <CheckboxField
                  label={t("React_Join_AcknowledgeSeparation")}
                  aria-required="true"
                  error={fieldError("acknowledgesMembershipSeparation")}
                  {...form.register("acknowledgesMembershipSeparation")}
                />
                <CheckboxField
                  label={t("React_Join_AcknowledgeData")}
                  aria-required="true"
                  error={fieldError("acknowledgesDataProcessing")}
                  {...form.register("acknowledgesDataProcessing")}
                />
                <CheckboxField
                  label={t("React_Join_AcknowledgeStatutes")}
                  aria-required="true"
                  error={fieldError("acknowledgesSatzung")}
                  {...form.register("acknowledgesSatzung")}
                />
                <Link to="/verein/satzung">{t("Pages_Verein_Index_ReadSatzung")}</Link>
              </fieldset>
              {create.isError && (
                <StatePanel kind="error">{t("React_Join_SubmitError")}</StatePanel>
              )}
              <ActionButton variant="gold" type="submit" disabled={create.isPending}>
                {create.isPending ? t("React_Join_Submitting") : t("React_Join_Submit")}
              </ActionButton>
            </form>
          )}
      </section>
      <section className="content-section public-section join-assurance">
        <CheckmarkCircle24Regular />
        <div>
          <h2>{t("Pages_Public_Join_CommunityChannelsTitle")}</h2>
          <p>{t("Pages_Public_Join_CommunityChannelsBody")}</p>
        </div>
      </section>
    </div>
  );
}

function MembershipRequestMeta({ request }: { request: VereinMembershipRequest }) {
  const { t } = useTranslation();
  return (
    <dl className="membership-request-meta">
      <div>
        <dt>{t("Pages_Public_Join_RequestSubmitted")}</dt>
        <dd>{formatDate(request.timeStampCreated)}</dd>
      </div>
      <div>
        <dt>{t("Pages_Public_Join_RequestUpdated")}</dt>
        <dd>{formatDate(request.timeStampUpdated)}</dd>
      </div>
      <div>
        <dt>{t("Pages_Public_Join_RequestApplicant")}</dt>
        <dd>
          {request.firstName} {request.lastName}
        </dd>
      </div>
    </dl>
  );
}
