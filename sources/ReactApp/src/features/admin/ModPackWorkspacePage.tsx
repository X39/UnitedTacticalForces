import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useParams } from "react-router";
import { formatDateTime } from "@/app/format";
import { useAuth } from "@/auth/auth";
import { useAppToastOnChange } from "@/components/AppToasts";
import {
  ConfirmDialog,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import {
  materializeRevision,
  ModPackContentEditor,
  type ModPackEditorValue,
  validateModPackEditor,
} from "@/features/admin/ModPackContentEditor";
import {
  canAccessModPack,
  canDeleteModPack,
  canModifyModPack,
  modPackQueryKeys,
  modPackService,
  normalizeModPackSection,
  type ModCatalogItem,
  type ModPackMembership,
  type ModPackWorkspaceSection,
} from "@/features/admin/modPacks";
import "@/features/admin/modPacks.css";

function editorValue(
  membership: ModPackMembership | undefined,
  catalog: ModCatalogItem[],
): ModPackEditorValue {
  return {
    mods: (membership?.mods ?? []).map((mod) => {
      const catalogMod = catalog.find((item) => item.primaryKey === mod.modFk);
      const selectedVersion = mod.pinnedVersionFk
        ? catalogMod?.versions.find((version) => version.primaryKey === mod.pinnedVersionFk)
        : catalogMod?.versions[0];
      return {
        key: `catalog:${mod.modFk}`,
        modId: mod.modFk,
        pinnedVersionId: mod.pinnedVersionFk,
        source: mod.canonicalSourceIdentifier,
        title: mod.title,
        providerIdentifier: mod.providerIdentifier,
        usageRole:
          mod.usageRole === "ClientOnly" || mod.usageRole === "ServerOnly"
            ? mod.usageRole
            : "Shared",
        versionLabel: selectedVersion?.versionLabel ?? "Latest",
        versionSelector: selectedVersion?.versionSelector ?? "",
        installerIdentifier: selectedVersion?.installerIdentifier ?? "",
        installerConfiguration: selectedVersion?.installerConfiguration ?? "{}",
        uploadFile: null,
      };
    }),
    dependencies: (membership?.dependencies ?? []).map((dependency) => ({
      definitionId: dependency.childDefinitionFk,
      title: `#${dependency.childDefinitionFk}`,
      pinnedRevisionId: dependency.pinnedChildRevisionFk,
    })),
  };
}

function contentIdentity(value: ModPackEditorValue): string {
  return JSON.stringify({
    mods: value.mods.map((mod) => [mod.modId, mod.key, mod.pinnedVersionId]),
    dependencies: value.dependencies.map((dependency) => [
      dependency.definitionId,
      dependency.pinnedRevisionId,
    ]),
  });
}

export function Component() {
  const { t: translate } = useTranslation();
  const t = (key: string) => translate(key as never, { defaultValue: key });
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const params = useParams<{ modPackId: string; section?: string }>();
  const routeId = params.modPackId ?? "";
  const modPackId = Number(routeId);
  const validId = Number.isSafeInteger(modPackId);
  const authorized = validId && canAccessModPack(user, modPackId);
  const section = normalizeModPackSection(params.section);
  const workspaceQuery = useQuery({
    queryKey: [modPackQueryKeys.workspace(modPackId), "normalized"],
    queryFn: ({ signal }) => modPackService.normalizedWorkspace(modPackId, signal),
    enabled: authorized,
  });
  const workspace = workspaceQuery.data;
  const latest =
    workspace?.revisions.find((revision) => revision.isActive) ?? workspace?.revisions[0];
  const revisionId = latest?.revisionId;
  const directQuery = useQuery({
    queryKey: modPackQueryKeys.members(revisionId ?? 0, false),
    queryFn: ({ signal }) => modPackService.members(revisionId!, false, signal),
    enabled: revisionId != null,
  });
  const effectiveQuery = useQuery({
    queryKey: modPackQueryKeys.members(revisionId ?? 0, true),
    queryFn: ({ signal }) => modPackService.members(revisionId!, true, signal),
    enabled: revisionId != null,
  });
  const capabilities = useQuery({
    queryKey: modPackQueryKeys.capabilities,
    queryFn: ({ signal }) => modPackService.capabilities(signal),
    enabled: authorized,
  });
  const capability = capabilities.data?.find(
    (item) => item.gameIdentifier === workspace?.gameIdentifier,
  );
  const catalogQuery = useQuery({
    queryKey: modPackQueryKeys.mods(workspace?.gameIdentifier ?? "", ""),
    queryFn: ({ signal }) => modPackService.mods(workspace!.gameIdentifier, "", signal),
    enabled: Boolean(workspace?.gameIdentifier),
  });
  const baseContent = useMemo(
    () => editorValue(directQuery.data, catalogQuery.data ?? []),
    [catalogQuery.data, directQuery.data],
  );
  const [contentOverride, setContentOverride] = useState<ModPackEditorValue>();
  const [dangerOpen, setDangerOpen] = useState(false);
  const content = contentOverride ?? baseContent;
  const isDirty =
    contentOverride != null && contentIdentity(contentOverride) !== contentIdentity(baseContent);
  useUnsavedChanges(isDirty);
  const validation = validateModPackEditor(content);
  const appendMutation = useMutation({
    mutationFn: async () => {
      if (!workspace?.gameIdentifier || validation)
        throw new Error(validation ?? "Mod pack is unavailable.");
      return modPackService.appendRevision(
        modPackId,
        await materializeRevision(workspace.gameIdentifier, content),
      );
    },
    onSuccess: async () => {
      setContentOverride(undefined);
      await queryClient.invalidateQueries({ queryKey: modPackQueryKeys.all });
      await workspaceQuery.refetch();
    },
  });
  const deactivateMutation = useMutation({
    mutationFn: () => modPackService.deactivate(modPackId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: modPackQueryKeys.all });
      navigate("/command/mod-packs");
    },
  });
  useAppToastOnChange(
    appendMutation.isSuccess ? t("A new immutable revision was saved.") : undefined,
  );
  useAppToastOnChange(
    appendMutation.isError
      ? appendMutation.error instanceof Error
        ? appendMutation.error.message
        : t("The revision could not be saved.")
      : deactivateMutation.isError
        ? t("Auto_ModPackCouldNotBeDeactivatedRetryFromThe_3003a328")
        : undefined,
    "error",
  );
  const href = (target?: ModPackWorkspaceSection) =>
    target && target !== "overview"
      ? `/command/mod-packs/${routeId}/${target}`
      : `/command/mod-packs/${routeId}`;

  if (!validId || !authorized)
    return (
      <StatePanel kind="forbidden" title={t("Auto_ModPackAdministratorAccessRequired_95eb8250")}>
        {t("Auto_ModPackDetailsAreVisibleOnlyToAuthorizedAsset_0513063c")}
      </StatePanel>
    );

  const title = workspace?.title?.trim() || `Mod pack #${routeId}`;
  return (
    <section className="modpack-workspace-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_CurrentArea_ModPackDetail")}
        title={title}
        description={t("Normalized mod membership and immutable revision history.")}
        actions={
          <>
            <Link className="button button--ghost" to="/command/mod-packs">
              {t("Auto_AllModPacks_82ff50f7")}
            </Link>
            <button
              className="button button--ghost"
              type="button"
              disabled={workspaceQuery.isFetching}
              onClick={() => void workspaceQuery.refetch()}
            >
              {t("Common_Refresh")}
            </button>
            {canModifyModPack(user, modPackId) && (
              <Link className="button button--primary" to={href("edit")}>
                {t("Edit")}
              </Link>
            )}
          </>
        }
      />
      {(workspaceQuery.isError || directQuery.isError || catalogQuery.isError) && (
        <StatePanel kind="error">{t("Auto_FailedToLoadModPack_8b2eb217")}</StatePanel>
      )}
      <section className="command-panel modpack-workspace">
        {workspace && (
          <>
            <div className="modpack-workspace__summary">
              <div>
                <StatusBadge tone="info">{workspace.gameIdentifier}</StatusBadge>
                <strong>{title}</strong>
              </div>
              <dl>
                <div>
                  <dt>{t("Definition")}</dt>
                  <dd>{workspace.definitionId}</dd>
                </div>
                <div>
                  <dt>{t("Latest revision")}</dt>
                  <dd>#{revisionId ?? "—"}</dd>
                </div>
                <div>
                  <dt>{t("Effective mods")}</dt>
                  <dd>{effectiveQuery.data?.mods.length ?? "—"}</dd>
                </div>
              </dl>
            </div>
            <nav
              className="workspace-tabs"
              aria-label={t("Auto_ModPackWorkspaceNavigation_755c1101")}
            >
              {(["overview", "artifact", "edit", "revisions", "danger"] as const).map((target) => (
                <Link
                  key={target}
                  to={href(target)}
                  className={`${section === target ? "active" : ""}${target === "danger" ? " danger" : ""}`.trim()}
                  aria-current={section === target ? "page" : undefined}
                >
                  {t(
                    target === "artifact" ? "Content" : target[0]!.toUpperCase() + target.slice(1),
                  )}
                </Link>
              ))}
            </nav>
          </>
        )}
        {workspaceQuery.isPending ? (
          <StatePanel kind="loading">{t("Auto_LoadingModPackWorkspace_b80419c1")}</StatePanel>
        ) : !workspace ? (
          <StatePanel kind="empty">{t("Auto_ModPackNotFound_7d5fe348")}</StatePanel>
        ) : section === "edit" ? (
          canModifyModPack(user, modPackId) ? (
            <section className="modpack-workspace__section command-panel">
              <header className="command-panel__header">
                <div>
                  <span className="kicker">{t("New revision")}</span>
                  <h2>{t("Edit normalized content")}</h2>
                </div>
                <StatusBadge tone={isDirty && !validation ? "success" : "neutral"}>
                  {isDirty ? t("Unsaved") : t("Current")}
                </StatusBadge>
              </header>
              {capability ? (
                <ModPackContentEditor
                  capability={capability}
                  value={content}
                  onChange={setContentOverride}
                  excludeDefinitionId={modPackId}
                />
              ) : (
                <StatePanel kind="error">
                  {t("Game content capabilities are unavailable.")}
                </StatePanel>
              )}
              {validation && <StatePanel kind="warning">{validation}</StatePanel>}
              <div className="form-actions">
                <button
                  className="button button--primary"
                  type="button"
                  disabled={!isDirty || Boolean(validation) || appendMutation.isPending}
                  onClick={() => appendMutation.mutate()}
                >
                  {appendMutation.isPending ? t("Saving…") : t("Save new revision")}
                </button>
                <button
                  className="button button--ghost"
                  type="button"
                  disabled={appendMutation.isPending}
                  onClick={() => setContentOverride(undefined)}
                >
                  {t("Reset")}
                </button>
              </div>
            </section>
          ) : (
            <StatePanel kind="forbidden">
              {t("Auto_ModifyAccessIsRequiredToViewRevisionHistory_db813722")}
            </StatePanel>
          )
        ) : section === "revisions" ? (
          <section className="modpack-workspace__section command-panel">
            <header className="command-panel__header">
              <div>
                <span className="kicker">{t("Auto_RevisionHistory_5ae0d3d1")}</span>
                <h2>{t("Immutable revisions")}</h2>
              </div>
              <StatusBadge>{workspace.revisions.length}</StatusBadge>
            </header>
            <div className="command-table-scroll">
              <table className="command-table">
                <thead>
                  <tr>
                    <th>{t("Revision")}</th>
                    <th>{t("Created")}</th>
                    <th>{t("Status")}</th>
                    <th>{t("Downloads")}</th>
                  </tr>
                </thead>
                <tbody>
                  {workspace.revisions.map((revision) => (
                    <tr key={revision.revisionId}>
                      <td>#{revision.revisionId}</td>
                      <td>{formatDateTime(revision.timeStampCreated)}</td>
                      <td>
                        <StatusBadge tone={revision.isActive ? "success" : "neutral"}>
                          {revision.isActive ? t("Active") : t("Immutable")}
                        </StatusBadge>
                      </td>
                      <td>
                        <RevisionFormatActions
                          revisionId={revision.revisionId}
                          formats={capability?.modPackFormats ?? []}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        ) : section === "danger" ? (
          canDeleteModPack(user, modPackId) ? (
            <section className="modpack-workspace__section command-panel danger-panel">
              <h2>{t("Auto_DeactivateModPack_9b34e1b1")}</h2>
              <p>{t("Auto_DeactivationHidesThisModPackFromActiveSelectionLists_bf5e78cd")}</p>
              <button
                className="button button--danger"
                type="button"
                onClick={() => setDangerOpen(true)}
              >
                {t("Deactivate")}
              </button>
            </section>
          ) : (
            <StatePanel kind="forbidden">
              {t("Auto_YouNeedModPackDeleteAccessBeforeThisPackage_d15df0c2")}
            </StatePanel>
          )
        ) : (
          <ContentSummary
            direct={directQuery.data}
            effective={effectiveQuery.data}
            formats={capability?.modPackFormats ?? []}
            revisionId={revisionId}
          />
        )}
      </section>
      <ConfirmDialog
        open={dangerOpen}
        title={t("Auto_DeactivateModPack_9b34e1b1")}
        description={t("Auto_DeactivationHidesThisModPackFromActiveSelectionLists_bf5e78cd")}
        confirmLabel={t("Deactivate")}
        cancelLabel={t("Cancel")}
        challenge={title}
        onCancel={() => setDangerOpen(false)}
        onConfirm={() => {
          setDangerOpen(false);
          deactivateMutation.mutate();
        }}
      />
    </section>
  );
}

function RevisionFormatActions({
  revisionId,
  formats,
}: {
  revisionId: number;
  formats: Array<{ identifier: string; displayName: string; fileExtension: string }>;
}) {
  const download = useMutation({
    mutationFn: async (format: { identifier: string; fileExtension: string }) => {
      const blob = await modPackService.download(
        modPackService.formatDownloadPath(revisionId, format.identifier),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `mod-pack-${revisionId}.${format.fileExtension}`;
      anchor.click();
      URL.revokeObjectURL(url);
    },
  });
  return (
    <div className="form-actions">
      {formats.map((format) => (
        <button
          className="button button--ghost"
          type="button"
          key={format.identifier}
          disabled={download.isPending}
          onClick={() => download.mutate(format)}
        >
          {format.displayName}
        </button>
      ))}
    </div>
  );
}

function ContentSummary({
  direct,
  effective,
  formats,
  revisionId,
}: {
  direct: ModPackMembership | undefined;
  effective: ModPackMembership | undefined;
  formats: Array<{ identifier: string; displayName: string; fileExtension: string }>;
  revisionId: number | undefined;
}) {
  const { t: translate } = useTranslation();
  const t = (key: string) => translate(key as never, { defaultValue: key });
  const download = useMutation({
    mutationFn: async (format: {
      identifier: string;
      displayName: string;
      fileExtension: string;
    }) => {
      if (revisionId == null) return;
      const blob = await modPackService.download(
        modPackService.formatDownloadPath(revisionId, format.identifier),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `mod-pack.${format.fileExtension}`;
      anchor.click();
      URL.revokeObjectURL(url);
    },
  });
  return (
    <div className="modpack-overview">
      <section className="command-panel modpack-workspace__section">
        <header className="command-panel__header">
          <div>
            <span className="kicker">{t("Content")}</span>
            <h2>{t("Effective mod list")}</h2>
          </div>
          <StatusBadge>{effective?.mods.length ?? 0}</StatusBadge>
        </header>
        {!effective ? (
          <StatePanel kind="loading">{t("Common_LoadingData")}</StatePanel>
        ) : effective.mods.length === 0 ? (
          <StatePanel>{t("No mods are resolved for this revision.")}</StatePanel>
        ) : (
          <div className="command-table-scroll">
            <table className="command-table">
              <thead>
                <tr>
                  <th>{t("Order")}</th>
                  <th>{t("Mod")}</th>
                  <th>{t("Provider")}</th>
                  <th>{t("Usage")}</th>
                </tr>
              </thead>
              <tbody>
                {effective.mods.map((mod, index) => (
                  <tr key={mod.modFk}>
                    <td>{index + 1}</td>
                    <td>
                      <strong>{mod.title}</strong>
                      <span>{mod.canonicalSourceIdentifier}</span>
                    </td>
                    <td>{mod.providerIdentifier}</td>
                    <td>{mod.usageRole}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      <section className="command-panel modpack-workspace__section">
        <header className="command-panel__header">
          <div>
            <span className="kicker">{t("Direct membership")}</span>
            <h2>{t("Nested mod packs")}</h2>
          </div>
          <StatusBadge>{direct?.dependencies?.length ?? 0}</StatusBadge>
        </header>
        {(direct?.dependencies?.length ?? 0) === 0 ? (
          <StatePanel>{t("No nested mod packs.")}</StatePanel>
        ) : (
          <ul className="modpack-dependency-list">
            {direct?.dependencies?.map((dependency) => (
              <li key={dependency.childDefinitionFk}>
                <strong>#{dependency.childDefinitionFk}</strong>
                <span>
                  {dependency.pinnedChildRevisionFk
                    ? `${t("Pinned revision")} #${dependency.pinnedChildRevisionFk}`
                    : t("Tracks latest revision")}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
      <section className="command-panel modpack-workspace__section">
        <header className="command-panel__header">
          <div>
            <span className="kicker">{t("Game formats")}</span>
            <h2>{t("Import and export")}</h2>
          </div>
        </header>
        {formats.length ? (
          <>
            <div className="form-actions">
              {formats.map((format) => (
                <button
                  className="button button--ghost"
                  type="button"
                  key={format.identifier}
                  disabled={revisionId == null || download.isPending}
                  onClick={() => download.mutate(format)}
                >
                  {t("Download")} {format.displayName}
                </button>
              ))}
            </div>
            {download.isError && <StatePanel kind="error">{download.error.message}</StatePanel>}
          </>
        ) : (
          <StatePanel>
            {t("This game has no custom mod-pack file format. Use the normalized list above.")}
          </StatePanel>
        )}
      </section>
    </div>
  );
}
