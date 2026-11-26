import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router";
import { resolveImageUrl } from "@/api/client";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageSiteImages } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { useAppToastOnChange } from "@/components/AppToasts";
import { PageStack } from "@/components/PageStack";
import { ConfirmDialog, PageHeader, StatePanel, StatusBadge } from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { useLeaveGuard } from "@/components/useLeaveGuard";
import { GalleryAssetPicker } from "@/features/admin/GalleryAssetPicker";
import {
  loadSiteImageSettings,
  normalizeSiteImageGroup,
  siteImageGroups,
  updateSiteImageSetting,
  type MediaAsset,
  type SiteImageSetting,
} from "@/features/admin/siteImages";
import "@/features/admin/mediaAdmin.css";

/**
 * Renders the page header above a state view so every state of the page keeps exactly one `h1`.
 *
 * The title is the group independent navigation label; the dynamic title with the group name belongs to the
 * loaded page, which is the recorded exception to "the heading equals the navigation label".
 */
function SiteImagesStateFrame({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <PageStack>
      <PageHeader title={t("Shared_CommandCenter_Nav_PublicImages")} />
      {children}
    </PageStack>
  );
}

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
  const leaveGuard = useLeaveGuard({ dirty: dirty && !save.isPending });
  useAppToastOnChange(success ? t("Pages_Command_SiteImages_SavedToast") : undefined);
  const groupLabel = t(siteImageGroups.find(([key]) => key === group)![1]);
  const resetPending = () => {
    setSelectedAssets({});
    setSuccess(undefined);
  };

  if (!canManage)
    return (
      <SiteImagesStateFrame>
        <ForbiddenState title={t("Pages_Command_SiteImages_AccessRequiredTitle")}>
          {t("Pages_Command_SiteImages_AccessRequiredDescription")}
        </ForbiddenState>
      </SiteImagesStateFrame>
    );

  return (
    <PageStack>
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_PublicImages")}
        title={formatResource(t("Pages_Command_SiteImages_TitleFormat"), groupLabel)}
        description={t("Pages_Command_SiteImages_Description")}
        actions={
          <>
            <ActionLink variant="ghost" to="/command/content/gallery">
              {t("Pages_Command_SiteImages_OpenGallery")}
            </ActionLink>
            <ActionButton
              variant="ghost"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Common_Refresh")}
            </ActionButton>
          </>
        }
      />

      <nav
        className="site-images-groups"
        aria-label={t("Pages_Command_SiteImages_GroupNavigationAria")}
      >
        {siteImageGroups.map(([key, labelKey]) => (
          <ActionLink
            key={key}
            variant="ghost"
            className="site-images-group"
            aria-current={key === group ? "page" : undefined}
            to={`/command/content/images/${key}`}
          >
            {t(labelKey)}
          </ActionLink>
        ))}
      </nav>

      {query.isPending && <LoadingState label={t("Pages_Command_SiteImages_Loading")} />}
      {query.isError && (
        <ErrorState onRetry={() => void query.refetch()}>{query.error.message}</ErrorState>
      )}
      {save.isError && <StatePanel kind="error">{save.error.message}</StatePanel>}
      {query.data && query.data.length === 0 && (
        <EmptyState title={t("Pages_Command_SiteImages_EmptyTitle")}>
          {t("Pages_Command_SiteImages_EmptyDescription")}
        </EmptyState>
      )}
      {query.data && query.data.length > 0 && (
        <section className="command-panel">
          <header className="command-panel__header media-admin-panel-header">
            <div>
              <span className="kicker">{t("Pages_Command_SiteImages_ListKicker")}</span>
              <h2>{groupLabel}</h2>
            </div>
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
          <div className="site-images-grid">
            {query.data.map((setting) => {
              const selected = selectedAssets[setting.key];
              const imagePath = selected?.imagePath || setting.imagePath;
              const isSaving = save.isPending && save.variables?.setting.key === setting.key;
              return (
                <article key={setting.key} className="site-images-card">
                  <div
                    className="site-images-card__preview"
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
                  <div className="site-images-card__body">
                    <div>
                      <span className="kicker">{setting.key}</span>
                      <h3>{setting.title}</h3>
                      {setting.description && <p>{setting.description}</p>}
                    </div>
                    <section
                      className="site-images-selection"
                      aria-label={t("Components_GalleryImagePicker_Aria")}
                    >
                      <strong>{selected?.originalFileName || setting.title}</strong>
                      <span>
                        {selected
                          ? t("Pages_Command_SiteImages_PendingSelection")
                          : t("Components_GalleryImagePicker_SelectedMeta")}
                      </span>
                      <ActionButton
                        variant="ghost"
                        size="compact"
                        disabled={isSaving}
                        onClick={() => setPickerSetting(setting)}
                      >
                        {t(
                          selected || setting.imageAssetFk
                            ? "Components_GalleryImagePicker_Change"
                            : "Components_GalleryImagePicker_Select",
                        )}
                      </ActionButton>
                    </section>
                    <div className="site-images-actions">
                      <ActionButton
                        variant="primary"
                        disabled={!selected || isSaving}
                        onClick={() => selected && save.mutate({ setting, asset: selected })}
                      >
                        {isSaving
                          ? t("Pages_Command_SiteImages_Saving")
                          : t("Pages_Command_SiteImages_Save")}
                      </ActionButton>
                      <ActionButton
                        variant="ghost"
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
                      </ActionButton>
                    </div>
                  </div>
                </article>
              );
            })}
          </div>
          {dirty && (
            <footer className="site-images-footer">
              <span>{t("Pages_Command_SiteImages_Unsaved")}</span>
              <ActionButton variant="ghost" onClick={resetPending}>
                {t("Pages_Command_SiteImages_ResetAll")}
              </ActionButton>
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
