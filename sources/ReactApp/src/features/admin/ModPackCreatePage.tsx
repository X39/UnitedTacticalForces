import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { useAuth } from "@/auth/auth";
import { canManageModPacks } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { SelectField, TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import { PageHeader, StatePanel, StatusBadge, useUnsavedChanges } from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { toneOf } from "@/components/statusTones";
import { assetReadinessTones } from "@/features/admin/assetTones";
import {
  materializeRevision,
  ModPackContentEditor,
  type ModPackEditorValue,
  validateModPackEditor,
} from "@/features/admin/ModPackContentEditor";
import { modPackQueryKeys, modPackService } from "@/features/admin/modPacks";
import "@/features/admin/modPacks.css";

const emptyContent: ModPackEditorValue = { mods: [], dependencies: [] };

/**
 * The mod pack creation wizard: game, title and content on one page, then a review step before the pack is created.
 *
 * The forbidden state renders below the same header as the form, and the game picker is the shared select.
 */
export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const authorized = canManageModPacks(user);
  const [gameOverride, setGameOverride] = useState("");
  const [title, setTitle] = useState("");
  const [content, setContent] = useState<ModPackEditorValue>(emptyContent);
  const [reviewed, setReviewed] = useState(false);
  const [hasSubmitted, setHasSubmitted] = useState(false);
  const capabilities = useQuery({
    queryKey: modPackQueryKeys.capabilities,
    queryFn: ({ signal }) => modPackService.capabilities(signal),
    enabled: authorized,
  });
  const supported = useMemo(
    () => (capabilities.data ?? []).filter((capability) => capability.supportsModPacks),
    [capabilities.data],
  );
  const gameIdentifier = supported.some((game) => game.gameIdentifier === gameOverride)
    ? gameOverride
    : (supported[0]?.gameIdentifier ?? "");
  const capability = supported.find((game) => game.gameIdentifier === gameIdentifier);
  const validation = title.trim()
    ? validateModPackEditor(content)
    : t("React_ModPackEditor_TitleRequired");
  const isDirty = Boolean(title || content.mods.length || content.dependencies.length);
  useUnsavedChanges(isDirty && !hasSubmitted);
  const mutation = useMutation({
    mutationFn: async () => {
      if (!capability || validation)
        throw new Error(validation ?? "Game capability is unavailable.");
      const revision = await materializeRevision(gameIdentifier, content);
      return modPackService.createNormalized({ gameIdentifier, title: title.trim(), revision });
    },
    onSuccess: async (created) => {
      setHasSubmitted(true);
      await queryClient.invalidateQueries({ queryKey: modPackQueryKeys.all });
      navigate(`/command/mod-packs/${created.definitionId}`);
    },
  });
  const changeGame = (next: string) => {
    setGameOverride(next);
    setContent(emptyContent);
    setReviewed(false);
    mutation.reset();
  };

  if (!authorized)
    return (
      <section>
        <PageHeader
          kicker={t("Shared_CommandCenter_Nav_ModPacks")}
          title={t("Auto_CreateModPack_04e0630f")}
        />
        <PageStack>
          <ForbiddenState title={t("Auto_ModPackAdministratorAccessRequired_95eb8250")}>
            {t("Auto_ModPackCreationIsVisibleOnlyToAuthorizedAsset_24514d7e")}
          </ForbiddenState>
        </PageStack>
      </section>
    );

  return (
    <section>
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_ModPacks")}
        title={t("Auto_CreateModPack_04e0630f")}
        description={t("React_ModPackEditor_CreateDescription")}
        actions={
          <ActionLink variant="ghost" to="/command/mod-packs">
            {t("Auto_BackToModPacks_9037ecf6")}
          </ActionLink>
        }
      />
      <PageStack>
        {capabilities.isError && (
          <ErrorState onRetry={() => void capabilities.refetch()}>
            {t("Hosting_Create_LoadError")}
          </ErrorState>
        )}
        {mutation.isError && (
          <StatePanel kind="error">
            {mutation.error instanceof Error
              ? mutation.error.message
              : t("Auto_ModPackCouldNotBeCreatedCheckThePackage_cd6c5632")}
          </StatePanel>
        )}
        <section className="command-panel modpack-wizard">
          {capabilities.isPending ? (
            <LoadingState />
          ) : !capability ? (
            <EmptyState title={t("Pages_ModPacks_Create_NoGameTitle")}>
              {t("Pages_ModPacks_Create_NoGameBody")}
            </EmptyState>
          ) : (
            <div className="modpack-wizard__body">
              <section className="modpack-wizard__section">
                <header>
                  <div>
                    <span className="kicker">{t("Definition")}</span>
                    <h2>{t("Package identity")}</h2>
                  </div>
                  <StatusBadge
                    tone={toneOf(assetReadinessTones, !validation ? "ready" : "needs-attention")}
                  >
                    {!validation ? t("Ready") : t("React_ModPackEditor_NeedsContent")}
                  </StatusBadge>
                </header>
                <div className="modpack-definition-grid">
                  <SelectField
                    label={t("React_Terrains_GameLabel")}
                    value={gameIdentifier}
                    options={supported.map((game) => ({
                      value: game.gameIdentifier ?? "",
                      label: game.displayName ?? game.gameIdentifier ?? "",
                    }))}
                    onChange={(event) => changeGame(event.target.value)}
                  />
                  <TextField
                    label={t("Title")}
                    maxLength={1024}
                    value={title}
                    onChange={(event) => {
                      setTitle(event.target.value);
                      setReviewed(false);
                    }}
                  />
                </div>
                <ModPackContentEditor
                  capability={capability}
                  value={content}
                  onChange={(value) => {
                    setContent(value);
                    setReviewed(false);
                  }}
                />
                {(capability.modPackFormats?.length ?? 0) > 0 && (
                  <div className="modpack-format-note">
                    <strong>{t("React_ModPackEditor_GameFormats")}</strong>
                    <span>
                      {capability.modPackFormats?.map((format) => format.displayName).join(", ")}
                    </span>
                    <small>{t("React_ModPackEditor_ImportReviewHint")}</small>
                  </div>
                )}
                {validation && <StatePanel kind="warning">{validation}</StatePanel>}
                {reviewed && !validation && (
                  <section className="modpack-review" aria-label={t("Review")}>
                    <h3>{t("Review")}</h3>
                    <p>
                      {title.trim()} · {capability.displayName}
                    </p>
                    <p>
                      {content.mods.length} {t("React_ModPackEditor_ModsCount")} ·{" "}
                      {content.dependencies.length} {t("React_ModPackEditor_NestedModPacksCount")}
                    </p>
                    <p>
                      {content.mods.filter((mod) => !mod.modId).length}{" "}
                      {t("React_ModPackEditor_NewEntriesCount")}
                    </p>
                  </section>
                )}
                <div className="form-actions">
                  {!reviewed ? (
                    <ActionButton
                      variant="gold"
                      disabled={Boolean(validation)}
                      onClick={() => setReviewed(true)}
                    >
                      {t("React_ModPackEditor_ReviewModPack")}
                    </ActionButton>
                  ) : (
                    <ActionButton
                      variant="gold"
                      disabled={Boolean(validation) || mutation.isPending}
                      onClick={() => mutation.mutate()}
                    >
                      {mutation.isPending
                        ? t("React_ModPackEditor_Creating")
                        : t("React_ModPackEditor_CreateModPack")}
                    </ActionButton>
                  )}
                  {reviewed && (
                    <ActionButton
                      variant="ghost"
                      disabled={mutation.isPending}
                      onClick={() => setReviewed(false)}
                    >
                      {t("React_ModPackEditor_BackToEditing")}
                    </ActionButton>
                  )}
                </div>
              </section>
            </div>
          )}
        </section>
      </PageStack>
    </section>
  );
}
