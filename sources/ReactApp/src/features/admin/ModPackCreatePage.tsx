import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useSearchParams } from "react-router";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { canManageModPacks } from "@/auth/claims";
import { PageHeader, StatePanel, StatusBadge, useUnsavedChanges } from "@/components/primitives";
import { ModPackStandaloneSelector } from "@/features/admin/ModPackStandaloneSelector";
import {
  latestActiveRevision,
  modPackQueryKeys,
  modPackService,
  type FullModPackDefinition,
} from "@/features/admin/modPacks";
import "@/features/admin/modPacks.css";

type CreationKind = "standalone" | "composition";
const finalStep = 3;
const maxHtmlBytes = 10 * 1024 * 1024;

function selectedRevisionIds(definitions: FullModPackDefinition[]): number[] {
  return definitions
    .map((definition) => latestActiveRevision(definition)?.primaryKey)
    .filter((id): id is number => id != null);
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();
  const authorized = canManageModPacks(user);
  const initialKind: CreationKind =
    searchParams.get("kind")?.toLocaleLowerCase() === "composition" ? "composition" : "standalone";
  const [kind, setKind] = useState<CreationKind>(initialKind);
  const [step, setStep] = useState(1);
  const [selectedGameIdentifier, setSelectedGameIdentifier] = useState("");
  const [title, setTitle] = useState("");
  const [html, setHtml] = useState("");
  const [htmlFileName, setHtmlFileName] = useState("");
  const [selected, setSelected] = useState<FullModPackDefinition[]>([]);
  const [fileError, setFileError] = useState<string>();
  const [hasSubmitted, setHasSubmitted] = useState(false);
  const capabilities = useQuery({
    queryKey: modPackQueryKeys.capabilities,
    queryFn: ({ signal }) => modPackService.capabilities(signal),
    enabled: authorized,
  });
  const defaultGameIdentifier = capabilities.data?.[0]?.gameIdentifier ?? "";
  const gameIdentifier = (capabilities.data ?? []).some(
    (capability) => capability.gameIdentifier === selectedGameIdentifier,
  )
    ? selectedGameIdentifier
    : defaultGameIdentifier;
  const revisions = useMemo(() => selectedRevisionIds(selected), [selected]);
  const definitionReady = Boolean(gameIdentifier && title.trim());
  const artifactReady =
    kind === "composition"
      ? selected.length > 0 && revisions.length === selected.length
      : Boolean(html.trim());
  const canCreate = definitionReady && artifactReady;
  const isDirty = Boolean(
    title || html || selected.length || gameIdentifier !== defaultGameIdentifier,
  );
  useUnsavedChanges(isDirty && !hasSubmitted);
  const mutation = useMutation({
    mutationFn: () =>
      kind === "composition"
        ? modPackService.createComposition({
            gameIdentifier,
            title: title.trim(),
            revisionIds: revisions,
          })
        : modPackService.createStandalone({ gameIdentifier, title: title.trim(), html }),
    onSuccess: async (created) => {
      setHasSubmitted(true);
      await queryClient.invalidateQueries({ queryKey: modPackQueryKeys.all });
      navigate(
        created.primaryKey == null
          ? "/command/mod-packs"
          : `/command/mod-packs/${created.primaryKey}`,
      );
    },
  });
  const readHtml = async (file: File | undefined) => {
    setFileError(undefined);
    if (!file) return;
    const htmlFile = /\.html?$/i.test(file.name) || file.type === "text/html";
    if (!htmlFile || file.size > maxHtmlBytes) {
      setHtml("");
      setHtmlFileName("");
      setFileError(t("Panels_CreateModPackPanel_Html_OnlyHtmlFilesAreSupported"));
      return;
    }
    setHtml(await file.text());
    setHtmlFileName(file.name);
  };
  const changeKind = (value: CreationKind) => {
    setKind(value);
    mutation.reset();
  };
  const stepLabels = [
    t("Pages_Data_ModPacks_Create_Step_Definition_Label"),
    t("Pages_Data_ModPacks_Create_Step_Artifact_Label"),
    t("Pages_Data_ModPacks_Create_Step_Review_Label"),
  ];

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
        description={t("Auto_CreateAStandaloneHTMLModPackOrAComposition_fc31ae64")}
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
          {t("Auto_ModPackCouldNotBeCreatedCheckThePackage_cd6c5632")}
        </StatePanel>
      )}
      {fileError && <StatePanel kind="warning">{fileError}</StatePanel>}

      <section className="command-panel modpack-wizard">
        <ol className="stepper" aria-label={t("Auto_CreateModPackSteps_cf46b9fc")}>
          {stepLabels.map((label, index) => {
            const value = index + 1;
            const complete =
              value === 1 ? definitionReady : value === 2 ? artifactReady : canCreate;
            return (
              <li
                key={label}
                className={step === value ? "active" : complete ? "complete" : undefined}
              >
                <button
                  type="button"
                  onClick={() => setStep(value)}
                  aria-current={step === value ? "step" : undefined}
                >
                  <span>{value}</span>
                  <strong>{label}</strong>
                </button>
              </li>
            );
          })}
        </ol>

        <div className="modpack-wizard__body">
          {capabilities.isPending ? (
            <StatePanel kind="loading">{t("Common_LoadingData")}</StatePanel>
          ) : step === 1 ? (
            <section className="modpack-wizard__section">
              <header>
                <span className="kicker">{t("Definition")}</span>
                <h2>{t("Auto_PackageIdentity_1284e9ed")}</h2>
                <StatusBadge tone={definitionReady ? "success" : "warning"}>
                  {definitionReady
                    ? t("Auto_DefinitionReady_5e834525")
                    : t("Auto_TitleRequired_c9a0236c")}
                </StatusBadge>
              </header>
              <div
                className="modpack-kind-grid"
                role="radiogroup"
                aria-label={t("Auto_ModPackType_4d91d271")}
              >
                <button
                  type="button"
                  role="radio"
                  aria-checked={kind === "standalone"}
                  className={kind === "standalone" ? "active" : undefined}
                  onClick={() => changeKind("standalone")}
                >
                  <StatusBadge>{t("Standalone")}</StatusBadge>
                  <strong>{t("Auto_HTMLPackage_56c8e9c6")}</strong>
                  <small>{t("Auto_CreatesAnOwnedModPackDefinitionWithAnInitial_61cd85d0")}</small>
                </button>
                <button
                  type="button"
                  role="radio"
                  aria-checked={kind === "composition"}
                  className={kind === "composition" ? "active" : undefined}
                  onClick={() => changeKind("composition")}
                >
                  <StatusBadge tone="info">{t("Composition")}</StatusBadge>
                  <strong>{t("Auto_RevisionBundle_8f3de2a4")}</strong>
                  <small>
                    {t("Auto_CreatesAPackageFromLatestActiveRevisionsOfExisting_80b2a4d8")}
                  </small>
                </button>
              </div>
              <label className="field">
                <span>{t("Hosting_Create_Game")}</span>
                <select
                  value={gameIdentifier}
                  onChange={(event) => {
                    setSelectedGameIdentifier(event.target.value);
                    setSelected([]);
                  }}
                >
                  {(capabilities.data ?? []).map((capability) => (
                    <option key={capability.gameIdentifier} value={capability.gameIdentifier}>
                      {capability.displayName || capability.gameIdentifier}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span>{t("Title")}</span>
                <input
                  maxLength={1024}
                  value={title}
                  onChange={(event) => setTitle(event.target.value)}
                />
              </label>
            </section>
          ) : step === 2 ? (
            <section className="modpack-wizard__section">
              <header>
                <span className="kicker">
                  {kind === "composition" ? t("Composition") : t("Standalone")}
                </span>
                <h2>
                  {kind === "composition"
                    ? t("Auto_SelectMemberRevisions_686f0cd0")
                    : t("Auto_UploadHTMLArtifact_d37a21b4")}
                </h2>
                <StatusBadge tone={artifactReady ? "success" : "warning"}>
                  {artifactReady
                    ? kind === "composition"
                      ? t("Auto_MembersReady_55b06fbf")
                      : t("Auto_ArtifactReady_6be01f39")
                    : kind === "composition"
                      ? t("Auto_SelectAtLeastOneModPack_0ef5008a")
                      : t("Auto_HTMLArtifactRequired_aa5544da")}
                </StatusBadge>
              </header>
              {kind === "composition" ? (
                <ModPackStandaloneSelector
                  gameIdentifier={gameIdentifier}
                  selected={selected}
                  onChange={setSelected}
                />
              ) : (
                <div className="modpack-html-upload">
                  <label className="file-drop">
                    <span>{t("Panels_CreateModPackPanel_Html_StartText")}</span>
                    <strong>{t("Panels_CreateModPackPanel_Html_HintText")}</strong>
                    <span>{t("Panels_CreateModPackPanel_Html_EndText")}</span>
                    <input
                      type="file"
                      accept=".html,.htm,text/html"
                      onChange={(event) => void readHtml(event.target.files?.[0])}
                    />
                  </label>
                  <small>{htmlFileName || t("Auto_HTMLArtifactRequired_aa5544da")}</small>
                  {html && (
                    <iframe
                      title={t("Auto_UploadedModPackHTMLPreview_43533584")}
                      sandbox=""
                      srcDoc={html}
                    />
                  )}
                </div>
              )}
            </section>
          ) : (
            <section className="modpack-wizard__section">
              <header>
                <span className="kicker">{t("Review")}</span>
                <h2>{t("Auto_ConfirmPackage_c66a3a8e")}</h2>
                <StatusBadge tone={canCreate ? "success" : "warning"}>
                  {canCreate ? t("Auto_ReadyToCreate_ff2d530f") : t("Auto_NeedsAttention_c1ebc781")}
                </StatusBadge>
              </header>
              <div className="modpack-review-grid">
                <article>
                  <strong>{t("Definition")}</strong>
                  <dl>
                    <dt>{t("Type")}</dt>
                    <dd>{kind === "composition" ? t("Composition") : t("Standalone")}</dd>
                    <dt>{t("Hosting_Create_Game")}</dt>
                    <dd>{gameIdentifier || "—"}</dd>
                    <dt>{t("Title")}</dt>
                    <dd>{title.trim() || t("Auto_UntitledModPack_68e62d9a")}</dd>
                  </dl>
                </article>
                <article>
                  <strong>{kind === "composition" ? t("Members") : t("Artifact")}</strong>
                  <dl>
                    {kind === "composition" ? (
                      <>
                        <dt>{t("Selected")}</dt>
                        <dd>
                          {selected.length === 1
                            ? t("Auto_1StandalonePackSelected_d8f0dc9c")
                            : formatResource(
                                t("Auto_StandalonePacksSelected_e437ccf0"),
                                selected.length,
                              )}
                        </dd>
                        <dt>{t("Auto_RevisionIDs_ba9f5fcc")}</dt>
                        <dd>{revisions.length ? revisions.join(", ") : "—"}</dd>
                      </>
                    ) : (
                      <>
                        <dt>{t("File")}</dt>
                        <dd>{htmlFileName || t("Auto_HTMLArtifactRequired_aa5544da")}</dd>
                        <dt>{t("Size")}</dt>
                        <dd>
                          {html
                            ? formatResource(
                                t("Pages_Data_ModPacks_Create_SizeCharacters"),
                                html.length,
                              )
                            : "—"}
                        </dd>
                      </>
                    )}
                  </dl>
                </article>
              </div>
              <div className="modpack-readiness">
                {[
                  [t("Auto_TitleDefined_288586ef"), Boolean(title.trim())],
                  [t("Auto_TypeSelected_875c9448"), true],
                  [
                    kind === "composition"
                      ? t("Auto_MemberRevisionsSelected_e6bb4b51")
                      : t("Auto_HTMLArtifactUploaded_f3a32db4"),
                    artifactReady,
                  ],
                ].map(([label, complete]) => (
                  <div key={String(label)} className={complete ? "complete" : undefined}>
                    <span aria-hidden="true" />
                    <strong>{label}</strong>
                    <small>{complete ? t("Complete") : t("Auto_NeedsAttention_c1ebc781")}</small>
                  </div>
                ))}
              </div>
            </section>
          )}
        </div>

        <footer className="modpack-wizard__footer">
          <div>
            <span>{stepLabels[step - 1]}</span>
            <strong>
              {canCreate ? t("Auto_ReadyToCreate_ff2d530f") : t("Auto_NeedsAttention_c1ebc781")}
            </strong>
          </div>
          <div>
            <button
              className="button button--ghost"
              type="button"
              disabled={step <= 1 || mutation.isPending}
              onClick={() => setStep((current) => Math.max(1, current - 1))}
            >
              {t("Previous")}
            </button>
            {step < finalStep ? (
              <button
                className="button button--primary"
                type="button"
                onClick={() => setStep((current) => Math.min(finalStep, current + 1))}
              >
                {t("Next")}
              </button>
            ) : (
              <button
                className="button button--primary"
                type="button"
                disabled={!canCreate || mutation.isPending}
                onClick={() => mutation.mutate()}
              >
                {mutation.isPending
                  ? t("Pages_Data_ModPacks_Create_ActionCreating")
                  : t("Pages_Data_ModPacks_Create_ActionCreate")}
              </button>
            )}
          </div>
        </footer>
      </section>
    </section>
  );
}
