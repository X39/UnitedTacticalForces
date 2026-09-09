import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { useAuth } from "@/auth/auth";
import { canManageModPacks } from "@/auth/claims";
import { PageHeader, StatePanel, StatusBadge, useUnsavedChanges } from "@/components/primitives";
import {
  materializeRevision,
  ModPackContentEditor,
  type ModPackEditorValue,
  validateModPackEditor,
} from "@/features/admin/ModPackContentEditor";
import { modPackQueryKeys, modPackService } from "@/features/admin/modPacks";
import "@/features/admin/modPacks.css";

const emptyContent: ModPackEditorValue = { mods: [], dependencies: [] };

export function Component() {
  const { t: translate } = useTranslation();
  const t = (key: string) => translate(key as never, { defaultValue: key });
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
  const validation = title.trim() ? validateModPackEditor(content) : t("A title is required.");
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
      <StatePanel kind="forbidden" title={t("Auto_ModPackAdministratorAccessRequired_95eb8250")}>
        {t("Auto_ModPackCreationIsVisibleOnlyToAuthorizedAsset_24514d7e")}
      </StatePanel>
    );

  return (
    <section className="modpack-create-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_ModPacks")}
        title={t("Auto_CreateModPack_04e0630f")}
        description={t(
          "Build a versioned mod pack from catalog mods, source providers, and nested packs.",
        )}
        actions={
          <Link className="button button--ghost" to="/command/mod-packs">
            {t("Auto_BackToModPacks_9037ecf6")}
          </Link>
        }
      />
      {capabilities.isError && (
        <StatePanel kind="error">{t("Hosting_Create_LoadError")}</StatePanel>
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
          <StatePanel kind="loading">{t("Common_LoadingData")}</StatePanel>
        ) : !capability ? (
          <StatePanel kind="empty">{t("No registered game supports mod packs.")}</StatePanel>
        ) : (
          <div className="modpack-wizard__body">
            <section className="modpack-wizard__section">
              <header>
                <div>
                  <span className="kicker">{t("Definition")}</span>
                  <h2>{t("Package identity")}</h2>
                </div>
                <StatusBadge tone={!validation ? "success" : "warning"}>
                  {!validation ? t("Ready") : t("Needs content")}
                </StatusBadge>
              </header>
              <div className="modpack-definition-grid">
                <label className="field">
                  <span>{t("Game")}</span>
                  <select
                    value={gameIdentifier}
                    onChange={(event) => changeGame(event.target.value)}
                  >
                    {supported.map((game) => (
                      <option key={game.gameIdentifier} value={game.gameIdentifier}>
                        {game.displayName}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="field">
                  <span>{t("Title")}</span>
                  <input
                    maxLength={1024}
                    value={title}
                    onChange={(event) => {
                      setTitle(event.target.value);
                      setReviewed(false);
                    }}
                  />
                </label>
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
                  <strong>{t("Game formats")}</strong>
                  <span>
                    {capability.modPackFormats?.map((format) => format.displayName).join(", ")}
                  </span>
                  <small>
                    {t(
                      "Use the game format importer above to review its normalized content before saving.",
                    )}
                  </small>
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
                    {content.mods.length} {t("mods")} · {content.dependencies.length}{" "}
                    {t("nested mod packs")}
                  </p>
                  <p>
                    {content.mods.filter((mod) => !mod.modId).length}{" "}
                    {t("new catalog entries will be created")}
                  </p>
                </section>
              )}
              <div className="form-actions">
                {!reviewed ? (
                  <button
                    className="button button--primary"
                    type="button"
                    disabled={Boolean(validation)}
                    onClick={() => setReviewed(true)}
                  >
                    {t("Review mod pack")}
                  </button>
                ) : (
                  <button
                    className="button button--primary"
                    type="button"
                    disabled={Boolean(validation) || mutation.isPending}
                    onClick={() => mutation.mutate()}
                  >
                    {mutation.isPending ? t("Creating…") : t("Create mod pack")}
                  </button>
                )}
                {reviewed && (
                  <button
                    className="button button--ghost"
                    type="button"
                    disabled={mutation.isPending}
                    onClick={() => setReviewed(false)}
                  >
                    {t("Back to editing")}
                  </button>
                )}
              </div>
            </section>
          </div>
        )}
      </section>
    </section>
  );
}
