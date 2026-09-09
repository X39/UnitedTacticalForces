import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useParams, useSearchParams } from "react-router";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { useAppRouteToast, useAppToast, useAppToastOnChange } from "@/components/AppToasts";
import { ConfirmDialog, StatePanel, StatusBadge, useUnsavedChanges } from "@/components/primitives";
import { getHostingPermissions } from "@/features/hosting/authorization";
import { ConfigurationProfileAccessSection } from "@/features/hosting/ConfigurationProfileAccessSection";
import {
  ConfigurationProfileFields,
  gameLabel,
} from "@/features/hosting/ConfigurationProfileFields";
import { ConfigurationProfileRevisionHistory } from "@/features/hosting/ConfigurationProfileRevisionHistory";
import {
  buildConfigurationValues,
  configurationDraftValue,
  isConfigurationDraftDirty,
  validateConfigurationDraft,
  type ConfigurationValidationIssue,
} from "@/features/hosting/model";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type {
  ConfigurationProfileAccessMatrix,
  ConfigurationProfileDetails,
} from "@/features/hosting/types";

type ProfileTab = "configuration" | "revisions" | "access";

function requestedTab(value: string | null): ProfileTab {
  return value === "revisions" || value === "access" ? value : "configuration";
}

export function ConfigurationProfileWorkspacePage() {
  const { t } = useTranslation();
  const { profileId = "" } = useParams();
  const location = useLocation();
  const { user, isLoading: authLoading } = useAuth();
  const permissions = getHostingPermissions(user);
  const queryClient = useQueryClient();
  const { showToast } = useAppToast();
  const [searchParams, setSearchParams] = useSearchParams();
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [clearedKeys, setClearedKeys] = useState<string[]>([]);
  const [issues, setIssues] = useState<ConfigurationValidationIssue[]>([]);
  const [message, setMessage] = useState<string>();
  const [archiveOpen, setArchiveOpen] = useState(false);
  const profile = useQuery({
    queryKey: hostingQueryKeys.profile(profileId),
    queryFn: ({ signal }) => hostingService.profile(profileId, signal),
    enabled: Boolean(profileId) && permissions.accessConfigurationProfiles,
  });
  const tabRequest = requestedTab(searchParams.get("tab"));
  const tab =
    tabRequest === "access" && profile.data && !profile.data.canManageAccess
      ? "configuration"
      : tabRequest;
  const access = useQuery({
    queryKey: hostingQueryKeys.profileAccess(profileId),
    queryFn: ({ signal }) => hostingService.profileAccess(profileId, signal),
    enabled: Boolean(profileId && profile.data?.canManageAccess && tab === "access"),
  });
  const definitions = useMemo(() => profile.data?.definitions ?? [], [profile.data?.definitions]);
  const latest = useMemo(
    () =>
      profile.data?.revisions.reduce(
        (current, revision) =>
          !current || revision.revisionNumber > current.revisionNumber ? revision : current,
        undefined as ConfigurationProfileDetails["revisions"][number] | undefined,
      ),
    [profile.data?.revisions],
  );
  const baseDraft = useMemo(
    () =>
      Object.fromEntries(
        definitions.map((definition) => [
          definition.key,
          configurationDraftValue(definition, latest?.values[definition.key]),
        ]),
      ),
    [definitions, latest],
  );
  const effectiveDraft = { ...baseDraft, ...draft };
  const dirty = isConfigurationDraftDirty(definitions, baseDraft, draft) || clearedKeys.length > 0;
  const changedDraftKeys = definitions
    .filter(
      (definition) =>
        (draft[definition.key] ?? baseDraft[definition.key] ?? "") !==
        (baseDraft[definition.key] ?? ""),
    )
    .map((definition) => definition.key);
  const changeCount = new Set([...changedDraftKeys, ...clearedKeys]).size;
  useUnsavedChanges(dirty);
  useAppRouteToast((location.state as { message?: string } | null)?.message);
  useAppToastOnChange(message);

  useEffect(() => {
    if (profile.data && tabRequest === "access" && !profile.data.canManageAccess) {
      setSearchParams({ tab: "configuration" }, { replace: true });
    }
  }, [profile.data, setSearchParams, tabRequest]);

  const publish = useMutation({
    mutationFn: ({
      values,
      resetKeys,
    }: {
      values: ReturnType<typeof buildConfigurationValues>;
      resetKeys: string[];
    }) =>
      hostingService.createProfileRevision(
        profileId,
        profile.data?.profile.latestRevisionNumber ?? 0,
        values,
        resetKeys,
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: hostingQueryKeys.profile(profileId) });
      setDraft({});
      setClearedKeys([]);
      setIssues([]);
      setMessage(t("Hosting_ConfigurationProfiles_Published"));
    },
    onError: (error: Error) => showToast(error.message, "error"),
  });
  const archive = useMutation({
    mutationFn: () => hostingService.archiveProfile(profileId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: hostingQueryKeys.profiles }),
        queryClient.invalidateQueries({ queryKey: hostingQueryKeys.profile(profileId) }),
      ]);
      setArchiveOpen(false);
      setDraft({});
      setMessage(t("Hosting_ConfigurationProfiles_ArchivedSuccess"));
    },
    onError: (error: Error) => showToast(error.message, "error"),
  });
  const changeAccess = useMutation({
    mutationFn: (matrix: ConfigurationProfileAccessMatrix) =>
      hostingService.updateProfileAccessMatrix(profileId, matrix),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: hostingQueryKeys.profileAccess(profileId) });
      setMessage(t("React_ConfigurationProfiles_AccessSaved"));
    },
    onError: (error: Error) => showToast(error.message, "error"),
  });
  const selectTab = (nextTab: ProfileTab) => setSearchParams({ tab: nextTab });
  const submit = () => {
    const nextIssues = validateConfigurationDraft(definitions, effectiveDraft);
    setIssues(nextIssues);
    setMessage(undefined);
    if (nextIssues.length !== 0) return;
    const resetKeys = definitions
      .filter(
        (definition) =>
          clearedKeys.includes(definition.key) ||
          (changedDraftKeys.includes(definition.key) && !effectiveDraft[definition.key]?.trim()),
      )
      .map((definition) => definition.key);
    const changedDefinitions = definitions.filter(
      (definition) =>
        changedDraftKeys.includes(definition.key) &&
        !resetKeys.includes(definition.key) &&
        Boolean(effectiveDraft[definition.key]?.trim()),
    );
    publish.mutate({
      values: buildConfigurationValues(changedDefinitions, effectiveDraft),
      resetKeys,
    });
  };

  if (!permissions.accessConfigurationProfiles)
    return (
      <div className="next-hosting-page">
        <StatePanel kind="forbidden" title={t("Hosting_ConfigurationProfiles_ForbiddenTitle")}>
          {t("Hosting_ConfigurationProfiles_ForbiddenDescription")}
        </StatePanel>
      </div>
    );
  if (authLoading || profile.isLoading)
    return (
      <div className="next-hosting-page">
        <StatePanel kind="loading">{t("Hosting_ConfigurationProfiles_Loading")}</StatePanel>
      </div>
    );
  if (profile.error)
    return (
      <div className="next-hosting-page">
        <StatePanel
          kind="error"
          title={t("Hosting_ConfigurationProfiles_ProfileLoadError").replace(": {0}", "")}
          action={
            <button
              className="button button--ghost"
              type="button"
              onClick={() => void profile.refetch()}
            >
              {t("Hosting_ConfigurationProfiles_Retry")}
            </button>
          }
        >
          {profile.error.message}
        </StatePanel>
      </div>
    );
  if (!profile.data)
    return (
      <div className="next-hosting-page">
        <StatePanel kind="empty" title={t("Hosting_ConfigurationProfiles_NotFoundTitle")}>
          {t("Hosting_ConfigurationProfiles_NotFoundDescription")}
        </StatePanel>
      </div>
    );

  const data = profile.data;
  return (
    <div className="next-hosting-page next-hosting-profile-workspace-page">
      <Link className="next-hosting-back-link" to="/command/configuration-profiles">
        ← {t("React_ConfigurationProfiles_BackToProfiles")}
      </Link>
      <header className="next-hosting-profile-identity">
        <div>
          <span className="kicker">{gameLabel(data.profile.gameIdentifier)}</span>
          <h1>{data.profile.title}</h1>
          <p>
            {t("Hosting_ConfigurationProfiles_Revision")} {data.profile.latestRevisionNumber}
          </p>
        </div>
        <div className="next-hosting-profile-identity-actions">
          <StatusBadge tone={data.profile.isArchived ? "warning" : "success"}>
            {data.profile.isArchived
              ? t("Hosting_ConfigurationProfiles_Archived")
              : t("Hosting_ConfigurationProfiles_Active")}
          </StatusBadge>
          {data.canArchive && !data.profile.isArchived && (
            <button
              className="button button--danger"
              type="button"
              onClick={() => setArchiveOpen(true)}
            >
              {t("Hosting_ConfigurationProfiles_Archive")}
            </button>
          )}
        </div>
      </header>
      <nav
        className="next-hosting-profile-tabs"
        role="tablist"
        aria-label={t("React_ConfigurationProfiles_ProfileSections")}
      >
        {(["configuration", "revisions"] as const).map((item) => (
          <button
            key={item}
            type="button"
            role="tab"
            aria-selected={tab === item}
            onClick={() => selectTab(item)}
          >
            {t(
              item === "configuration"
                ? "React_ConfigurationProfiles_ConfigurationTab"
                : "React_ConfigurationProfiles_RevisionsTab",
            )}
          </button>
        ))}
        {data.canManageAccess && (
          <button
            type="button"
            role="tab"
            aria-selected={tab === "access"}
            onClick={() => selectTab("access")}
          >
            {t("React_ConfigurationProfiles_AccessTab")}
          </button>
        )}
      </nav>
      {tab === "configuration" && (
        <section className="next-hosting-panel next-hosting-profile-configuration">
          {data.profile.isArchived ? (
            <StatePanel kind="warning" title={t("Hosting_ConfigurationProfiles_ArchivedTitle")}>
              {t("Hosting_ConfigurationProfiles_ArchiveDescription")}
            </StatePanel>
          ) : data.canModify ? (
            <>
              <header className="next-hosting-section-heading">
                <div>
                  <span className="kicker">{t("Hosting_ConfigurationProfiles_Draft")}</span>
                  <h2>{t("Hosting_ConfigurationProfiles_PublishTitle")}</h2>
                  <p>{t("Hosting_ConfigurationProfiles_PublishDescription")}</p>
                </div>
              </header>
              <ConfigurationProfileFields
                definitions={definitions}
                values={effectiveDraft}
                issues={issues}
                configuredSensitiveKeys={
                  latest?.sensitiveConfiguredKeys ?? latest?.redactedKeys ?? []
                }
                clearedKeys={clearedKeys}
                onChange={(key, value) => {
                  setClearedKeys((current) => current.filter((item) => item !== key));
                  setDraft((current) => ({ ...current, [key]: value }));
                }}
                onClear={(key) => {
                  setDraft((current) => ({ ...current, [key]: "" }));
                  setClearedKeys((current) => [...new Set([...current, key])]);
                }}
              />
            </>
          ) : (
            <StatePanel kind="info" title={t("Hosting_ConfigurationProfiles_ReadOnlyTitle")}>
              {t("Hosting_ConfigurationProfiles_ReadOnlyDescription")}
            </StatePanel>
          )}
        </section>
      )}
      {tab === "revisions" && (
        <ConfigurationProfileRevisionHistory profile={data} definitions={definitions} />
      )}
      {tab === "access" && data.canManageAccess && (
        <ConfigurationProfileAccessSection
          profileId={data.profile.primaryKey}
          access={access.data}
          loading={access.isLoading}
          error={access.error}
          pending={changeAccess.isPending}
          onRetry={() => void access.refetch()}
          onSave={(matrix) => {
            setMessage(undefined);
            changeAccess.mutate(matrix);
          }}
        />
      )}
      {tab === "configuration" && dirty && data.canModify && !data.profile.isArchived && (
        <div
          className="next-hosting-profile-savebar"
          role="region"
          aria-label={t("React_ConfigurationProfiles_UnsavedChanges")}
        >
          <span>
            <strong>
              {formatResource(t("React_ConfigurationProfiles_ChangeCount"), changeCount)}
            </strong>
            {formatResource(
              t("Hosting_ConfigurationProfiles_BasedOnRevision"),
              data.profile.latestRevisionNumber,
            )}
          </span>
          <div>
            <button
              className="button button--ghost"
              type="button"
              disabled={publish.isPending}
              onClick={() => {
                setDraft({});
                setClearedKeys([]);
                setIssues([]);
              }}
            >
              {t("React_ConfigurationProfiles_DiscardChanges")}
            </button>
            <button
              className="button button--primary"
              type="button"
              disabled={publish.isPending}
              onClick={submit}
            >
              {publish.isPending
                ? t("Hosting_ConfigurationProfiles_Publishing")
                : t("Hosting_ConfigurationProfiles_Publish")}
            </button>
          </div>
        </div>
      )}
      <ConfirmDialog
        open={archiveOpen}
        title={t("Hosting_ConfigurationProfiles_Archive")}
        description={t("Hosting_ConfigurationProfiles_ArchiveDescription")}
        confirmLabel={
          archive.isPending
            ? t("Hosting_ConfigurationProfiles_Archiving")
            : t("Hosting_ConfigurationProfiles_Archive")
        }
        cancelLabel={t("Hosting_Common_Cancel")}
        challenge={data.profile.title}
        onCancel={() => {
          if (!archive.isPending) setArchiveOpen(false);
        }}
        onConfirm={() => {
          if (!archive.isPending) archive.mutate();
        }}
      />
    </div>
  );
}
