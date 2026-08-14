import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { resolveImageUrl } from "@/api/client";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageSiteImages } from "@/auth/claims";
import { PageHeader, StatePanel, StatusBadge, useUnsavedChanges } from "@/components/primitives";
import { GalleryAssetPicker } from "@/features/admin/GalleryAssetPicker";
import {
  loadSiteImageSettings,
  normalizeSiteImageGroup,
  siteImageGroups,
  updateSiteImageSetting,
  type MediaAsset,
  type SiteImageSetting,
} from "@/features/admin/siteImages";

export function Component() {
  const { t } = useTranslation();
  const { group: routeGroup } = useParams();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const group = normalizeSiteImageGroup(routeGroup);
  const canManage = canManageSiteImages(user);
  const queryKey = ["command-center", "site-images", group] as const;
  const query = useQuery({
    queryKey,
    queryFn: ({ signal }) => loadSiteImageSettings(group, signal),
    enabled: canManage,
  });
  const [selectedAssets, setSelectedAssets] = useState<Record<string, MediaAsset>>({});
  const [pickerSetting, setPickerSetting] = useState<SiteImageSetting>();
  const [success, setSuccess] = useState<string>();
  const save = useMutation({
    mutationFn: ({ setting, asset }: { setting: SiteImageSetting; asset: MediaAsset }) =>
      updateSiteImageSetting(setting.key, asset.primaryKey),
    onSuccess: async (updated) => {
      queryClient.setQueryData<SiteImageSetting[]>(queryKey, (current) =>
        (current ?? []).map((setting) => (setting.key === updated.key ? updated : setting)),
      );
      setSelectedAssets((current) => {
        const next = { ...current };
        delete next[updated.key];
        return next;
      });
      setSuccess(updated.key);
      await queryClient.invalidateQueries({ queryKey: ["site-images"] });
    },
  });
  const dirty = Object.keys(selectedAssets).length > 0;
  useUnsavedChanges(dirty && !save.isPending);
  const groupLabel = t(siteImageGroups.find(([key]) => key === group)![1]);
  const resetPending = () => {
    setSelectedAssets({});
    setSuccess(undefined);
  };

  if (!canManage)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Command_SiteImages_AccessRequiredTitle")}>
        {t("Pages_Command_SiteImages_AccessRequiredDescription")}
      </StatePanel>
    );

  return (
    <section className="site-image-editor-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_PublicImages")}
        title={formatResource(t("Pages_Command_SiteImages_TitleFormat"), groupLabel)}
        description={t("Pages_Command_SiteImages_Description")}
        actions={
          <>
            <Link className="button button--ghost" to="/command/content/gallery">
              {t("Pages_Command_SiteImages_OpenGallery")}
            </Link>
            <button
              className="button button--ghost"
              type="button"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Common_Refresh")}
            </button>
          </>
        }
      />

      <nav
        className="toolbar site-image-group-toolbar"
        aria-label={t("Pages_Command_SiteImages_GroupNavigationAria")}
      >
        <div className="filter-chips">
          {siteImageGroups.map(([key, labelKey]) => (
            <Link
              key={key}
              className={key === group ? "active" : undefined}
              aria-current={key === group ? "page" : undefined}
              to={`/command/content/images/${key}`}
            >
              {t(labelKey)}
            </Link>
          ))}
        </div>
      </nav>

      {query.isPending && (
        <StatePanel kind="loading">{t("Pages_Command_SiteImages_Loading")}</StatePanel>
      )}
      {query.isError && (
        <StatePanel
          kind="error"
          action={
            <button className="button button--ghost" onClick={() => void query.refetch()}>
              {t("Common_Refresh")}
            </button>
          }
        >
          {query.error.message}
        </StatePanel>
      )}
      {save.isError && <StatePanel kind="error">{save.error.message}</StatePanel>}
      {success && (
        <StatePanel kind="success">{t("Pages_Command_SiteImages_SavedToast")}</StatePanel>
      )}

      {query.data && query.data.length === 0 && (
        <StatePanel kind="empty" title={t("Pages_Command_SiteImages_EmptyTitle")}>
          {t("Pages_Command_SiteImages_EmptyDescription")}
        </StatePanel>
      )}
      {query.data && query.data.length > 0 && (
        <section className="command-panel site-image-settings-list">
          <header className="command-panel__header site-image-settings-list__header">
            <span className="kicker">{t("Pages_Command_SiteImages_ListKicker")}</span>
            <h2>{groupLabel}</h2>
            <StatusBadge>
              {formatResource(
                t(
                  query.data.length === 1
                    ? "Pages_Command_SiteImages_CountSingular"
                    : "Pages_Command_SiteImages_CountPlural",
                ),
                query.data.length,
              )}
            </StatusBadge>
          </header>
          <div className="site-image-settings-grid">
            {query.data.map((setting) => {
              const selected = selectedAssets[setting.key];
              const imagePath = selected?.imagePath || setting.imagePath;
              const isSaving = save.isPending && save.variables?.setting.key === setting.key;
              return (
                <article key={setting.key} className="site-image-setting-card">
                  <div
                    className="site-image-setting-card__preview"
                    role="img"
                    aria-label={setting.title}
                    style={
                      imagePath
                        ? {
                            backgroundImage: `url("${resolveImageUrl(imagePath)?.replaceAll('"', "%22")}")`,
                          }
                        : undefined
                    }
                  />
                  <div className="site-image-setting-card__body">
                    <div>
                      <span className="kicker">{setting.key}</span>
                      <h3>{setting.title}</h3>
                      {setting.description && <p>{setting.description}</p>}
                    </div>
                    <section
                      className="site-image-selection"
                      aria-label={t("Components_GalleryImagePicker_Aria")}
                    >
                      <strong>{selected?.originalFileName || setting.title}</strong>
                      <span>
                        {selected
                          ? t("Pages_Command_SiteImages_PendingSelection")
                          : t("Components_GalleryImagePicker_SelectedMeta")}
                      </span>
                      <button
                        className="button button--ghost button--small"
                        type="button"
                        disabled={isSaving}
                        onClick={() => setPickerSetting(setting)}
                      >
                        {t(
                          selected
                            ? "Components_GalleryImagePicker_Change"
                            : setting.imageAssetFk
                              ? "Components_GalleryImagePicker_Change"
                              : "Components_GalleryImagePicker_Select",
                        )}
                      </button>
                    </section>
                    <div className="site-image-setting-card__actions">
                      <button
                        className="button button--primary"
                        type="button"
                        disabled={!selected || isSaving}
                        onClick={() => selected && save.mutate({ setting, asset: selected })}
                      >
                        {isSaving
                          ? t("Pages_Command_SiteImages_Saving")
                          : t("Pages_Command_SiteImages_Save")}
                      </button>
                      <button
                        className="button button--ghost"
                        type="button"
                        disabled={!selected || isSaving}
                        onClick={() =>
                          setSelectedAssets((current) => {
                            const next = { ...current };
                            delete next[setting.key];
                            return next;
                          })
                        }
                      >
                        {t("Pages_Command_SiteImages_Reset")}
                      </button>
                    </div>
                  </div>
                </article>
              );
            })}
          </div>
          {dirty && (
            <footer className="site-image-settings-list__footer">
              <span>{t("Pages_Command_SiteImages_Unsaved")}</span>
              <button className="button button--ghost" type="button" onClick={resetPending}>
                {t("Pages_Command_SiteImages_ResetAll")}
              </button>
            </footer>
          )}
        </section>
      )}

      <GalleryAssetPicker
        open={Boolean(pickerSetting)}
        selectedAssetId={
          pickerSetting
            ? selectedAssets[pickerSetting.key]?.primaryKey || pickerSetting.imageAssetFk
            : ""
        }
        onCancel={() => setPickerSetting(undefined)}
        onSelect={(asset) => {
          if (pickerSetting)
            setSelectedAssets((current) => ({ ...current, [pickerSetting.key]: asset }));
          setPickerSetting(undefined);
          setSuccess(undefined);
        }}
      />
    </section>
  );
}
