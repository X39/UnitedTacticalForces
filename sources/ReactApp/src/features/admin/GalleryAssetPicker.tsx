import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { resolveImageUrl } from "@/api/client";
import { formatResource } from "@/app/i18n";
import { StatePanel } from "@/components/primitives";
import {
  loadMediaAsset,
  loadMediaAssets,
  uploadMediaAsset,
  type MediaAsset,
} from "@/features/admin/siteImages";

const maximumImageBytes = 10 * 1024 * 1024;
const acceptedTypes = new Set([
  "image/avif",
  "image/bmp",
  "image/gif",
  "image/jpeg",
  "image/png",
  "image/webp",
]);

function formatBytes(value: number): string {
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / 1024 / 1024).toFixed(1)} MB`;
}

export function GalleryAssetPicker({
  open,
  selectedAssetId,
  uploadTags = ["site-image"],
  onSelect,
  onCancel,
}: {
  open: boolean;
  selectedAssetId: string;
  uploadTags?: readonly string[];
  onSelect(asset: MediaAsset): void;
  onCancel(): void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [draftAsset, setDraftAsset] = useState<MediaAsset>();
  const [validationError, setValidationError] = useState<string>();
  const query = useInfiniteQuery({
    queryKey: ["media-assets", "picker", search],
    queryFn: ({ signal, pageParam }) => loadMediaAssets(search, pageParam, signal),
    initialPageParam: 0,
    getNextPageParam: (page, pages) => (page.length === 48 ? pages.length * 48 : undefined),
    enabled: open,
  });
  const upload = useMutation({
    mutationFn: (file: File) => uploadMediaAsset(file, uploadTags),
    onSuccess: async (asset) => {
      setDraftAsset(asset);
      setValidationError(undefined);
      await queryClient.invalidateQueries({ queryKey: ["media-assets"] });
    },
  });
  const assets = query.data?.pages.flat() ?? [];
  const loadedSelection = assets.find((asset) => asset.primaryKey === selectedAssetId);
  const selectedQuery = useQuery({
    queryKey: ["media-assets", "detail", selectedAssetId],
    queryFn: ({ signal }) => loadMediaAsset(selectedAssetId, signal),
    enabled: open && Boolean(selectedAssetId) && !loadedSelection && !draftAsset,
  });
  const selected = draftAsset ?? loadedSelection ?? selectedQuery.data;

  useEffect(() => {
    if (open && !dialogRef.current?.open) dialogRef.current?.showModal();
    if (!open && dialogRef.current?.open) dialogRef.current.close();
  }, [open]);

  const closePicker = () => {
    setDraftAsset(undefined);
    setValidationError(undefined);
    onCancel();
  };

  const applySearch = (event: FormEvent) => {
    event.preventDefault();
    setSearch(searchDraft.trim());
  };
  const chooseFile = (file: File | undefined) => {
    if (!file) return;
    if (file.size > maximumImageBytes) {
      setValidationError(t("Components_GalleryImagePicker_UploadTooLarge"));
      return;
    }
    if (!acceptedTypes.has(file.type.toLocaleLowerCase())) {
      setValidationError(t("Components_GalleryImagePicker_UploadUnsupported"));
      return;
    }
    setValidationError(undefined);
    upload.mutate(file);
  };

  return (
    <dialog
      ref={dialogRef}
      className="gallery-picker-dialog"
      onCancel={(event) => {
        event.preventDefault();
        closePicker();
      }}
    >
      <section className="gallery-picker-dialog__shell">
        <header>
          <span className="kicker">{t("Components_GalleryImagePicker_ResultsKicker")}</span>
          <h2>{t("Components_GalleryImagePicker_DialogTitle")}</h2>
          <button
            className="icon-button"
            type="button"
            aria-label={t("Components_GalleryImagePicker_DialogCancel")}
            onClick={closePicker}
          >
            ×
          </button>
        </header>
        <form className="gallery-picker-dialog__tools" onSubmit={applySearch}>
          <label className="search-field">
            <span className="sr-only">{t("Components_GalleryImagePicker_SearchLabel")}</span>
            <input
              value={searchDraft}
              placeholder={t("Components_GalleryImagePicker_SearchPlaceholder")}
              onChange={(event) => setSearchDraft(event.target.value)}
            />
          </label>
          <button className="button button--ghost" type="submit">
            {t("Components_GalleryImagePicker_SearchAction")}
          </button>
          <label className="button button--ghost gallery-picker-dialog__upload">
            {upload.isPending
              ? t("Components_GalleryImagePicker_LoadingMore")
              : t("Components_GalleryImagePicker_UploadText")}
            <input
              type="file"
              accept={[...acceptedTypes].join(",")}
              disabled={upload.isPending}
              onChange={(event) => chooseFile(event.target.files?.[0])}
            />
          </label>
        </form>

        {(validationError || upload.isError) && (
          <StatePanel kind="error">{validationError || upload.error?.message}</StatePanel>
        )}
        {query.isError && <StatePanel kind="error">{query.error.message}</StatePanel>}

        <div className="gallery-picker-dialog__workspace">
          <section>
            <div className="gallery-picker-dialog__results-header">
              <h3>{t("Components_GalleryImagePicker_ResultsTitle")}</h3>
              <span>
                {formatResource(t("Components_GalleryImagePicker_VisibleCount"), assets.length)}
              </span>
            </div>
            {query.isPending ? (
              <StatePanel kind="loading">{t("Components_GalleryImagePicker_Loading")}</StatePanel>
            ) : assets.length === 0 ? (
              <StatePanel>{t("Components_GalleryImagePicker_NoResults")}</StatePanel>
            ) : (
              <div className="gallery-picker-dialog__grid">
                {assets.map((asset) => (
                  <button
                    key={asset.primaryKey}
                    className={
                      selected?.primaryKey === asset.primaryKey ? "is-selected" : undefined
                    }
                    type="button"
                    aria-pressed={selected?.primaryKey === asset.primaryKey}
                    onClick={() => setDraftAsset(asset)}
                  >
                    <img
                      src={resolveImageUrl(asset.imagePath)}
                      alt={formatResource(
                        t("Components_GalleryImagePicker_AssetAlt"),
                        asset.originalFileName,
                      )}
                      loading="lazy"
                    />
                    <strong>{asset.originalFileName}</strong>
                    <span>
                      {asset.contentType} · {formatBytes(asset.size)}
                    </span>
                  </button>
                ))}
              </div>
            )}
            {query.hasNextPage && (
              <button
                className="button button--ghost gallery-picker-dialog__load-more"
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
          <aside>
            <span className="kicker">{t("Components_GalleryImagePicker_SelectionKicker")}</span>
            <h3>
              {selected?.originalFileName || t("Components_GalleryImagePicker_NoSelectionTitle")}
            </h3>
            {selected ? (
              <>
                <img
                  src={resolveImageUrl(selected.imagePath)}
                  alt={formatResource(
                    t("Components_GalleryImagePicker_SelectedImageAltFormat"),
                    selected.originalFileName,
                  )}
                />
                <dl>
                  <dt>{t("Components_GalleryImagePicker_TypeLabel")}</dt>
                  <dd>{selected.contentType}</dd>
                  <dt>{t("Components_GalleryImagePicker_SizeLabel")}</dt>
                  <dd>{formatBytes(selected.size)}</dd>
                </dl>
              </>
            ) : (
              <p>{t("Components_GalleryImagePicker_NoSelectionDescription")}</p>
            )}
          </aside>
        </div>
        <footer>
          <button className="button button--ghost" type="button" onClick={closePicker}>
            {t("Components_GalleryImagePicker_DialogCancel")}
          </button>
          <button
            className="button button--primary"
            type="button"
            disabled={!selected}
            onClick={() => {
              if (!selected) return;
              onSelect(selected);
              setDraftAsset(undefined);
              setValidationError(undefined);
            }}
          >
            {t("Components_GalleryImagePicker_DialogApply")}
          </button>
        </footer>
      </section>
    </dialog>
  );
}
