import {
  useInfiniteQuery,
  useMutation,
  useQueryClient,
  type InfiniteData,
} from "@tanstack/react-query";
import { useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { resolveImageUrl } from "@/api/client";
import { formatDateTime } from "@/app/format";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageGallery } from "@/auth/claims";
import { PageHeader, StatePanel, StatusBadge, useUnsavedChanges } from "@/components/primitives";
import {
  loadMediaAssetPage,
  mediaAssetMatchesUsageDomains,
  mediaAssetTagsEqual,
  parseMediaAssetTags,
  replaceMediaAssetTags,
  uploadMediaAsset,
  type MediaAsset,
} from "@/features/admin/siteImages";

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
  useUnsavedChanges(tagDirty);

  const upload = useMutation({
    mutationFn: (file: File) => uploadMediaAsset(file, parseMediaAssetTags(uploadTags)),
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
  const copyReference = async () => {
    if (!selectedAsset) return;
    try {
      await navigator.clipboard.writeText(selectedAsset.imagePath);
      setNotice({ tone: "success", text: t("Pages_Command_Gallery_CopiedReference") });
    } catch {
      setNotice({ tone: "error", text: t("Pages_Command_Gallery_CopyReferenceFailed") });
    }
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
      <StatePanel kind="forbidden" title={t("Pages_Command_Gallery_NoAccessTitle")}>
        {t("Pages_Command_Gallery_NoAccessMessage")}
      </StatePanel>
    );

  return (
    <section className="media-gallery-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_Gallery")}
        title={t("Pages_Command_Gallery_PageTitle")}
        description={t("Pages_Command_Gallery_PageDescription")}
        actions={
          <button
            className="button button--ghost"
            type="button"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            {t("Common_Refresh")}
          </button>
        }
      />

      {notice && <StatePanel kind={notice.tone}>{notice.text}</StatePanel>}
      <form
        className="toolbar media-gallery-toolbar"
        aria-label={t("Pages_Command_Gallery_FilterAria")}
        onSubmit={applySearch}
      >
        <label className="search-field">
          <span className="sr-only">{t("Pages_Command_Gallery_SearchPlaceholder")}</span>
          <input
            value={searchDraft}
            placeholder={t("Pages_Command_Gallery_SearchPlaceholder")}
            onChange={(event) => setSearchDraft(event.target.value)}
          />
        </label>
        <button className="button button--ghost" type="submit">
          {t("Components_GalleryImagePicker_SearchAction")}
        </button>
        <div className="filter-chips" aria-label={t("Pages_Command_Gallery_DomainFilterAria")}>
          {sourceDomains.map((domain) => (
            <button
              key={domain}
              className={enabledSources.has(domain) ? "active" : undefined}
              type="button"
              aria-pressed={enabledSources.has(domain)}
              onClick={() => toggleSource(domain)}
            >
              {sourceLabel(domain)}
            </button>
          ))}
        </div>
      </form>

      <form className="command-panel media-gallery-tag-filters" onSubmit={applyTagFilters}>
        <label>
          <span>{t("Pages_Command_Gallery_IncludeTagsLabel")}</span>
          <input
            value={includeTagsDraft}
            placeholder={t("Pages_Command_Gallery_TagPlaceholder")}
            onChange={(event) => setIncludeTagsDraft(event.target.value)}
          />
        </label>
        <label>
          <span>{t("Pages_Command_Gallery_ExcludeTagsLabel")}</span>
          <input
            value={excludeTagsDraft}
            placeholder={t("Pages_Command_Gallery_NegativeTagPlaceholder")}
            onChange={(event) => setExcludeTagsDraft(event.target.value)}
          />
        </label>
        <button className="button button--ghost" type="submit" disabled={query.isFetching}>
          {t("Pages_Command_Gallery_ApplyTagFilters")}
        </button>
      </form>

      <section
        className="command-panel media-gallery-upload"
        aria-label={t("Pages_Command_Gallery_UploadAria")}
      >
        <div>
          <span className="kicker">{t("Pages_Command_Gallery_UploadKicker")}</span>
          <h2>{t("Pages_Command_Gallery_UploadTitle")}</h2>
          <p>{t("Pages_Command_Gallery_UploadDescription")}</p>
        </div>
        <div className="media-gallery-upload__controls">
          <label>
            <span>{t("Pages_Command_Gallery_UploadTagsLabel")}</span>
            <input
              value={uploadTags}
              placeholder={t("Pages_Command_Gallery_TagPlaceholder")}
              onChange={(event) => setUploadTags(event.target.value)}
            />
          </label>
          <label className="media-gallery-upload__drop">
            <strong>{t("Pages_Command_Gallery_UploadDropText")}</strong>
            <span>{t("Pages_Command_Gallery_UploadHintText")}</span>
            <input
              type="file"
              accept={[...acceptedTypes].join(",")}
              disabled={upload.isPending}
              onChange={(event) => chooseFile(event.target.files?.[0])}
            />
          </label>
        </div>
      </section>
      {fileError && <StatePanel kind="error">{fileError}</StatePanel>}

      {query.isPending ? (
        <StatePanel kind="loading">{t("Pages_Command_Gallery_Loading")}</StatePanel>
      ) : query.isError ? (
        <StatePanel
          kind="error"
          action={
            <button className="button button--ghost" onClick={() => void query.refetch()}>
              {t("Common_Refresh")}
            </button>
          }
        >
          {formatResource(t("Pages_Command_Gallery_LoadError"), query.error.message)}
        </StatePanel>
      ) : (
        <div className="media-gallery-workspace">
          <section className="command-panel media-gallery-assets">
            <header className="command-panel__header">
              <div>
                <span className="kicker">{t("Pages_Command_Gallery_ListKicker")}</span>
                <h2>{t("Pages_Command_Gallery_ListTitle")}</h2>
              </div>
              <StatusBadge>
                {formatResource(t("Pages_Command_Gallery_AssetCount"), visibleAssets.length)}
              </StatusBadge>
            </header>
            {visibleAssets.length === 0 ? (
              <StatePanel kind="empty">{t("Pages_Command_Gallery_Empty")}</StatePanel>
            ) : (
              <div
                className="media-gallery-assets__grid"
                role="listbox"
                aria-label={t("Pages_Command_Gallery_ListTitle")}
              >
                {visibleAssets.map((asset) => (
                  <button
                    key={asset.primaryKey}
                    className={
                      selectedAsset?.primaryKey === asset.primaryKey ? "is-selected" : undefined
                    }
                    type="button"
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
                    <span>
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
                  </button>
                ))}
              </div>
            )}
            {query.hasNextPage && (
              <button
                className="button button--ghost media-gallery-assets__load-more"
                type="button"
                disabled={query.isFetchingNextPage}
                onClick={() => void query.fetchNextPage()}
              >
                {query.isFetchingNextPage
                  ? t("Components_GalleryImagePicker_LoadingMore")
                  : t("Components_GalleryImagePicker_LoadMore")}
              </button>
            )}
          </section>

          <aside className="command-panel media-gallery-detail">
            {!selectedAsset ? (
              <StatePanel>{t("Pages_Command_Gallery_NoSelection")}</StatePanel>
            ) : (
              <>
                <header className="command-panel__header">
                  <div>
                    <span className="kicker">{t("Pages_Command_Gallery_DetailKicker")}</span>
                    <h2>{selectedAsset.originalFileName}</h2>
                  </div>
                </header>
                <img
                  className="media-gallery-detail__image"
                  src={resolveImageUrl(selectedAsset.imagePath)}
                  alt={formatResource(
                    t("Pages_Command_Gallery_ImageAlt"),
                    selectedAsset.originalFileName,
                  )}
                />
                <div className="media-gallery-detail__scroll">
                  <div className="media-gallery-reference">
                    <label>
                      <span>{t("Pages_Command_Gallery_ReferenceLabel")}</span>
                      <input readOnly value={selectedAsset.imagePath} />
                    </label>
                    <button className="button button--ghost" type="button" onClick={copyReference}>
                      {t("Pages_Command_Gallery_CopyReference")}
                    </button>
                  </div>
                  <dl>
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
                    className="media-gallery-detail__section"
                    onSubmit={(event) => {
                      event.preventDefault();
                      saveTags.mutate({
                        assetId: selectedAsset.primaryKey,
                        tags: parseMediaAssetTags(tagEditorValue),
                      });
                    }}
                  >
                    <h3>{t("Pages_Command_Gallery_TagsTitle")}</h3>
                    <label>
                      <span>{t("Pages_Command_Gallery_TagEditorLabel")}</span>
                      <input
                        value={tagEditorValue}
                        onChange={(event) =>
                          setTagEditor({
                            assetId: selectedAsset.primaryKey,
                            value: event.target.value,
                          })
                        }
                      />
                    </label>
                    <button
                      className="button button--primary"
                      type="submit"
                      disabled={!tagDirty || saveTags.isPending}
                    >
                      {saveTags.isPending
                        ? t("Pages_Command_Gallery_SavingTags")
                        : t("Pages_Command_Gallery_SaveTags")}
                    </button>
                    <div className="media-gallery-tags">
                      {selectedAsset.tags.map((tag) => (
                        <span key={tag}>{tag}</span>
                      ))}
                    </div>
                  </form>
                  <section className="media-gallery-detail__section">
                    <h3>{t("Pages_Command_Gallery_UsageTitle")}</h3>
                    {selectedAsset.usages.length === 0 ? (
                      <p>{t("Pages_Command_Gallery_NoUsage")}</p>
                    ) : (
                      <ul className="media-gallery-usages">
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
                </div>
              </>
            )}
          </aside>
        </div>
      )}
    </section>
  );
}
