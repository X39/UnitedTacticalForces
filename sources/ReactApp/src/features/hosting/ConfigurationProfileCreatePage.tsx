import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { PageHeader, StatePanel, useUnsavedChanges } from "@/components/primitives";
import { getHostingPermissions } from "@/features/hosting/authorization";
import {
  ConfigurationProfileFields,
  gameLabel,
} from "@/features/hosting/ConfigurationProfileFields";
import {
  buildConfigurationValues,
  configurationDraftValue,
  validateConfigurationDraft,
  type ConfigurationValidationIssue,
} from "@/features/hosting/model";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";

const steps = [
  "React_ConfigurationProfiles_WizardBasics",
  "React_ConfigurationProfiles_WizardConfiguration",
  "React_ConfigurationProfiles_WizardReview",
] as const;

export function ConfigurationProfileCreatePage() {
  const { t } = useTranslation();
  const { user, isLoading: authLoading } = useAuth();
  const permissions = getHostingPermissions(user);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const catalog = useQuery({
    queryKey: hostingQueryKeys.profileCatalog,
    queryFn: ({ signal }) => hostingService.profileCatalog(signal),
    enabled: permissions.accessConfigurationProfiles,
  });
  const games = useMemo(() => catalog.data?.games ?? [], [catalog.data?.games]);
  const [step, setStep] = useState(0);
  const [title, setTitle] = useState("");
  const [game, setGame] = useState("");
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [issues, setIssues] = useState<ConfigurationValidationIssue[]>([]);
  const [allowNavigation, setAllowNavigation] = useState(false);
  const activeGame = game || games[0]?.gameIdentifier || "";
  const definitions = useMemo(
    () =>
      (games.find((item) => item.gameIdentifier === activeGame)?.definitions ?? []).filter(
        (definition) => !definition.sensitive,
      ),
    [activeGame, games],
  );
  const defaults = useMemo(
    () =>
      Object.fromEntries(
        definitions.map((definition) => [
          definition.key,
          configurationDraftValue(definition, undefined),
        ]),
      ),
    [definitions],
  );
  const effectiveDraft = { ...defaults, ...draft };
  const dirty = title.length > 0 || game.length > 0 || Object.keys(draft).length > 0;
  useUnsavedChanges(dirty && !allowNavigation);

  const create = useMutation({
    mutationFn: () =>
      hostingService.createProfile(
        title.trim(),
        activeGame,
        buildConfigurationValues(definitions, effectiveDraft),
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: hostingQueryKeys.profiles });
      setAllowNavigation(true);
    },
  });

  useEffect(() => {
    if (!allowNavigation || !create.data) return;
    navigate(`/command/configuration-profiles/${create.data.primaryKey}`, {
      replace: true,
      state: {
        message: formatResource(t("Hosting_ConfigurationProfiles_Created"), create.data.title),
      },
    });
  }, [allowNavigation, create.data, navigate, t]);

  const validateSettings = () => {
    const nextIssues = validateConfigurationDraft(definitions, effectiveDraft);
    setIssues(nextIssues);
    if (nextIssues.length === 0) setStep(2);
  };
  const changeGame = (value: string) => {
    if (
      Object.keys(draft).length > 0 &&
      !window.confirm(t("React_ConfigurationProfiles_ChangeGameWarning"))
    )
      return;
    setGame(value);
    setDraft({});
    setIssues([]);
  };

  return (
    <div className="next-hosting-page next-hosting-profile-create-page">
      <Link className="next-hosting-back-link" to="/command/configuration-profiles">
        ← {t("React_ConfigurationProfiles_BackToProfiles")}
      </Link>
      <PageHeader
        kicker={t("Hosting_ConfigurationProfiles_NewBaseline")}
        title={t("Hosting_ConfigurationProfiles_CreateTitle")}
        description={t("Hosting_ConfigurationProfiles_CreateDescription")}
      />
      {!permissions.accessConfigurationProfiles || !permissions.createConfigurationProfiles ? (
        <StatePanel kind="forbidden" title={t("Hosting_ConfigurationProfiles_ViewOnlyTitle")}>
          {t("Hosting_ConfigurationProfiles_ViewOnlyDescription")}
        </StatePanel>
      ) : authLoading || catalog.isLoading ? (
        <StatePanel kind="loading">{t("Hosting_ConfigurationProfiles_Loading")}</StatePanel>
      ) : catalog.error ? (
        <StatePanel
          kind="error"
          title={t("Hosting_ConfigurationProfiles_LoadError").replace(": {0}", "")}
          action={
            <button
              className="button button--ghost"
              type="button"
              onClick={() => void catalog.refetch()}
            >
              {t("Hosting_ConfigurationProfiles_Retry")}
            </button>
          }
        >
          {catalog.error.message}
        </StatePanel>
      ) : !catalog.data?.canCreate ? (
        <StatePanel kind="forbidden" title={t("Hosting_ConfigurationProfiles_ViewOnlyTitle")}>
          {t("Hosting_ConfigurationProfiles_ViewOnlyDescription")}
        </StatePanel>
      ) : games.length === 0 ? (
        <StatePanel kind="empty" title={t("Hosting_ConfigurationProfiles_NoGamesTitle")}>
          {t("Hosting_ConfigurationProfiles_NoGamesDescription")}
        </StatePanel>
      ) : (
        <>
          <nav
            className="next-hosting-profile-stepper"
            aria-label={t("React_ConfigurationProfiles_WizardProgress")}
          >
            {steps.map((key, index) => (
              <button
                key={key}
                type="button"
                aria-current={step === index ? "step" : undefined}
                disabled={index > step}
                onClick={() => setStep(index)}
              >
                <span>{index + 1}</span>
                {t(key)}
              </button>
            ))}
          </nav>
          {create.error && (
            <StatePanel
              kind="error"
              title={t("Hosting_ConfigurationProfiles_OperationFailed").replace(": {0}", "")}
            >
              {create.error.message}
            </StatePanel>
          )}
          <section className="next-hosting-panel next-hosting-profile-wizard">
            {step === 0 && (
              <div className="next-hosting-profile-wizard-step">
                <div>
                  <span className="kicker">{t("React_ConfigurationProfiles_StepOne")}</span>
                  <h2>{t("React_ConfigurationProfiles_WizardBasics")}</h2>
                  <p>{t("React_ConfigurationProfiles_BasicsDescription")}</p>
                </div>
                <div className="next-hosting-profile-meta">
                  <label className="field" htmlFor="configuration-profile-title">
                    <span>{t("Hosting_ConfigurationProfiles_ProfileTitle")}</span>
                    <input
                      id="configuration-profile-title"
                      aria-label={t("Hosting_ConfigurationProfiles_ProfileTitle")}
                      value={title}
                      maxLength={256}
                      required
                      placeholder={t("Hosting_ConfigurationProfiles_ProfileTitlePlaceholder")}
                      onChange={(event) => setTitle(event.target.value)}
                    />
                    <small>{t("Hosting_ConfigurationProfiles_ProfileTitleHint")}</small>
                  </label>
                  <label className="field" htmlFor="configuration-profile-game">
                    <span>{t("Hosting_ConfigurationProfiles_Game")}</span>
                    <select
                      id="configuration-profile-game"
                      aria-label={t("Hosting_ConfigurationProfiles_Game")}
                      value={activeGame}
                      onChange={(event) => changeGame(event.target.value)}
                    >
                      {games.map((item) => (
                        <option key={item.gameIdentifier} value={item.gameIdentifier}>
                          {gameLabel(item.gameIdentifier)}
                        </option>
                      ))}
                    </select>
                    <small>{t("Hosting_ConfigurationProfiles_GameHint")}</small>
                  </label>
                </div>
              </div>
            )}
            {step === 1 && (
              <div className="next-hosting-profile-wizard-step">
                <div>
                  <span className="kicker">{t("React_ConfigurationProfiles_StepTwo")}</span>
                  <h2>{t("React_ConfigurationProfiles_WizardConfiguration")}</h2>
                  <p>
                    {formatResource(
                      t("Hosting_ConfigurationProfiles_SettingCount"),
                      definitions.length,
                    )}
                  </p>
                </div>
                <ConfigurationProfileFields
                  definitions={definitions}
                  values={effectiveDraft}
                  issues={issues}
                  onChange={(key, value) => setDraft((current) => ({ ...current, [key]: value }))}
                />
              </div>
            )}
            {step === 2 && (
              <div className="next-hosting-profile-wizard-step">
                <div>
                  <span className="kicker">{t("React_ConfigurationProfiles_StepThree")}</span>
                  <h2>{t("React_ConfigurationProfiles_WizardReview")}</h2>
                  <p>{t("React_ConfigurationProfiles_ReviewDescription")}</p>
                </div>
                <dl className="next-hosting-profile-review-meta">
                  <div>
                    <dt>{t("Hosting_ConfigurationProfiles_ProfileTitle")}</dt>
                    <dd>{title.trim()}</dd>
                  </div>
                  <div>
                    <dt>{t("Hosting_ConfigurationProfiles_Game")}</dt>
                    <dd>{gameLabel(activeGame)}</dd>
                  </div>
                </dl>
                <div className="next-hosting-profile-review-values">
                  {definitions.map((definition) => {
                    const value = effectiveDraft[definition.key] ?? "";
                    return (
                      <div key={definition.key}>
                        <span>
                          <strong>{definition.displayName}</strong>
                          <small>{definition.targetPath}</small>
                        </span>
                        <span>
                          {definition.sensitive && value
                            ? t("React_ConfigurationProfiles_Configured")
                            : value || t("Hosting_ConfigurationProfiles_NotSet")}
                        </span>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
            <footer className="next-hosting-profile-wizard-actions">
              <span>
                {formatResource(t("React_ConfigurationProfiles_StepCount"), step + 1, steps.length)}
              </span>
              <div>
                {step > 0 && (
                  <button
                    className="button button--ghost"
                    type="button"
                    onClick={() => setStep(step - 1)}
                  >
                    {t("React_ConfigurationProfiles_Previous")}
                  </button>
                )}
                {step === 0 ? (
                  <button
                    className="button button--primary"
                    type="button"
                    disabled={!title.trim() || !activeGame}
                    onClick={() => setStep(1)}
                  >
                    {t("React_ConfigurationProfiles_Next")}
                  </button>
                ) : step === 1 ? (
                  <button
                    className="button button--primary"
                    type="button"
                    onClick={validateSettings}
                  >
                    {t("React_ConfigurationProfiles_ReviewProfile")}
                  </button>
                ) : (
                  <button
                    className="button button--primary"
                    type="button"
                    disabled={create.isPending}
                    onClick={() => create.mutate()}
                  >
                    {create.isPending
                      ? t("Hosting_ConfigurationProfiles_Creating")
                      : t("Hosting_ConfigurationProfiles_Create")}
                  </button>
                )}
              </div>
            </footer>
          </section>
        </>
      )}
    </div>
  );
}
