import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { StatePanel } from "@/components/primitives";
import type { ConfigurationProfileAccess } from "@/features/hosting/types";

const permissionKeys: Record<string, TranslationKey> = {
  cfg: "Hosting_ConfigurationProfiles_PermissionFullControl",
  "cfg:view": "Hosting_ConfigurationProfiles_PermissionView",
  "cfg:modify": "Hosting_ConfigurationProfiles_PermissionModify",
  "cfg:manage-access": "Hosting_ConfigurationProfiles_PermissionManageAccess",
  "cfg:archive": "Hosting_ConfigurationProfiles_PermissionArchive",
};

export interface ConfigurationProfileAccessChange {
  subject: "user" | "role";
  id: string;
  permission: string;
  grant: boolean;
}

export function ConfigurationProfileAccessSection({
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
  onChange(change: ConfigurationProfileAccessChange): void;
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
  const permissionLabel = (value: string) => {
    const key = permissionKeys[value];
    return key ? t(key) : value;
  };

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
          <h2 id={`${profileId}-access-title`}>
            {t("Hosting_ConfigurationProfiles_ScopedAccess")}
          </h2>
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
              {(["user", "role"] as const).map((item) => (
                <label key={item}>
                  <input
                    type="radio"
                    name="profile-access-subject"
                    checked={subject === item}
                    onChange={() => {
                      setSubject(item);
                      setSubjectId("");
                      setValidationError(undefined);
                    }}
                  />
                  {t(
                    item === "user"
                      ? "Hosting_ConfigurationProfiles_User"
                      : "Hosting_ConfigurationProfiles_Role",
                  )}
                </label>
              ))}
            </fieldset>
            <label className="field" htmlFor="profile-access-subject-id">
              <span>
                {t(
                  subject === "user"
                    ? "Hosting_ConfigurationProfiles_UserId"
                    : "Hosting_ConfigurationProfiles_RoleId",
                )}
              </span>
              <input
                id="profile-access-subject-id"
                aria-label={t(
                  subject === "user"
                    ? "Hosting_ConfigurationProfiles_UserId"
                    : "Hosting_ConfigurationProfiles_RoleId",
                )}
                value={subjectId}
                inputMode={subject === "role" ? "numeric" : "text"}
                autoComplete="off"
                aria-invalid={Boolean(validationError)}
                onChange={(event) => setSubjectId(event.target.value)}
              />
            </label>
            <label className="field" htmlFor="profile-access-permission">
              <span>{t("Hosting_ConfigurationProfiles_Permission")}</span>
              <select
                id="profile-access-permission"
                aria-label={t("Hosting_ConfigurationProfiles_Permission")}
                value={selectedPermission}
                disabled={allowedPermissions.length === 0}
                onChange={(event) => setPermission(event.target.value)}
              >
                {allowedPermissions.map((item) => (
                  <option key={item} value={item}>
                    {permissionLabel(item)}
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
                    <h3>{t("Hosting_ConfigurationProfiles_DirectUsers")}</h3>
                    {access?.users.map((user) => (
                      <article key={user.userId}>
                        <div>
                          <strong>{user.nickname}</strong>
                          <small>{user.userId}</small>
                        </div>
                        <ul>
                          {user.permissions.map((item) => (
                            <li key={item}>
                              <span>{permissionLabel(item)}</span>
                              <button
                                className="button button--ghost button--small"
                                type="button"
                                disabled={pending}
                                aria-label={formatResource(
                                  t("Hosting_ConfigurationProfiles_RevokeNamed"),
                                  permissionLabel(item),
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
                    <h3>{t("Hosting_ConfigurationProfiles_DirectRoles")}</h3>
                    {access?.roles.map((role) => (
                      <article key={role.roleId}>
                        <div>
                          <strong>{role.title}</strong>
                          <small>{role.description || `#${role.roleId}`}</small>
                        </div>
                        <ul>
                          {role.permissions.map((item) => (
                            <li key={item}>
                              <span>{permissionLabel(item)}</span>
                              <button
                                className="button button--ghost button--small"
                                type="button"
                                disabled={pending}
                                aria-label={formatResource(
                                  t("Hosting_ConfigurationProfiles_RevokeNamed"),
                                  permissionLabel(item),
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
