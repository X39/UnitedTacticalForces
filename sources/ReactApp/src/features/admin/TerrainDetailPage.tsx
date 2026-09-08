import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useParams } from "react-router";
import { ApiError, resolveImageUrl } from "@/api/client";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canAccessTerrain, canDeleteTerrain, canModifyTerrain } from "@/auth/claims";
import { useAppToastOnChange } from "@/components/AppToasts";
import { NotFoundPage } from "@/components/NotFoundPage";
import {
  ConfirmDialog,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { TerrainImageField } from "@/features/admin/TerrainImageField";
import {
  createTerrainUpdateFormSchema,
  deactivateTerrain,
  formatTerrainTitle,
  isLongRouteValue,
  loadTerrain,
  terrainHasImage,
  terrainQueryKeys,
  updateTerrain,
  type TerrainUpdateFormValues,
} from "@/features/admin/terrains";
import type { MediaAsset } from "@/features/admin/siteImages";

type Notice = { kind: "success" | "error" | "warning"; text: string };

export function Component() {
  const { terrainId } = useParams();
  if (!isLongRouteValue(terrainId)) return <NotFoundPage />;
  return <TerrainDetail terrainId={terrainId} />;
}

function TerrainDetail({ terrainId }: { terrainId: string }) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const canAccess = canAccessTerrain(user, terrainId);
  const canModify = canModifyTerrain(user, terrainId);
  const canDelete = canDeleteTerrain(user, terrainId);
  const [selectedAsset, setSelectedAsset] = useState<MediaAsset>();
  const [notice, setNotice] = useState<Notice>();
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [allowNavigation, setAllowNavigation] = useState(false);
  const [deactivated, setDeactivated] = useState(false);
  const schema = useMemo(
    () =>
      createTerrainUpdateFormSchema({
        titleRequired: t("Auto_TitleRequired_c9a0236c"),
        titleTooLong: t("React_Terrains_TitleTooLong"),
      }),
    [t],
  );
  const form = useForm<TerrainUpdateFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { title: "" },
    mode: "onChange",
  });
  const title = useWatch({ control: form.control, name: "title" });
  const query = useQuery({
    queryKey: terrainQueryKeys.detail(terrainId),
    queryFn: ({ signal }) => loadTerrain(terrainId, signal),
    enabled: canAccess,
  });
  const terrain = query.data;
  const titleChanged = Boolean(terrain && title.trim() !== terrain.title);
  const hasChanges = titleChanged || Boolean(selectedAsset);
  useUnsavedChanges(!allowNavigation && hasChanges);
  useAppToastOnChange(notice?.text, notice?.kind ?? "success");

  useEffect(() => {
    if (terrain) form.reset({ title: terrain.title });
  }, [form, terrain]);

  useEffect(() => {
    if (deactivated) navigate("/command/terrains", { replace: true });
  }, [deactivated, navigate]);

  const save = useMutation({
    mutationFn: (values: TerrainUpdateFormValues) =>
      updateTerrain(terrainId, {
        ...(values.title.trim() !== terrain?.title ? { title: values.title.trim() } : {}),
        ...(selectedAsset ? { imageAssetFk: selectedAsset.primaryKey } : {}),
      }),
    onMutate: () => setNotice(undefined),
    onSuccess: async () => {
      setSelectedAsset(undefined);
      const refreshed = await query.refetch();
      if (refreshed.data) form.reset({ title: refreshed.data.title });
      await queryClient.invalidateQueries({ queryKey: ["terrains", "list"] });
      setNotice({ kind: "success", text: t("Auto_TerrainChangesSaved_63c6a8f0") });
    },
    onError: (error) =>
      setNotice({
        kind: error instanceof ApiError && error.code === "conflict" ? "warning" : "error",
        text:
          error instanceof ApiError && error.code === "conflict"
            ? t("React_Terrains_Conflict")
            : t("Auto_TerrainChangesCouldNotBeSavedCheckTheInputs_b8dfe7de"),
      }),
  });
  const deactivate = useMutation({
    mutationFn: () => deactivateTerrain(terrainId),
    onMutate: () => setNotice(undefined),
    onSuccess: async () => {
      setAllowNavigation(true);
      setDeleteOpen(false);
      await queryClient.invalidateQueries({ queryKey: terrainQueryKeys.all });
      setDeactivated(true);
    },
    onError: () => {
      setDeleteOpen(false);
      setNotice({
        kind: "error",
        text: t("Auto_TerrainCouldNotBeDeactivatedRetryFromTheDetail_d843ca7a"),
      });
    },
  });

  if (!canAccess)
    return (
      <StatePanel kind="forbidden" title={t("Auto_TerrainAdministratorAccessRequired_22faf377")}>
        {t("Auto_TerrainDetailsAreVisibleOnlyToAuthorizedAssetOperators_5b3d72af")}
      </StatePanel>
    );

  if (query.isPending)
    return <StatePanel kind="loading">{t("Auto_LoadingTerrainDetail_df82cbfe")}</StatePanel>;

  if (
    query.isError &&
    query.error instanceof ApiError &&
    (query.error.code === "unauthorized" || query.error.code === "forbidden")
  )
    return (
      <StatePanel kind="forbidden" title={t("Auto_TerrainAdministratorAccessRequired_22faf377")}>
        {t("Auto_TerrainDetailsAreVisibleOnlyToAuthorizedAssetOperators_5b3d72af")}
      </StatePanel>
    );

  if (query.isError)
    return (
      <StatePanel
        kind="error"
        action={
          <button className="button button--ghost" type="button" onClick={() => query.refetch()}>
            {t("React_UI_Retry")}
          </button>
        }
      >
        {t("Auto_TerrainDataCouldNotBeLoadedCheckTheAPI_ff13b797")}
      </StatePanel>
    );

  if (!terrain)
    return (
      <StatePanel kind="empty" title={t("Auto_TerrainNotFound_e99f0e69")}>
        {formatResource(t("Auto_NoTerrainWasFoundForID_3bcd5fa2"), terrainId)}
      </StatePanel>
    );

  const displayTitle = formatTerrainTitle(terrain, t("Auto_UntitledTerrain_6aba91cc"));
  const hasImage = terrainHasImage(terrain);
  const statusLabel = terrain.isActive
    ? t("Pages_Data_Terrains_All_StatusActive")
    : t("Pages_Data_Terrains_All_StatusInactive");
  const canSave = canModify && hasChanges && form.formState.isValid && !save.isPending;

  return (
    <section className="terrain-detail-page">
      <PageHeader
        kicker={t("Auto_TerrainRecord_51769df4")}
        title={displayTitle}
        description={t("Auto_TerrainRecordInlineEditingImageStateAndCommandCenter_a52c4fd4")}
        actions={
          <>
            <Link className="button button--ghost" to="/command/terrains">
              {t("Auto_AllTerrains_b84779a5")}
            </Link>
            {canDelete && terrain.isActive && (
              <button
                className="button button--danger"
                type="button"
                disabled={save.isPending || deactivate.isPending}
                onClick={() => setDeleteOpen(true)}
              >
                {t("Deactivate")}
              </button>
            )}
          </>
        }
      />

      <section
        className="command-metrics terrain-detail-metrics"
        aria-label={t("Auto_TerrainDetailSummary_84968ec0")}
      >
        <div>
          <span>{t("Identifier")}</span>
          <strong>{t("Auto_TerrainRecord_51769df4")}</strong>
          <small>{terrain.primaryKey}</small>
        </div>
        <div>
          <span>{t("Status")}</span>
          <strong>{statusLabel}</strong>
          <small>
            {terrain.isActive
              ? t("Auto_AvailableForActiveSelections_622c00f1")
              : t("Auto_DeactivatedAndHiddenFromActiveTerrainLists_52ca2f75")}
          </small>
        </div>
        <div>
          <span>{t("Image")}</span>
          <strong>
            {hasImage ? t("Auto_ImageReady_7e953d5b") : t("Auto_MissingImage_2c8126ac")}
          </strong>
          <small>
            {hasImage
              ? t("Auto_ReferenceImageryIsAvailable_8fcbe772")
              : t("Auto_NoImageIsStoredForThisTerrain_3045a490")}
          </small>
        </div>
        <div>
          <span>{t("React_Terrains_GameLabel")}</span>
          <strong>{terrain.gameIdentifier || "—"}</strong>
          <small>{t("Auto_AssetSize_91ddf030")}: —</small>
        </div>
      </section>

      <div className="terrain-detail-workspace">
        <section
          className="command-panel terrain-detail-preview"
          aria-labelledby="terrain-image-title"
        >
          <header className="command-panel__header">
            <div>
              <span className="kicker">{t("Auto_TerrainImage_20701e8d")}</span>
              <h2 id="terrain-image-title">{displayTitle}</h2>
            </div>
            <StatusBadge tone={terrain.isActive ? "success" : "neutral"}>{statusLabel}</StatusBadge>
          </header>
          <div className={`terrain-detail-image${hasImage ? "" : " is-empty"}`}>
            {hasImage ? (
              <img
                src={resolveImageUrl(terrain.imagePath)}
                alt={`${displayTitle}: ${t("Pages_Data_Terrains_All_StatusImagery")}`}
              />
            ) : (
              <span>{t("Pages_Data_Terrains_All_NoImage")}</span>
            )}
          </div>
        </section>

        <aside className="command-panel terrain-detail-editor">
          <header className="command-panel__header">
            <div>
              <span className="kicker">{t("Metadata")}</span>
              <h2>
                {canModify ? t("Auto_InlineEditing_e0013c16") : t("Auto_TerrainMetadata_74a769e8")}
              </h2>
            </div>
          </header>
          {canModify && (
            <form
              className="terrain-edit-form"
              onSubmit={form.handleSubmit((values) => save.mutate(values))}
            >
              <label className="field">
                <span>{t("Panels_UpdateTerrainPanel_Title")}</span>
                <input
                  {...form.register("title")}
                  maxLength={1024}
                  disabled={save.isPending}
                  aria-invalid={Boolean(form.formState.errors.title)}
                />
                {form.formState.errors.title && (
                  <small className="field-error" role="alert">
                    {form.formState.errors.title.message}
                  </small>
                )}
              </label>
              <TerrainImageField
                selected={selectedAsset}
                currentAssetId={terrain.imageAssetFk}
                currentImagePath={terrain.imagePath}
                disabled={save.isPending}
                onChange={(asset) => {
                  setSelectedAsset(asset);
                  setNotice(undefined);
                }}
              />
              <div className="terrain-edit-actions">
                <button className="button button--primary" type="submit" disabled={!canSave}>
                  {save.isPending
                    ? t("Auto_SavingChanges_2c149159")
                    : t("Auto_SaveChanges_dd0ae7a5")}
                </button>
                <button
                  className="button button--ghost"
                  type="button"
                  disabled={save.isPending}
                  onClick={() => {
                    form.reset({ title: terrain.title });
                    setSelectedAsset(undefined);
                    setNotice(undefined);
                  }}
                >
                  {t("Reset")}
                </button>
              </div>
            </form>
          )}
          <dl className="terrain-detail-meta">
            <dt>{t("Title")}</dt>
            <dd>{displayTitle}</dd>
            <dt>{t("Key")}</dt>
            <dd>{terrain.primaryKey}</dd>
            <dt>{t("React_Terrains_GameLabel")}</dt>
            <dd>{terrain.gameIdentifier || "—"}</dd>
            <dt>{t("Status")}</dt>
            <dd>{statusLabel}</dd>
            <dt>{t("Image")}</dt>
            <dd>
              {hasImage
                ? t("Pages_Data_Terrains_All_StatusImagery")
                : t("Pages_Data_Terrains_All_NoImage")}
            </dd>
          </dl>
        </aside>
      </div>

      <ConfirmDialog
        open={deleteOpen}
        title={t("Pages_Data_Terrains_All_ConfirmDeleteTerrain_Title")}
        description={t("Pages_Data_Terrains_All_ConfirmDeleteTerrain_Body")}
        confirmLabel={t("Common_Delete")}
        cancelLabel={t("Common_Cancel")}
        onCancel={() => setDeleteOpen(false)}
        onConfirm={() => {
          setDeleteOpen(false);
          deactivate.mutate();
        }}
      />
    </section>
  );
}
