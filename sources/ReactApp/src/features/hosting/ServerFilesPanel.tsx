import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import { ConfirmDialog, StatePanel, StatusBadge, useUnsavedChanges } from "@/components/primitives";
import { formatBytes, formatDate, humanizeToken } from "@/features/hosting/model";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type {
  GameServer,
  HostingFile,
  HostingFileContext,
  PreservationSelectionInput,
} from "@/features/hosting/types";
import {
  combineServerPath,
  parentServerPath,
  pathBreadcrumbs,
  sortHostingFiles,
} from "@/features/hosting/workspaceModel";

const maximumUploadBytes = 16 * 1024 * 1024;

export function ServerFilesPanel({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const client = useQueryClient();
  const [context, setContext] = useState<HostingFileContext>("persistent");
  const [path, setPath] = useState("");
  const [sortColumn, setSortColumn] = useState<"name" | "modified" | "size">("name");
  const [sortDescending, setSortDescending] = useState(false);
  const [upload, setUpload] = useState<File>();
  const [uploadPath, setUploadPath] = useState("");
  const [confirmGeneratedOverride, setConfirmGeneratedOverride] = useState(false);
  const [directoryPath, setDirectoryPath] = useState("");
  const [selection, setSelection] = useState<PreservationSelectionInput>({
    path: "",
    scope: "File",
    mode: "CreateOrUpdate",
  });
  const [deleteTarget, setDeleteTarget] = useState<HostingFile>();
  const [localError, setLocalError] = useState<string>();
  useUnsavedChanges(Boolean(upload || uploadPath || directoryPath || selection.path));
  const files = useQuery({
    queryKey: hostingQueryKeys.files(server.primaryKey, context, path),
    queryFn: ({ signal }) => hostingService.files(server.primaryKey, context, path, signal),
  });
  const preservation = useQuery({
    queryKey: hostingQueryKeys.preservation(server.primaryKey),
    queryFn: ({ signal }) => hostingService.preservationSelections(server.primaryKey, signal),
    enabled: server.canModify,
  });
  const sorted = useMemo(
    () => sortHostingFiles(files.data ?? [], sortColumn, sortDescending),
    [files.data, sortColumn, sortDescending],
  );
  const generatedTarget = (files.data ?? []).find(
    (entry) =>
      entry.isGeneratedTarget &&
      !entry.overridesGenerated &&
      entry.path.toLowerCase() === uploadPath.toLowerCase(),
  );
  const refreshFiles = () =>
    client.invalidateQueries({ queryKey: ["hosting", "servers", server.primaryKey, "files"] });
  const uploadMutation = useMutation({
    mutationFn: () => {
      if (!upload) throw new Error(t("React_ServerWorkspace_SelectFile"));
      return hostingService.uploadFile(
        server.primaryKey,
        context,
        uploadPath,
        upload,
        confirmGeneratedOverride,
      );
    },
    onSuccess: async () => {
      setUpload(undefined);
      setUploadPath("");
      setConfirmGeneratedOverride(false);
      await refreshFiles();
    },
  });
  const directoryMutation = useMutation({
    mutationFn: () => hostingService.createDirectory(server.primaryKey, context, directoryPath),
    onSuccess: async () => {
      setDirectoryPath("");
      await refreshFiles();
    },
  });
  const deleteMutation = useMutation({
    mutationFn: (entry: HostingFile) =>
      hostingService.deleteFile(server.primaryKey, context, entry.path, entry.isDirectory),
    onSuccess: async () => {
      setDeleteTarget(undefined);
      await refreshFiles();
    },
  });
  const selectionMutation = useMutation({
    mutationFn: () => hostingService.savePreservationSelection(server.primaryKey, selection),
    onSuccess: async () => {
      setSelection((current) => ({ ...current, path: "" }));
      await client.invalidateQueries({
        queryKey: hostingQueryKeys.preservation(server.primaryKey),
      });
    },
  });
  const deleteSelection = useMutation({
    mutationFn: (selectionId: string) =>
      hostingService.deletePreservationSelection(server.primaryKey, selectionId),
    onSuccess: () =>
      client.invalidateQueries({ queryKey: hostingQueryKeys.preservation(server.primaryKey) }),
  });
  const error =
    localError ??
    files.error?.message ??
    preservation.error?.message ??
    uploadMutation.error?.message ??
    directoryMutation.error?.message ??
    deleteMutation.error?.message ??
    selectionMutation.error?.message ??
    deleteSelection.error?.message;
  const canMutate = server.canModify && context === "persistent";
  const setSort = (column: typeof sortColumn) => {
    if (sortColumn === column) setSortDescending((value) => !value);
    else {
      setSortColumn(column);
      setSortDescending(false);
    }
  };
  return (
    <>
      <section className="next-hosting-panel next-hosting-files-panel">
        <header className="next-hosting-section-heading">
          <div>
            <span className="kicker">{t("Hosting_Detail_FilesKicker")}</span>
            <h2>{t("Hosting_Detail_FilesTitle")}</h2>
            <p>{t("Hosting_Detail_FilesDescription")}</p>
          </div>
        </header>
        {error && (
          <p role="alert" className="next-hosting-inline-error">
            {error}
          </p>
        )}
        <div
          className="next-hosting-file-contexts"
          role="tablist"
          aria-label={t("Hosting_Detail_FileContexts")}
        >
          <button
            role="tab"
            type="button"
            aria-selected={context === "persistent"}
            onClick={() => {
              setContext("persistent");
              setPath("");
            }}
          >
            {t("Hosting_Detail_PersistentFiles")}
          </button>
          <button
            role="tab"
            type="button"
            aria-selected={context === "live"}
            onClick={() => {
              setContext("live");
              setPath("");
            }}
          >
            {t("Hosting_Detail_LiveFiles")}
          </button>
        </div>
        <div className="next-hosting-file-toolbar">
          <button
            className="button button--ghost"
            type="button"
            disabled={!path || files.isFetching}
            onClick={() => setPath(parentServerPath(path))}
          >
            {t("Hosting_Detail_ParentFolder")}
          </button>
          <nav aria-label={t("Hosting_Detail_Breadcrumbs")}>
            <button type="button" onClick={() => setPath("")}>
              {context === "persistent"
                ? t("React_ServerWorkspace_PersistentShort")
                : t("React_ServerWorkspace_LiveShort")}
            </button>
            {pathBreadcrumbs(path).map((crumb) => (
              <span key={crumb.path}>
                {" "}
                /{" "}
                <button type="button" onClick={() => setPath(crumb.path)}>
                  {crumb.label}
                </button>
              </span>
            ))}
          </nav>
          <button
            className="button button--ghost"
            type="button"
            disabled={files.isFetching}
            onClick={() => void files.refetch()}
          >
            {t("Common_Refresh")}
          </button>
        </div>
        {files.isLoading ? (
          <StatePanel kind="loading">{t("Hosting_Detail_FilesLoading")}</StatePanel>
        ) : sorted.length === 0 ? (
          <StatePanel kind="empty">{t("Hosting_Detail_FilesEmpty")}</StatePanel>
        ) : (
          <div className="next-hosting-table-scroll">
            <table>
              <caption className="sr-only">{t("Hosting_Detail_FilesTitle")}</caption>
              <thead>
                <tr>
                  <th>
                    <button type="button" onClick={() => setSort("name")}>
                      {t("Hosting_Detail_FileName")}
                    </button>
                  </th>
                  <th>
                    <button type="button" onClick={() => setSort("modified")}>
                      {t("Hosting_Detail_FileModified")}
                    </button>
                  </th>
                  <th>{t("Hosting_Detail_FileType")}</th>
                  <th>
                    <button type="button" onClick={() => setSort("size")}>
                      {t("Hosting_Detail_FileSize")}
                    </button>
                  </th>
                  <th>{t("Hosting_Detail_FileActions")}</th>
                </tr>
              </thead>
              <tbody>
                {sorted.map((entry) => (
                  <tr key={entry.path}>
                    <td>
                      {entry.isDirectory ? (
                        <button
                          className="next-hosting-file-name"
                          type="button"
                          onClick={() => setPath(entry.path)}
                        >
                          📁 {entry.name}
                        </button>
                      ) : (
                        <span>📄 {entry.name}</span>
                      )}
                      {entry.isGeneratedTarget && (
                        <small>
                          {entry.overridesGenerated
                            ? t("React_ServerWorkspace_PersistentOverride")
                            : entry.configurationStatus === "Incomplete"
                              ? t("React_ServerWorkspace_GeneratedIncomplete")
                              : t("Hosting_FileClassification_Generated")}
                        </small>
                      )}
                    </td>
                    <td>{formatDate(entry.modifiedAt)}</td>
                    <td>{humanizeToken(entry.classification)}</td>
                    <td>{entry.isDirectory ? "—" : formatBytes(entry.size)}</td>
                    <td>
                      <div className="next-hosting-file-actions">
                        {!entry.isDirectory && (
                          <a
                            className="button button--ghost"
                            href={hostingService.fileDownloadUrl(
                              server.primaryKey,
                              context,
                              entry.path,
                            )}
                            download={entry.name}
                          >
                            {t("Hosting_Detail_Download")}
                          </a>
                        )}
                        {entry.canModify &&
                          (!entry.isGeneratedTarget || entry.overridesGenerated) && (
                            <button
                              className="button button--danger"
                              type="button"
                              disabled={deleteMutation.isPending}
                              onClick={() => setDeleteTarget(entry)}
                            >
                              {t("Hosting_Detail_Delete")}
                            </button>
                          )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {canMutate && (
          <div className="next-hosting-file-mutations">
            <section>
              <h3>{t("Hosting_Detail_UploadTitle")}</h3>
              <label className="field">
                <span>{t("React_ServerWorkspace_FileMaximum")}</span>
                <input
                  type="file"
                  onChange={(event) => {
                    const file = event.target.files?.[0];
                    setLocalError(undefined);
                    if (!file) {
                      setUpload(undefined);
                      return;
                    }
                    if (file.size > maximumUploadBytes) {
                      setLocalError(t("React_ServerWorkspace_FileTooLarge"));
                      setUpload(undefined);
                      return;
                    }
                    setUpload(file);
                    setUploadPath(combineServerPath(path, file.name));
                    setConfirmGeneratedOverride(false);
                  }}
                />
              </label>
              <label className="field">
                <span>{t("Hosting_Detail_TargetPath")}</span>
                <input
                  value={uploadPath}
                  onChange={(event) => {
                    setUploadPath(event.target.value);
                    setConfirmGeneratedOverride(false);
                  }}
                />
              </label>
              {generatedTarget && (
                <label className="next-hosting-check">
                  <input
                    type="checkbox"
                    checked={confirmGeneratedOverride}
                    onChange={(event) => setConfirmGeneratedOverride(event.target.checked)}
                  />
                  <span>{t("React_ServerWorkspace_CreateGeneratedOverride")}</span>
                </label>
              )}
              <button
                className="button button--primary"
                type="button"
                disabled={
                  !upload ||
                  !uploadPath.trim() ||
                  Boolean(generatedTarget && !confirmGeneratedOverride) ||
                  uploadMutation.isPending
                }
                onClick={() => uploadMutation.mutate()}
              >
                {t("Hosting_Detail_Upload")}
              </button>
            </section>
            <section>
              <h3>{t("Hosting_Detail_CreateFolderTitle")}</h3>
              <label className="field">
                <span>{t("Hosting_Detail_TargetPath")}</span>
                <input
                  value={directoryPath}
                  onChange={(event) => setDirectoryPath(event.target.value)}
                />
              </label>
              <button
                className="button button--ghost"
                type="button"
                disabled={!directoryPath.trim() || directoryMutation.isPending}
                onClick={() => directoryMutation.mutate()}
              >
                {t("Hosting_Detail_CreateFolder")}
              </button>
            </section>
          </div>
        )}
      </section>
      {server.canModify && (
        <section className="next-hosting-panel next-hosting-preservation-panel">
          <header className="next-hosting-section-heading">
            <div>
              <span className="kicker">{t("Hosting_Detail_PreservationKicker")}</span>
              <h2>{t("Hosting_Detail_PreservationTitle")}</h2>
              <p>{t("Hosting_Detail_PreservationDescription")}</p>
            </div>
          </header>
          <div className="next-hosting-preservation-grid">
            <div className="next-hosting-selection-list">
              {(preservation.data ?? []).length === 0 && (
                <p>{t("React_ServerWorkspace_NoPreservation")}</p>
              )}
              {(preservation.data ?? []).map((item) => (
                <article key={item.primaryKey}>
                  <div>
                    <strong>{item.path}</strong>
                    <span>
                      {humanizeToken(item.scope)} · {humanizeToken(item.mode)}
                    </span>
                    {item.seededByGame && (
                      <StatusBadge tone="info">{t("Hosting_Detail_SeededByGame")}</StatusBadge>
                    )}
                  </div>
                  <button
                    className="button button--danger"
                    type="button"
                    disabled={item.seededByGame || deleteSelection.isPending}
                    title={
                      item.seededByGame
                        ? t("React_ServerWorkspace_GameSeededCannotRemove")
                        : undefined
                    }
                    onClick={() => deleteSelection.mutate(item.primaryKey)}
                  >
                    {t("Hosting_Detail_Remove")}
                  </button>
                </article>
              ))}
            </div>
            <form
              onSubmit={(event) => {
                event.preventDefault();
                selectionMutation.mutate();
              }}
            >
              <h3>{t("Hosting_Detail_AddPreservation")}</h3>
              <label className="field">
                <span>{t("Hosting_Detail_TargetPath")}</span>
                <input
                  required
                  value={selection.path}
                  onChange={(event) =>
                    setSelection((current) => ({ ...current, path: event.target.value }))
                  }
                />
              </label>
              <label className="field">
                <span>{t("Hosting_Detail_Scope")}</span>
                <select
                  value={selection.scope}
                  onChange={(event) =>
                    setSelection((current) => ({
                      ...current,
                      scope: event.target.value as PreservationSelectionInput["scope"],
                    }))
                  }
                >
                  <option value="File">{t("Hosting_Detail_ScopeFile")}</option>
                  <option value="FlatFolder">{t("Hosting_Detail_ScopeFlat")}</option>
                  <option value="RecursiveFolder">{t("Hosting_Detail_ScopeRecursive")}</option>
                </select>
              </label>
              <label className="field">
                <span>{t("Hosting_Detail_DeletionPolicy")}</span>
                <select
                  value={selection.mode}
                  onChange={(event) =>
                    setSelection((current) => ({
                      ...current,
                      mode: event.target.value as PreservationSelectionInput["mode"],
                    }))
                  }
                >
                  <option value="CreateOrUpdate">{t("Hosting_Detail_ModeCreateUpdate")}</option>
                  <option value="MirrorDeletions">{t("Hosting_Detail_ModeMirror")}</option>
                </select>
              </label>
              <button
                className="button button--primary"
                type="submit"
                disabled={!selection.path.trim() || selectionMutation.isPending}
              >
                {t("Hosting_Detail_Add")}
              </button>
            </form>
          </div>
        </section>
      )}
      <ConfirmDialog
        open={Boolean(deleteTarget)}
        title={t("React_ServerWorkspace_DeleteFile")}
        description={formatResource(
          t("Hosting_Detail_DeleteFileConfirm"),
          deleteTarget?.path ?? "",
        )}
        confirmLabel={t("Hosting_Detail_Delete")}
        cancelLabel={t("Common_Cancel")}
        onCancel={() => setDeleteTarget(undefined)}
        onConfirm={() => {
          if (deleteTarget) deleteMutation.mutate(deleteTarget);
        }}
      />
    </>
  );
}
