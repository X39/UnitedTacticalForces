import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useParams } from "react-router";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { useAppToastOnChange } from "@/components/AppToasts";
import {
  ConfirmDialog,
  Metric,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { ModPackStandaloneSelector } from "@/features/admin/ModPackStandaloneSelector";
import {
  canAccessModPack,
  latestActiveRevision,
  modPackDownloadPath,
  modPackQueryKeys,
  modPackService,
  normalizeModPackSection,
  type FullModPackDefinition,
  type ModPackDownloadAudit,
  type ModPackRevision,
  type ModPackWorkspace,
  type ModPackWorkspaceSection,
} from "@/features/admin/modPacks";
import "@/features/admin/modPacks.css";

const maxHtmlBytes = 10 * 1024 * 1024;

function title(workspace: ModPackWorkspace | undefined, id: string, fallback: string): string {
  return workspace?.definition.title?.trim() || formatResource(fallback, id);
}

function selectedDefinitions(workspace: ModPackWorkspace): FullModPackDefinition[] {
  const unique = new Map<number, FullModPackDefinition>();
  workspace.compositionMembers.forEach((member) => {
    const id = member.definition.primaryKey;
    if (id == null) return;
    const existing = unique.get(id);
    if (!existing) {
      unique.set(id, {
        ...member.definition,
        owner: member.owner,
        modPackRevisions: [member.revision],
      });
      return;
    }
    if (
      !existing.modPackRevisions.some(
        (revision) => revision.primaryKey === member.revision.primaryKey,
      )
    )
      existing.modPackRevisions.push(member.revision);
  });
  return [...unique.values()];
}

function revisionIds(definitions: FullModPackDefinition[]): number[] {
  return definitions
    .map((definition) => latestActiveRevision(definition)?.primaryKey)
    .filter((id): id is number => id != null);
}

function sameIds(left: number[], right: number[]): boolean {
  if (left.length !== right.length) return false;
  const sortedLeft = [...left].sort((a, b) => a - b);
  const sortedRight = [...right].sort((a, b) => a - b);
  return sortedLeft.every((value, index) => value === sortedRight[index]);
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
  const query = useQuery({
    queryKey: modPackQueryKeys.workspace(modPackId),
    queryFn: ({ signal }) => modPackService.workspace(modPackId, signal),
    enabled: authorized,
  });
  const workspace = query.data;
  const isComposition = workspace?.definition.isComposition ?? false;
  const modPackTitle = title(
    workspace,
    routeId,
    t("Panels_CreateModPackPanel_FallbackModPackTitle"),
  );
  const [editTitleOverride, setEditTitleOverride] = useState<string>();
  const [replacementHtml, setReplacementHtml] = useState("");
  const [replacementHtmlFileName, setReplacementHtmlFileName] = useState("");
  const [selectedOverride, setSelectedOverride] = useState<FullModPackDefinition[]>();
  const [fileError, setFileError] = useState<string>();
  const [dangerOpen, setDangerOpen] = useState(false);
  const editTitle = editTitleOverride ?? workspace?.definition.title ?? "";
  const baseSelected = useMemo(
    () => (workspace?.definition.isComposition ? selectedDefinitions(workspace) : []),
    [workspace],
  );
  const selected = selectedOverride ?? baseSelected;
  const selectedIds = useMemo(() => revisionIds(selected), [selected]);
  const workspaceMemberIds = useMemo(
    () =>
      (workspace?.compositionMembers ?? [])
        .map((member) => member.revision.primaryKey)
        .filter((id): id is number => id != null),
    [workspace?.compositionMembers],
  );
  const titleChanged = Boolean(
    workspace && editTitle.trim() !== (workspace.definition.title ?? ""),
  );
  const membersChanged = Boolean(
    workspace?.definition.isComposition && !sameIds(selectedIds, workspaceMemberIds),
  );
  const isDirty = titleChanged || membersChanged || Boolean(replacementHtml);
  useUnsavedChanges(isDirty);
  const canSave = Boolean(
    workspace?.canModify &&
    editTitle.trim() &&
    (isComposition
      ? selected.length > 0 &&
        selectedIds.length === selected.length &&
        (titleChanged || membersChanged)
      : titleChanged || replacementHtml),
  );
  const updateMutation = useMutation({
    mutationFn: () =>
      isComposition
        ? modPackService.updateComposition(modPackId, {
            title: titleChanged ? editTitle.trim() : null,
            modPackRevisionIds: membersChanged ? selectedIds : null,
            useLatest: membersChanged ? true : null,
          })
        : modPackService.updateStandalone(modPackId, {
            title: titleChanged ? editTitle.trim() : null,
            html: replacementHtml || null,
          }),
    onSuccess: async () => {
      setEditTitleOverride(undefined);
      setSelectedOverride(undefined);
      setReplacementHtml("");
      setReplacementHtmlFileName("");
      await queryClient.invalidateQueries({ queryKey: modPackQueryKeys.all });
      await query.refetch();
    },
  });
  const deactivateMutation = useMutation({
    mutationFn: () => modPackService.deactivate(modPackId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: modPackQueryKeys.all });
      navigate("/command/mod-packs");
    },
  });
  const downloadMutation = useMutation({
    mutationFn: async () => {
      if (!workspace) return;
      const blob = await modPackService.download(modPackDownloadPath(workspace));
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `${modPackTitle.replace(/[\\/:*?"<>|]+/g, "-") || "mod-pack"}.html`;
      anchor.click();
      URL.revokeObjectURL(url);
    },
  });
  useAppToastOnChange(
    updateMutation.isSuccess ? t("Auto_ModPackChangesWereSaved_fcb4be49") : undefined,
  );
  useAppToastOnChange(
    updateMutation.isError
      ? t("Auto_ModPackChangesCouldNotBeSavedCheckThe_11fc0ea1")
      : deactivateMutation.isError
        ? t("Auto_ModPackCouldNotBeDeactivatedRetryFromThe_3003a328")
        : downloadMutation.isError
          ? t("Pages_Events_Detail_Error_Download")
          : undefined,
    "error",
  );
  const readReplacementHtml = async (file: File | undefined) => {
    setFileError(undefined);
    if (!file) return;
    if ((!/\.html?$/i.test(file.name) && file.type !== "text/html") || file.size > maxHtmlBytes) {
      setReplacementHtml("");
      setReplacementHtmlFileName("");
      setFileError(t("Panels_CreateModPackPanel_Html_OnlyHtmlFilesAreSupported"));
      return;
    }
    setReplacementHtml(await file.text());
    setReplacementHtmlFileName(file.name);
  };
  const resetEditState = () => {
    if (!workspace) return;
    setEditTitleOverride(undefined);
    setReplacementHtml("");
    setReplacementHtmlFileName("");
    setSelectedOverride(undefined);
    setFileError(undefined);
  };
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

  return (
    <section className="modpack-workspace-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_CurrentArea_ModPackDetail")}
        title={modPackTitle}
        description={t(
          "Auto_OperatorWorkspaceForModPackReadinessArtifactsRevisionsDownloads_b3bb3d3e",
        )}
        actions={
          <>
            <Link className="button button--ghost" to="/command/mod-packs">
              {t("Auto_AllModPacks_82ff50f7")}
            </Link>
            <button
              className="button button--ghost"
              type="button"
              disabled={query.isFetching}
              onClick={() => {
                resetEditState();
                void query.refetch();
              }}
            >
              {t("Common_Refresh")}
            </button>
            {workspace && (
              <button
                className="button button--ghost"
                type="button"
                disabled={downloadMutation.isPending || workspace.definition.primaryKey == null}
                onClick={() => downloadMutation.mutate()}
              >
                {t("Auto_DownloadLatest_fd1c57ad")}
              </button>
            )}
            {workspace?.canModify && (
              <Link className="button button--primary" to={href("edit")}>
                {t("Edit")}
              </Link>
            )}
          </>
        }
      />

      {query.isError && (
        <StatePanel kind="error">
          {formatResource(t("Auto_FailedToLoadModPack_8b2eb217"), routeId, "")}
        </StatePanel>
      )}
      {fileError && <StatePanel kind="warning">{fileError}</StatePanel>}
      <section className="command-panel modpack-workspace">
        {workspace && (
          <>
            <div className="modpack-workspace__summary">
              <div>
                <StatusBadge tone={isComposition ? "info" : "neutral"}>
                  {isComposition ? t("Composition") : t("Standalone")}
                </StatusBadge>
                <strong>{modPackTitle}</strong>
                <span>{workspace.owner?.nickname || "—"}</span>
              </div>
              <dl>
                <div>
                  <dt>{t("Definition")}</dt>
                  <dd>{workspace.definition.primaryKey ?? "—"}</dd>
                </div>
                <div>
                  <dt>{t("Latest")}</dt>
                  <dd>#{workspace.latestRevision?.primaryKey ?? "—"}</dd>
                </div>
                <div>
                  <dt>{t("Downloads")}</dt>
                  <dd>{formatResource(t("Auto_Users_f5731263"), workspace.downloadUserCount)}</dd>
                </div>
              </dl>
            </div>
            <nav
              className="workspace-tabs"
              aria-label={t("Auto_ModPackWorkspaceNavigation_755c1101")}
            >
              {(
                [
                  ["overview", "Overview"],
                  ["artifact", "Artifact"],
                  ["edit", "Edit"],
                  ["revisions", "Revisions"],
                  ["downloads", "Downloads"],
                  ["danger", "Danger"],
                ] as const
              ).map(([target, label]) => (
                <Link
                  key={target}
                  to={href(target)}
                  className={`${section === target ? "active" : ""}${target === "danger" ? " danger" : ""}`.trim()}
                  aria-current={section === target ? "page" : undefined}
                >
                  {t(label)}
                </Link>
              ))}
            </nav>
          </>
        )}

        {query.isPending ? (
          <StatePanel kind="loading">{t("Auto_LoadingModPackWorkspace_b80419c1")}</StatePanel>
        ) : !workspace ? (
          <StatePanel kind="empty" title={t("Auto_ModPackNotFound_7d5fe348")}>
            {formatResource(t("Auto_NoModPackWasFoundForID_231b899a"), routeId)}
          </StatePanel>
        ) : (
          <WorkspaceSection
            workspace={workspace}
            section={section}
            title={modPackTitle}
            editTitle={editTitle}
            selected={selected}
            replacementHtmlFileName={replacementHtmlFileName}
            canSave={canSave && !updateMutation.isPending}
            isSaving={updateMutation.isPending}
            onEditTitle={setEditTitleOverride}
            onSelected={setSelectedOverride}
            onReadReplacement={readReplacementHtml}
            onSave={() => updateMutation.mutate()}
            onReset={resetEditState}
            onDownload={() => downloadMutation.mutate()}
            onDanger={() => setDangerOpen(true)}
          />
        )}
      </section>

      <ConfirmDialog
        open={dangerOpen}
        title={t("Auto_DeactivateModPack_9b34e1b1")}
        description={t("Auto_DeactivationHidesThisModPackFromActiveSelectionLists_bf5e78cd")}
        confirmLabel={t("Deactivate")}
        cancelLabel={t("Cancel")}
        challenge={modPackTitle}
        onCancel={() => setDangerOpen(false)}
        onConfirm={() => {
          setDangerOpen(false);
          deactivateMutation.mutate();
        }}
      />
    </section>
  );
}

function WorkspaceSection({
  workspace,
  section,
  title,
  editTitle,
  selected,
  replacementHtmlFileName,
  canSave,
  isSaving,
  onEditTitle,
  onSelected,
  onReadReplacement,
  onSave,
  onReset,
  onDownload,
  onDanger,
}: {
  workspace: ModPackWorkspace;
  section: ModPackWorkspaceSection;
  title: string;
  editTitle: string;
  selected: FullModPackDefinition[];
  replacementHtmlFileName: string;
  canSave: boolean;
  isSaving: boolean;
  onEditTitle(value: string): void;
  onSelected(value: FullModPackDefinition[]): void;
  onReadReplacement(file: File | undefined): Promise<void>;
  onSave(): void;
  onReset(): void;
  onDownload(): void;
  onDanger(): void;
}) {
  const { t } = useTranslation();
  const isComposition = workspace.definition.isComposition ?? false;
  if (section === "artifact")
    return (
      <section className="modpack-workspace__section command-panel">
        <header className="command-panel__header">
          <div>
            <span className="kicker">{t("Auto_RevisionArtifact_6abc0385")}</span>
            <h2>
              {isComposition
                ? t("Auto_CompositionMembers_4ddf9ad2")
                : t("Auto_LatestHTMLPreview_c8066774")}
            </h2>
          </div>
          <button className="button button--ghost" type="button" onClick={onDownload}>
            {t("Auto_DownloadLatest_fd1c57ad")}
          </button>
        </header>
        <ArtifactPreview workspace={workspace} title={title} />
      </section>
    );

  if (section === "edit")
    return workspace.canModify ? (
      <section className="modpack-workspace__section command-panel">
        <header className="command-panel__header">
          <div>
            <span className="kicker">{t("Edit")}</span>
            <h2>{t("Auto_PackageDefinition_3f802f56")}</h2>
          </div>
          <StatusBadge tone={canSave ? "success" : "neutral"}>
            {canSave ? t("Auto_ReadyToSave_255249e7") : t("Auto_NoValidChanges_3df50783")}
          </StatusBadge>
        </header>
        <div className="modpack-edit-form">
          <label className="field">
            <span>{t("Title")}</span>
            <input
              maxLength={1024}
              value={editTitle}
              onChange={(event) => onEditTitle(event.target.value)}
            />
          </label>
          {isComposition ? (
            <ModPackStandaloneSelector
              gameIdentifier={workspace.definition.gameIdentifier ?? ""}
              selected={selected}
              onChange={onSelected}
            />
          ) : (
            <div className="modpack-html-upload">
              <label className="file-drop">
                <span>{t("Auto_ReplacementHTML_9659da76")}</span>
                <strong>{t("Auto_DropReplacementHTMLHereOrBrowse_6d051b04")}</strong>
                <span>{t("Auto_LeaveEmptyToKeepTheCurrentRevision_cb4dc7b1")}</span>
                <input
                  type="file"
                  accept=".html,.htm,text/html"
                  onChange={(event) => void onReadReplacement(event.target.files?.[0])}
                />
              </label>
              <small>{replacementHtmlFileName || t("Auto_CurrentHTMLWillBeKept_d8ccb23b")}</small>
            </div>
          )}
          <div className="form-actions">
            <button
              className="button button--primary"
              type="button"
              disabled={!canSave}
              onClick={onSave}
            >
              {isSaving ? t("Auto_SavingChanges_2c149159") : t("Auto_SaveChanges_dd0ae7a5")}
            </button>
            <button
              className="button button--ghost"
              type="button"
              disabled={isSaving}
              onClick={onReset}
            >
              {t("Reset")}
            </button>
          </div>
        </div>
      </section>
    ) : (
      <StatePanel kind="forbidden" title={t("Auto_ModifyAccessRequired_d7bf23e9")}>
        {t("Auto_YouNeedModPackModifyAccessBeforeThisPackage_6c91d52c")}
      </StatePanel>
    );

  if (section === "revisions") return <RevisionLedger workspace={workspace} />;
  if (section === "downloads") return <DownloadLedger workspace={workspace} />;
  if (section === "danger")
    return workspace.canDelete ? (
      <section className="modpack-workspace__section command-panel danger-panel">
        <header className="command-panel__header">
          <div>
            <span className="kicker">{t("Danger")}</span>
            <h2>{t("Auto_DeactivateModPack_9b34e1b1")}</h2>
          </div>
          <StatusBadge tone="warning">
            {t("Auto_OperatorConfirmationRequired_77dcdce2")}
          </StatusBadge>
        </header>
        <p>{t("Auto_DeactivationHidesThisModPackFromActiveSelectionLists_bf5e78cd")}</p>
        <button className="button button--danger" type="button" onClick={onDanger}>
          {t("Deactivate")}
        </button>
      </section>
    ) : (
      <StatePanel kind="forbidden" title={t("Auto_DeleteAccessRequired_1dcc15d9")}>
        {t("Auto_YouNeedModPackDeleteAccessBeforeThisPackage_d15df0c2")}
      </StatePanel>
    );

  const revisionActive = isComposition
    ? workspace.compositionMembers.length > 0
    : (workspace.latestRevision?.isActive ?? false);
  return (
    <div className="modpack-overview">
      <section className="command-metrics" aria-label={t("Auto_ModPackWorkspaceSummary_452859d2")}>
        <Metric
          label={t("Auto_PackageType_da0bbf9f")}
          value={isComposition ? t("Composition") : t("Standalone")}
          detail={`#${workspace.definition.primaryKey ?? "—"}`}
        />
        <Metric
          label={t("Auto_DefinitionStatus_0341b402")}
          value={workspace.definition.isActive ? t("Active") : t("Inactive")}
          detail={formatDateTime(workspace.definition.timeStampCreated)}
        />
        <Metric
          label={t("Auto_LatestRevision_305ad7b9")}
          value={
            isComposition
              ? workspace.compositionMembers.length
                ? t("Linked")
                : t("Empty")
              : revisionActive
                ? t("Active")
                : t("Inactive")
          }
          detail={`#${workspace.latestRevision?.primaryKey ?? "—"}`}
        />
        <Metric
          label={t("Downloads")}
          value={formatResource(t("Auto_Users_f5731263"), workspace.downloadUserCount)}
          detail={
            workspace.lastDownloaded
              ? formatResource(
                  t("Auto_LastDownloaded_e6788b06"),
                  formatDateTime(workspace.lastDownloaded),
                )
              : t("Auto_NoRecordedDownloads_fb2fac90")
          }
        />
      </section>
      <div className="modpack-overview__grid">
        <section className="command-panel">
          <header className="command-panel__header">
            <div>
              <span className="kicker">{t("Artifact")}</span>
              <h2>
                {isComposition
                  ? t("Auto_CompositionMembers_4ddf9ad2")
                  : t("Auto_LatestHTMLPreview_c8066774")}
              </h2>
            </div>
            <button className="button button--ghost" type="button" onClick={onDownload}>
              {t("Auto_DownloadLatest_fd1c57ad")}
            </button>
          </header>
          <ArtifactPreview workspace={workspace} title={title} compact />
        </section>
        <aside className="command-panel modpack-metadata">
          <header className="command-panel__header">
            <div>
              <span className="kicker">{t("Auto_DefinitionLedger_6a38869e")}</span>
              <h2>{t("Auto_OperationalState_b11247c7")}</h2>
            </div>
            <StatusBadge tone={workspace.definition.isActive ? "success" : "neutral"}>
              {workspace.definition.isActive ? t("Active") : t("Inactive")}
            </StatusBadge>
          </header>
          <dl>
            <dt>{t("Owner")}</dt>
            <dd>{workspace.owner?.nickname || "—"}</dd>
            <dt>{t("Auto_OwnerID_1611f5e0")}</dt>
            <dd>{workspace.definition.ownerFk ?? "—"}</dd>
            <dt>{t("Created")}</dt>
            <dd>{formatDateTime(workspace.definition.timeStampCreated)}</dd>
            <dt>
              {isComposition
                ? t("Auto_MemberRevisions_aab20f7c")
                : t("Auto_LatestRevisionID_98c00280")}
            </dt>
            <dd>
              {isComposition
                ? workspace.compositionMembers.length
                : (workspace.latestRevision?.primaryKey ?? "—")}
            </dd>
            <dt>{t("Auto_EventUsage_57fe465e")}</dt>
            <dd>
              {workspace.eventUsageCount}{" "}
              {workspace.eventUsageCount === 1 ? t("Auto_Event_b8e1f80b") : t("events")}
            </dd>
          </dl>
        </aside>
      </div>
      <div className="modpack-ledger-grid">
        <RevisionLedger workspace={workspace} />
        <DownloadLedger workspace={workspace} />
      </div>
    </div>
  );
}

function ArtifactPreview({
  workspace,
  title,
  compact = false,
}: {
  workspace: ModPackWorkspace;
  title: string;
  compact?: boolean;
}) {
  const { t } = useTranslation();
  if (workspace.definition.isComposition)
    return workspace.compositionMembers.length === 0 ? (
      <StatePanel>{t("Auto_NoMemberRevisionsAreLinkedToThisComposition_23dc3057")}</StatePanel>
    ) : (
      <div className="command-table-scroll">
        <table className="command-table">
          <thead>
            <tr>
              <th>{t("Auto_MemberDefinition_a65c6a18")}</th>
              <th>{t("Revision")}</th>
              {!compact && <th>{t("Owner")}</th>}
              <th>{t("Status")}</th>
            </tr>
          </thead>
          <tbody>
            {workspace.compositionMembers.map((member) => (
              <tr key={`${member.definition.primaryKey}-${member.revision.primaryKey}`}>
                <td>
                  <strong>{member.definition.title || t("Auto_UntitledModPack_68e62d9a")}</strong>
                  <span>#{member.definition.primaryKey ?? "—"}</span>
                </td>
                <td>
                  <strong>#{member.revision.primaryKey ?? "—"}</strong>
                  <span>{formatDateTime(member.revision.timeStampCreated)}</span>
                </td>
                {!compact && <td>{member.owner?.nickname || "—"}</td>}
                <td>
                  <StatusBadge tone={member.revision.isActive ? "success" : "warning"}>
                    {member.revision.isActive ? t("Active") : t("Inactive")}
                  </StatusBadge>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  return workspace.definition.revisionHtml ? (
    <iframe
      className={compact ? "modpack-preview compact" : "modpack-preview"}
      title={formatResource(t("Pages_Data_ModPacks_Detail_LatestRevisionPreviewTitle"), title)}
      sandbox=""
      srcDoc={workspace.definition.revisionHtml}
    />
  ) : (
    <StatePanel>{t("Auto_NoHTMLPreviewIsAvailableForTheLatestRevision_c59154d6")}</StatePanel>
  );
}

function RevisionLedger({ workspace }: { workspace: ModPackWorkspace }) {
  const { t } = useTranslation();
  return (
    <section className="command-panel modpack-ledger">
      <header className="command-panel__header">
        <div>
          <span className="kicker">{t("Auto_RevisionHistory_5ae0d3d1")}</span>
          <h2>
            {workspace.definition.isComposition
              ? t("Auto_LinkedMemberRevisions_eeae3f10")
              : t("Auto_OwnedRevisions_d8fba5b9")}
          </h2>
        </div>
        <StatusBadge>{workspace.revisionPreview.length}</StatusBadge>
      </header>
      {!workspace.canViewLedgers ? (
        <StatePanel kind="forbidden">
          {t("Auto_ModifyAccessIsRequiredToViewRevisionHistory_db813722")}
        </StatePanel>
      ) : workspace.revisionPreview.length === 0 ? (
        <StatePanel>{t("Auto_NoRevisionRecordsWereReturnedForThisModPack_f59f059a")}</StatePanel>
      ) : (
        <RevisionTable revisions={workspace.revisionPreview} />
      )}
    </section>
  );
}

function RevisionTable({ revisions }: { revisions: ModPackRevision[] }) {
  const { t } = useTranslation();
  return (
    <div className="command-table-scroll">
      <table className="command-table">
        <thead>
          <tr>
            <th>{t("Revision")}</th>
            <th>{t("Definition")}</th>
            <th>{t("Created")}</th>
            <th>{t("Auto_UpdatedBy_20f0c472")}</th>
            <th>{t("Status")}</th>
          </tr>
        </thead>
        <tbody>
          {revisions.map((revision) => (
            <tr key={revision.primaryKey}>
              <td>#{revision.primaryKey ?? "—"}</td>
              <td>#{revision.definitionFk ?? "—"}</td>
              <td>{formatDateTime(revision.timeStampCreated)}</td>
              <td>{revision.updatedByFk ?? "—"}</td>
              <td>
                <StatusBadge tone={revision.isActive ? "success" : "warning"}>
                  {revision.isActive ? t("Active") : t("Inactive")}
                </StatusBadge>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function DownloadLedger({ workspace }: { workspace: ModPackWorkspace }) {
  const { t } = useTranslation();
  return (
    <section className="command-panel modpack-ledger">
      <header className="command-panel__header">
        <div>
          <span className="kicker">{t("Auto_DownloadEvidence_a0f725da")}</span>
          <h2>{t("Auto_UsersWhoDownloadedThisModPack_9cc5e954")}</h2>
        </div>
        <StatusBadge tone={workspace.downloadUserCount > 0 ? "success" : "warning"}>
          {formatResource(t("Auto_Users_f5731263"), workspace.downloadUserCount)}
        </StatusBadge>
      </header>
      {!workspace.canViewLedgers ? (
        <StatePanel kind="forbidden">
          {t("Auto_ModifyAccessIsRequiredToViewDownloadEvidence_f28cfc00")}
        </StatePanel>
      ) : workspace.downloadPreview.length === 0 ? (
        <StatePanel>{t("Auto_NoUserDownloadRecordsExistForThisModPack_22dc1e5d")}</StatePanel>
      ) : (
        <DownloadTable audits={workspace.downloadPreview} />
      )}
    </section>
  );
}

function DownloadTable({ audits }: { audits: ModPackDownloadAudit[] }) {
  const { t } = useTranslation();
  return (
    <div className="command-table-scroll">
      <table className="command-table">
        <thead>
          <tr>
            <th>{t("User")}</th>
            <th>{t("Auto_DownloadedRevision_0476b013")}</th>
            <th>{t("Auto_RevisionOwner_6996d7e7")}</th>
            <th>{t("Auto_LastDownloaded_d1b0c35f")}</th>
          </tr>
        </thead>
        <tbody>
          {audits.map((audit) => (
            <tr key={`${audit.user?.primaryKey}-${audit.modPackRevisionId}`}>
              <td>
                <strong>{audit.user?.nickname || "—"}</strong>
                <span>{audit.user?.primaryKey ?? "—"}</span>
              </td>
              <td>
                <strong>#{audit.modPackRevisionId ?? "—"}</strong>
                <span>{formatDateTime(audit.modPackRevisionTimeStampCreated)}</span>
              </td>
              <td>
                <strong>{audit.modPackRevisionDefinitionTitle || "—"}</strong>
                <span>#{audit.modPackRevisionDefinitionId ?? "—"}</span>
              </td>
              <td>{formatDateTime(audit.timeStampDownloaded)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
