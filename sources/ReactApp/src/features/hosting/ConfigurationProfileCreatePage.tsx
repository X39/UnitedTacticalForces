import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { formatResource } from "@/app/i18n";
import { useAuth } from "@/auth/auth";
import { ActionButton } from "@/components/ActionControl";
import { SelectField, TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import { PageHeader, useUnsavedChanges } from "@/components/primitives";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
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
      <PageStack>
        {!permissions.accessConfigurationProfiles || !permissions.createConfigurationProfiles ? (
          <ForbiddenState title={t("Hosting_ConfigurationProfiles_ViewOnlyTitle")}>
            {t("Hosting_ConfigurationProfiles_ViewOnlyDescription")}
          </ForbiddenState>
        ) : authLoading || catalog.isLoading ? (
          <LoadingState label={t("Hosting_ConfigurationProfiles_Loading")} />
        ) : catalog.error ? (
          <ErrorState
            title={t("Hosting_ConfigurationProfiles_LoadError").replace(": {0}", "")}
            onRetry={() => void catalog.refetch()}
          >
            {catalog.error.message}
          </ErrorState>
        ) : !catalog.data?.canCreate ? (
          <ForbiddenState title={t("Hosting_ConfigurationProfiles_ViewOnlyTitle")}>
            {t("Hosting_ConfigurationProfiles_ViewOnlyDescription")}
          </ForbiddenState>
        ) : games.length === 0 ? (
          <EmptyState title={t("Hosting_ConfigurationProfiles_NoGamesTitle")}>
            {t("Hosting_ConfigurationProfiles_NoGamesDescription")}
          </EmptyState>
        ) : (
          <>
            <nav
              className="next-hosting-profile-stepper"
              aria-label={t("React_ConfigurationProfiles_WizardProgress")}
            >
              {steps.map((key, index) => (
                <ActionButton
                  key={key}
                  variant="ghost"
                  aria-current={step === index ? "step" : undefined}
                  disabled={index > step}
                  onClick={() => setStep(index)}
                >
                  <span>{index + 1}</span>
                  {t(key)}
                </ActionButton>
              ))}
            </nav>
            {create.error && (
              <ErrorState
                title={t("Hosting_ConfigurationProfiles_OperationFailed").replace(": {0}", "")}
                onRetry={() => create.mutate()}
              >
                {create.error.message}
              </ErrorState>
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
                    <TextField
                      label={t("Hosting_ConfigurationProfiles_ProfileTitle")}
                      hint={t("Hosting_ConfigurationProfiles_ProfileTitleHint")}
                      value={title}
                      maxLength={256}
                      required
                      placeholder={t("Hosting_ConfigurationProfiles_ProfileTitlePlaceholder")}
                      onChange={(event) => setTitle(event.target.value)}
                    />
                    <SelectField
                      label={t("Hosting_ConfigurationProfiles_Game")}
                      hint={t("Hosting_ConfigurationProfiles_GameHint")}
                      value={activeGame}
                      onChange={(event) => changeGame(event.target.value)}
                      options={games.map((item) => ({
                        value: item.gameIdentifier,
                        label: gameLabel(item.gameIdentifier),
                      }))}
                    />
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
                    gameIdentifier={activeGame}
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
                  {formatResource(
                    t("React_ConfigurationProfiles_StepCount"),
                    step + 1,
                    steps.length,
                  )}
                </span>
                <div>
                  {step > 0 && (
                    <ActionButton variant="ghost" onClick={() => setStep(step - 1)}>
                      {t("React_ConfigurationProfiles_Previous")}
                    </ActionButton>
                  )}
                  {step === 0 ? (
                    <ActionButton
                      variant="gold"
                      disabled={!title.trim() || !activeGame}
                      onClick={() => setStep(1)}
                    >
                      {t("React_ConfigurationProfiles_Next")}
                    </ActionButton>
                  ) : step === 1 ? (
                    <ActionButton variant="gold" onClick={validateSettings}>
                      {t("React_ConfigurationProfiles_ReviewProfile")}
                    </ActionButton>
                  ) : (
                    <ActionButton
                      variant="gold"
                      disabled={create.isPending}
                      onClick={() => create.mutate()}
                    >
                      {create.isPending
                        ? t("Hosting_ConfigurationProfiles_Creating")
                        : t("Hosting_ConfigurationProfiles_Create")}
                    </ActionButton>
                  )}
                </div>
              </footer>
            </section>
          </>
        )}
      </PageStack>
    </div>
  );
}
