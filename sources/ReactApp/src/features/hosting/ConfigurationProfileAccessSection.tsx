import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { TranslationKey } from "@/app/localizationTypes";
import { StatePanel, useUnsavedChanges } from "@/components/primitives";
import { hostingService } from "@/features/hosting/services";
import type {
  ConfigurationProfileAccess,
  ConfigurationProfileAccessMatrix,
  ConfigurationProfileRoleCandidate,
  ConfigurationProfileUserCandidate,
} from "@/features/hosting/types";

const permissionKeys: Record<string, string> = {
  cfg: "Hosting_ConfigurationProfiles_PermissionFullControl",
  "cfg:view": "Hosting_ConfigurationProfiles_PermissionView",
  "cfg:modify": "Hosting_ConfigurationProfiles_PermissionModify",
  "cfg:manage-access": "Hosting_ConfigurationProfiles_PermissionManageAccess",
  "cfg:archive": "Hosting_ConfigurationProfiles_PermissionArchive",
};

const permissionDescriptionKeys: Record<string, string> = {
  cfg: "React_ConfigurationProfiles_PermissionFullControlDescription",
  "cfg:view": "React_ConfigurationProfiles_PermissionViewDescription",
  "cfg:modify": "React_ConfigurationProfiles_PermissionModifyDescription",
  "cfg:manage-access": "React_ConfigurationProfiles_PermissionManageAccessDescription",
  "cfg:archive": "React_ConfigurationProfiles_PermissionArchiveDescription",
};

type MatrixRow = {
  kind: "user" | "role";
  id: string;
  label: string;
  description: string;
  permissions: string[];
};

export function ConfigurationProfileAccessSection({
  profileId,
  access,
  loading,
  error,
  pending,
  onRetry,
  onSave,
}: {
  profileId: string;
  access: ConfigurationProfileAccess | undefined;
  loading: boolean;
  error: Error | null;
  pending: boolean;
  onRetry(): void;
  onSave(matrix: ConfigurationProfileAccessMatrix): void;
}) {
  const { t } = useTranslation();
  const [subject, setSubject] = useState<"user" | "role">("user");
  const [search, setSearch] = useState("");
  const [activeCandidateIndex, setActiveCandidateIndex] = useState(-1);
  const [draftState, setDraftState] = useState<{
    baselineSignature: string;
    rows: MatrixRow[];
  }>();
  const baselineRows = useMemo(() => accessRows(access), [access]);
  const baselineSignature = JSON.stringify(baselineRows);
  const draftRows =
    draftState?.baselineSignature === baselineSignature ? draftState.rows : undefined;
  const setDraftRows = (nextRows: MatrixRow[] | undefined) =>
    setDraftState(nextRows === undefined ? undefined : { baselineSignature, rows: nextRows });
  const rows = draftRows ?? baselineRows;
  const dirty = draftRows !== undefined && JSON.stringify(draftRows) !== baselineSignature;
  useUnsavedChanges(dirty);

  const candidates = useQuery<
    Array<ConfigurationProfileUserCandidate | ConfigurationProfileRoleCandidate>
  >({
    queryKey: ["configuration-profile-access-candidates", profileId, subject, search.trim()],
    queryFn: async ({ signal }) =>
      subject === "user"
        ? await hostingService.profileUserCandidates(profileId, search.trim(), signal)
        : await hostingService.profileRoleCandidates(profileId, search.trim(), signal),
    enabled: !loading && !error && search.trim().length >= 1,
    staleTime: 20_000,
  });
  const availableCandidates = (candidates.data ?? []).filter((candidate) => {
    const id = "userId" in candidate ? candidate.userId : String(candidate.roleId);
    return !rows.some((row) => row.kind === subject && row.id === id);
  });
  const permissionLabel = (permission: string) => {
    const key = permissionKeys[permission];
    return key ? t(key as TranslationKey) : permission;
  };
  const permissionDescription = (permission: string) => {
    const key = permissionDescriptionKeys[permission];
    return key ? t(key as TranslationKey) : "";
  };
  const addCandidate = (
    candidate: ConfigurationProfileUserCandidate | ConfigurationProfileRoleCandidate,
  ) => {
    const user = "userId" in candidate;
    setDraftRows([
      ...rows,
      {
        kind: subject,
        id: user ? candidate.userId : String(candidate.roleId),
        label: user ? candidate.nickname : candidate.title,
        description: user ? candidate.userId : candidate.description,
        permissions: [],
      },
    ]);
    setSearch("");
    setActiveCandidateIndex(-1);
  };
  const updatePermission = (row: MatrixRow, permission: string, checked: boolean) => {
    const fullControl = permission === "cfg";
    let permissions = row.permissions;
    if (checked)
      permissions = fullControl
        ? ["cfg"]
        : [...permissions.filter((item) => item !== "cfg"), permission];
    else permissions = permissions.filter((item) => item !== permission);
    setDraftRows(
      rows.map((item) =>
        item.kind === row.kind && item.id === row.id ? { ...item, permissions } : item,
      ),
    );
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
            {t("React_ConfigurationProfiles_AccessMatrixTitle")}
          </h2>
          <p>{t("React_ConfigurationProfiles_AccessMatrixDescription")}</p>
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
          <section
            className="next-hosting-access-picker"
            aria-labelledby={`${profileId}-add-access-title`}
          >
            <div>
              <h3 id={`${profileId}-add-access-title`}>
                {t("React_ConfigurationProfiles_AddPrincipal")}
              </h3>
              <p>{t("React_ConfigurationProfiles_AddPrincipalDescription")}</p>
            </div>
            <fieldset>
              <legend>{t("Hosting_ConfigurationProfiles_SubjectType")}</legend>
              {(["user", "role"] as const).map((kind) => (
                <label key={kind}>
                  <input
                    type="radio"
                    name="profile-access-subject"
                    checked={subject === kind}
                    onChange={() => {
                      setSubject(kind);
                      setSearch("");
                      setActiveCandidateIndex(-1);
                    }}
                  />
                  <span>
                    {t(
                      kind === "user"
                        ? "Hosting_ConfigurationProfiles_User"
                        : "Hosting_ConfigurationProfiles_Role",
                    )}
                  </span>
                </label>
              ))}
            </fieldset>
            <label className="field next-hosting-access-search">
              <span>
                {t(
                  subject === "user"
                    ? "React_ConfigurationProfiles_SearchUsers"
                    : "React_ConfigurationProfiles_SearchRoles",
                )}
              </span>
              <input
                role="combobox"
                aria-expanded={search.trim().length > 0}
                aria-controls={`${profileId}-principal-options`}
                aria-activedescendant={
                  activeCandidateIndex >= 0
                    ? `${profileId}-principal-option-${activeCandidateIndex}`
                    : undefined
                }
                autoComplete="off"
                value={search}
                placeholder={t("React_ConfigurationProfiles_PrincipalSearchPlaceholder")}
                onChange={(event) => {
                  setSearch(event.target.value);
                  setActiveCandidateIndex(-1);
                }}
                onKeyDown={(event) => {
                  if (event.key === "ArrowDown" && availableCandidates.length > 0) {
                    event.preventDefault();
                    setActiveCandidateIndex((current) =>
                      Math.min(current + 1, availableCandidates.length - 1),
                    );
                  } else if (event.key === "ArrowUp" && availableCandidates.length > 0) {
                    event.preventDefault();
                    setActiveCandidateIndex((current) =>
                      current <= 0 ? availableCandidates.length - 1 : current - 1,
                    );
                  } else if (event.key === "Enter" && activeCandidateIndex >= 0) {
                    event.preventDefault();
                    const candidate = availableCandidates[activeCandidateIndex];
                    if (candidate) addCandidate(candidate);
                  } else if (event.key === "Escape") {
                    setSearch("");
                    setActiveCandidateIndex(-1);
                  }
                }}
              />
            </label>
            {search.trim() && (
              <div
                id={`${profileId}-principal-options`}
                className="next-hosting-access-options"
                role="listbox"
              >
                {candidates.isLoading ? (
                  <span>{t("Hosting_ConfigurationProfiles_AccessLoading")}</span>
                ) : candidates.isError ? (
                  <span role="alert">{t("Hosting_ConfigurationProfiles_AccessLoadError")}</span>
                ) : availableCandidates.length === 0 ? (
                  <span>{t("React_ConfigurationProfiles_NoPrincipalMatches")}</span>
                ) : (
                  availableCandidates.map((candidate, index) => {
                    const user = "userId" in candidate;
                    const id = user ? candidate.userId : String(candidate.roleId);
                    const label = user ? candidate.nickname : candidate.title;
                    const description = user ? candidate.userId : candidate.description;
                    return (
                      <button
                        id={`${profileId}-principal-option-${index}`}
                        key={`${subject}-${id}`}
                        type="button"
                        role="option"
                        aria-selected={activeCandidateIndex === index}
                        onMouseEnter={() => setActiveCandidateIndex(index)}
                        onClick={() => addCandidate(candidate)}
                      >
                        <strong>{label}</strong>
                        <small>{description}</small>
                      </button>
                    );
                  })
                )}
              </div>
            )}
          </section>
          {rows.length === 0 ? (
            <StatePanel kind="empty" title={t("Hosting_ConfigurationProfiles_NoDirectAccess")}>
              {t("React_ConfigurationProfiles_NoDirectAccessDescription")}
            </StatePanel>
          ) : (
            <div className="next-hosting-access-matrix-scroll">
              <table className="next-hosting-access-matrix">
                <thead>
                  <tr>
                    <th scope="col">{t("React_ConfigurationProfiles_Principal")}</th>
                    {(access?.allowedPermissions ?? []).map((permission) => (
                      <th scope="col" key={permission} title={permissionDescription(permission)}>
                        <span>{permissionLabel(permission)}</span>
                        <small>{permissionDescription(permission)}</small>
                      </th>
                    ))}
                    <th scope="col">
                      <span className="sr-only">
                        {t("React_ConfigurationProfiles_RemovePrincipal")}
                      </span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={`${row.kind}-${row.id}`}>
                      <th scope="row">
                        <strong>{row.label}</strong>
                        <span>
                          {t(
                            row.kind === "user"
                              ? "Hosting_ConfigurationProfiles_User"
                              : "Hosting_ConfigurationProfiles_Role",
                          )}
                        </span>
                        <small>{row.description}</small>
                      </th>
                      {(access?.allowedPermissions ?? []).map((permission) => (
                        <td key={permission} data-label={permissionLabel(permission)}>
                          <label className="next-hosting-access-check">
                            <input
                              type="checkbox"
                              checked={row.permissions.includes(permission)}
                              aria-label={`${row.label}: ${permissionLabel(permission)}`}
                              onChange={(event) =>
                                updatePermission(row, permission, event.target.checked)
                              }
                            />
                            <span aria-hidden="true" />
                          </label>
                        </td>
                      ))}
                      <td>
                        <button
                          className="button button--ghost button--small"
                          type="button"
                          onClick={() =>
                            setDraftRows(
                              rows.filter((item) => item.kind !== row.kind || item.id !== row.id),
                            )
                          }
                        >
                          {t("React_ConfigurationProfiles_RemovePrincipal")}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {dirty && (
            <div
              className="next-hosting-profile-savebar"
              role="region"
              aria-label={t("React_ConfigurationProfiles_UnsavedAccessChanges")}
            >
              <strong>{t("React_ConfigurationProfiles_UnsavedAccessChanges")}</strong>
              <div>
                <button
                  className="button button--ghost"
                  type="button"
                  disabled={pending}
                  onClick={() => setDraftRows(undefined)}
                >
                  {t("React_ConfigurationProfiles_DiscardChanges")}
                </button>
                <button
                  className="button button--primary"
                  type="button"
                  disabled={pending}
                  onClick={() => onSave(toMatrix(rows))}
                >
                  {pending
                    ? t("React_ConfigurationProfiles_SavingAccess")
                    : t("React_ConfigurationProfiles_SaveAccess")}
                </button>
              </div>
            </div>
          )}
        </>
      )}
    </section>
  );
}

function accessRows(access: ConfigurationProfileAccess | undefined): MatrixRow[] {
  return [
    ...(access?.users ?? []).map((user) => ({
      kind: "user" as const,
      id: user.userId,
      label: user.nickname,
      description: user.userId,
      permissions: [...user.permissions].sort(),
    })),
    ...(access?.roles ?? []).map((role) => ({
      kind: "role" as const,
      id: String(role.roleId),
      label: role.title,
      description: role.description || `#${role.roleId}`,
      permissions: [...role.permissions].sort(),
    })),
  ];
}

function toMatrix(rows: MatrixRow[]): ConfigurationProfileAccessMatrix {
  const activeRows = rows.filter((row) => row.permissions.length > 0);
  return {
    users: activeRows
      .filter((row) => row.kind === "user")
      .map((row) => ({ userId: row.id, permissions: row.permissions })),
    roles: activeRows
      .filter((row) => row.kind === "role")
      .map((row) => ({ roleId: Number(row.id), permissions: row.permissions })),
  };
}
