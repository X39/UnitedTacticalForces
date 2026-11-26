import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { resolveImageUrl } from "@/api/client";
import { formatResource } from "@/app/i18n";
import { ActionButton } from "@/components/ActionControl";
import { FileChooserButton } from "@/components/FileChooserButton";
import { ConfirmDialog } from "@/components/primitives";
import { SearchField } from "@/components/SearchField";
import { EmptyState, ErrorState, LoadingState } from "@/components/StateViews";
import {
  loadMediaAsset,
  loadMediaAssets,
  uploadMediaAsset,
  type MediaAsset,
} from "@/features/admin/siteImages";
import "@/features/admin/mediaAdmin.css";

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

/**
 * A modal picker that lets an editor choose, search for or upload a gallery image.
 *
 * The picker is a {@link ConfirmDialog} shell variant: the dismiss action names its effect, Escape and the dismiss
 * button call `onCancel`, and the apply action stays unavailable until an image is selected. Pressing Enter in the
 * search field runs the search instead of confirming the dialog.
 *
 * @param props.open Whether the dialog is shown.
 * @param props.selectedAssetId The id of the image that is currently assigned, preselected when it can be found.
 * @param props.uploadTags The tags attached to an image uploaded from inside the picker.
 * @param props.onSelect Called with the chosen image when the editor applies the selection.
 * @param props.onCancel Called when the editor dismisses the dialog.
 */
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
  const dialogError = validationError ?? (upload.isError ? upload.error.message : undefined);

  const closePicker = () => {
    setDraftAsset(undefined);
    setValidationError(undefined);
    onCancel();
  };
  const applySelection = () => {
    if (!selected) return;
    onSelect(selected);
    setDraftAsset(undefined);
    setValidationError(undefined);
  };
  const applySearch = () => setSearch(searchDraft.trim());
  const searchOnEnter = (event: KeyboardEvent<HTMLElement>) => {
    // The dialog body lives inside the confirm form, so Enter would confirm the selection without this.
    if (event.key !== "Enter" || !(event.target instanceof HTMLInputElement)) return;
    event.preventDefault();
    applySearch();
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
    <ConfirmDialog
      open={open}
      className="dialog--gallery-picker"
      kicker={t("Components_GalleryImagePicker_ResultsKicker")}
      title={t("Components_GalleryImagePicker_DialogTitle")}
      description={t("Components_GalleryImagePicker_NoSelectionMeta")}
      confirmLabel={t("Components_GalleryImagePicker_DialogApply")}
      confirmVariant="primary"
      cancelLabel={t("Components_GalleryImagePicker_Dismiss")}
      confirmDisabled={!selected}
      error={dialogError}
      onConfirm={applySelection}
      onCancel={closePicker}
    >
      <div className="gallery-picker__tools" onKeyDown={searchOnEnter}>
        <SearchField
          label={t("Components_GalleryImagePicker_SearchLabel")}
          placeholder={t("Components_GalleryImagePicker_SearchPlaceholder")}
          clearLabel={t("Components_GalleryImagePicker_Clear")}
          value={searchDraft}
          onChange={setSearchDraft}
        />
        <ActionButton variant="ghost" onClick={applySearch}>
          {t("Components_GalleryImagePicker_SearchAction")}
        </ActionButton>
        <FileChooserButton
          inputLabel={t("Components_GalleryImagePicker_UploadText")}
          disabled={upload.isPending}
          onFiles={(files) => chooseFile(files[0])}
        >
          {upload.isPending
            ? t("Components_GalleryImagePicker_LoadingMore")
            : t("Components_GalleryImagePicker_UploadText")}
        </FileChooserButton>
      </div>

      <div className="gallery-picker__workspace">
        <section className="gallery-picker__results">
          <div className="gallery-picker__results-header">
            <h3>{t("Components_GalleryImagePicker_ResultsTitle")}</h3>
            <span>
              {formatResource(t("Components_GalleryImagePicker_VisibleCount"), assets.length)}
            </span>
          </div>
          {query.isError && <ErrorState onRetry={() => void query.refetch()} />}
          {query.isPending ? (
            <LoadingState label={t("Components_GalleryImagePicker_Loading")} />
          ) : assets.length === 0 ? (
            !query.isError && (
              <EmptyState title={t("Components_GalleryImagePicker_EmptyTitle")}>
                {t("Components_GalleryImagePicker_EmptyBody")}
              </EmptyState>
            )
          ) : (
            <div className="gallery-picker__grid">
              {assets.map((asset) => (
                <ActionButton
                  key={asset.primaryKey}
                  variant="ghost"
                  className="media-admin-tile"
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
                  <span className="media-admin-tile__text">
                    <strong>{asset.originalFileName}</strong>
                    <small>
                      {asset.contentType} · {formatBytes(asset.size)}
                    </small>
                  </span>
                </ActionButton>
              ))}
            </div>
          )}
          {query.hasNextPage && (
            <ActionButton
              variant="ghost"
              disabled={query.isFetchingNextPage}
              onClick={() => void query.fetchNextPage()}
            >
              {query.isFetchingNextPage
                ? t("Components_GalleryImagePicker_LoadingMore")
                : t("Components_GalleryImagePicker_LoadMore")}
            </ActionButton>
          )}
        </section>
        <aside className="gallery-picker__selection">
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
              <dl className="media-admin-facts">
                <dt>{t("Components_GalleryImagePicker_TypeLabel")}</dt>
                <dd>{selected.contentType}</dd>
                <dt>{t("Components_GalleryImagePicker_SizeLabel")}</dt>
                <dd>{formatBytes(selected.size)}</dd>
              </dl>
            </>
          ) : (
            <p className="media-admin-hint">
              {t("Components_GalleryImagePicker_NoSelectionDescription")}
            </p>
          )}
        </aside>
      </div>
    </ConfirmDialog>
  );
}
