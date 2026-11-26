import {
  ArrowSync20Regular,
  ChevronDown16Regular,
  ChevronRight16Regular,
  Dismiss16Regular,
  Document20Regular,
  Folder20Regular,
  FolderOpen20Regular,
} from "@fluentui/react-icons";
import { useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useRef, useState, type DragEvent, type KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useNavigate, useNavigationType, useSearchParams } from "react-router";
import type { TFunction } from "i18next";
import { ApiError } from "@/api/client";
import { formatResource } from "@/app/i18n";
import { ActionAnchor, ActionButton } from "@/components/ActionControl";
import { FileChooserButton } from "@/components/FileChooserButton";
import { CheckboxField, RadioGroupField, SelectField, TextField } from "@/components/FormFields";
import { ConfirmDialog, StatePanel, StatusBadge } from "@/components/primitives";
import { FileBrowser } from "@/components/server/FileBrowser";
import { FileUploadDropzone } from "@/components/server/FileUploadDropzone";
import { SearchField } from "@/components/SearchField";
import { EmptyState, LoadingState } from "@/components/StateViews";
import { useLeaveGuard } from "@/components/useLeaveGuard";
import { filesLocationTo, readFilesLocation } from "@/features/hosting/filesLocation";
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
import { leavesWorkspaceSection } from "@/features/hosting/workspaceSections";

const maximumUploadBytes = 16 * 1024 * 1024;
const uploadConcurrency = 3;

type UploadStatus = "queued" | "uploading" | "succeeded" | "failed" | "cancelled" | "rejected";

/** The badge tone of each upload status: waiting states are neutral, progress is info, the outcomes follow their result. */
const uploadStatusTone: Record<UploadStatus, "neutral" | "success" | "danger" | "info"> = {
  queued: "neutral",
  uploading: "info",
  succeeded: "success",
  failed: "danger",
  rejected: "danger",
  cancelled: "neutral",
};

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
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const navigationType = useNavigationType();
  const read = readFilesLocation(searchParams);
  const { context, path } = read.location;
  const pathInvalid = read.invalid;
  const [expandedPaths, setExpandedPaths] = useState<Set<string>>(
    () => new Set(["", ...directoryAncestors(path)]),
  );
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
  const [mountKey] = useState(location.key);
  const [notice, setNotice] = useState<"invalid" | "notFound" | null>(null);
  const [noticeKey, setNoticeKey] = useState<string | null>(null);
  // Every location change (link, Back, Forward, tree item, context switch) resets the folder-local state. It is
  // adjusted while rendering, keyed on the router's location key, instead of inside an effect or `navigateTo`.
  const [seenLocation, setSeenLocation] = useState({ key: location.key, context });
  if (seenLocation.key !== location.key) {
    setSeenLocation({ key: location.key, context });
    const contextChanged = seenLocation.context !== context;
    setSearch("");
    setSelectedPath(undefined);
    setTreeOpen(false);
    // The fallback replace keeps its notice; any other navigation (link, Back, Forward) clears it.
    if (navigationType !== "REPLACE") setNotice(null);
    setExpandedPaths((current) => {
      const next = contextChanged ? new Set([""]) : new Set(current);
      next.add("");
      for (const ancestor of directoryAncestors(path)) next.add(ancestor);
      return next;
    });
  }
  const newFolderButtonRef = useRef<HTMLButtonElement>(null);
  const uploadButtonRef = useRef<HTMLButtonElement>(null);
  const uploadAbortRef = useRef<AbortController | undefined>(undefined);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const deleteInvokerRef = useRef<HTMLElement | null>(null);
  const dropzoneRef = useRef<HTMLDivElement>(null);
  const afterUploadRemoval = useRef<{ nextId: string | null } | null>(null);

  // Only work that the section itself would lose counts as dirty; folder changes stay inside the section and are
  // never blocked (D-10). File operations are immediate and never enter the staged draft (D-01).
  const dirty = Boolean(selection.path || uploadQueue.some((item) => item.status === "queued"));
  const leaveGuard = useLeaveGuard({
    dirty,
    shouldBlock: ({ currentLocation, nextLocation }) =>
      leavesWorkspaceSection(currentLocation, nextLocation, "files"),
  });

  const files = useQuery({
    queryKey: hostingQueryKeys.files(server.primaryKey, context, path),
    queryFn: ({ signal }) => hostingService.files(server.primaryKey, context, path, signal),
  });
  // An invalid ?path= was already reduced to the top level by the parser. A valid non-root path that the API
  // reports as not found falls back to the top level once; a top-level not-found keeps the error panel and never
  // navigates, so no fallback loop can exist.
  const notFoundFallback =
    path !== "" && files.error instanceof ApiError && files.error.code === "not-found";
  const fallback = pathInvalid ? "invalid" : notFoundFallback ? "notFound" : null;
  if (fallback !== null && noticeKey !== location.key) {
    setNoticeKey(location.key);
    setNotice(fallback);
  }
  const { pathname, search: locationSearch } = location;
  useEffect(() => {
    if (fallback === null) return;
    void navigate(
      filesLocationTo(pathname, new URLSearchParams(locationSearch), { path: "", context }),
      { replace: true },
    );
  }, [fallback, pathname, locationSearch, context, navigate]);

  // After a user-driven change (a push other than the one that mounted the panel) the folder heading takes focus
  // once its listing is available. Back, Forward, replace and the first load never move focus. A change of the file
  // context keeps focus on the context radios instead, so the Arrow keys can keep moving between the contexts.
  const loaded = files.data !== undefined;
  // The context switch arms the flag; the effect settles it on the first render of the new location, whether or not
  // its listing ever loads, and remembers that location's key, so a failed or abandoned switch cannot swallow the
  // heading focus of a later, unrelated navigation.
  const keepContextFocusRef = useRef(false);
  const contextFocusKeyRef = useRef<string | null>(null);
  useEffect(() => {
    if (navigationType !== "PUSH" || location.key === mountKey) return;
    if (keepContextFocusRef.current) {
      keepContextFocusRef.current = false;
      contextFocusKeyRef.current = location.key;
    }
    if (!loaded || contextFocusKeyRef.current === location.key) return;
    headingRef.current?.focus();
  }, [navigationType, loaded, location.key, mountKey]);

  // The upload dialog opens on its first choose control. The dialog body is a focusable region, so the browser's own
  // initial focus would land there; this effect runs after the dialog has been shown and moves focus on.
  useEffect(() => {
    if (!uploadOpen) return;
    dropzoneRef.current?.querySelector<HTMLElement>("button")?.focus();
  }, [uploadOpen]);

  // After a queued file is removed its row is gone together with the focused remove control. Focus moves to the
  // remove control of the row that took its place, or to the first choose control of the drop zone once no
  // removable row is left, so the member keeps their place inside the modal dialog.
  useEffect(() => {
    const pending = afterUploadRemoval.current;
    if (!pending) return;
    afterUploadRemoval.current = null;
    const zone = dropzoneRef.current;
    if (!zone) return;
    if (pending.nextId !== null) {
      const controls = zone
        .closest("dialog")
        ?.querySelectorAll<HTMLElement>("[data-upload-remove]");
      for (const control of controls ?? []) {
        if (control.dataset["uploadRemove"] === pending.nextId) {
          control.focus();
          return;
        }
      }
    }
    zone.querySelector<HTMLElement>("button")?.focus();
  }, [uploadQueue]);

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
        classificationLabel(entry.classification, t).toLocaleLowerCase().includes(query),
    );
  }, [search, sorted, t]);
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

  const refreshFiles = () =>
    client.invalidateQueries({ queryKey: ["hosting", "servers", server.primaryKey, "files"] });

  const deleteMutation = useMutation({
    mutationFn: (entry: HostingFile) =>
      hostingService.deleteFile(server.primaryKey, context, entry.path, entry.isDirectory),
    onSuccess: async () => {
      setDeleteTarget(undefined);
      setSelectedPath(undefined);
      // The row that held the invoker is gone after a delete, so the folder heading takes focus instead.
      deleteInvokerRef.current = null;
      window.setTimeout(() => headingRef.current?.focus());
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
    (notFoundFallback ? undefined : files.error?.message) ??
    preservation.error?.message ??
    deleteMutation.error?.message ??
    selectionMutation.error?.message ??
    deleteSelection.error?.message;

  /** The router location of a folder, built from the normalized path and never from the raw URL value. */
  function folderTo(nextPath: string, nextContext: HostingFileContext = context) {
    return filesLocationTo(location.pathname, searchParams, {
      path: normalizeServerRelativePath(nextPath),
      context: nextContext,
    });
  }

  function navigateTo(nextPath: string, nextContext: HostingFileContext = context) {
    const normalized = normalizeServerRelativePath(nextPath);
    if (normalized === path && nextContext === context) return;
    void navigate(folderTo(normalized, nextContext));
  }

  /** Drops the queued uploads and the unsaved preservation selection, then lets the held navigation continue. */
  function discardAndLeave() {
    setUploadOpen(false);
    setUploadQueue([]);
    setDirectoriesToCreate([]);
    setSelection({ path: "", scope: "File", mode: "CreateOrUpdate" });
    leaveGuard.proceed();
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

  function closeDelete() {
    setDeleteTarget(undefined);
    const invoker = deleteInvokerRef.current;
    window.setTimeout(() => invoker?.focus());
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

  /**
   * Takes one queued file out of the queue. The collision approvals follow the remaining queue because they are
   * derived from it, and the folders that only the removed file needed are no longer created.
   */
  function removeUpload(id: string) {
    const remaining = uploadQueue.filter((item) => item.id !== id);
    // Only a queued row carries a remove control: prefer the next one, then the previous one.
    const removable = uploadQueue.filter((item) => item.status === "queued");
    const position = removable.findIndex((item) => item.id === id);
    afterUploadRemoval.current = {
      nextId: (removable[position + 1] ?? removable[position - 1])?.id ?? null,
    };
    setUploadQueue(remaining);
    setDirectoriesToCreate((current) =>
      current.filter((directory) =>
        remaining.some(
          (item) => item.status !== "rejected" && item.targetPath.startsWith(`${directory}/`),
        ),
      ),
    );
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

  async function handleChosenFiles(chosen: File[]) {
    await prepareUploads(
      chosen.map((file) => ({ file, relativePath: file.webkitRelativePath || file.name })),
    );
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

  const crumbs = pathBreadcrumbs(path);
  const currentFolder = path === "" ? "" : path.slice(path.lastIndexOf("/") + 1);
  const folderLabel = path === "" ? t("Server_Files_TopLevel") : currentFolder;
  const announcement = files.data
    ? t("Server_Files_FolderAnnounce", { folder: folderLabel, count: files.data.length })
    : "";
  const rootListing = treeListings.get("");
  const rootHasFolders = (rootListing?.files ?? []).some((entry) => entry.isDirectory);
  const rootLabel =
    context === "persistent"
      ? t("React_ServerWorkspace_PersistentShort")
      : t("React_ServerWorkspace_LiveShort");
  // The heading of the list pane is the folder's name, or this context label at the root, so it is never empty.
  const contextLabel =
    context === "persistent" ? t("Hosting_Detail_PersistentFiles") : t("Hosting_Detail_LiveFiles");

  return (
    <>
      <section className="workspace-panel next-hosting-files-panel">
        <header className="workspace-panel__head next-hosting-files-heading">
          <div>
            <span className="kicker">{t("Hosting_Detail_FilesKicker")}</span>
            <h2>{t("Hosting_Detail_FilesTitle")}</h2>
            <p>{t("Hosting_Detail_FilesDescription")}</p>
          </div>
          <RadioGroupField
            layout="segmented"
            hideLegend
            legend={t("Hosting_Detail_FileContexts")}
            name="file-context"
            value={context}
            options={[
              { value: "persistent", label: t("Hosting_Detail_PersistentFiles") },
              { value: "live", label: t("Hosting_Detail_LiveFiles") },
            ]}
            onChange={(value) => {
              keepContextFocusRef.current = true;
              navigateTo("", value === "live" ? "live" : "persistent");
            }}
          />
        </header>
        {notice && (
          <StatePanel
            kind="info"
            action={
              <ActionButton variant="ghost" size="compact" onClick={() => setNotice(null)}>
                {t("Server_Files_NoticeDismiss")}
              </ActionButton>
            }
          >
            {notice === "invalid" ? t("Server_Files_PathInvalid") : t("Server_Files_PathFallback")}
          </StatePanel>
        )}
        {error && (
          <p role="alert" className="next-hosting-inline-error">
            {error}
          </p>
        )}
        <ActionButton
          variant="ghost"
          className="next-hosting-tree-toggle"
          aria-expanded={treeOpen}
          onClick={() => setTreeOpen((value) => !value)}
        >
          {treeOpen ? t("React_ServerFiles_HideFolders") : t("React_ServerFiles_ShowFolders")}
        </ActionButton>
        <FileBrowser
          toolbarLabel={t("Server_Files_Toolbar_Label")}
          treeOpen={treeOpen}
          dragActive={dragActive}
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
          breadcrumb={
            <nav aria-label={t("Server_Files_PathLabel")}>
              {path === "" ? (
                <span aria-current="location">{rootLabel}</span>
              ) : (
                <Link to={folderTo("")}>{rootLabel}</Link>
              )}
              {crumbs.map((crumb, index) => (
                <span key={crumb.path}>
                  <span aria-hidden="true"> / </span>
                  {index === crumbs.length - 1 ? (
                    <span aria-current="location">{crumb.label}</span>
                  ) : (
                    <Link to={folderTo(crumb.path)}>{crumb.label}</Link>
                  )}
                </span>
              ))}
            </nav>
          }
          search={
            <SearchField
              label={t("React_ServerFiles_Search")}
              placeholder={t("React_ServerFiles_SearchPlaceholder")}
              clearLabel={t("Server_Fleet_SearchClear")}
              value={search}
              onChange={setSearch}
            />
          }
          refresh={
            <ActionButton
              variant="ghost"
              iconOnly
              aria-label={t("Common_Refresh")}
              disabled={files.isFetching}
              onClick={() => void files.refetch()}
            >
              <ArrowSync20Regular aria-hidden="true" />
            </ActionButton>
          }
          actions={
            canMutate ? (
              <>
                <ActionButton ref={uploadButtonRef} variant="gold" onClick={openUpload}>
                  {t("React_ServerFiles_Upload")}
                </ActionButton>
                <ActionButton ref={newFolderButtonRef} variant="ghost" onClick={openNewFolder}>
                  {t("Hosting_Detail_CreateFolder")}
                </ActionButton>
              </>
            ) : null
          }
          treePane={
            <aside className="next-hosting-file-tree-pane">
              <p className="file-browser__tree-title">{contextLabel}</p>
              {rootListing?.loading ? (
                <p className="next-hosting-tree-state">{t("Hosting_Detail_FilesLoading")}</p>
              ) : rootListing?.error ? (
                <p className="next-hosting-tree-state">{t("React_ServerFiles_FolderLoadFailed")}</p>
              ) : !rootHasFolders ? (
                <p className="next-hosting-tree-state">{t("React_ServerFiles_NoFolders")}</p>
              ) : (
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
              )}
              {(preservation.data ?? []).length > 0 && (
                <div className="next-hosting-preserved-shortcuts">
                  <h3>{t("React_ServerFiles_PreservedPaths")}</h3>
                  <ul>
                    {(preservation.data ?? []).map((item) => (
                      <li key={item.primaryKey}>
                        <ActionButton
                          variant="ghost"
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
                        </ActionButton>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </aside>
          }
          listPane={
            <>
              {dragActive && (
                <div
                  className="file-browser__drop-overlay next-hosting-drop-overlay"
                  aria-live="polite"
                >
                  <Document20Regular aria-hidden="true" />
                  <strong>{t("React_ServerFiles_DropToUpload")}</strong>
                  <span>{displayPath(path, context, t)}</span>
                </div>
              )}
              <h3
                ref={headingRef}
                tabIndex={-1}
                className="file-browser__heading next-hosting-file-folder-heading"
              >
                {currentFolder === "" ? contextLabel : currentFolder}
              </h3>
              <div className="sr-only" aria-live="polite">
                {announcement}
              </div>
              {files.isLoading ? (
                <LoadingState label={t("Hosting_Detail_FilesLoading")} />
              ) : sorted.length === 0 ? (
                <EmptyState
                  title={t("Hosting_Detail_FilesEmptyTitle")}
                  action={
                    canMutate ? (
                      <ActionButton variant="primary" onClick={openUpload}>
                        {t("React_ServerFiles_Upload")}
                      </ActionButton>
                    ) : undefined
                  }
                >
                  {t("Hosting_Detail_FilesEmptyBody")}
                </EmptyState>
              ) : visibleFiles.length === 0 ? (
                <EmptyState title={t("React_ServerFiles_NoSearchResultsTitle")}>
                  {t("React_ServerFiles_NoSearchResultsBody")}
                </EmptyState>
              ) : (
                <div className="next-hosting-table-scroll next-hosting-file-table file-browser__table">
                  <table>
                    <caption className="sr-only">{t("Hosting_Detail_FilesTitle")}</caption>
                    <thead>
                      <tr>
                        <th>
                          <ActionButton
                            variant="ghost"
                            size="compact"
                            onClick={() => setSort("name")}
                          >
                            {t("Hosting_Detail_FileName")}
                          </ActionButton>
                        </th>
                        <th>
                          <ActionButton
                            variant="ghost"
                            size="compact"
                            onClick={() => setSort("modified")}
                          >
                            {t("Hosting_Detail_FileModified")}
                          </ActionButton>
                        </th>
                        <th>{t("Hosting_Detail_FileType")}</th>
                        <th>
                          <ActionButton
                            variant="ghost"
                            size="compact"
                            onClick={() => setSort("size")}
                          >
                            {t("Hosting_Detail_FileSize")}
                          </ActionButton>
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
                              <Link className="next-hosting-file-name" to={folderTo(entry.path)}>
                                <Folder20Regular aria-hidden="true" />
                                <span>{entry.name}</span>
                              </Link>
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
                          <td>{classificationLabel(entry.classification, t)}</td>
                          <td>{entry.isDirectory ? "—" : formatBytes(entry.size)}</td>
                          <td>
                            <div className="next-hosting-file-actions">
                              {entry.isDirectory && (
                                <ActionButton
                                  variant="ghost"
                                  size="compact"
                                  aria-label={t("React_ServerFiles_OpenNamed", {
                                    name: entry.name,
                                  })}
                                  onClick={() => navigateTo(entry.path)}
                                >
                                  {t("React_ServerFiles_Open")}
                                </ActionButton>
                              )}
                              {!entry.isDirectory && (
                                <ActionAnchor
                                  variant="ghost"
                                  size="compact"
                                  aria-label={t("React_ServerFiles_DownloadNamed", {
                                    name: entry.name,
                                  })}
                                  href={hostingService.fileDownloadUrl(
                                    server.primaryKey,
                                    context,
                                    entry.path,
                                  )}
                                  download={entry.name}
                                >
                                  {t("Hosting_Detail_Download")}
                                </ActionAnchor>
                              )}
                              {canMutate &&
                                entry.canModify &&
                                (!entry.isGeneratedTarget || entry.overridesGenerated) && (
                                  <ActionButton
                                    variant="danger"
                                    size="compact"
                                    aria-label={t("React_ServerFiles_DeleteNamed", {
                                      name: entry.name,
                                    })}
                                    disabled={deleteMutation.isPending}
                                    onClick={(event) => {
                                      deleteInvokerRef.current = event.currentTarget;
                                      setDeleteTarget(entry);
                                    }}
                                  >
                                    {t("Hosting_Detail_Delete")}
                                  </ActionButton>
                                )}
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </>
          }
          footer={
            <>
              <span>
                {search.trim()
                  ? t("React_ServerFiles_FilteredCount", {
                      visible: visibleFiles.length,
                      total: sorted.length,
                    })
                  : t("React_ServerFiles_ItemCount", { count: sorted.length })}
              </span>
              <span className="file-browser__footer-note">
                {t("Server_Files_Footer_Immediate")}
              </span>
            </>
          }
        />
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

      <ConfirmDialog
        open={newFolderOpen}
        className="next-hosting-file-dialog"
        kicker={t("Hosting_Detail_FilesKicker")}
        title={t("Hosting_Detail_CreateFolderTitle")}
        description={t("React_ServerFiles_Destination", {
          path: displayPath(path, "persistent", t),
        })}
        confirmLabel={t("Hosting_Detail_CreateFolder")}
        confirmVariant="gold"
        cancelLabel={t("React_ServerFiles_NewFolder_Dismiss")}
        busy={directoryMutation.isPending}
        confirmDisabled={!folderNameIsValid(folderName)}
        error={directoryMutation.error?.message}
        onCancel={closeNewFolder}
        onConfirm={() => directoryMutation.mutate()}
      >
        <TextField
          label={t("React_ServerFiles_FolderName")}
          value={folderName}
          error={
            folderName && !folderNameIsValid(folderName)
              ? t("React_ServerFiles_FolderNameInvalid")
              : undefined
          }
          onChange={(event) => {
            setFolderName(event.target.value);
            directoryMutation.reset();
          }}
        />
      </ConfirmDialog>

      <ConfirmDialog
        open={uploadOpen}
        className="next-hosting-file-dialog next-hosting-upload-dialog"
        kicker={t("Hosting_Detail_FilesKicker")}
        title={t("Hosting_Detail_UploadTitle")}
        description={t("React_ServerFiles_Destination", {
          path: displayPath(uploadDestination, "persistent", t),
        })}
        confirmLabel={t("React_ServerFiles_Upload")}
        confirmVariant="gold"
        cancelLabel={t("React_ServerFiles_Upload_Dismiss")}
        blockFileDrops
        busy={batchRunning}
        bodyLabel={t("Hosting_Detail_UploadTitle")}
        confirmUnavailableReason={
          !batchRunning &&
          !preflightPending &&
          !uploadQueue.some((item) => item.status === "queued")
            ? t("React_ServerFiles_SelectOrDrop")
            : undefined
        }
        confirmDisabled={
          preflightPending ||
          (collisions.length > 0 && !replacementApproved) ||
          (generatedCollisions.length > 0 && !generatedApproved)
        }
        extraAction={
          retryableCount > 0 && !batchRunning
            ? {
                label: t("React_ServerFiles_RetryFailed", { count: retryableCount }),
                variant: "ghost",
                onClick: () => void runUpload(true),
              }
            : undefined
        }
        onCancel={closeUpload}
        onConfirm={() => void runUpload()}
      >
        <FileUploadDropzone
          ref={dropzoneRef}
          title={t("Server_Files_Upload_DropTitle")}
          body={t("Server_Files_Upload_DropBody")}
          disabled={batchRunning || preflightPending}
          onDrop={(event) => void handleDrop(event)}
          chooseFiles={
            <FileChooserButton
              multiple
              inputLabel={t("React_ServerWorkspace_FileMaximum")}
              onFiles={(chosen) => void handleChosenFiles(chosen)}
            >
              {t("React_ServerFiles_ChooseFiles")}
            </FileChooserButton>
          }
          chooseFolder={
            <FileChooserButton
              directory
              inputLabel={t("React_ServerFiles_ChooseFolder")}
              onFiles={(chosen) => void handleChosenFiles(chosen)}
            >
              {t("React_ServerFiles_ChooseFolder")}
            </FileChooserButton>
          }
        />
        <span className="file-dropzone__limit">{t("React_ServerWorkspace_FileMaximum")}</span>
        {preflightPending ? (
          <StatePanel kind="loading">{t("React_ServerFiles_CheckingDestinations")}</StatePanel>
        ) : uploadQueue.length === 0 ? null : (
          <>
            <div className="file-upload-summary next-hosting-upload-summary" aria-live="polite">
              <strong>
                {t("React_ServerFiles_UploadProgress", {
                  completed: completedCount,
                  total: uploadQueue.length,
                })}
              </strong>
              <progress max={uploadQueue.length} value={completedCount} />
              {batchRunning && (
                <ActionButton
                  variant="danger"
                  size="compact"
                  onClick={() => uploadAbortRef.current?.abort()}
                >
                  {t("React_ServerFiles_CancelUpload")}
                </ActionButton>
              )}
            </div>
            <ul className="file-upload-queue next-hosting-upload-queue">
              {uploadQueue.map((item) => (
                <li key={item.id} className="file-upload-queue__row" data-status={item.status}>
                  <Document20Regular aria-hidden="true" />
                  <span className="file-upload-queue__name">
                    <strong className="file-upload-queue__path">{item.relativePath}</strong>
                    <small className="file-upload-queue__size">{formatBytes(item.file.size)}</small>
                    {item.error && (
                      <small className="file-upload-queue__error next-hosting-upload-error">
                        {item.error}
                      </small>
                    )}
                  </span>
                  <span className="file-upload-queue__end">
                    <StatusBadge tone={uploadStatusTone[item.status]}>
                      {t(`React_ServerFiles_Status_${item.status}`)}
                    </StatusBadge>
                    {item.existing && (
                      <StatusBadge tone="warning">{t("React_ServerFiles_WillReplace")}</StatusBadge>
                    )}
                    {item.status === "queued" && !batchRunning && (
                      <ActionButton
                        variant="ghost"
                        size="compact"
                        iconOnly
                        data-upload-remove={item.id}
                        aria-label={t("Server_Files_Upload_RemoveNamed", {
                          name: item.relativePath,
                        })}
                        onClick={() => removeUpload(item.id)}
                      >
                        <Dismiss16Regular aria-hidden="true" />
                      </ActionButton>
                    )}
                  </span>
                </li>
              ))}
            </ul>
            {collisions.length > 0 && (
              <CheckboxField
                label={t("React_ServerFiles_ApproveReplacements", { count: collisions.length })}
                checked={replacementApproved}
                disabled={batchRunning}
                onChange={(event) => setReplacementApproved(event.target.checked)}
              />
            )}
            {generatedCollisions.length > 0 && (
              <CheckboxField
                label={t("React_ServerWorkspace_CreateGeneratedOverride")}
                checked={generatedApproved}
                disabled={batchRunning}
                onChange={(event) => setGeneratedApproved(event.target.checked)}
              />
            )}
          </>
        )}
      </ConfirmDialog>

      <ConfirmDialog
        open={leaveGuard.blocked}
        className="next-hosting-file-dialog"
        title={t("Server_Files_Leave_Title")}
        description={t("Server_Files_Leave_Body")}
        cancelLabel={t("Server_Change_Leave_Stay")}
        confirmLabel={t("Server_Files_Leave_Discard")}
        confirmVariant="danger"
        initialFocus="dismiss"
        onCancel={leaveGuard.stay}
        onConfirm={discardAndLeave}
      />

      <ConfirmDialog
        open={Boolean(deleteTarget)}
        className="next-hosting-file-dialog"
        title={t("React_ServerWorkspace_DeleteFile")}
        description={formatResource(
          t("Hosting_Detail_DeleteFileConfirm"),
          deleteTarget?.path ?? "",
        )}
        confirmLabel={t("Hosting_Detail_Delete")}
        cancelLabel={t("React_ServerFiles_DeleteFile_Dismiss")}
        initialFocus="dismiss"
        busy={deleteMutation.isPending}
        onCancel={closeDelete}
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
    return (
      <li role="none">
        <span className="next-hosting-tree-state">{t("Hosting_Detail_FilesLoading")}</span>
      </li>
    );
  if (listing?.error)
    return (
      <li role="none">
        <span className="next-hosting-tree-state">{t("React_ServerFiles_FolderLoadFailed")}</span>
      </li>
    );
  const folders = (listing?.files ?? [])
    .filter((entry) => entry.isDirectory)
    .sort((left, right) => left.name.localeCompare(right.name, undefined, { sensitivity: "base" }));
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
              aria-current={selected ? "location" : undefined}
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
              <ActionButton
                variant="ghost"
                iconOnly
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
                {expanded ? (
                  <ChevronDown16Regular aria-hidden="true" />
                ) : (
                  <ChevronRight16Regular aria-hidden="true" />
                )}
              </ActionButton>
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
                  {t(preservationScopeKeys[item.scope])} · {t(preservationModeKeys[item.mode])}
                </span>
                {item.seededByGame && (
                  <StatusBadge tone="info">{t("Hosting_Detail_SeededByGame")}</StatusBadge>
                )}
              </div>
              <ActionButton
                variant="danger"
                size="compact"
                disabled={deletePending}
                unavailableReason={
                  item.seededByGame ? t("React_ServerWorkspace_GameSeededCannotRemove") : undefined
                }
                onClick={() => onDelete(item.primaryKey)}
              >
                {t("Hosting_Detail_Remove")}
              </ActionButton>
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
          <TextField
            required
            label={t("Hosting_Detail_TargetPath")}
            value={selection.path}
            onChange={(event) =>
              setSelection((current) => ({ ...current, path: event.target.value }))
            }
          />
          <SelectField
            label={t("Hosting_Detail_Scope")}
            value={selection.scope}
            options={[
              { value: "File", label: t("Hosting_Detail_ScopeFile") },
              { value: "FlatFolder", label: t("Hosting_Detail_ScopeFlat") },
              { value: "RecursiveFolder", label: t("Hosting_Detail_ScopeRecursive") },
            ]}
            onChange={(event) =>
              setSelection((current) => ({
                ...current,
                scope: event.target.value as PreservationSelectionInput["scope"],
              }))
            }
          />
          <SelectField
            label={t("Hosting_Detail_DeletionPolicy")}
            value={selection.mode}
            options={[
              { value: "CreateOrUpdate", label: t("Hosting_Detail_ModeCreateUpdate") },
              { value: "MirrorDeletions", label: t("Hosting_Detail_ModeMirror") },
            ]}
            onChange={(event) =>
              setSelection((current) => ({
                ...current,
                mode: event.target.value as PreservationSelectionInput["mode"],
              }))
            }
          />
          <ActionButton
            variant="primary"
            type="submit"
            disabled={!selection.path.trim() || selectionPending}
          >
            {t("Hosting_Detail_Add")}
          </ActionButton>
        </form>
      </div>
    </section>
  );
}

/** The localized labels of the preservation scopes; the same texts the add form offers. */
const preservationScopeKeys = {
  File: "Hosting_Detail_ScopeFile",
  FlatFolder: "Hosting_Detail_ScopeFlat",
  RecursiveFolder: "Hosting_Detail_ScopeRecursive",
} as const satisfies Record<PreservationSelection["scope"], string>;

/** The localized labels of the preservation deletion policies; the same texts the add form offers. */
const preservationModeKeys = {
  CreateOrUpdate: "Hosting_Detail_ModeCreateUpdate",
  MirrorDeletions: "Hosting_Detail_ModeMirror",
} as const satisfies Record<PreservationSelection["mode"], string>;

/** The locale key of every classification token the API reports. */
const classificationKeys = {
  Configuration: "Hosting_FileClassification_Configuration",
  Downloaded: "Hosting_FileClassification_Downloaded",
  GameFile: "Hosting_FileClassification_GameFile",
  Generated: "Hosting_FileClassification_Generated",
  Log: "Hosting_FileClassification_Log",
  Mission: "Hosting_FileClassification_Mission",
  Mod: "Hosting_FileClassification_Mod",
  SaveFile: "Hosting_FileClassification_SaveFile",
  Unknown: "Hosting_FileClassification_Unknown",
  Upload: "Hosting_FileClassification_Upload",
} as const;

/**
 * Names the classification of a file in the Type column.
 *
 * Every classification the API reports has a localized label; a token added to the API later falls back to its
 * humanized form until the locale files carry a label for it.
 *
 * @param classification The classification token of the file.
 * @param t The translation function of the panel.
 * @returns The text of the Type cell.
 */
function classificationLabel(classification: string, t: TFunction): string {
  return Object.hasOwn(classificationKeys, classification)
    ? t(classificationKeys[classification as keyof typeof classificationKeys])
    : humanizeToken(classification);
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
