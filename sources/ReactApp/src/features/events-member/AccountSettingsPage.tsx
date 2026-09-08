import {
  ArrowSync20Regular,
  CloudArrowUp24Regular,
  ShieldTask24Regular,
} from "@fluentui/react-icons";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState, type ChangeEvent, type ReactNode } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { z } from "zod";
import { getApiClient } from "@/api/client";
import { identityService, queryKeys } from "@/api/services";
import type { ProfileSettings } from "@/api/types";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatResource } from "@/app/i18n";
import { localReturnUrl, useAuth } from "@/auth/auth";
import { useAppToastOnChange } from "@/components/AppToasts";
import {
  ConfirmDialog,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { eventsMemberQueryKeys, eventsMemberService, submitAccountDeletion } from "./services";
import type { ProfileSettingsFormValues, ProfileSettingsUpdate } from "./types";

const optionalText = (maximum: number) => z.string().max(maximum);
export const profileSettingsSchema = z.object({
  nickname: z.string().trim().min(1).max(1024),
  eMail: z.union([z.literal(""), z.email()]).pipe(z.string().max(1024)),
  firstName: optionalText(256),
  lastName: optionalText(256),
  dateOfBirth: z
    .string()
    .refine(
      (value) =>
        !value ||
        (/^\d{4}-\d{2}-\d{2}$/.test(value) &&
          !Number.isNaN(new Date(`${value}T00:00:00Z`).getTime())),
    ),
  phoneNumber: optionalText(64),
  addressLine1: optionalText(512),
  addressLine2: optionalText(512),
  postalCode: optionalText(32),
  city: optionalText(256),
  country: optionalText(128),
});

const emptyForm: ProfileSettingsFormValues = {
  nickname: "",
  eMail: "",
  firstName: "",
  lastName: "",
  dateOfBirth: "",
  phoneNumber: "",
  addressLine1: "",
  addressLine2: "",
  postalCode: "",
  city: "",
  country: "",
};

export function profileSettingsToForm(settings: ProfileSettings): ProfileSettingsFormValues {
  return {
    nickname: settings.nickname,
    eMail: settings.eMail ?? "",
    firstName: settings.firstName ?? "",
    lastName: settings.lastName ?? "",
    dateOfBirth: settings.dateOfBirth ?? "",
    phoneNumber: settings.phoneNumber ?? "",
    addressLine1: settings.addressLine1 ?? "",
    addressLine2: settings.addressLine2 ?? "",
    postalCode: settings.postalCode ?? "",
    city: settings.city ?? "",
    country: settings.country ?? "",
  };
}

export function profileFormToPayload(
  values: ProfileSettingsFormValues,
  avatar?: { bytes: string; mimeType: string },
): ProfileSettingsUpdate {
  return {
    avatar: avatar?.bytes ?? null,
    avatarMimeType: avatar?.mimeType ?? null,
    nickname: values.nickname.trim(),
    eMail: values.eMail,
    firstName: values.firstName,
    lastName: values.lastName,
    dateOfBirth: values.dateOfBirth || null,
    phoneNumber: values.phoneNumber,
    addressLine1: values.addressLine1,
    addressLine2: values.addressLine2,
    postalCode: values.postalCode,
    city: values.city,
    country: values.country,
  };
}

export async function avatarFileToPayload(
  file: File,
): Promise<{ bytes: string; mimeType: string } | undefined> {
  const mimeType = file.type.toLocaleLowerCase();
  if (
    !new Set(["image/png", "image/jpeg", "image/webp", "image/avif", "image/bmp", "image/gif"]).has(
      mimeType,
    )
  )
    return undefined;
  if (file.size > 8 * 1024 * 1024) return undefined;
  const bytes = new Uint8Array(await file.arrayBuffer());
  let binary = "";
  for (const value of bytes) binary += String.fromCharCode(value);
  return { bytes: btoa(binary), mimeType };
}

function avatarSource(
  settings: ProfileSettings,
  pendingAvatar?: { bytes: string; mimeType: string },
) {
  const bytes = pendingAvatar?.bytes ?? settings.avatar;
  const mimeType = pendingAvatar?.mimeType ?? settings.avatarMimeType;
  return bytes && mimeType ? `data:${mimeType};base64,${bytes}` : undefined;
}

export function AccountSettingsPage() {
  const { t } = useTranslation();
  const auth = useAuth();
  const queryClient = useQueryClient();
  const [avatar, setAvatar] = useState<{ bytes: string; mimeType: string }>();
  const [avatarError, setAvatarError] = useState(false);
  const [deleteConfirmation, setDeleteConfirmation] = useState(false);
  const [saveSucceeded, setSaveSucceeded] = useState(false);
  const query = useQuery({
    queryKey: eventsMemberQueryKeys.profileSettings,
    queryFn: ({ signal }) => identityService.profileSettings(signal),
    enabled: auth.isAuthenticated,
    retry: false,
  });
  const form = useForm<ProfileSettingsFormValues>({
    resolver: zodResolver(profileSettingsSchema),
    defaultValues: emptyForm,
  });
  const settings = query.data;
  const nickname = useWatch({ control: form.control, name: "nickname" });
  useEffect(() => {
    if (!settings) return;
    form.reset(profileSettingsToForm(settings));
  }, [form, settings]);
  useUnsavedChanges(form.formState.isDirty || avatar != null);
  useDocumentTitle(t("Shared_NavMenu_UserSettings"));

  const save = useMutation({
    mutationFn: (payload: ProfileSettingsUpdate) =>
      eventsMemberService.updateProfileSettings(payload),
    onSuccess: async (updated) => {
      setSaveSucceeded(true);
      setAvatar(undefined);
      if (updated) {
        queryClient.setQueryData(eventsMemberQueryKeys.profileSettings, updated);
        form.reset(profileSettingsToForm(updated));
      }
      await queryClient.invalidateQueries({ queryKey: queryKeys.me });
    },
  });
  useAppToastOnChange(saveSucceeded ? t("Pages_Settings_SaveSuccess") : undefined);
  useAppToastOnChange(
    save.isError ? formatResource(t("React_Settings_SaveError"), save.error.message) : undefined,
    "error",
  );

  const preview = useMemo(
    () => (settings ? avatarSource(settings, avatar) : undefined),
    [avatar, settings],
  );
  const handleAvatar = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;
    const payload = await avatarFileToPayload(file);
    setAvatarError(!payload);
    setAvatar(payload);
    setSaveSucceeded(false);
  };

  if (auth.isLoading) {
    return (
      <div className="narrow-page">
        <StatePanel kind="loading">{t("Common_LoadingData")}</StatePanel>
      </div>
    );
  }
  if (!auth.isAuthenticated) {
    const provider = auth.providers.steam ? "steam" : auth.providers.discord ? "discord" : null;
    return (
      <div className="narrow-page">
        <StatePanel
          kind="forbidden"
          title={t("Pages_Public_Join_LoginRequiredTitle")}
          action={
            provider ? (
              <button
                className="button button--gold"
                type="button"
                onClick={() => auth.login(provider)}
              >
                {t("Pages_Unit_Kassenbuch_Index_SignIn")}
              </button>
            ) : undefined
          }
        >
          {t("Pages_Public_Join_LoginHint")}
        </StatePanel>
      </div>
    );
  }
  if (query.isLoading) {
    return (
      <div className="narrow-page">
        <StatePanel kind="loading">{t("Common_LoadingData")}</StatePanel>
      </div>
    );
  }
  if (query.isError || !settings) {
    return (
      <div className="narrow-page">
        <StatePanel
          kind="error"
          action={
            <button
              className="button button--ghost"
              type="button"
              onClick={() => void query.refetch()}
            >
              <ArrowSync20Regular /> {t("React_UI_Retry")}
            </button>
          }
        >
          {formatResource(
            t("Pages_Public_Join_ProfileLoadError"),
            query.error?.message ?? t("React_Join_ProfileError"),
          )}
        </StatePanel>
      </div>
    );
  }

  const memberStatus = settings.isVereinMember
    ? settings.vereinMemberNumber
      ? formatResource(t("Pages_Settings_MemberNumber"), settings.vereinMemberNumber)
      : t("Pages_Settings_ActiveMember")
    : t("Pages_Settings_NotActive");

  return (
    <div className="page-wrap em-settings-page">
      <PageHeader
        kicker={t("Pages_Settings_AccountSettings")}
        title={t("Shared_NavMenu_UserSettings")}
        description={t("Pages_Settings_HeaderDescription")}
        actions={
          <button
            className="button button--ghost"
            type="button"
            onClick={() => void query.refetch()}
          >
            <ArrowSync20Regular /> {t("Common_Refresh")}
          </button>
        }
      />

      <form
        className="em-settings-workspace"
        onSubmit={form.handleSubmit((values) => {
          setSaveSucceeded(false);
          save.mutate(profileFormToPayload(values, avatar));
        })}
      >
        <main>
          <section className="content-section em-panel">
            <PageHeader
              kicker={t("Pages_Settings_AccountSettings")}
              title={t("Pages_Settings_PublicProfile")}
              description={t("Pages_Settings_PublicProfileDescription")}
              actions={
                <StatusBadge tone={settings.isVerified ? "success" : "warning"}>
                  {settings.isVerified
                    ? t("Pages_Settings_Verified")
                    : t("Pages_Settings_Unverified")}
                </StatusBadge>
              }
            />
            <div className="em-profile-row">
              <span
                className="em-avatar"
                style={preview ? { backgroundImage: `url("${preview}")` } : undefined}
                aria-hidden="true"
              >
                {!preview && (nickname.trim()[0] ?? "U").toLocaleUpperCase()}
              </span>
              <label className="button button--ghost em-avatar-upload">
                <CloudArrowUp24Regular />
                {t("Pages_Settings_AccountSettings_SetAvatar")}
                <input type="file" accept="image/*" onChange={handleAvatar} />
              </label>
              <small>{t("Pages_Settings_AvatarRequirements")}</small>
            </div>
            {avatarError && (
              <StatePanel kind="error">{t("Pages_Settings_AvatarRequirements")}</StatePanel>
            )}
            <div className="em-form-grid">
              <SettingsField
                label={t("Pages_Settings_AccountSettings_Nickname")}
                {...(form.formState.errors.nickname
                  ? { error: t("Pages_Settings_NicknameRequired") }
                  : {})}
              >
                <input maxLength={1024} autoComplete="nickname" {...form.register("nickname")} />
              </SettingsField>
              <SettingsField
                label={t("Pages_Settings_AccountSettings_EMail")}
                {...(form.formState.errors.eMail
                  ? { error: t("React_Settings_EMailInvalid") }
                  : {})}
              >
                <input
                  type="email"
                  maxLength={1024}
                  autoComplete="email"
                  {...form.register("eMail")}
                />
              </SettingsField>
            </div>
          </section>

          <section className="content-section em-panel">
            <PageHeader
              kicker={t("Pages_Settings_Verein")}
              title={t("Pages_Settings_FormalMemberDetails")}
              description={t("Pages_Settings_FormalMemberDescription")}
              actions={
                <StatusBadge tone={settings.isVereinMember ? "success" : "neutral"}>
                  {memberStatus}
                </StatusBadge>
              }
            />
            <div className="em-form-grid em-form-grid--member">
              <SettingsField label={t("Pages_Settings_FirstName")}>
                <input maxLength={256} autoComplete="given-name" {...form.register("firstName")} />
              </SettingsField>
              <SettingsField label={t("Pages_Settings_LastName")}>
                <input maxLength={256} autoComplete="family-name" {...form.register("lastName")} />
              </SettingsField>
              <SettingsField
                label={t("Pages_Settings_DateOfBirth")}
                {...(form.formState.errors.dateOfBirth
                  ? { error: t("Pages_Public_Join_ValidationDateOfBirth") }
                  : {})}
              >
                <input type="date" {...form.register("dateOfBirth")} />
              </SettingsField>
              <SettingsField label={t("Pages_Settings_Phone")}>
                <input maxLength={64} autoComplete="tel" {...form.register("phoneNumber")} />
              </SettingsField>
              <SettingsField label={t("Pages_Settings_AddressLine1")} wide>
                <input
                  maxLength={512}
                  autoComplete="address-line1"
                  {...form.register("addressLine1")}
                />
              </SettingsField>
              <SettingsField label={t("Pages_Settings_AddressLine2")} wide>
                <input
                  maxLength={512}
                  autoComplete="address-line2"
                  {...form.register("addressLine2")}
                />
              </SettingsField>
              <SettingsField label={t("Pages_Settings_PostalCode")}>
                <input maxLength={32} autoComplete="postal-code" {...form.register("postalCode")} />
              </SettingsField>
              <SettingsField label={t("Pages_Settings_City")}>
                <input maxLength={256} autoComplete="address-level2" {...form.register("city")} />
              </SettingsField>
              <SettingsField label={t("Pages_Settings_Country")}>
                <input maxLength={128} autoComplete="country-name" {...form.register("country")} />
              </SettingsField>
            </div>
            <p>
              {settings.isVereinMember
                ? t("Pages_Settings_VereinVerificationNote")
                : t("Pages_Settings_VereinApplicantNote")}
            </p>
          </section>
        </main>

        <aside>
          <section className="content-section em-panel">
            <PageHeader
              kicker={t("Pages_Settings_LinkedAccounts")}
              title={t("Pages_Settings_LinkedIdentities")}
              description={t("Pages_Settings_LinkedIdentitiesDescription")}
            />
            <dl className="em-identities">
              <div>
                <dt>{t("Pages_Settings_AccountSettings_SteamId64")}</dt>
                <dd>{settings.steamId64 || t("Pages_Settings_NotLinked")}</dd>
                {auth.providers.steam && (
                  <button
                    className="button button--ghost button--small"
                    type="button"
                    onClick={() => auth.login("steam")}
                  >
                    {t("Steam")}
                  </button>
                )}
              </div>
              <div>
                <dt>{t("Pages_Settings_AccountSettings_Discord")}</dt>
                <dd>
                  {settings.discordUsername || settings.discordId || t("Pages_Settings_NotLinked")}
                </dd>
                {auth.providers.discord && (
                  <button
                    className="button button--ghost button--small"
                    type="button"
                    onClick={() => auth.login("discord")}
                  >
                    {t("Discord")}
                  </button>
                )}
              </div>
            </dl>
          </section>

          <section className="content-section em-panel em-danger-panel">
            <ShieldTask24Regular />
            <PageHeader
              kicker={t("Pages_Settings_DangerZone")}
              title={t("Pages_Settings_AccountSettings_DeleteAccount")}
              description={t("Pages_Settings_DangerZoneDescription")}
            />
            <p>
              {settings.canDeleteAccount
                ? t("Pages_Settings_DeleteAccountAvailableDescription")
                : t("Pages_Settings_DeleteAccountBlockedFormalMember")}
            </p>
            <button
              className="button button--danger"
              type="button"
              disabled={!settings.canDeleteAccount}
              onClick={() => setDeleteConfirmation(true)}
            >
              {t("Pages_Settings_AccountSettings_DeleteAccount")}
            </button>
          </section>
        </aside>

        <footer className="em-settings-actions">
          <div>
            <strong>
              {form.formState.isDirty || avatar
                ? t("Pages_Settings_UnsavedChanges")
                : t("Pages_Settings_NoUnsavedChanges")}
            </strong>
            <p>
              {form.formState.isDirty || avatar
                ? t("Pages_Settings_UnsavedChangesDescription")
                : t("Pages_Settings_NoUnsavedChangesDescription")}
            </p>
          </div>
          <button
            className="button button--ghost"
            type="button"
            onClick={() => {
              form.reset(profileSettingsToForm(settings));
              setAvatar(undefined);
              setAvatarError(false);
            }}
          >
            {t("Common_Reset")}
          </button>
          <button className="button button--gold" type="submit" disabled={save.isPending}>
            {save.isPending ? t("Common_Saving") : t("Pages_Settings_SaveSettings")}
          </button>
        </footer>
      </form>

      <ConfirmDialog
        open={deleteConfirmation}
        title={t("Pages_Settings_AccountSettings_DeleteAccount_Confirm_Title")}
        description={t("Pages_Settings_AccountSettings_DeleteAccount_Confirm_Message")}
        confirmLabel={t(
          "Pages_Settings_AccountSettings_DeleteAccount_Confirm_YesIamSureIWantToDeleteMyAccount",
        )}
        cancelLabel={t("Common_Cancel")}
        onCancel={() => setDeleteConfirmation(false)}
        onConfirm={() => {
          const apiTarget = getApiClient().url("users/me/delete");
          submitAccountDeletion(localReturnUrl(apiTarget));
        }}
      />
    </div>
  );
}

function SettingsField({
  label,
  error,
  wide = false,
  children,
}: {
  label: string;
  error?: string;
  wide?: boolean;
  children: ReactNode;
}) {
  return (
    <label className={`field${wide ? " em-field--wide" : ""}`}>
      <span>{label}</span>
      {children}
      {error && <small role="alert">{error}</small>}
    </label>
  );
}
