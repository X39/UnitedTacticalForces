import {
  useInfiniteQuery,
  useMutation,
  useQueryClient,
  type InfiniteData,
} from "@tanstack/react-query";
import { Checkmark16Regular } from "@fluentui/react-icons";
import { useMemo, useState, type FormEvent, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { resolveImageUrl } from "@/api/client";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageGallery } from "@/auth/claims";
import { ActionButton } from "@/components/ActionControl";
import { useAppToastOnChange } from "@/components/AppToasts";
import { CopyButton } from "@/components/CopyButton";
import { FileChooserButton } from "@/components/FileChooserButton";
import { TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import { ConfirmDialog, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import { SearchField } from "@/components/SearchField";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { useLeaveGuard } from "@/components/useLeaveGuard";
import {
  loadMediaAssetPage,
  mediaAssetMatchesUsageDomains,
  mediaAssetTagsEqual,
  parseMediaAssetTags,
  replaceMediaAssetTags,
  uploadMediaAsset,
  type MediaAsset,
} from "@/features/admin/siteImages";
import "@/features/admin/mediaAdmin.css";

const pageSize = 60;
const maximumImageBytes = 10 * 1024 * 1024;
const acceptedTypes = new Set([
  "image/avif",
  "image/bmp",
  "image/gif",
  "image/jpeg",
  "image/png",
  "image/webp",
]);
const sourceDomains = [
  "terrain",
  "event",
  "field-report",
  "public-page",
  "site-image",
  "unused",
] as const;
type SourceDomain = (typeof sourceDomains)[number];

function formatBytes(value: number): string {
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / 1024 / 1024).toFixed(1)} MB`;
}

function upsertAsset(
  current: InfiniteData<MediaAsset[], number> | undefined,
  asset: MediaAsset,
): InfiniteData<MediaAsset[], number> {
  if (!current) return { pages: [[asset]], pageParams: [0] };
  const pages = current.pages.map((page) =>
    page.filter((item) => item.primaryKey !== asset.primaryKey),
  );
  pages[0] = [asset, ...(pages[0] ?? [])];
  return { ...current, pages };
}

/** Renders the page header above a state view so every state of the gallery keeps exactly one `h1`. */
function GalleryStateFrame({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <PageStack>
      <PageHeader title={t("Shared_CommandCenter_Nav_Gallery")} />
      {children}
    </PageStack>
  );
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const canManage = canManageGallery(user);
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [includeTagsDraft, setIncludeTagsDraft] = useState("");
  const [excludeTagsDraft, setExcludeTagsDraft] = useState("");
  const [includeTags, setIncludeTags] = useState<string[]>([]);
  const [excludeTags, setExcludeTags] = useState<string[]>([]);
  const [uploadTags, setUploadTags] = useState("");
  const [enabledSources, setEnabledSources] = useState<Set<SourceDomain>>(
    () => new Set(sourceDomains),
  );
  const [selectedAssetId, setSelectedAssetId] = useState("");
  const [tagEditor, setTagEditor] = useState<{ assetId: string; value: string }>();
  const [notice, setNotice] = useState<{ tone: "success" | "error"; text: string }>();
  const [fileError, setFileError] = useState<string>();
  const queryKey = ["media-assets", "gallery", search, includeTags, excludeTags] as const;
  const query = useInfiniteQuery({
    queryKey,
    queryFn: ({ signal, pageParam }) =>
      loadMediaAssetPage(
        { search, includeTags, excludeTags, skip: pageParam, take: pageSize },
        signal,
      ),
    initialPageParam: 0,
    getNextPageParam: (page, pages) =>
      page.length === pageSize ? pages.length * pageSize : undefined,
    enabled: canManage,
  });
  const assets = useMemo(() => query.data?.pages.flat() ?? [], [query.data]);
  const visibleAssets = useMemo(
    () => assets.filter((asset) => mediaAssetMatchesUsageDomains(asset, enabledSources)),
    [assets, enabledSources],
  );
  const selectedAsset =
    visibleAssets.find((asset) => asset.primaryKey === selectedAssetId) ?? visibleAssets[0];
  const tagEditorValue =
    tagEditor && tagEditor.assetId === selectedAsset?.primaryKey
      ? tagEditor.value
      : (selectedAsset?.tags.join(", ") ?? "");
  const tagDirty = Boolean(
    selectedAsset && !mediaAssetTagsEqual(tagEditorValue, selectedAsset.tags),
  );
  const leaveGuard = useLeaveGuard({ dirty: tagDirty });
  useAppToastOnChange(notice?.text, notice?.tone ?? "success");

  const upload = useMutation({
    mutationFn: (file: File) => uploadMediaAsset(file, parseMediaAssetTags(uploadTags)),
    onMutate: () => setNotice(undefined),
    onSuccess: async (asset) => {
      queryClient.setQueryData<InfiniteData<MediaAsset[], number>>(queryKey, (current) =>
        upsertAsset(current, asset),
      );
      setSelectedAssetId(asset.primaryKey);
      setTagEditor({ assetId: asset.primaryKey, value: asset.tags.join(", ") });
      setUploadTags("");
      setFileError(undefined);
      setNotice({ tone: "success", text: t("Pages_Command_Gallery_UploadSuccess") });
      await queryClient.invalidateQueries({ queryKey: ["media-assets"] });
    },
    onError: (error) =>
      setNotice({
        tone: "error",
        text: formatResource(t("Pages_Command_Gallery_UploadFailed"), error.message),
      }),
  });
  const saveTags = useMutation({
    mutationFn: ({ assetId, tags }: { assetId: string; tags: string[] }) =>
      replaceMediaAssetTags(assetId, tags),
    onMutate: () => setNotice(undefined),
    onSuccess: async (asset) => {
      queryClient.setQueryData<InfiniteData<MediaAsset[], number>>(queryKey, (current) =>
        upsertAsset(current, asset),
      );
      setSelectedAssetId(asset.primaryKey);
      setTagEditor({ assetId: asset.primaryKey, value: asset.tags.join(", ") });
      setNotice({ tone: "success", text: t("Pages_Command_Gallery_TagsSaved") });
      await queryClient.invalidateQueries({ queryKey: ["media-assets"] });
    },
    onError: (error) =>
      setNotice({
        tone: "error",
        text: formatResource(t("Pages_Command_Gallery_TagsSaveFailed"), error.message),
      }),
  });

  const applySearch = (event: FormEvent) => {
    event.preventDefault();
    setSearch(searchDraft.trim());
    setNotice(undefined);
  };
  const applyTagFilters = (event: FormEvent) => {
    event.preventDefault();
    setIncludeTags(parseMediaAssetTags(includeTagsDraft));
    setExcludeTags(parseMediaAssetTags(excludeTagsDraft));
    setNotice(undefined);
  };
  const chooseFile = (file: File | undefined) => {
    if (!file) return;
    if (file.size > maximumImageBytes) {
      setFileError(t("Pages_Command_Gallery_UploadTooLarge"));
      return;
    }
    if (!acceptedTypes.has(file.type.toLocaleLowerCase())) {
      setFileError(t("Pages_Command_Gallery_UploadUnsupportedType"));
      return;
    }
    setFileError(undefined);
    setNotice(undefined);
    upload.mutate(file);
  };
  const toggleSource = (domain: SourceDomain) => {
    setEnabledSources((current) => {
      const next = new Set(current);
      if (next.has(domain)) next.delete(domain);
      else next.add(domain);
      return next;
    });
    setNotice(undefined);
  };
  const sourceLabel = (domain: SourceDomain) => {
    switch (domain) {
      case "terrain":
        return t("Shared_CommandCenter_Nav_Terrains");
      case "event":
        return t("Shared_CommandCenter_Nav_Events");
      case "field-report":
        return t("Pages_Command_Gallery_FilterFieldReports");
      case "public-page":
        return t("Pages_Command_Gallery_FilterPublicPages");
      case "site-image":
        return t("Shared_CommandCenter_Nav_PublicImages");
      case "unused":
        return t("Pages_Command_Gallery_FilterUnused");
    }
  };

  if (!canManage)
    return (
      <GalleryStateFrame>
        <ForbiddenState title={t("Pages_Command_Gallery_NoAccessTitle")}>
          {t("Pages_Command_Gallery_NoAccessMessage")}
        </ForbiddenState>
      </GalleryStateFrame>
    );

  return (
    <PageStack>
      <PageHeader
        title={t("Shared_CommandCenter_Nav_Gallery")}
        description={t("Pages_Command_Gallery_PageDescription")}
        actions={
          <ActionButton
            variant="ghost"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            {t("Common_Refresh")}
          </ActionButton>
        }
      />

      <form
        className="media-admin-toolbar"
        aria-label={t("Pages_Command_Gallery_FilterAria")}
        onSubmit={applySearch}
      >
        <SearchField
          label={t("Pages_Command_Gallery_SearchPlaceholder")}
          placeholder={t("Pages_Command_Gallery_SearchPlaceholder")}
          clearLabel={t("Components_GalleryImagePicker_Clear")}
          value={searchDraft}
          onChange={setSearchDraft}
        />
        <ActionButton variant="ghost" type="submit">
          {t("Components_GalleryImagePicker_SearchAction")}
        </ActionButton>
        <div
          role="group"
          aria-label={t("Pages_Command_Gallery_DomainFilterAria")}
          className="filter-chip-group"
        >
          {sourceDomains.map((domain) => {
            const pressed = enabledSources.has(domain);
            return (
              <ActionButton
                key={domain}
                variant="ghost"
                className="filter-chip"
                aria-pressed={pressed}
                onClick={() => toggleSource(domain)}
              >
                {pressed && (
                  <Checkmark16Regular className="filter-chip__check" aria-hidden="true" />
                )}
                <span className="filter-chip__label">{sourceLabel(domain)}</span>
              </ActionButton>
            );
          })}
        </div>
      </form>

      <form className="command-panel media-admin-fields" onSubmit={applyTagFilters}>
        <TextField
          label={t("Pages_Command_Gallery_IncludeTagsLabel")}
          value={includeTagsDraft}
          placeholder={t("Pages_Command_Gallery_TagPlaceholder")}
          onChange={(event) => setIncludeTagsDraft(event.target.value)}
        />
        <TextField
          label={t("Pages_Command_Gallery_ExcludeTagsLabel")}
          value={excludeTagsDraft}
          placeholder={t("Pages_Command_Gallery_NegativeTagPlaceholder")}
          onChange={(event) => setExcludeTagsDraft(event.target.value)}
        />
        <ActionButton variant="ghost" type="submit" disabled={query.isFetching}>
          {t("Pages_Command_Gallery_ApplyTagFilters")}
        </ActionButton>
      </form>

      <section
        className="command-panel media-admin-upload"
        aria-label={t("Pages_Command_Gallery_UploadAria")}
      >
        <div className="media-admin-upload__intro">
          <span className="kicker">{t("Pages_Command_Gallery_UploadKicker")}</span>
          <h2>{t("Pages_Command_Gallery_UploadTitle")}</h2>
          <p>{t("Pages_Command_Gallery_UploadDescription")}</p>
        </div>
        <div className="media-admin-upload__controls">
          <TextField
            label={t("Pages_Command_Gallery_UploadTagsLabel")}
            value={uploadTags}
            placeholder={t("Pages_Command_Gallery_TagPlaceholder")}
            onChange={(event) => setUploadTags(event.target.value)}
          />
          <div>
            <FileChooserButton
              inputLabel={t("Pages_Command_Gallery_UploadDropText")}
              accept={[...acceptedTypes].join(",")}
              disabled={upload.isPending}
              onFiles={(files) => chooseFile(files[0])}
            >
              {t("Pages_Command_Gallery_UploadDropText")}
            </FileChooserButton>
          </div>
          <p className="media-admin-hint">{t("Pages_Command_Gallery_UploadHintText")}</p>
        </div>
      </section>
      {fileError && <StatePanel kind="error">{fileError}</StatePanel>}

      {query.isError && (
        <ErrorState onRetry={() => void query.refetch()}>
          {formatResource(t("Pages_Command_Gallery_LoadError"), query.error.message)}
        </ErrorState>
      )}
      {query.isPending ? (
        <LoadingState label={t("Pages_Command_Gallery_Loading")} />
      ) : query.data ? (
        <div className="media-admin-workspace">
          <section className="command-panel media-admin-assets">
            <header className="command-panel__header media-admin-panel-header">
              <div>
                <span className="kicker">{t("Pages_Command_Gallery_ListKicker")}</span>
                <h2>{t("Pages_Command_Gallery_ListTitle")}</h2>
              </div>
              <StatusBadge>
                {formatResource(t("Pages_Command_Gallery_AssetCount"), visibleAssets.length)}
              </StatusBadge>
            </header>
            {visibleAssets.length === 0 ? (
              <EmptyState title={t("Pages_Command_Gallery_EmptyTitle")}>
                {t("Pages_Command_Gallery_EmptyBody")}
              </EmptyState>
            ) : (
              <div
                className="media-admin-grid"
                role="listbox"
                aria-label={t("Pages_Command_Gallery_ListTitle")}
              >
                {visibleAssets.map((asset) => (
                  <ActionButton
                    key={asset.primaryKey}
                    variant="ghost"
                    className="media-admin-tile"
                    role="option"
                    aria-selected={selectedAsset?.primaryKey === asset.primaryKey}
                    onClick={() => {
                      setSelectedAssetId(asset.primaryKey);
                      setTagEditor({ assetId: asset.primaryKey, value: asset.tags.join(", ") });
                      setNotice(undefined);
                    }}
                  >
                    <img
                      src={resolveImageUrl(asset.imagePath)}
                      alt={formatResource(
                        t("Pages_Command_Gallery_ImageAlt"),
                        asset.originalFileName,
                      )}
                      loading="lazy"
                    />
                    <span className="media-admin-tile__text">
                      <strong>{asset.originalFileName}</strong>
                      <small>
                        {asset.contentType} · {formatBytes(asset.size)}
                      </small>
                      <StatusBadge tone={asset.usages.length > 0 ? "success" : "neutral"}>
                        {asset.usages.length > 0
                          ? formatResource(
                              t("Pages_Command_Gallery_UsedCount"),
                              asset.usages.length,
                            )
                          : t("Pages_Command_Gallery_Unused")}
                      </StatusBadge>
                    </span>
                  </ActionButton>
                ))}
              </div>
            )}
            {query.hasNextPage && (
              <div>
                <ActionButton
                  variant="ghost"
                  disabled={query.isFetchingNextPage}
                  onClick={() => void query.fetchNextPage()}
                >
                  {query.isFetchingNextPage
                    ? t("Components_GalleryImagePicker_LoadingMore")
                    : t("Components_GalleryImagePicker_LoadMore")}
                </ActionButton>
              </div>
            )}
          </section>

          <aside className="command-panel media-admin-detail">
            {!selectedAsset ? (
              <EmptyState title={t("Pages_Command_Gallery_NoSelectionTitle")}>
                {t("Pages_Command_Gallery_NoSelectionBody")}
              </EmptyState>
            ) : (
              <>
                <header className="command-panel__header">
                  <div>
                    <span className="kicker">{t("Pages_Command_Gallery_DetailKicker")}</span>
                    <h2>{selectedAsset.originalFileName}</h2>
                  </div>
                </header>
                <img
                  className="media-admin-detail__image"
                  src={resolveImageUrl(selectedAsset.imagePath)}
                  alt={formatResource(
                    t("Pages_Command_Gallery_ImageAlt"),
                    selectedAsset.originalFileName,
                  )}
                />
                <TextField
                  readOnly
                  label={t("Pages_Command_Gallery_ReferenceLabel")}
                  value={selectedAsset.imagePath}
                  trailing={
                    <CopyButton
                      value={selectedAsset.imagePath}
                      label={t("Pages_Command_Gallery_CopyReference")}
                      copiedMessage={t("Pages_Command_Gallery_CopiedReference")}
                    />
                  }
                />
                <dl className="media-admin-facts">
                  <dt>{t("Pages_Command_Gallery_ContentTypeLabel")}</dt>
                  <dd>{selectedAsset.contentType}</dd>
                  <dt>{t("Pages_Command_Gallery_SizeLabel")}</dt>
                  <dd>{formatBytes(selectedAsset.size)}</dd>
                  <dt>{t("Pages_Command_Gallery_CreatedLabel")}</dt>
                  <dd>{formatDateTime(selectedAsset.timeStampCreated)}</dd>
                  <dt>{t("Pages_Command_Gallery_UploadedByLabel")}</dt>
                  <dd>{selectedAsset.createdByName ?? t("Pages_Command_Gallery_UnknownUser")}</dd>
                </dl>
                <form
                  className="media-admin-section"
                  onSubmit={(event) => {
                    event.preventDefault();
                    saveTags.mutate({
                      assetId: selectedAsset.primaryKey,
                      tags: parseMediaAssetTags(tagEditorValue),
                    });
                  }}
                >
                  <h3>{t("Pages_Command_Gallery_TagsTitle")}</h3>
                  <TextField
                    label={t("Pages_Command_Gallery_TagEditorLabel")}
                    value={tagEditorValue}
                    onChange={(event) =>
                      setTagEditor({
                        assetId: selectedAsset.primaryKey,
                        value: event.target.value,
                      })
                    }
                  />
                  <div>
                    <ActionButton
                      variant="primary"
                      type="submit"
                      disabled={!tagDirty || saveTags.isPending}
                    >
                      {saveTags.isPending
                        ? t("Pages_Command_Gallery_SavingTags")
                        : t("Pages_Command_Gallery_SaveTags")}
                    </ActionButton>
                  </div>
                  <ul className="media-admin-tags">
                    {selectedAsset.tags.map((tag) => (
                      <li key={tag}>
                        <StatusBadge>{tag}</StatusBadge>
                      </li>
                    ))}
                  </ul>
                </form>
                <section className="media-admin-section">
                  <h3>{t("Pages_Command_Gallery_UsageTitle")}</h3>
                  {selectedAsset.usages.length === 0 ? (
                    <p>{t("Pages_Command_Gallery_NoUsage")}</p>
                  ) : (
                    <ul className="media-admin-usages">
                      {selectedAsset.usages.map((usage) => (
                        <li key={`${usage.domain}:${usage.domainKey}`}>
                          <strong>{usage.displayName}</strong>
                          <span>{usage.domain}</span>
                          {usage.path && (
                            <Link to={usage.path}>{t("Pages_Command_Gallery_OpenUsage")}</Link>
                          )}
                        </li>
                      ))}
                    </ul>
                  )}
                </section>
              </>
            )}
          </aside>
        </div>
      ) : null}

      <ConfirmDialog
        open={leaveGuard.blocked}
        title={t("Server_Alerts_Leave_Title")}
        description={t("React_UnsavedChanges")}
        cancelLabel={t("Server_Change_KeepEditing")}
        confirmLabel={t("Pages_Command_Content_PageEditorLeave_Dismiss")}
        initialFocus="dismiss"
        onCancel={leaveGuard.stay}
        onConfirm={leaveGuard.proceed}
      />
    </PageStack>
  );
}
