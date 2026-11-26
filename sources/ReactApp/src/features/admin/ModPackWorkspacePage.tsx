import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { TranslationKey } from "@/app/localizationTypes";
import { Link, useNavigate, useParams } from "react-router";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { useAppToastOnChange } from "@/components/AppToasts";
import { PageStack } from "@/components/PageStack";
import {
  ConfirmDialog,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { toneOf } from "@/components/statusTones";
import { assetStateTones, editStateTones } from "@/features/admin/assetTones";
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

const workspaceTabs = ["overview", "artifact", "edit", "revisions", "danger"] as const;

/** Locale keys of the workspace tab labels, one per tab. */
const workspaceTabKeys: Record<(typeof workspaceTabs)[number], TranslationKey> = {
  overview: "Overview",
  artifact: "React_ModPackWorkspace_Tab_Content",
  edit: "Edit",
  revisions: "Revisions",
  danger: "React_ModPackWorkspace_Tab_Danger",
};

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
  const { t } = useTranslation();
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
    // The API answers 404 with no body; a query cannot resolve to undefined, so a missing mod pack becomes null.
    queryFn: async ({ signal }) =>
      (await modPackService.normalizedWorkspace(modPackId, signal)) ?? null,
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
    appendMutation.isSuccess ? t("React_ModPackEditor_RevisionSaved") : undefined,
  );
  useAppToastOnChange(
    appendMutation.isError
      ? appendMutation.error instanceof Error
        ? appendMutation.error.message
        : t("React_ModPackEditor_RevisionSaveFailed")
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
      <section>
        <PageHeader
          kicker={t("Shared_CommandCenter_Nav_ModPacks")}
          title={t("Shared_CommandCenter_CurrentArea_ModPackDetail")}
        />
        <PageStack>
          <ForbiddenState title={t("Auto_ModPackAdministratorAccessRequired_95eb8250")}>
            {t("Auto_ModPackDetailsAreVisibleOnlyToAuthorizedAsset_0513063c")}
          </ForbiddenState>
        </PageStack>
      </section>
    );

  const title = workspace?.title?.trim() || `Mod pack #${routeId}`;
  const loadFailure = [workspaceQuery, directQuery, catalogQuery].find((query) => query.isError);
  return (
    <section>
      <PageHeader
        kicker={t("Shared_CommandCenter_CurrentArea_ModPackDetail")}
        title={title}
        description={t("React_ModPackEditor_WorkspaceDescription")}
        actions={
          <>
            <ActionLink variant="ghost" to="/command/mod-packs">
              {t("Auto_AllModPacks_82ff50f7")}
            </ActionLink>
            <ActionButton
              variant="ghost"
              disabled={workspaceQuery.isFetching}
              onClick={() => void workspaceQuery.refetch()}
            >
              {t("Common_Refresh")}
            </ActionButton>
            {canModifyModPack(user, modPackId) && (
              <ActionLink variant="primary" to={href("edit")}>
                {t("Edit")}
              </ActionLink>
            )}
          </>
        }
      />
      <PageStack>
        {loadFailure && (
          <ErrorState
            onRetry={() => {
              void workspaceQuery.refetch();
              void directQuery.refetch();
              void catalogQuery.refetch();
            }}
          >
            {formatResource(
              t("Auto_FailedToLoadModPack_8b2eb217"),
              routeId,
              loadFailure.error instanceof Error ? loadFailure.error.message : "",
            )}
          </ErrorState>
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
                    <dt>{t("React_ModPackEditor_EffectiveMods")}</dt>
                    <dd>{effectiveQuery.data?.mods.length ?? "—"}</dd>
                  </div>
                </dl>
              </div>
              <nav
                className="workspace-tabs"
                aria-label={t("Auto_ModPackWorkspaceNavigation_755c1101")}
              >
                {workspaceTabs.map((target) => (
                  <Link
                    key={target}
                    to={href(target)}
                    className={`${section === target ? "active" : ""}${target === "danger" ? " danger" : ""}`.trim()}
                    aria-current={section === target ? "page" : undefined}
                  >
                    {t(workspaceTabKeys[target])}
                  </Link>
                ))}
              </nav>
            </>
          )}
          {workspaceQuery.isPending ? (
            <LoadingState label={t("Auto_LoadingModPackWorkspace_b80419c1")} />
          ) : !workspace ? (
            <EmptyState
              title={t("Pages_ModPacks_Workspace_NotFoundTitle")}
              action={
                <ActionLink variant="ghost" to="/command/mod-packs">
                  {t("Auto_AllModPacks_82ff50f7")}
                </ActionLink>
              }
            >
              {t("Pages_ModPacks_Workspace_NotFoundBody")}
            </EmptyState>
          ) : section === "edit" ? (
            canModifyModPack(user, modPackId) ? (
              <section className="modpack-workspace__section command-panel">
                <header className="command-panel__header">
                  <div>
                    <span className="kicker">{t("React_ModPackEditor_NewRevision")}</span>
                    <h2>{t("React_ModPackEditor_EditContent")}</h2>
                  </div>
                  <StatusBadge
                    tone={toneOf(editStateTones, isDirty && !validation ? "unsaved" : "current")}
                  >
                    {isDirty ? t("React_ModPackEditor_Unsaved") : t("React_ModPackEditor_Current")}
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
                  <ErrorState onRetry={() => void capabilities.refetch()}>
                    {t("React_ModPackEditor_CapabilitiesUnavailable")}
                  </ErrorState>
                )}
                {validation && <StatePanel kind="warning">{validation}</StatePanel>}
                <div className="form-actions">
                  <ActionButton
                    variant="gold"
                    disabled={!isDirty || Boolean(validation) || appendMutation.isPending}
                    onClick={() => appendMutation.mutate()}
                  >
                    {appendMutation.isPending
                      ? t("React_ModPackEditor_Saving")
                      : t("React_ModPackEditor_SaveRevision")}
                  </ActionButton>
                  <ActionButton
                    variant="ghost"
                    disabled={appendMutation.isPending}
                    onClick={() => setContentOverride(undefined)}
                  >
                    {t("Reset")}
                  </ActionButton>
                </div>
              </section>
            ) : (
              <ForbiddenState>
                {t("Auto_ModifyAccessIsRequiredToViewRevisionHistory_db813722")}
              </ForbiddenState>
            )
          ) : section === "revisions" ? (
            <section className="modpack-workspace__section command-panel">
              <header className="command-panel__header">
                <div>
                  <span className="kicker">{t("Auto_RevisionHistory_5ae0d3d1")}</span>
                  <h2>{t("React_ModPackEditor_ImmutableRevisions")}</h2>
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
                          <StatusBadge
                            tone={toneOf(
                              assetStateTones,
                              revision.isActive ? "active" : "inactive",
                            )}
                          >
                            {revision.isActive ? t("Active") : t("React_ModPackEditor_Immutable")}
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
                <ActionButton variant="danger" onClick={() => setDangerOpen(true)}>
                  {t("Deactivate")}
                </ActionButton>
              </section>
            ) : (
              <ForbiddenState>
                {t("Auto_YouNeedModPackDeleteAccessBeforeThisPackage_d15df0c2")}
              </ForbiddenState>
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
      </PageStack>
      <ConfirmDialog
        open={dangerOpen}
        title={t("Auto_DeactivateModPack_9b34e1b1")}
        description={t("Auto_DeactivationHidesThisModPackFromActiveSelectionLists_bf5e78cd")}
        confirmLabel={t("Deactivate")}
        cancelLabel={t("React_ModPackWorkspace_Deactivate_Dismiss")}
        initialFocus="dismiss"
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
        <ActionButton
          variant="ghost"
          key={format.identifier}
          disabled={download.isPending}
          onClick={() => download.mutate(format)}
        >
          {format.displayName}
        </ActionButton>
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
  const { t } = useTranslation();
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
            <span className="kicker">{t("React_ModPackWorkspace_Tab_Content")}</span>
            <h2>{t("React_ModPackEditor_EffectiveModList")}</h2>
          </div>
          <StatusBadge>{effective?.mods.length ?? 0}</StatusBadge>
        </header>
        {/* Without a revision the membership query never runs, so an absent result is the empty list, not a load. */}
        {!effective && revisionId != null ? (
          <LoadingState />
        ) : !effective || effective.mods.length === 0 ? (
          <EmptyState title={t("Pages_ModPacks_Workspace_NoModsTitle")}>
            {t("Pages_ModPacks_Workspace_NoModsBody")}
          </EmptyState>
        ) : (
          <div className="command-table-scroll">
            <table className="command-table">
              <thead>
                <tr>
                  <th>{t("React_ModPackEditor_Order")}</th>
                  <th>{t("React_ModPackEditor_Mod")}</th>
                  <th>{t("Provider")}</th>
                  <th>{t("React_ModPackEditor_Usage")}</th>
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
            <span className="kicker">{t("React_ModPackEditor_DirectMembership")}</span>
            <h2>{t("React_ModPackEditor_NestedModPacks")}</h2>
          </div>
          <StatusBadge>{direct?.dependencies?.length ?? 0}</StatusBadge>
        </header>
        {(direct?.dependencies?.length ?? 0) === 0 ? (
          <EmptyState title={t("Pages_ModPacks_Workspace_NoNestedTitle")}>
            {t("Pages_ModPacks_Workspace_NoNestedBody")}
          </EmptyState>
        ) : (
          <ul className="modpack-dependency-list">
            {direct?.dependencies?.map((dependency) => (
              <li key={dependency.childDefinitionFk}>
                <strong>#{dependency.childDefinitionFk}</strong>
                <span>
                  {dependency.pinnedChildRevisionFk
                    ? `${t("React_ModPackEditor_PinnedRevision")} #${dependency.pinnedChildRevisionFk}`
                    : t("React_ModPackEditor_TracksLatest")}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
      <section className="command-panel modpack-workspace__section">
        <header className="command-panel__header">
          <div>
            <span className="kicker">{t("React_ModPackEditor_GameFormats")}</span>
            <h2>{t("React_ModPackEditor_ImportAndExport")}</h2>
          </div>
        </header>
        {formats.length ? (
          <>
            <div className="form-actions">
              {formats.map((format) => (
                <ActionButton
                  variant="ghost"
                  key={format.identifier}
                  disabled={revisionId == null || download.isPending}
                  onClick={() => download.mutate(format)}
                >
                  {t("React_ModPackEditor_Download")} {format.displayName}
                </ActionButton>
              ))}
            </div>
            {download.isError && <StatePanel kind="error">{download.error.message}</StatePanel>}
          </>
        ) : (
          <EmptyState title={t("Pages_ModPacks_Workspace_NoFormatTitle")}>
            {t("Pages_ModPacks_Workspace_NoFormatBody")}
          </EmptyState>
        )}
      </section>
    </div>
  );
}
