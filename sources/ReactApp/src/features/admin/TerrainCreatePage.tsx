import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { ApiError } from "@/api/client";
import { useAuth } from "@/auth/auth";
import { canCreateTerrains } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { SelectField, TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import { PageHeader, StatePanel, StatusBadge, useUnsavedChanges } from "@/components/primitives";
import { ErrorState, ForbiddenState } from "@/components/StateViews";
import { toneOf } from "@/components/statusTones";
import { assetReadinessTones } from "@/features/admin/assetTones";
import { TerrainImageField } from "@/features/admin/TerrainImageField";
import {
  createTerrain,
  createTerrainFormSchema,
  terrainQueryKeys,
  type TerrainFormValues,
} from "@/features/admin/terrains";
import type { MediaAsset } from "@/features/admin/siteImages";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import "@/features/admin/terrains.css";

const finalStep = 3;

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const canCreate = canCreateTerrains(user);
  const [step, setStep] = useState(1);
  const [selectedAsset, setSelectedAsset] = useState<MediaAsset>();
  const [imageError, setImageError] = useState(false);
  const [allowNavigation, setAllowNavigation] = useState(false);
  const [createdTerrainId, setCreatedTerrainId] = useState<number>();
  const schema = useMemo(
    () =>
      createTerrainFormSchema({
        gameRequired: t("React_Terrains_GameRequired"),
        titleRequired: t("Auto_TitleRequired_c9a0236c"),
        titleTooLong: t("React_Terrains_TitleTooLong"),
      }),
    [t],
  );
  const form = useForm<TerrainFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { gameIdentifier: "", title: "" },
    mode: "onChange",
  });
  const values = useWatch({ control: form.control });
  const capabilities = useQuery({
    queryKey: hostingQueryKeys.capabilities,
    queryFn: ({ signal }) => hostingService.capabilities(signal),
    enabled: canCreate,
  });
  const mutation = useMutation({
    mutationFn: (formValues: TerrainFormValues) =>
      createTerrain(formValues, selectedAsset!.primaryKey),
    onSuccess: async (terrain) => {
      setAllowNavigation(true);
      form.reset({ gameIdentifier: terrain.gameIdentifier, title: terrain.title });
      await queryClient.invalidateQueries({ queryKey: terrainQueryKeys.all });
      setCreatedTerrainId(terrain.primaryKey);
    },
  });
  const isDirty = form.formState.isDirty || Boolean(selectedAsset);
  useUnsavedChanges(!allowNavigation && isDirty);

  useEffect(() => {
    const firstGame = capabilities.data?.[0]?.gameIdentifier;
    if (!form.getValues("gameIdentifier") && firstGame)
      form.setValue("gameIdentifier", firstGame, { shouldDirty: false, shouldValidate: true });
  }, [capabilities.data, form]);

  useEffect(() => {
    if (createdTerrainId !== undefined)
      navigate(`/command/terrains/${createdTerrainId}`, { replace: true });
  }, [createdTerrainId, navigate]);

  if (!canCreate)
    return (
      <section>
        <PageHeader
          kicker={t("Shared_CommandCenter_Nav_Terrains")}
          title={t("Auto_CreateTerrain_636ac171")}
        />
        <PageStack>
          <ForbiddenState title={t("Auto_TerrainAdministratorAccessRequired_22faf377")}>
            {t("Auto_TerrainCreationIsVisibleOnlyToAuthorizedAssetOperators_05b4c15c")}
          </ForbiddenState>
        </PageStack>
      </section>
    );

  const hasDefinition = Boolean(values.gameIdentifier?.trim() && values.title?.trim());
  const hasImage = Boolean(selectedAsset);
  const ready = form.formState.isValid && hasImage && !mutation.isPending;
  const stepLabel = (index: number) => {
    if (index === 1) return t("Pages_Data_Terrains_Create_Step_Definition_Label");
    if (index === 2) return t("Pages_Data_Terrains_Create_Step_Image_Label");
    return t("Pages_Data_Terrains_Create_Step_Review_Label");
  };
  const stepDescription = (index: number) => {
    if (index === 1) return t("Pages_Data_Terrains_Create_Step_Definition_Description");
    if (index === 2) return t("Pages_Data_Terrains_Create_Step_Image_Description");
    return t("Pages_Data_Terrains_Create_Step_Review_Description");
  };
  const isStepComplete = (index: number) =>
    index === 1 ? hasDefinition : index === 2 ? hasImage : ready;
  const advance = async () => {
    if (step === 1 && !(await form.trigger())) return;
    if (step === 2 && !selectedAsset) {
      setImageError(true);
      return;
    }
    setStep((value) => Math.min(finalStep, value + 1));
  };
  const mutationMessage =
    mutation.error instanceof ApiError && mutation.error.code === "conflict"
      ? t("React_Terrains_Conflict")
      : t("Auto_TerrainCouldNotBeCreatedCheckTheTitleAnd_4e51c776");

  const capabilitiesDenied =
    capabilities.error instanceof ApiError &&
    (capabilities.error.code === "unauthorized" || capabilities.error.code === "forbidden");

  return (
    <section>
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_Terrains")}
        title={t("Auto_CreateTerrain_636ac171")}
        description={t("Auto_CreateATerrainRecordWithTheTitleAndReference_59bfb0a1")}
        actions={
          <ActionLink variant="ghost" to="/command/terrains">
            {t("Auto_BackToTerrains_c72569b2")}
          </ActionLink>
        }
      />

      <PageStack>
        {capabilities.isError &&
          (capabilitiesDenied ? (
            <ForbiddenState title={t("Auto_TerrainAdministratorAccessRequired_22faf377")}>
              {t("Auto_TerrainCreationIsVisibleOnlyToAuthorizedAssetOperators_05b4c15c")}
            </ForbiddenState>
          ) : (
            <ErrorState onRetry={() => void capabilities.refetch()}>
              {t("Auto_TerrainDataCouldNotBeLoadedCheckTheAPI_ff13b797")}
            </ErrorState>
          ))}
        {mutation.isError && <StatePanel kind="error">{mutationMessage}</StatePanel>}

        <form
          className="command-panel terrain-create-flow"
          onSubmit={form.handleSubmit((formValues) => {
            if (!selectedAsset) {
              setImageError(true);
              setStep(2);
              return;
            }
            mutation.mutate(formValues);
          })}
        >
          <ol className="terrain-stepper" aria-label={t("Auto_CreateTerrainSteps_aff7544c")}>
            {[1, 2, 3].map((index) => (
              <li
                key={index}
                className={`${step === index ? "is-active" : ""}${isStepComplete(index) ? " is-complete" : ""}`}
              >
                <ActionButton
                  variant="ghost"
                  className="terrain-step"
                  aria-current={step === index ? "step" : undefined}
                  onClick={() => setStep(index)}
                >
                  <span>{index}</span>
                  <strong>{stepLabel(index)}</strong>
                  <small>{stepDescription(index)}</small>
                </ActionButton>
              </li>
            ))}
          </ol>

          <div className="terrain-create-step">
            {step === 1 && (
              <section>
                <header className="terrain-section-header">
                  <div>
                    <span className="kicker">{t("Definition")}</span>
                    <h2>{t("Auto_TerrainIdentity_3eb3ba27")}</h2>
                  </div>
                  <StatusBadge
                    tone={toneOf(assetReadinessTones, hasDefinition ? "ready" : "needs-attention")}
                  >
                    {hasDefinition
                      ? t("Auto_DefinitionReady_5e834525")
                      : t("Auto_TitleRequired_c9a0236c")}
                  </StatusBadge>
                </header>
                <div className="terrain-form-grid">
                  <SelectField
                    label={t("React_Terrains_GameLabel")}
                    {...form.register("gameIdentifier")}
                    disabled={capabilities.isPending || capabilities.isError}
                    error={form.formState.errors.gameIdentifier?.message}
                    options={[
                      ...(capabilities.isPending
                        ? [{ value: "", label: t("Common_LoadingData") }]
                        : []),
                      ...(capabilities.data ?? []).map((capability) => ({
                        value: capability.gameIdentifier,
                        label: capability.displayName,
                      })),
                    ]}
                  />
                  <TextField
                    label={t("Panels_CreateTerrainPanel_Title")}
                    {...form.register("title")}
                    maxLength={1024}
                    autoComplete="off"
                    error={form.formState.errors.title?.message}
                  />
                </div>
              </section>
            )}

            {step === 2 && (
              <section>
                <header className="terrain-section-header">
                  <div>
                    <span className="kicker">{t("Imagery")}</span>
                    <h2>{t("Auto_UploadTerrainImage_b50dd465")}</h2>
                  </div>
                  <StatusBadge
                    tone={toneOf(assetReadinessTones, hasImage ? "ready" : "needs-attention")}
                  >
                    {hasImage ? t("Auto_ImageReady_7e953d5b") : t("Auto_ImageRequired_7e9a789a")}
                  </StatusBadge>
                </header>
                <TerrainImageField
                  selected={selectedAsset}
                  disabled={mutation.isPending}
                  onChange={(asset) => {
                    setSelectedAsset(asset);
                    setImageError(false);
                  }}
                />
                {imageError && (
                  <p className="field-error" role="alert">
                    {t("Auto_TerrainImageRequired_addf7b1b")}
                  </p>
                )}
              </section>
            )}

            {step === 3 && (
              <section>
                <header className="terrain-section-header">
                  <div>
                    <span className="kicker">{t("Review")}</span>
                    <h2>{t("Auto_ConfirmTerrain_09fe1c07")}</h2>
                  </div>
                  <StatusBadge
                    tone={toneOf(assetReadinessTones, ready ? "ready" : "needs-attention")}
                  >
                    {ready ? t("Auto_ReadyToCreate_ff2d530f") : t("Auto_NeedsAttention_c1ebc781")}
                  </StatusBadge>
                </header>
                <div className="terrain-review-grid">
                  <article>
                    <header>
                      <strong>{t("Definition")}</strong>
                      <ActionButton variant="ghost" size="compact" onClick={() => setStep(1)}>
                        {t("Edit")}
                      </ActionButton>
                    </header>
                    <dl>
                      <dt>{t("React_Terrains_GameLabel")}</dt>
                      <dd>{values.gameIdentifier || "—"}</dd>
                      <dt>{t("Title")}</dt>
                      <dd>{values.title?.trim() || t("Auto_UntitledTerrain_6aba91cc")}</dd>
                      <dt>{t("Status")}</dt>
                      <dd>{t("Auto_ActiveAfterCreation_cf374b33")}</dd>
                    </dl>
                  </article>
                  <article>
                    <header>
                      <strong>{t("Image")}</strong>
                      <ActionButton variant="ghost" size="compact" onClick={() => setStep(2)}>
                        {t("Edit")}
                      </ActionButton>
                    </header>
                    <dl>
                      <dt>{t("File")}</dt>
                      <dd>
                        {selectedAsset?.originalFileName || t("Auto_TerrainImageRequired_addf7b1b")}
                      </dd>
                      <dt>{t("Type")}</dt>
                      <dd>{selectedAsset?.contentType || "—"}</dd>
                      <dt>{t("Size")}</dt>
                      <dd>{selectedAsset ? `${Math.ceil(selectedAsset.size / 1024)} KB` : "—"}</dd>
                    </dl>
                  </article>
                </div>
                <div className="terrain-checklist">
                  {[
                    [t("Auto_TitleDefined_288586ef"), Boolean(values.title?.trim())],
                    [t("Auto_ImageUploaded_6c3ba36c"), hasImage],
                    [t("Auto_ImageTypeKnown_0e953683"), Boolean(selectedAsset?.contentType)],
                  ].map(([label, complete]) => (
                    <div key={String(label)} className={complete ? "is-complete" : undefined}>
                      <span aria-hidden="true" />
                      <strong>{label}</strong>
                      <small>{complete ? t("Complete") : t("Auto_NeedsAttention_c1ebc781")}</small>
                    </div>
                  ))}
                </div>
              </section>
            )}
          </div>

          <footer className="terrain-command-bar">
            <div>
              <span>{stepLabel(step)}</span>
              <strong>
                {ready ? t("Auto_ReadyToCreate_ff2d530f") : t("Auto_NeedsAttention_c1ebc781")}
              </strong>
            </div>
            <div>
              <ActionButton
                variant="ghost"
                disabled={step <= 1 || mutation.isPending}
                onClick={() => setStep((value) => Math.max(1, value - 1))}
              >
                {t("Previous")}
              </ActionButton>
              {step < finalStep ? (
                <ActionButton
                  variant="gold"
                  disabled={mutation.isPending}
                  onClick={(event) => {
                    // Prevent a final-step rerender from turning this in-flight click into a submit.
                    event.preventDefault();
                    void advance();
                  }}
                >
                  {t("Next")}
                </ActionButton>
              ) : (
                <ActionButton variant="gold" type="submit" disabled={!ready}>
                  {mutation.isPending
                    ? t("Pages_Data_Terrains_Create_ActionCreating")
                    : t("Pages_Data_Terrains_Create_ActionCreate")}
                </ActionButton>
              )}
            </div>
          </footer>
        </form>
      </PageStack>
    </section>
  );
}
