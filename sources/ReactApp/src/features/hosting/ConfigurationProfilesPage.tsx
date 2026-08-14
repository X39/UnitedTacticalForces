import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import {
  ConfirmDialog,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { getHostingPermissions } from "@/features/hosting/authorization";
import {
  buildConfigurationValues,
  configurationDraftValue,
  formatDate,
  isConfigurationDraftDirty,
  validateConfigurationDraft,
  type ConfigurationValidationIssue,
} from "@/features/hosting/model";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type {
  ConfigurationDefinition,
  ConfigurationProfileAccess,
  ConfigurationProfileDetails,
  ConfigurationProfileSummary,
  ConfigurationValue,
} from "@/features/hosting/types";

type Translate = (key: TranslationKey) => string;

const permissionKeys: Record<string, TranslationKey> = {
  cfg: "Hosting_ConfigurationProfiles_PermissionFullControl",
  "cfg:view": "Hosting_ConfigurationProfiles_PermissionView",
  "cfg:modify": "Hosting_ConfigurationProfiles_PermissionModify",
  "cfg:manage-access": "Hosting_ConfigurationProfiles_PermissionManageAccess",
  "cfg:archive": "Hosting_ConfigurationProfiles_PermissionArchive",
};

const validationKeys: Record<ConfigurationValidationIssue["code"], TranslationKey> = {
  required: "Hosting_ConfigurationProfiles_ValidationRequired",
  integer: "Hosting_ConfigurationProfiles_ValidationInteger",
  number: "Hosting_ConfigurationProfiles_ValidationNumber",
  minimum: "Hosting_ConfigurationProfiles_ValidationMinimum",
  maximum: "Hosting_ConfigurationProfiles_ValidationMaximum",
  selection: "Hosting_ConfigurationProfiles_ValidationSelection",
};

export function configurationValidationMessage(
  issue: ConfigurationValidationIssue,
  translate: Translate,
): string {
  return formatResource(
    translate(validationKeys[issue.code]),
    issue.displayName,
    issue.limit ?? "",
  );
}

function gameLabel(identifier: string): string {
  return identifier.toLowerCase() === "arma3" ? "Arma 3" : identifier;
}

function permissionLabel(permission: string, translate: Translate): string {
  const key = permissionKeys[permission];
  return key ? translate(key) : permission;
}

function ConfigurationFields({
  definitions,
  values,
  issues,
  onChange,
}: {
  definitions: ConfigurationDefinition[];
  values: Record<string, string>;
  issues: ConfigurationValidationIssue[];
  onChange(key: string, value: string): void;
}) {
  const { t } = useTranslation();
  const targets = Object.entries(
    definitions.reduce<Record<string, ConfigurationDefinition[]>>((result, definition) => {
      (result[definition.targetPath || t("Hosting_ConfigurationProfiles_GeneratedFile")] ??=
        []).push(definition);
      return result;
    }, {}),
  );
  const issueByKey = new Map(issues.map((issue) => [issue.key, issue]));

  return (
    <div className="next-hosting-configuration-groups">
      {targets.map(([targetPath, targetDefinitions]) => (
        <section className="next-hosting-configuration-target" key={targetPath}>
          <header>
            <span>{t("Hosting_ConfigurationProfiles_GeneratedFile")}</span>
            <code>{targetPath}</code>
          </header>
          {Object.entries(
            targetDefinitions.reduce<Record<string, ConfigurationDefinition[]>>(
              (result, definition) => {
                (result[definition.group || t("Hosting_ConfigurationProfiles_General")] ??=
                  []).push(definition);
                return result;
              },
              {},
            ),
          ).map(([group, items]) => (
            <fieldset key={group}>
              <legend>{group}</legend>
              {items
                .toSorted((left, right) => left.order - right.order)
                .map((definition) => {
                  const id = `configuration-${definition.key.replace(/[^a-z0-9_-]/gi, "-")}`;
                  const descriptionId = `${id}-description`;
                  const errorId = `${id}-error`;
                  const value = values[definition.key] ?? "";
                  const issue = issueByKey.get(definition.key);
                  const kind = definition.kind.toLowerCase();
                  const describedBy = issue ? `${descriptionId} ${errorId}` : descriptionId;
                  const common = {
                    id,
                    required: definition.required,
                    "aria-invalid": Boolean(issue),
                    "aria-describedby": describedBy,
                  } as const;

                  return (
                    <div className="field next-hosting-configuration-field" key={definition.key}>
                      <div className="next-hosting-configuration-field-heading">
                        <label htmlFor={id}>{definition.displayName}</label>
                        <span>
                          {definition.required && (
                            <strong>{t("Hosting_ConfigurationProfiles_Required")}</strong>
                          )}
                          {definition.sensitive && (
                            <strong>{t("Hosting_ConfigurationProfiles_Sensitive")}</strong>
                          )}
                        </span>
                      </div>
                      {kind === "boolean" ? (
                        <label className="next-hosting-configuration-toggle" htmlFor={id}>
                          <input
                            {...common}
                            type="checkbox"
                            checked={value === "true"}
                            onChange={(event) =>
                              onChange(definition.key, event.target.checked ? "true" : "false")
                            }
                          />
                          <span>
                            {value === "true"
                              ? t("Hosting_ConfigurationProfiles_Enabled")
                              : t("Hosting_ConfigurationProfiles_Disabled")}
                          </span>
                        </label>
                      ) : (definition.allowedValues?.length ?? 0) > 0 ? (
                        <select
                          {...common}
                          value={value}
                          onChange={(event) => onChange(definition.key, event.target.value)}
                        >
                          {!definition.required && (
                            <option value="">{t("Hosting_ConfigurationProfiles_NotSet")}</option>
                          )}
                          {(definition.allowedValues ?? []).map((option) => (
                            <option key={option} value={option}>
                              {option}
                            </option>
                          ))}
                        </select>
                      ) : kind === "stringarray" ? (
                        <textarea
                          {...common}
                          rows={3}
                          value={value}
                          placeholder={t("Hosting_ConfigurationProfiles_EnterOnePerLine")}
                          onChange={(event) => onChange(definition.key, event.target.value)}
                        />
                      ) : (
                        <input
                          {...common}
                          type={
                            definition.sensitive
                              ? "password"
                              : kind === "integer" || kind === "number" || kind === "decimal"
                                ? "number"
                                : "text"
                          }
                          step={
                            kind === "integer"
                              ? 1
                              : kind === "number" || kind === "decimal"
                                ? "any"
                                : undefined
                          }
                          min={definition.minimum ?? undefined}
                          max={definition.maximum ?? undefined}
                          value={value}
                          autoComplete={definition.sensitive ? "new-password" : "off"}
                          onChange={(event) => onChange(definition.key, event.target.value)}
                        />
                      )}
                      <div className="next-hosting-configuration-field-meta">
                        <small id={descriptionId}>{definition.description}</small>
                        {!definition.required && value !== "" && (
                          <button
                            className="button button--ghost button--small"
                            type="button"
                            onClick={() => onChange(definition.key, "")}
                          >
                            {t("Hosting_ConfigurationProfiles_ClearValue")}
                          </button>
                        )}
                      </div>
                      {issue && (
                        <small className="next-hosting-field-error" id={errorId} role="alert">
                          {configurationValidationMessage(issue, t)}
                        </small>
                      )}
                    </div>
                  );
                })}
            </fieldset>
          ))}
        </section>
      ))}
    </div>
  );
}

function ProfileBrowser({
  profiles,
  selectedId,
  canCreate,
  onSelect,
  onCreate,
}: {
  profiles: ConfigurationProfileSummary[];
  selectedId?: string | undefined;
  canCreate: boolean;
  onSelect(profileId: string): void;
  onCreate(): void;
}) {
  const { t } = useTranslation();
  return (
    <aside
      className="next-hosting-panel next-hosting-profile-browser"
      aria-label={t("Hosting_ConfigurationProfiles_Title")}
    >
      <header className="next-hosting-section-heading">
        <div>
          <span className="kicker">{t("Hosting_ConfigurationProfiles_Library")}</span>
          <h2>{t("Hosting_ConfigurationProfiles_Profiles")}</h2>
        </div>
        {canCreate && (
          <button className="button button--primary" type="button" onClick={onCreate}>
            {t("Hosting_ConfigurationProfiles_NewProfile")}
          </button>
        )}
      </header>
      {profiles.length === 0 ? (
        <StatePanel kind="empty" title={t("Hosting_ConfigurationProfiles_NoProfilesTitle")}>
          {t("Hosting_ConfigurationProfiles_NoProfilesDescription")}
        </StatePanel>
      ) : (
        <div className="next-hosting-profile-list" role="listbox">
          {profiles.map((profile) => (
            <button
              key={profile.primaryKey}
              type="button"
              role="option"
              aria-selected={profile.primaryKey === selectedId}
              onClick={() => onSelect(profile.primaryKey)}
            >
              <span>
                <strong>{profile.title}</strong>
                <small>
                  {gameLabel(profile.gameIdentifier)} ·{" "}
                  {t("Hosting_ConfigurationProfiles_Revision")} {profile.latestRevisionNumber}
                </small>
              </span>
              <StatusBadge tone={profile.isArchived ? "warning" : "success"}>
                {profile.isArchived
                  ? t("Hosting_ConfigurationProfiles_Archived")
                  : t("Hosting_ConfigurationProfiles_Active")}
              </StatusBadge>
            </button>
          ))}
        </div>
      )}
    </aside>
  );
}

type AccessChange = {
  subject: "user" | "role";
  id: string;
  permission: string;
  grant: boolean;
};

function ProfileAccessSection({
  profileId,
  access,
  loading,
  error,
  pending,
  onRetry,
  onChange,
}: {
  profileId: string;
  access: ConfigurationProfileAccess | undefined;
  loading: boolean;
  error: Error | null;
  pending: boolean;
  onRetry(): void;
  onChange(change: AccessChange): void;
}) {
  const { t } = useTranslation();
  const [subject, setSubject] = useState<"user" | "role">("user");
  const [subjectId, setSubjectId] = useState("");
  const [permission, setPermission] = useState("cfg:view");
  const [validationError, setValidationError] = useState<string>();
  const allowedPermissions = access?.allowedPermissions ?? [];
  const selectedPermission = allowedPermissions.includes(permission)
    ? permission
    : (allowedPermissions[0] ?? "");

  const submit = (grant: boolean) => {
    const id = subjectId.trim();
    const isValidUser = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id);
    const roleId = Number(id);
    if (subject === "user" && !isValidUser) {
      setValidationError(t("Hosting_ConfigurationProfiles_AccessInvalidUser"));
      return;
    }
    if (subject === "role" && (!Number.isSafeInteger(roleId) || roleId <= 0)) {
      setValidationError(t("Hosting_ConfigurationProfiles_AccessInvalidRole"));
      return;
    }
    if (!selectedPermission) return;
    setValidationError(undefined);
    onChange({ subject, id, permission: selectedPermission, grant });
  };

  return (
    <section
      className="next-hosting-panel next-hosting-profile-access"
      aria-labelledby={`${profileId}-access-title`}
    >
      <header className="next-hosting-section-heading">
        <div>
          <span className="kicker">{t("Hosting_ConfigurationProfiles_Permissions")}</span>
          <h3 id={`${profileId}-access-title`}>
            {t("Hosting_ConfigurationProfiles_ScopedAccess")}
          </h3>
          <p>{t("Hosting_ConfigurationProfiles_AccessDescription")}</p>
        </div>
      </header>
      {loading ? (
        <StatePanel kind="loading">{t("Hosting_ConfigurationProfiles_AccessLoading")}</StatePanel>
      ) : error ? (
        <StatePanel
          kind="error"
          title={t("Hosting_ConfigurationProfiles_AccessLoadError")}
          action={
            <button className="button button--ghost" type="button" onClick={onRetry}>
              {t("Hosting_ConfigurationProfiles_Retry")}
            </button>
          }
        >
          {error.message}
        </StatePanel>
      ) : (
        <>
          <form
            className="next-hosting-access-form"
            onSubmit={(event: FormEvent) => {
              event.preventDefault();
              submit(true);
            }}
          >
            <fieldset>
              <legend>{t("Hosting_ConfigurationProfiles_SubjectType")}</legend>
              <label>
                <input
                  type="radio"
                  name="profile-access-subject"
                  checked={subject === "user"}
                  onChange={() => {
                    setSubject("user");
                    setSubjectId("");
                    setValidationError(undefined);
                  }}
                />
                {t("Hosting_ConfigurationProfiles_User")}
              </label>
              <label>
                <input
                  type="radio"
                  name="profile-access-subject"
                  checked={subject === "role"}
                  onChange={() => {
                    setSubject("role");
                    setSubjectId("");
                    setValidationError(undefined);
                  }}
                />
                {t("Hosting_ConfigurationProfiles_Role")}
              </label>
            </fieldset>
            <label className="field">
              <span>
                {subject === "user"
                  ? t("Hosting_ConfigurationProfiles_UserId")
                  : t("Hosting_ConfigurationProfiles_RoleId")}
              </span>
              <input
                value={subjectId}
                inputMode={subject === "role" ? "numeric" : "text"}
                autoComplete="off"
                aria-invalid={Boolean(validationError)}
                onChange={(event) => setSubjectId(event.target.value)}
              />
            </label>
            <label className="field">
              <span>{t("Hosting_ConfigurationProfiles_Permission")}</span>
              <select
                value={selectedPermission}
                disabled={allowedPermissions.length === 0}
                onChange={(event) => setPermission(event.target.value)}
              >
                {allowedPermissions.map((item) => (
                  <option key={item} value={item}>
                    {permissionLabel(item, t)}
                  </option>
                ))}
              </select>
            </label>
            {validationError && (
              <p className="next-hosting-inline-error" role="alert">
                {validationError}
              </p>
            )}
            <div className="next-hosting-form-actions">
              <button
                className="button button--ghost"
                type="button"
                disabled={pending || !selectedPermission}
                onClick={() => submit(false)}
              >
                {t("Hosting_ConfigurationProfiles_Revoke")}
              </button>
              <button
                className="button button--primary"
                type="submit"
                disabled={pending || !selectedPermission}
              >
                {t("Hosting_ConfigurationProfiles_Grant")}
              </button>
            </div>
          </form>
          <div className="next-hosting-access-grants">
            {(access?.users.length ?? 0) === 0 && (access?.roles.length ?? 0) === 0 ? (
              <p className="next-hosting-muted">
                {t("Hosting_ConfigurationProfiles_NoDirectAccess")}
              </p>
            ) : (
              <>
                {(access?.users.length ?? 0) > 0 && (
                  <section>
                    <h4>{t("Hosting_ConfigurationProfiles_DirectUsers")}</h4>
                    {access?.users.map((user) => (
                      <article key={user.userId}>
                        <div>
                          <strong>{user.nickname}</strong>
                          <small>{user.userId}</small>
                        </div>
                        <ul>
                          {user.permissions.map((item) => (
                            <li key={item}>
                              <span>{permissionLabel(item, t)}</span>
                              <button
                                className="button button--ghost button--small"
                                type="button"
                                disabled={pending}
                                aria-label={formatResource(
                                  t("Hosting_ConfigurationProfiles_RevokeNamed"),
                                  permissionLabel(item, t),
                                  user.nickname,
                                )}
                                onClick={() =>
                                  onChange({
                                    subject: "user",
                                    id: user.userId,
                                    permission: item,
                                    grant: false,
                                  })
                                }
                              >
                                {t("Hosting_ConfigurationProfiles_Revoke")}
                              </button>
                            </li>
                          ))}
                        </ul>
                      </article>
                    ))}
                  </section>
                )}
                {(access?.roles.length ?? 0) > 0 && (
                  <section>
                    <h4>{t("Hosting_ConfigurationProfiles_DirectRoles")}</h4>
                    {access?.roles.map((role) => (
                      <article key={role.roleId}>
                        <div>
                          <strong>{role.title}</strong>
                          <small>{role.description || `#${role.roleId}`}</small>
                        </div>
                        <ul>
                          {role.permissions.map((item) => (
                            <li key={item}>
                              <span>{permissionLabel(item, t)}</span>
                              <button
                                className="button button--ghost button--small"
                                type="button"
                                disabled={pending}
                                aria-label={formatResource(
                                  t("Hosting_ConfigurationProfiles_RevokeNamed"),
                                  permissionLabel(item, t),
                                  role.title,
                                )}
                                onClick={() =>
                                  onChange({
                                    subject: "role",
                                    id: String(role.roleId),
                                    permission: item,
                                    grant: false,
                                  })
                                }
                              >
                                {t("Hosting_ConfigurationProfiles_Revoke")}
                              </button>
                            </li>
                          ))}
                        </ul>
                      </article>
                    ))}
                  </section>
                )}
              </>
            )}
          </div>
        </>
      )}
    </section>
  );
}

export function ConfigurationProfilesPage() {
  const { t } = useTranslation();
  const { user, isLoading: authLoading } = useAuth();
  const permissions = getHostingPermissions(user);
  const queryClient = useQueryClient();
  const profiles = useQuery({
    queryKey: hostingQueryKeys.profiles,
    queryFn: ({ signal }) => hostingService.profiles(signal),
    enabled: permissions.accessConfigurationProfiles,
  });
  const catalog = useQuery({
    queryKey: hostingQueryKeys.profileCatalog,
    queryFn: ({ signal }) => hostingService.profileCatalog(signal),
    enabled: permissions.accessConfigurationProfiles,
  });
  const [selectedId, setSelectedId] = useState<string>();
  const [creating, setCreating] = useState(true);
  const [title, setTitle] = useState("");
  const [game, setGame] = useState("");
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [issues, setIssues] = useState<ConfigurationValidationIssue[]>([]);
  const [message, setMessage] = useState<string>();
  const [archiveOpen, setArchiveOpen] = useState(false);
  const selected = useQuery({
    queryKey: hostingQueryKeys.profile(selectedId ?? ""),
    queryFn: ({ signal }) => hostingService.profile(selectedId ?? "", signal),
    enabled: Boolean(selectedId) && permissions.accessConfigurationProfiles,
  });
  const access = useQuery({
    queryKey: hostingQueryKeys.profileAccess(selectedId ?? ""),
    queryFn: ({ signal }) => hostingService.profileAccess(selectedId ?? "", signal),
    enabled: Boolean(selectedId && selected.data?.canManageAccess),
  });
  const games = useMemo(() => catalog.data?.games ?? [], [catalog.data?.games]);
  const activeGame = game || games[0]?.gameIdentifier || "";
  const definitions = useMemo(
    () =>
      creating
        ? (games.find((item) => item.gameIdentifier === activeGame)?.definitions ?? [])
        : (selected.data?.definitions ?? []),
    [activeGame, creating, games, selected.data?.definitions],
  );
  const baseDraft = useMemo(() => {
    const latest = selected.data?.revisions.reduce(
      (current, revision) =>
        !current || revision.revisionNumber > current.revisionNumber ? revision : current,
      undefined as ConfigurationProfileDetails["revisions"][number] | undefined,
    );
    return Object.fromEntries(
      definitions.map((definition) => [
        definition.key,
        configurationDraftValue(definition, creating ? undefined : latest?.values[definition.key]),
      ]),
    );
  }, [creating, definitions, selected.data]);
  const effectiveDraft = { ...baseDraft, ...draft };
  const draftDirty = isConfigurationDraftDirty(definitions, baseDraft, draft);
  const isDirty =
    draftDirty ||
    (creating &&
      (title.length > 0 || (game.length > 0 && game !== (games[0]?.gameIdentifier ?? ""))));

  const invalidate = async (profileId?: string) => {
    await queryClient.invalidateQueries({ queryKey: hostingQueryKeys.profiles });
    if (profileId)
      await queryClient.invalidateQueries({ queryKey: hostingQueryKeys.profile(profileId) });
  };
  const createProfile = useMutation({
    mutationFn: (values: ConfigurationValue[]) =>
      hostingService.createProfile(title.trim(), activeGame, values),
    onSuccess: async (profile) => {
      await invalidate(profile.primaryKey);
      setSelectedId(profile.primaryKey);
      setCreating(false);
      setTitle("");
      setDraft({});
      setIssues([]);
      setMessage(formatResource(t("Hosting_ConfigurationProfiles_Created"), profile.title));
    },
  });
  const publish = useMutation({
    mutationFn: async (values: ConfigurationValue[]) => {
      if (selectedId) await hostingService.createProfileRevision(selectedId, values);
    },
    onSuccess: async () => {
      await invalidate(selectedId);
      setDraft({});
      setIssues([]);
      setMessage(t("Hosting_ConfigurationProfiles_Published"));
    },
  });
  const archive = useMutation({
    mutationFn: async () => {
      if (selectedId) await hostingService.archiveProfile(selectedId);
    },
    onSuccess: async () => {
      await invalidate(selectedId);
      setArchiveOpen(false);
      setDraft({});
      setMessage(t("Hosting_ConfigurationProfiles_ArchivedSuccess"));
    },
  });
  const changeAccess = useMutation({
    mutationFn: async (change: AccessChange) => {
      if (!selectedId) return;
      if (change.subject === "user") {
        await hostingService.changeProfileUserAccess(
          selectedId,
          change.id,
          change.permission,
          change.grant,
        );
      } else {
        await hostingService.changeProfileRoleAccess(
          selectedId,
          Number(change.id),
          change.permission,
          change.grant,
        );
      }
    },
    onSuccess: async (_result, change) => {
      if (selectedId)
        await queryClient.invalidateQueries({
          queryKey: hostingQueryKeys.profileAccess(selectedId),
        });
      setMessage(
        change.grant
          ? t("Hosting_ConfigurationProfiles_AccessGranted")
          : t("Hosting_ConfigurationProfiles_AccessRevoked"),
      );
    },
  });
  const operationError =
    createProfile.error ?? publish.error ?? archive.error ?? changeAccess.error;
  const mutationPending =
    createProfile.isPending || publish.isPending || archive.isPending || changeAccess.isPending;
  useUnsavedChanges(isDirty && !mutationPending);

  const confirmDiscard = () => !isDirty || window.confirm(t("React_UnsavedChanges"));
  const select = (profileId: string) => {
    if (!confirmDiscard()) return;
    setSelectedId(profileId);
    setCreating(false);
    setDraft({});
    setIssues([]);
    setMessage(undefined);
    createProfile.reset();
    publish.reset();
  };
  const startCreate = () => {
    if (!confirmDiscard()) return;
    setSelectedId(undefined);
    setCreating(true);
    setTitle("");
    setGame(games[0]?.gameIdentifier ?? "");
    setDraft({});
    setIssues([]);
    setMessage(undefined);
  };
  const submitValues = (action: (values: ConfigurationValue[]) => void) => {
    const nextIssues = validateConfigurationDraft(definitions, effectiveDraft);
    setIssues(nextIssues);
    setMessage(undefined);
    if (nextIssues.length === 0) action(buildConfigurationValues(definitions, effectiveDraft));
  };
  const loading = authLoading || profiles.isLoading || catalog.isLoading;
  const loadError = profiles.error ?? catalog.error;

  return (
    <div className="next-hosting-page">
      <PageHeader
        title={t("Hosting_ConfigurationProfiles_Title")}
        description={t("Hosting_ConfigurationProfiles_Description")}
      />
      {!permissions.accessConfigurationProfiles ? (
        <StatePanel kind="forbidden" title={t("Hosting_ConfigurationProfiles_ForbiddenTitle")}>
          {t("Hosting_ConfigurationProfiles_ForbiddenDescription")}
        </StatePanel>
      ) : loading ? (
        <StatePanel kind="loading">{t("Hosting_ConfigurationProfiles_Loading")}</StatePanel>
      ) : loadError ? (
        <StatePanel
          kind="error"
          title={t("Hosting_ConfigurationProfiles_LoadError").replace(": {0}", "")}
          action={
            <button
              className="button button--ghost"
              type="button"
              onClick={() => void Promise.all([profiles.refetch(), catalog.refetch()])}
            >
              {t("Hosting_ConfigurationProfiles_Retry")}
            </button>
          }
        >
          {loadError.message}
        </StatePanel>
      ) : (
        <>
          {operationError && (
            <p className="next-hosting-inline-error" role="alert">
              {formatResource(
                t("Hosting_ConfigurationProfiles_OperationFailed"),
                operationError.message,
              )}
            </p>
          )}
          {message && (
            <div aria-live="polite">
              <StatePanel kind="success">{message}</StatePanel>
            </div>
          )}
          <div className="next-hosting-profile-workspace">
            <ProfileBrowser
              profiles={profiles.data ?? []}
              selectedId={selectedId}
              canCreate={Boolean(
                catalog.data?.canCreate && permissions.createConfigurationProfiles,
              )}
              onSelect={select}
              onCreate={startCreate}
            />
            <main className="next-hosting-profile-editor">
              {creating ? (
                <section className="next-hosting-panel">
                  <span className="kicker">{t("Hosting_ConfigurationProfiles_NewBaseline")}</span>
                  <h2>{t("Hosting_ConfigurationProfiles_CreateTitle")}</h2>
                  <p>{t("Hosting_ConfigurationProfiles_CreateDescription")}</p>
                  {!catalog.data?.canCreate ? (
                    <StatePanel
                      kind="forbidden"
                      title={t("Hosting_ConfigurationProfiles_ViewOnlyTitle")}
                    >
                      {t("Hosting_ConfigurationProfiles_ViewOnlyDescription")}
                    </StatePanel>
                  ) : games.length === 0 ? (
                    <StatePanel
                      kind="empty"
                      title={t("Hosting_ConfigurationProfiles_NoGamesTitle")}
                    >
                      {t("Hosting_ConfigurationProfiles_NoGamesDescription")}
                    </StatePanel>
                  ) : (
                    <>
                      <div className="next-hosting-profile-meta">
                        <div className="field">
                          <label htmlFor="configuration-profile-title">
                            {t("Hosting_ConfigurationProfiles_ProfileTitle")}
                          </label>
                          <input
                            id="configuration-profile-title"
                            value={title}
                            maxLength={256}
                            required
                            placeholder={t("Hosting_ConfigurationProfiles_ProfileTitlePlaceholder")}
                            onChange={(event) => setTitle(event.target.value)}
                          />
                          <small>{t("Hosting_ConfigurationProfiles_ProfileTitleHint")}</small>
                        </div>
                        <div className="field">
                          <label htmlFor="configuration-profile-game">
                            {t("Hosting_ConfigurationProfiles_Game")}
                          </label>
                          <select
                            id="configuration-profile-game"
                            value={activeGame}
                            onChange={(event) => {
                              if (draftDirty && !window.confirm(t("React_UnsavedChanges"))) return;
                              setGame(event.target.value);
                              setDraft({});
                              setIssues([]);
                            }}
                          >
                            {games.map((item) => (
                              <option key={item.gameIdentifier} value={item.gameIdentifier}>
                                {gameLabel(item.gameIdentifier)}
                              </option>
                            ))}
                          </select>
                          <small>{t("Hosting_ConfigurationProfiles_GameHint")}</small>
                        </div>
                      </div>
                      <ConfigurationFields
                        definitions={definitions}
                        values={effectiveDraft}
                        issues={issues}
                        onChange={(key, value) =>
                          setDraft((current) => ({ ...current, [key]: value }))
                        }
                      />
                      <div className="next-hosting-form-actions">
                        <span>
                          {formatResource(
                            t("Hosting_ConfigurationProfiles_SettingCount"),
                            definitions.length,
                          )}
                        </span>
                        <button
                          className="button button--primary"
                          type="button"
                          disabled={
                            !catalog.data?.canCreate ||
                            !permissions.createConfigurationProfiles ||
                            !title.trim() ||
                            createProfile.isPending
                          }
                          onClick={() => submitValues((values) => createProfile.mutate(values))}
                        >
                          {createProfile.isPending
                            ? t("Hosting_ConfigurationProfiles_Creating")
                            : t("Hosting_ConfigurationProfiles_Create")}
                        </button>
                      </div>
                    </>
                  )}
                </section>
              ) : selected.isLoading ? (
                <StatePanel kind="loading">{t("Hosting_ConfigurationProfiles_Loading")}</StatePanel>
              ) : selected.error ? (
                <StatePanel
                  kind="error"
                  title={t("Hosting_ConfigurationProfiles_ProfileLoadError").replace(": {0}", "")}
                  action={
                    <button
                      className="button button--ghost"
                      type="button"
                      onClick={() => void selected.refetch()}
                    >
                      {t("Hosting_ConfigurationProfiles_Retry")}
                    </button>
                  }
                >
                  {selected.error.message}
                </StatePanel>
              ) : !selected.data ? (
                <StatePanel kind="empty" title={t("Hosting_ConfigurationProfiles_NotFoundTitle")}>
                  {t("Hosting_ConfigurationProfiles_NotFoundDescription")}
                </StatePanel>
              ) : (
                <>
                  <section className="next-hosting-panel">
                    <header className="next-hosting-section-heading">
                      <div>
                        <span className="kicker">
                          {gameLabel(selected.data.profile.gameIdentifier)}
                        </span>
                        <h2>{selected.data.profile.title}</h2>
                        <p>
                          {t("Hosting_ConfigurationProfiles_Revision")}{" "}
                          {selected.data.profile.latestRevisionNumber} ·{" "}
                          {selected.data.profile.isArchived
                            ? t("Hosting_ConfigurationProfiles_Archived")
                            : t("Hosting_ConfigurationProfiles_Active")}
                        </p>
                      </div>
                      {selected.data.canArchive && !selected.data.profile.isArchived && (
                        <button
                          className="button button--danger"
                          type="button"
                          onClick={() => {
                            if (confirmDiscard()) setArchiveOpen(true);
                          }}
                        >
                          {t("Hosting_ConfigurationProfiles_Archive")}
                        </button>
                      )}
                    </header>
                    {selected.data.profile.isArchived ? (
                      <StatePanel
                        kind="warning"
                        title={t("Hosting_ConfigurationProfiles_ArchivedTitle")}
                      >
                        {t("Hosting_ConfigurationProfiles_ArchiveDescription")}
                      </StatePanel>
                    ) : selected.data.canModify ? (
                      <>
                        <header className="next-hosting-section-heading">
                          <div>
                            <span className="kicker">
                              {t("Hosting_ConfigurationProfiles_Draft")}
                            </span>
                            <h3>{t("Hosting_ConfigurationProfiles_PublishTitle")}</h3>
                            <p>{t("Hosting_ConfigurationProfiles_PublishDescription")}</p>
                          </div>
                        </header>
                        <ConfigurationFields
                          definitions={definitions}
                          values={effectiveDraft}
                          issues={issues}
                          onChange={(key, value) =>
                            setDraft((current) => ({ ...current, [key]: value }))
                          }
                        />
                        <div className="next-hosting-form-actions">
                          <span>
                            {formatResource(
                              t("Hosting_ConfigurationProfiles_BasedOnRevision"),
                              selected.data.profile.latestRevisionNumber,
                            )}
                          </span>
                          <button
                            className="button button--primary"
                            type="button"
                            disabled={publish.isPending || !draftDirty}
                            onClick={() => submitValues((values) => publish.mutate(values))}
                          >
                            {publish.isPending
                              ? t("Hosting_ConfigurationProfiles_Publishing")
                              : t("Hosting_ConfigurationProfiles_Publish")}
                          </button>
                        </div>
                      </>
                    ) : (
                      <StatePanel
                        kind="info"
                        title={t("Hosting_ConfigurationProfiles_ReadOnlyTitle")}
                      >
                        {t("Hosting_ConfigurationProfiles_ReadOnlyDescription")}
                      </StatePanel>
                    )}
                    <section className="next-hosting-profile-history">
                      <header className="next-hosting-section-heading">
                        <div>
                          <span className="kicker">
                            {t("Hosting_ConfigurationProfiles_AuditTrail")}
                          </span>
                          <h3>{t("Hosting_ConfigurationProfiles_RevisionHistory")}</h3>
                        </div>
                        <p>
                          {formatResource(
                            t("Hosting_ConfigurationProfiles_RevisionCount"),
                            selected.data.revisions.length,
                          )}
                        </p>
                      </header>
                      {selected.data.revisions
                        .toSorted((left, right) => right.revisionNumber - left.revisionNumber)
                        .map((revision) => (
                          <details
                            key={revision.primaryKey}
                            open={
                              revision.revisionNumber ===
                              selected.data?.profile.latestRevisionNumber
                            }
                          >
                            <summary>
                              <span>
                                <strong>
                                  {t("Hosting_ConfigurationProfiles_Revision")}{" "}
                                  {revision.revisionNumber}
                                </strong>
                                <small>{formatDate(revision.timeStampCreated)}</small>
                              </span>
                              <span>
                                {formatResource(
                                  t("Hosting_ConfigurationProfiles_ValueCount"),
                                  Object.keys(revision.values).length,
                                )}
                              </span>
                            </summary>
                            <dl className="next-hosting-facts next-hosting-facts--compact">
                              {definitions
                                .filter((definition) => definition.key in revision.values)
                                .map((definition) => {
                                  const value = revision.values[definition.key];
                                  const redacted = revision.redactedKeys.includes(definition.key);
                                  return (
                                    <div key={definition.key}>
                                      <dt>{definition.displayName}</dt>
                                      <dd>
                                        {redacted
                                          ? t("Hosting_ConfigurationProfiles_StoredSecurely")
                                          : definition.sensitive
                                            ? "••••••••"
                                            : Array.isArray(value)
                                              ? value.join(", ")
                                              : value === null || value === undefined
                                                ? t("Hosting_ConfigurationProfiles_NotSet")
                                                : String(value)}
                                      </dd>
                                    </div>
                                  );
                                })}
                            </dl>
                            {revision.redactedKeys.length > 0 && (
                              <p className="next-hosting-muted">
                                {t("Hosting_ConfigurationProfiles_RedactedNotice")}
                              </p>
                            )}
                          </details>
                        ))}
                    </section>
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
                      challenge={selected.data.profile.title}
                      onCancel={() => {
                        if (!archive.isPending) setArchiveOpen(false);
                      }}
                      onConfirm={() => {
                        if (!archive.isPending) archive.mutate();
                      }}
                    />
                  </section>
                  {selected.data.canManageAccess && (
                    <ProfileAccessSection
                      profileId={selected.data.profile.primaryKey}
                      access={access.data}
                      loading={access.isLoading}
                      error={access.error}
                      pending={changeAccess.isPending}
                      onRetry={() => void access.refetch()}
                      onChange={(change) => {
                        setMessage(undefined);
                        changeAccess.mutate(change);
                      }}
                    />
                  )}
                </>
              )}
            </main>
          </div>
        </>
      )}
    </div>
  );
}
