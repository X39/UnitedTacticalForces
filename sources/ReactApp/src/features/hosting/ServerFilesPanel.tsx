import {
  ChevronDown16Regular,
  ChevronRight16Regular,
  Document20Regular,
  Folder20Regular,
  FolderOpen20Regular,
  MoreHorizontal20Regular,
  Search20Regular,
} from "@fluentui/react-icons";
import { useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
  type DragEvent,
  type KeyboardEvent,
} from "react";
import { useTranslation } from "react-i18next";
import type { TFunction } from "i18next";
import { formatResource } from "@/app/i18n";
import { ConfirmDialog, StatePanel, StatusBadge, useUnsavedChanges } from "@/components/primitives";
import { formatBytes, formatDate, humanizeToken } from "@/features/hosting/model";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type {
  GameServer,
  HostingFile,
  HostingFileContext,
  PreservationSelection,
  PreservationSelectionInput,
} from "@/features/hosting/types";
import {
  affectedDestinationDirectories,
  combineServerPath,
  directoryAncestors,
  findHostingFileByPath,
  normalizeServerRelativePath,
  orderDirectoriesShallowFirst,
  parentServerPath,
  pathBreadcrumbs,
  planUploads,
  sortHostingFiles,
} from "@/features/hosting/workspaceModel";

const maximumUploadBytes = 16 * 1024 * 1024;
const uploadConcurrency = 3;

type UploadStatus = "queued" | "uploading" | "succeeded" | "failed" | "cancelled" | "rejected";

interface SelectedUpload {
  file: File;
  relativePath: string;
}

interface QueuedUpload {
  id: string;
  file: File;
  relativePath: string;
  targetPath: string;
  status: UploadStatus;
  existing?: HostingFile | undefined;
  error?: string | undefined;
}

export function ServerFilesPanel({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const client = useQueryClient();
  const [context, setContext] = useState<HostingFileContext>("persistent");
  const [path, setPath] = useState("");
  const [expandedPaths, setExpandedPaths] = useState<Set<string>>(() => new Set([""]));
  const [treeOpen, setTreeOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [selectedPath, setSelectedPath] = useState<string>();
  const [sortColumn, setSortColumn] = useState<"name" | "modified" | "size">("name");
  const [sortDescending, setSortDescending] = useState(false);
  const [selection, setSelection] = useState<PreservationSelectionInput>({
    path: "",
    scope: "File",
    mode: "CreateOrUpdate",
  });
  const [deleteTarget, setDeleteTarget] = useState<HostingFile>();
  const [localError, setLocalError] = useState<string>();
  const [newFolderOpen, setNewFolderOpen] = useState(false);
  const [folderName, setFolderName] = useState("");
  const [uploadOpen, setUploadOpen] = useState(false);
  const [uploadDestination, setUploadDestination] = useState("");
  const [uploadQueue, setUploadQueue] = useState<QueuedUpload[]>([]);
  const [directoriesToCreate, setDirectoriesToCreate] = useState<string[]>([]);
  const [preflightPending, setPreflightPending] = useState(false);
  const [replacementApproved, setReplacementApproved] = useState(false);
  const [generatedApproved, setGeneratedApproved] = useState(false);
  const [batchRunning, setBatchRunning] = useState(false);
  const [dragActive, setDragActive] = useState(false);
  const newFolderDialogRef = useRef<HTMLDialogElement>(null);
  const uploadDialogRef = useRef<HTMLDialogElement>(null);
  const newFolderButtonRef = useRef<HTMLButtonElement>(null);
  const uploadButtonRef = useRef<HTMLButtonElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const folderInputRef = useRef<HTMLInputElement>(null);
  const uploadAbortRef = useRef<AbortController | undefined>(undefined);

  useUnsavedChanges(
    Boolean(selection.path || uploadQueue.some((item) => item.status === "queued")),
  );

  const files = useQuery({
    queryKey: hostingQueryKeys.files(server.primaryKey, context, path),
    queryFn: ({ signal }) => hostingService.files(server.primaryKey, context, path, signal),
  });
  const preservation = useQuery({
    queryKey: hostingQueryKeys.preservation(server.primaryKey),
    queryFn: ({ signal }) => hostingService.preservationSelections(server.primaryKey, signal),
    enabled: server.canModify,
  });
  const expandedPathList = useMemo(
    () => orderDirectoriesShallowFirst(expandedPaths).concat(expandedPaths.has("") ? [""] : []),
    [expandedPaths],
  );
  const treeQueries = useQueries({
    queries: expandedPathList.map((treePath) => ({
      queryKey: hostingQueryKeys.files(server.primaryKey, context, treePath),
      queryFn: ({ signal }: { signal: AbortSignal }) =>
        hostingService.files(server.primaryKey, context, treePath, signal),
    })),
  });
  const treeListings = useMemo(
    () =>
      new Map(
        expandedPathList.map((treePath, index) => [
          treePath,
          {
            files: treeQueries[index]?.data ?? [],
            loading: treeQueries[index]?.isLoading ?? false,
            error: treeQueries[index]?.isError ?? false,
          },
        ]),
      ),
    [expandedPathList, treeQueries],
  );
  const sorted = useMemo(
    () => sortHostingFiles(files.data ?? [], sortColumn, sortDescending),
    [files.data, sortColumn, sortDescending],
  );
  const visibleFiles = useMemo(() => {
    const query = search.trim().toLocaleLowerCase();
    if (!query) return sorted;
    return sorted.filter(
      (entry) =>
        entry.name.toLocaleLowerCase().includes(query) ||
        humanizeToken(entry.classification).toLocaleLowerCase().includes(query),
    );
  }, [search, sorted]);
  const canMutate = server.canModify && context === "persistent";
  const collisions = uploadQueue.filter((item) => item.existing && !item.existing.isDirectory);
  const generatedCollisions = collisions.filter(
    (item) => item.existing?.isGeneratedTarget && !item.existing.overridesGenerated,
  );
  const completedCount = uploadQueue.filter((item) =>
    ["succeeded", "failed", "cancelled", "rejected"].includes(item.status),
  ).length;
  const retryableCount = uploadQueue.filter((item) =>
    ["failed", "cancelled"].includes(item.status),
  ).length;

  useEffect(() => {
    folderInputRef.current?.setAttribute("webkitdirectory", "");
  }, []);

  useEffect(() => {
    const dialog = newFolderDialogRef.current;
    if (newFolderOpen && dialog && !dialog.open) dialog.showModal();
    if (!newFolderOpen && dialog?.open) dialog.close();
  }, [newFolderOpen]);

  useEffect(() => {
    const dialog = uploadDialogRef.current;
    if (uploadOpen && dialog && !dialog.open) dialog.showModal();
    if (!uploadOpen && dialog?.open) dialog.close();
  }, [uploadOpen]);

  const refreshFiles = () =>
    client.invalidateQueries({ queryKey: ["hosting", "servers", server.primaryKey, "files"] });

  const deleteMutation = useMutation({
    mutationFn: (entry: HostingFile) =>
      hostingService.deleteFile(server.primaryKey, context, entry.path, entry.isDirectory),
    onSuccess: async () => {
      setDeleteTarget(undefined);
      setSelectedPath(undefined);
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
  const directoryMutation = useMutation({
    mutationFn: () =>
      hostingService.createDirectory(
        server.primaryKey,
        "persistent",
        combineServerPath(path, folderName),
      ),
    onSuccess: async () => {
      setFolderName("");
      closeNewFolder();
      await refreshFiles();
    },
  });

  const error =
    localError ??
    files.error?.message ??
    preservation.error?.message ??
    directoryMutation.error?.message ??
    deleteMutation.error?.message ??
    selectionMutation.error?.message ??
    deleteSelection.error?.message;

  function setFileContext(nextContext: HostingFileContext) {
    setContext(nextContext);
    setPath("");
    setExpandedPaths(new Set([""]));
    setSearch("");
    setSelectedPath(undefined);
  }

  function navigateTo(nextPath: string, nextContext = context) {
    const normalized = normalizeServerRelativePath(nextPath);
    if (nextContext !== context) setContext(nextContext);
    setPath(normalized);
    setExpandedPaths((current) => {
      const next = new Set(current);
      next.add("");
      for (const ancestor of directoryAncestors(normalized)) next.add(ancestor);
      return next;
    });
    setSearch("");
    setSelectedPath(undefined);
    setTreeOpen(false);
  }

  function toggleTreePath(treePath: string) {
    setExpandedPaths((current) => {
      const next = new Set(current);
      if (next.has(treePath) && treePath) {
        for (const expandedPath of next) {
          if (expandedPath === treePath || expandedPath.startsWith(`${treePath}/`)) {
            next.delete(expandedPath);
          }
        }
      } else next.add(treePath);
      return next;
    });
  }

  function openNewFolder() {
    setFolderName("");
    directoryMutation.reset();
    setNewFolderOpen(true);
  }

  function closeNewFolder() {
    setNewFolderOpen(false);
    window.setTimeout(() => newFolderButtonRef.current?.focus());
  }

  function openUpload() {
    setUploadDestination(path);
    setUploadQueue([]);
    setDirectoriesToCreate([]);
    setReplacementApproved(false);
    setGeneratedApproved(false);
    setUploadOpen(true);
  }

  function closeUpload() {
    if (batchRunning) return;
    setUploadOpen(false);
    setUploadQueue([]);
    setDirectoriesToCreate([]);
    window.setTimeout(() => uploadButtonRef.current?.focus());
  }

  async function prepareUploads(selected: SelectedUpload[], destination = uploadDestination) {
    setLocalError(undefined);
    setPreflightPending(true);
    setReplacementApproved(false);
    setGeneratedApproved(false);
    try {
      const planned: QueuedUpload[] = selected.map(({ file, relativePath }, index) => {
        try {
          const upload = planUploads(destination, [{ item: file, relativePath }])[0]!;
          return {
            id: `${upload.targetPath}:${file.size}:${file.lastModified}:${index}`,
            file,
            relativePath: upload.relativePath,
            targetPath: upload.targetPath,
            status: file.size > maximumUploadBytes ? "rejected" : "queued",
            error:
              file.size > maximumUploadBytes ? t("React_ServerWorkspace_FileTooLarge") : undefined,
          };
        } catch (reason) {
          return {
            id: `${file.name}:${file.size}:${index}`,
            file,
            relativePath,
            targetPath: relativePath,
            status: "rejected",
            error: reason instanceof Error ? reason.message : t("React_ServerFiles_InvalidPath"),
          };
        }
      });
      const seenTargets = new Set<string>();
      for (const item of planned) {
        if (item.status !== "queued") continue;
        const key = item.targetPath.toLocaleLowerCase();
        if (seenTargets.has(key)) {
          item.status = "rejected";
          item.error = t("React_ServerFiles_DuplicateTarget");
        } else seenTargets.add(key);
      }
      const valid = planned.filter((item) => item.status === "queued");
      const requiredDirectories = affectedDestinationDirectories(
        valid.map((item) => item.targetPath),
      );
      const knownExisting = new Set<string>(["", destination, ...directoryAncestors(destination)]);
      const missing: string[] = [];
      const listings = new Map<string, HostingFile[]>();
      const listing = async (directory: string) => {
        const normalized = normalizeServerRelativePath(directory);
        const cached = listings.get(normalized);
        if (cached) return cached;
        const result = await client.fetchQuery({
          queryKey: hostingQueryKeys.files(server.primaryKey, "persistent", normalized),
          queryFn: ({ signal }) =>
            hostingService.files(server.primaryKey, "persistent", normalized, signal),
        });
        listings.set(normalized, result);
        return result;
      };
      for (const directory of requiredDirectories) {
        if (knownExisting.has(directory)) continue;
        const parent = parentServerPath(directory);
        if (!knownExisting.has(parent)) {
          missing.push(directory);
          continue;
        }
        const entries = await listing(parent);
        const existing = findHostingFileByPath(entries, directory);
        if (existing?.isDirectory) knownExisting.add(directory);
        else missing.push(directory);
      }
      const withCollisions = await Promise.all(
        planned.map(async (item) => {
          if (item.status !== "queued") return item;
          const parent = parentServerPath(item.targetPath);
          if (!knownExisting.has(parent)) return item;
          const existing = findHostingFileByPath(await listing(parent), item.targetPath);
          return existing && !existing.isDirectory ? { ...item, existing } : item;
        }),
      );
      setDirectoriesToCreate(orderDirectoriesShallowFirst(missing));
      setUploadQueue(withCollisions);
    } catch (reason) {
      setLocalError(
        reason instanceof Error ? reason.message : t("React_ServerFiles_PreflightFailed"),
      );
      setUploadQueue([]);
    } finally {
      setPreflightPending(false);
    }
  }

  async function handleInputFiles(event: ChangeEvent<HTMLInputElement>) {
    const selected = Array.from(event.target.files ?? []).map((file) => ({
      file,
      relativePath: file.webkitRelativePath || file.name,
    }));
    event.target.value = "";
    if (selected.length) await prepareUploads(selected);
  }

  async function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setDragActive(false);
    if (!canMutate) return;
    const destination = path;
    setUploadDestination(destination);
    setUploadOpen(true);
    const selected = await readDroppedItems(event.dataTransfer);
    if (selected.length) await prepareUploads(selected, destination);
  }

  async function runUpload(retryOnly = false) {
    if (batchRunning || preflightPending) return;
    if (collisions.length > 0 && !replacementApproved) return;
    if (generatedCollisions.length > 0 && !generatedApproved) return;
    const eligibleStatuses: UploadStatus[] = retryOnly
      ? ["failed", "cancelled"]
      : ["queued", "failed", "cancelled"];
    const pending = uploadQueue.filter((item) => eligibleStatuses.includes(item.status));
    if (!pending.length) return;
    setBatchRunning(true);
    setLocalError(undefined);
    const controller = new AbortController();
    uploadAbortRef.current = controller;
    const failedDirectories = new Set<string>();
    try {
      for (const directory of directoriesToCreate) {
        if (controller.signal.aborted) break;
        try {
          await hostingService.createDirectory(
            server.primaryKey,
            "persistent",
            directory,
            controller.signal,
          );
        } catch (reason) {
          try {
            await hostingService.files(
              server.primaryKey,
              "persistent",
              directory,
              controller.signal,
            );
          } catch {
            failedDirectories.add(directory);
            setLocalError(
              reason instanceof Error
                ? reason.message
                : t("React_ServerFiles_CreateDirectoryFailed"),
            );
          }
        }
      }
      let cursor = 0;
      const worker = async () => {
        while (cursor < pending.length && !controller.signal.aborted) {
          const item = pending[cursor++]!;
          const blockedByDirectory = [...failedDirectories].find(
            (directory) =>
              item.targetPath === directory || item.targetPath.startsWith(`${directory}/`),
          );
          if (blockedByDirectory) {
            updateUpload(item.id, {
              status: "failed",
              error: t("React_ServerFiles_ParentDirectoryFailed"),
            });
            continue;
          }
          updateUpload(item.id, { status: "uploading", error: undefined });
          try {
            await hostingService.uploadFile(
              server.primaryKey,
              "persistent",
              item.targetPath,
              item.file,
              Boolean(
                item.existing?.isGeneratedTarget &&
                !item.existing.overridesGenerated &&
                generatedApproved,
              ),
              controller.signal,
            );
            updateUpload(item.id, { status: "succeeded" });
          } catch (reason) {
            updateUpload(item.id, {
              status: controller.signal.aborted ? "cancelled" : "failed",
              error: controller.signal.aborted
                ? t("React_ServerFiles_UploadCancelled")
                : reason instanceof Error
                  ? reason.message
                  : t("React_ServerFiles_UploadFailed"),
            });
          }
        }
      };
      await Promise.all(
        Array.from({ length: Math.min(uploadConcurrency, pending.length) }, () => worker()),
      );
      if (controller.signal.aborted) {
        setUploadQueue((current) =>
          current.map((item) =>
            eligibleStatuses.includes(item.status)
              ? { ...item, status: "cancelled", error: t("React_ServerFiles_UploadCancelled") }
              : item,
          ),
        );
      }
    } finally {
      uploadAbortRef.current = undefined;
      setBatchRunning(false);
      await refreshFiles();
    }
  }

  function updateUpload(id: string, patch: Partial<QueuedUpload>) {
    setUploadQueue((current) =>
      current.map((item) => (item.id === id ? { ...item, ...patch } : item)),
    );
  }

  function setSort(column: typeof sortColumn) {
    if (sortColumn === column) setSortDescending((value) => !value);
    else {
      setSortColumn(column);
      setSortDescending(false);
    }
  }

  return (
    <>
      <section className="next-hosting-panel next-hosting-files-panel">
        <header className="next-hosting-section-heading next-hosting-files-heading">
          <div>
            <span className="kicker">{t("Hosting_Detail_FilesKicker")}</span>
            <h2>{t("Hosting_Detail_FilesTitle")}</h2>
            <p>{t("Hosting_Detail_FilesDescription")}</p>
          </div>
          <div
            className="next-hosting-file-contexts"
            role="tablist"
            aria-label={t("Hosting_Detail_FileContexts")}
          >
            <button
              role="tab"
              type="button"
              aria-selected={context === "persistent"}
              onClick={() => setFileContext("persistent")}
            >
              {t("Hosting_Detail_PersistentFiles")}
            </button>
            <button
              role="tab"
              type="button"
              aria-selected={context === "live"}
              onClick={() => setFileContext("live")}
            >
              {t("Hosting_Detail_LiveFiles")}
            </button>
          </div>
        </header>
        {error && (
          <p role="alert" className="next-hosting-inline-error">
            {error}
          </p>
        )}
        <button
          className="button button--ghost next-hosting-tree-toggle"
          type="button"
          aria-expanded={treeOpen}
          onClick={() => setTreeOpen((value) => !value)}
        >
          {treeOpen ? t("React_ServerFiles_HideFolders") : t("React_ServerFiles_ShowFolders")}
        </button>
        <div
          className={`next-hosting-explorer${treeOpen ? " next-hosting-explorer--tree-open" : ""}`}
        >
          <aside className="next-hosting-file-tree-pane">
            <h3>
              {context === "persistent"
                ? t("Hosting_Detail_PersistentFiles")
                : t("Hosting_Detail_LiveFiles")}
            </h3>
            <ul className="next-hosting-file-tree" role="tree">
              <FileTreeBranch
                parentPath=""
                currentPath={path}
                expandedPaths={expandedPaths}
                listings={treeListings}
                onNavigate={navigateTo}
                onToggle={toggleTreePath}
                depth={0}
                t={t}
              />
            </ul>
            {(preservation.data ?? []).length > 0 && (
              <div className="next-hosting-preserved-shortcuts">
                <h3>{t("React_ServerFiles_PreservedPaths")}</h3>
                <ul>
                  {(preservation.data ?? []).map((item) => (
                    <li key={item.primaryKey}>
                      <button
                        type="button"
                        title={item.path}
                        onClick={() =>
                          navigateTo(
                            item.scope === "File" ? parentServerPath(item.path) : item.path,
                            "persistent",
                          )
                        }
                      >
                        <Folder20Regular aria-hidden="true" />
                        <span>{item.path}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </aside>
          <div
            className={`next-hosting-file-content${dragActive ? " next-hosting-file-content--drag" : ""}`}
            onDragEnter={(event) => {
              if (!canMutate) return;
              event.preventDefault();
              setDragActive(true);
            }}
            onDragOver={(event) => {
              if (!canMutate) return;
              event.preventDefault();
              event.dataTransfer.dropEffect = "copy";
            }}
            onDragLeave={(event) => {
              if (!event.currentTarget.contains(event.relatedTarget as Node | null))
                setDragActive(false);
            }}
            onDrop={(event) => void handleDrop(event)}
          >
            {dragActive && (
              <div className="next-hosting-drop-overlay" aria-live="polite">
                <Document20Regular aria-hidden="true" />
                <strong>{t("React_ServerFiles_DropToUpload")}</strong>
                <span>{displayPath(path, context, t)}</span>
              </div>
            )}
            <div className="next-hosting-file-toolbar">
              <nav aria-label={t("Hosting_Detail_Breadcrumbs")}>
                <button type="button" onClick={() => navigateTo("")}>
                  {context === "persistent"
                    ? t("React_ServerWorkspace_PersistentShort")
                    : t("React_ServerWorkspace_LiveShort")}
                </button>
                {pathBreadcrumbs(path).map((crumb) => (
                  <span key={crumb.path}>
                    <span aria-hidden="true"> / </span>
                    <button type="button" onClick={() => navigateTo(crumb.path)}>
                      {crumb.label}
                    </button>
                  </span>
                ))}
              </nav>
              <label className="next-hosting-file-search">
                <Search20Regular aria-hidden="true" />
                <span className="sr-only">{t("React_ServerFiles_Search")}</span>
                <input
                  type="search"
                  value={search}
                  placeholder={t("React_ServerFiles_SearchPlaceholder")}
                  onChange={(event) => setSearch(event.target.value)}
                />
              </label>
              <button
                className="button button--ghost"
                type="button"
                disabled={files.isFetching}
                onClick={() => void files.refetch()}
              >
                {t("Common_Refresh")}
              </button>
              {canMutate && (
                <>
                  <button
                    ref={newFolderButtonRef}
                    className="button button--ghost"
                    type="button"
                    onClick={openNewFolder}
                  >
                    {t("Hosting_Detail_CreateFolder")}
                  </button>
                  <button
                    ref={uploadButtonRef}
                    className="button button--primary"
                    type="button"
                    onClick={openUpload}
                  >
                    {t("React_ServerFiles_Upload")}
                  </button>
                </>
              )}
            </div>
            {files.isLoading ? (
              <StatePanel kind="loading">{t("Hosting_Detail_FilesLoading")}</StatePanel>
            ) : sorted.length === 0 ? (
              <StatePanel kind="empty">
                {t("Hosting_Detail_FilesEmpty")}
                {canMutate && (
                  <button className="button button--primary" type="button" onClick={openUpload}>
                    {t("React_ServerFiles_Upload")}
                  </button>
                )}
              </StatePanel>
            ) : visibleFiles.length === 0 ? (
              <StatePanel kind="empty">{t("React_ServerFiles_NoSearchResults")}</StatePanel>
            ) : (
              <div className="next-hosting-table-scroll next-hosting-file-table">
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
                      <th className="next-hosting-actions-column">
                        <span className="sr-only">{t("Hosting_Detail_FileActions")}</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {visibleFiles.map((entry) => (
                      <tr
                        key={entry.path}
                        tabIndex={0}
                        data-selected={selectedPath === entry.path ? "true" : undefined}
                        onClick={() => setSelectedPath(entry.path)}
                        onFocus={() => setSelectedPath(entry.path)}
                        onKeyDown={(event) => handleFileRowKey(event, entry, navigateTo)}
                      >
                        <td>
                          {entry.isDirectory ? (
                            <button
                              className="next-hosting-file-name"
                              type="button"
                              onClick={() => navigateTo(entry.path)}
                            >
                              <Folder20Regular aria-hidden="true" />
                              <span>{entry.name}</span>
                            </button>
                          ) : (
                            <span className="next-hosting-file-label">
                              <Document20Regular aria-hidden="true" />
                              <span>{entry.name}</span>
                            </span>
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
                          <details className="next-hosting-file-menu">
                            <summary
                              aria-label={t("React_ServerFiles_ActionsFor", { name: entry.name })}
                            >
                              <MoreHorizontal20Regular aria-hidden="true" />
                            </summary>
                            <div role="menu">
                              {entry.isDirectory && (
                                <button
                                  role="menuitem"
                                  type="button"
                                  onClick={() => navigateTo(entry.path)}
                                >
                                  {t("React_ServerFiles_Open")}
                                </button>
                              )}
                              {!entry.isDirectory && (
                                <a
                                  role="menuitem"
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
                              {canMutate &&
                                entry.canModify &&
                                (!entry.isGeneratedTarget || entry.overridesGenerated) && (
                                  <button
                                    role="menuitem"
                                    type="button"
                                    disabled={deleteMutation.isPending}
                                    onClick={() => setDeleteTarget(entry)}
                                  >
                                    {t("Hosting_Detail_Delete")}
                                  </button>
                                )}
                            </div>
                          </details>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <footer className="next-hosting-file-footer" aria-live="polite">
              {search.trim()
                ? t("React_ServerFiles_FilteredCount", {
                    visible: visibleFiles.length,
                    total: sorted.length,
                  })
                : t("React_ServerFiles_ItemCount", { count: sorted.length })}
            </footer>
          </div>
        </div>
      </section>

      {server.canModify && (
        <PreservationPanel
          preservation={preservation.data ?? []}
          selection={selection}
          setSelection={setSelection}
          selectionPending={selectionMutation.isPending}
          deletePending={deleteSelection.isPending}
          onSave={() => selectionMutation.mutate()}
          onDelete={(id) => deleteSelection.mutate(id)}
          t={t}
        />
      )}

      <dialog
        ref={newFolderDialogRef}
        className="dialog next-hosting-file-dialog"
        aria-labelledby="server-files-new-folder-title"
        onCancel={(event) => {
          event.preventDefault();
          closeNewFolder();
        }}
      >
        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (folderNameIsValid(folderName)) directoryMutation.mutate();
          }}
        >
          <span className="kicker">{t("Hosting_Detail_FilesKicker")}</span>
          <h2 id="server-files-new-folder-title">{t("Hosting_Detail_CreateFolderTitle")}</h2>
          <p>{t("React_ServerFiles_Destination", { path: displayPath(path, "persistent", t) })}</p>
          <label className="field">
            <span>{t("React_ServerFiles_FolderName")}</span>
            <input
              autoFocus
              value={folderName}
              onChange={(event) => {
                setFolderName(event.target.value);
                directoryMutation.reset();
              }}
            />
          </label>
          {folderName && !folderNameIsValid(folderName) && (
            <p role="alert" className="next-hosting-inline-error">
              {t("React_ServerFiles_FolderNameInvalid")}
            </p>
          )}
          {directoryMutation.error && (
            <p role="alert" className="next-hosting-inline-error">
              {directoryMutation.error.message}
            </p>
          )}
          <div className="dialog__actions">
            <button className="button button--ghost" type="button" onClick={closeNewFolder}>
              {t("Common_Cancel")}
            </button>
            <button
              className="button button--primary"
              type="submit"
              disabled={!folderNameIsValid(folderName) || directoryMutation.isPending}
            >
              {t("Hosting_Detail_CreateFolder")}
            </button>
          </div>
        </form>
      </dialog>

      <dialog
        ref={uploadDialogRef}
        className="dialog next-hosting-file-dialog next-hosting-upload-dialog"
        aria-labelledby="server-files-upload-title"
        onCancel={(event) => {
          event.preventDefault();
          closeUpload();
        }}
      >
        <form onSubmit={(event) => event.preventDefault()}>
          <span className="kicker">{t("Hosting_Detail_FilesKicker")}</span>
          <h2 id="server-files-upload-title">{t("Hosting_Detail_UploadTitle")}</h2>
          <p>
            {t("React_ServerFiles_Destination", {
              path: displayPath(uploadDestination, "persistent", t),
            })}
          </p>
          <div className="next-hosting-upload-pickers">
            <button
              className="button button--ghost"
              type="button"
              onClick={() => fileInputRef.current?.click()}
            >
              {t("React_ServerFiles_ChooseFiles")}
            </button>
            <button
              className="button button--ghost"
              type="button"
              onClick={() => folderInputRef.current?.click()}
            >
              {t("React_ServerFiles_ChooseFolder")}
            </button>
            <span>{t("React_ServerWorkspace_FileMaximum")}</span>
            <input
              ref={fileInputRef}
              hidden
              type="file"
              multiple
              aria-label={t("React_ServerWorkspace_FileMaximum")}
              onChange={(event) => void handleInputFiles(event)}
            />
            <input
              ref={folderInputRef}
              hidden
              type="file"
              multiple
              aria-label={t("React_ServerFiles_ChooseFolder")}
              onChange={(event) => void handleInputFiles(event)}
            />
          </div>
          {preflightPending ? (
            <StatePanel kind="loading">{t("React_ServerFiles_CheckingDestinations")}</StatePanel>
          ) : uploadQueue.length === 0 ? (
            <div className="next-hosting-upload-empty">
              <Document20Regular aria-hidden="true" />
              <strong>{t("React_ServerFiles_SelectOrDrop")}</strong>
            </div>
          ) : (
            <>
              <div className="next-hosting-upload-summary" aria-live="polite">
                <strong>
                  {t("React_ServerFiles_UploadProgress", {
                    completed: completedCount,
                    total: uploadQueue.length,
                  })}
                </strong>
                <progress max={uploadQueue.length} value={completedCount} />
              </div>
              <ul className="next-hosting-upload-queue">
                {uploadQueue.map((item) => (
                  <li key={item.id} data-status={item.status}>
                    <Document20Regular aria-hidden="true" />
                    <span>
                      <strong>{item.relativePath}</strong>
                      <small>
                        {formatBytes(item.file.size)} ·{" "}
                        {t(`React_ServerFiles_Status_${item.status}`)}
                      </small>
                      {item.error && (
                        <small className="next-hosting-upload-error">{item.error}</small>
                      )}
                    </span>
                    {item.existing && (
                      <StatusBadge tone="warning">{t("React_ServerFiles_WillReplace")}</StatusBadge>
                    )}
                  </li>
                ))}
              </ul>
              {collisions.length > 0 && (
                <label className="next-hosting-check">
                  <input
                    type="checkbox"
                    checked={replacementApproved}
                    disabled={batchRunning}
                    onChange={(event) => setReplacementApproved(event.target.checked)}
                  />
                  <span>
                    {t("React_ServerFiles_ApproveReplacements", { count: collisions.length })}
                  </span>
                </label>
              )}
              {generatedCollisions.length > 0 && (
                <label className="next-hosting-check">
                  <input
                    type="checkbox"
                    checked={generatedApproved}
                    disabled={batchRunning}
                    onChange={(event) => setGeneratedApproved(event.target.checked)}
                  />
                  <span>{t("React_ServerWorkspace_CreateGeneratedOverride")}</span>
                </label>
              )}
            </>
          )}
          <div className="dialog__actions">
            {batchRunning ? (
              <button
                className="button button--danger"
                type="button"
                onClick={() => uploadAbortRef.current?.abort()}
              >
                {t("React_ServerFiles_CancelUpload")}
              </button>
            ) : (
              <button className="button button--ghost" type="button" onClick={closeUpload}>
                {t("Common_Cancel")}
              </button>
            )}
            {retryableCount > 0 && !batchRunning && (
              <button
                className="button button--ghost"
                type="button"
                onClick={() => void runUpload(true)}
              >
                {t("React_ServerFiles_RetryFailed", { count: retryableCount })}
              </button>
            )}
            <button
              className="button button--primary"
              type="button"
              disabled={
                batchRunning ||
                preflightPending ||
                !uploadQueue.some((item) => item.status === "queued") ||
                (collisions.length > 0 && !replacementApproved) ||
                (generatedCollisions.length > 0 && !generatedApproved)
              }
              onClick={() => void runUpload()}
            >
              {t("React_ServerFiles_Upload")}
            </button>
          </div>
        </form>
      </dialog>

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

function FileTreeBranch({
  parentPath,
  currentPath,
  expandedPaths,
  listings,
  onNavigate,
  onToggle,
  depth,
  t,
}: {
  parentPath: string;
  currentPath: string;
  expandedPaths: Set<string>;
  listings: Map<string, { files: HostingFile[]; loading: boolean; error: boolean }>;
  onNavigate(path: string): void;
  onToggle(path: string): void;
  depth: number;
  t: TFunction;
}) {
  const listing = listings.get(parentPath);
  if (listing?.loading)
    return <li className="next-hosting-tree-state">{t("Hosting_Detail_FilesLoading")}</li>;
  if (listing?.error)
    return <li className="next-hosting-tree-state">{t("React_ServerFiles_FolderLoadFailed")}</li>;
  const folders = (listing?.files ?? [])
    .filter((entry) => entry.isDirectory)
    .sort((left, right) => left.name.localeCompare(right.name, undefined, { sensitivity: "base" }));
  if (depth === 0 && folders.length === 0)
    return <li className="next-hosting-tree-state">{t("React_ServerFiles_NoFolders")}</li>;
  return (
    <>
      {folders.map((folder, index) => {
        const expanded = expandedPaths.has(folder.path);
        const selected = currentPath === folder.path;
        return (
          <li key={folder.path} role="none">
            <div
              className="next-hosting-tree-item"
              role="treeitem"
              aria-expanded={expanded}
              aria-selected={selected}
              tabIndex={selected || (depth === 0 && currentPath === "" && index === 0) ? 0 : -1}
              style={{ paddingInlineStart: `${0.35 + depth * 1.05}rem` }}
              onClick={() => onNavigate(folder.path)}
              onKeyDown={(event) => {
                if (event.key === "Enter" || event.key === " ") {
                  event.preventDefault();
                  onNavigate(folder.path);
                } else if (event.key === "ArrowRight" && !expanded) onToggle(folder.path);
                else if (event.key === "ArrowLeft" && expanded) onToggle(folder.path);
                else if (["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) {
                  moveTreeFocus(event);
                }
              }}
            >
              <button
                type="button"
                tabIndex={-1}
                aria-label={
                  expanded
                    ? t("React_ServerFiles_CollapseFolder", { name: folder.name })
                    : t("React_ServerFiles_ExpandFolder", { name: folder.name })
                }
                onClick={(event) => {
                  event.stopPropagation();
                  onToggle(folder.path);
                }}
              >
                {expanded ? <ChevronDown16Regular /> : <ChevronRight16Regular />}
              </button>
              {expanded ? (
                <FolderOpen20Regular aria-hidden="true" />
              ) : (
                <Folder20Regular aria-hidden="true" />
              )}
              <span>{folder.name}</span>
            </div>
            {expanded && (
              <ul role="group">
                <FileTreeBranch
                  parentPath={folder.path}
                  currentPath={currentPath}
                  expandedPaths={expandedPaths}
                  listings={listings}
                  onNavigate={onNavigate}
                  onToggle={onToggle}
                  depth={depth + 1}
                  t={t}
                />
              </ul>
            )}
          </li>
        );
      })}
    </>
  );
}

function PreservationPanel({
  preservation,
  selection,
  setSelection,
  selectionPending,
  deletePending,
  onSave,
  onDelete,
  t,
}: {
  preservation: PreservationSelection[];
  selection: PreservationSelectionInput;
  setSelection(
    value:
      | PreservationSelectionInput
      | ((current: PreservationSelectionInput) => PreservationSelectionInput),
  ): void;
  selectionPending: boolean;
  deletePending: boolean;
  onSave(): void;
  onDelete(id: string): void;
  t: TFunction;
}) {
  return (
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
          {preservation.length === 0 && <p>{t("React_ServerWorkspace_NoPreservation")}</p>}
          {preservation.map((item) => (
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
                disabled={item.seededByGame || deletePending}
                title={
                  item.seededByGame ? t("React_ServerWorkspace_GameSeededCannotRemove") : undefined
                }
                onClick={() => onDelete(item.primaryKey)}
              >
                {t("Hosting_Detail_Remove")}
              </button>
            </article>
          ))}
        </div>
        <form
          onSubmit={(event) => {
            event.preventDefault();
            onSave();
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
            disabled={!selection.path.trim() || selectionPending}
          >
            {t("Hosting_Detail_Add")}
          </button>
        </form>
      </div>
    </section>
  );
}

function folderNameIsValid(value: string): boolean {
  const trimmed = value.trim();
  return Boolean(trimmed && trimmed !== "." && trimmed !== ".." && !/[\\/]/.test(trimmed));
}

function displayPath(path: string, context: HostingFileContext, t: TFunction): string {
  const root =
    context === "persistent"
      ? t("React_ServerWorkspace_PersistentShort")
      : t("React_ServerWorkspace_LiveShort");
  return path ? `${root} / ${path}` : root;
}

function handleFileRowKey(
  event: KeyboardEvent<HTMLTableRowElement>,
  entry: HostingFile,
  navigate: (path: string) => void,
) {
  if (entry.isDirectory && (event.key === "Enter" || event.key === " ")) {
    event.preventDefault();
    navigate(entry.path);
  }
}

function moveTreeFocus(event: KeyboardEvent<HTMLDivElement>) {
  const tree = event.currentTarget.closest('[role="tree"]');
  const items = Array.from(tree?.querySelectorAll<HTMLElement>('[role="treeitem"]') ?? []);
  const currentIndex = items.indexOf(event.currentTarget);
  if (currentIndex < 0 || items.length === 0) return;
  const targetIndex =
    event.key === "Home"
      ? 0
      : event.key === "End"
        ? items.length - 1
        : event.key === "ArrowDown"
          ? Math.min(items.length - 1, currentIndex + 1)
          : Math.max(0, currentIndex - 1);
  event.preventDefault();
  items[targetIndex]?.focus();
}

async function readDroppedItems(dataTransfer: DataTransfer): Promise<SelectedUpload[]> {
  const entries = Array.from(dataTransfer.items)
    .map((item) => item.webkitGetAsEntry?.())
    .filter((entry): entry is FileSystemEntry => Boolean(entry));
  if (entries.length > 0) {
    const nested = await Promise.all(entries.map((entry) => readFileSystemEntry(entry, "")));
    return nested.flat();
  }
  return Array.from(dataTransfer.files).map((file) => ({ file, relativePath: file.name }));
}

async function readFileSystemEntry(
  entry: FileSystemEntry,
  parentPath: string,
): Promise<SelectedUpload[]> {
  const relativePath = parentPath ? `${parentPath}/${entry.name}` : entry.name;
  if (entry.isFile) {
    const file = await new Promise<File>((resolve, reject) =>
      (entry as FileSystemFileEntry).file(resolve, reject),
    );
    return [{ file, relativePath }];
  }
  if (!entry.isDirectory) return [];
  const reader = (entry as FileSystemDirectoryEntry).createReader();
  const children: FileSystemEntry[] = [];
  while (true) {
    const batch = await new Promise<FileSystemEntry[]>((resolve, reject) =>
      reader.readEntries(resolve, reject),
    );
    if (batch.length === 0) break;
    children.push(...batch);
  }
  return (
    await Promise.all(children.map((child) => readFileSystemEntry(child, relativePath)))
  ).flat();
}
