import { useState } from "react";
import { useTranslation } from "react-i18next";
import { resolveImageUrl } from "@/api/client";
import { formatResource } from "@/app/i18n";
import { GalleryAssetPicker } from "@/features/admin/GalleryAssetPicker";
import type { MediaAsset } from "@/features/admin/siteImages";

function formatBytes(value: number): string {
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / 1024 / 1024).toFixed(1)} MB`;
}

export function TerrainImageField({
  selected,
  currentAssetId = "",
  currentImagePath = "",
  disabled = false,
  onChange,
}: {
  selected: MediaAsset | undefined;
  currentAssetId?: string;
  currentImagePath?: string;
  disabled?: boolean;
  onChange(asset: MediaAsset | undefined): void;
}) {
  const { t } = useTranslation();
  const [pickerOpen, setPickerOpen] = useState(false);
  const previewPath = selected?.imagePath || currentImagePath;
  const hasImage = Boolean(previewPath);

  return (
    <section className="terrain-image-field" aria-label={t("Components_GalleryImagePicker_Aria")}>
      <div className={`terrain-image-field__preview${hasImage ? "" : " is-empty"}`}>
        {hasImage ? (
          <img
            src={resolveImageUrl(previewPath)}
            alt={
              selected
                ? formatResource(
                    t("Components_GalleryImagePicker_SelectedImageAltFormat"),
                    selected.originalFileName,
                  )
                : t("Components_GalleryImagePicker_SelectedImageAlt")
            }
          />
        ) : (
          <span>{t("Pages_Data_Terrains_All_NoImage")}</span>
        )}
      </div>
      <div className="terrain-image-field__meta">
        <strong>
          {selected?.originalFileName ||
            (hasImage
              ? t("Components_GalleryImagePicker_SelectedFallback")
              : t("Components_GalleryImagePicker_NoSelection"))}
        </strong>
        <span>
          {selected
            ? `${selected.contentType} · ${formatBytes(selected.size)}`
            : hasImage
              ? t("Components_GalleryImagePicker_SelectedMeta")
              : t("Components_GalleryImagePicker_NoSelectionMeta")}
        </span>
      </div>
      <div className="terrain-image-field__actions">
        <button
          className="button button--ghost button--small"
          type="button"
          disabled={disabled}
          onClick={() => setPickerOpen(true)}
        >
          {hasImage
            ? t("Components_GalleryImagePicker_Change")
            : t("Components_GalleryImagePicker_Select")}
        </button>
        {selected && (
          <button
            className="button button--ghost button--small"
            type="button"
            disabled={disabled}
            onClick={() => onChange(undefined)}
          >
            {t("Components_GalleryImagePicker_Clear")}
          </button>
        )}
      </div>
      <GalleryAssetPicker
        open={pickerOpen}
        selectedAssetId={selected?.primaryKey || currentAssetId}
        uploadTags={["terrain"]}
        onCancel={() => setPickerOpen(false)}
        onSelect={(asset) => {
          onChange(asset);
          setPickerOpen(false);
        }}
      />
    </section>
  );
}
