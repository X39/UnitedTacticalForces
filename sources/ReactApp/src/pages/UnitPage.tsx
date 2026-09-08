import {
  ArrowRight20Regular,
  BookOpen24Regular,
  CalendarLtr24Regular,
  CheckmarkCircle24Regular,
  CloudArrowUp24Regular,
  PeopleCommunity24Regular,
  ShieldTask24Regular,
} from "@fluentui/react-icons";
import type { CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { useSiteImage } from "@/app/siteImages";
import { ActionLink } from "@/components/ActionControl";
import { PublicHero } from "@/components/PublicHero";
import { PageHeader, StatusBadge } from "@/components/primitives";

export function Component() {
  const { t } = useTranslation();
  const heroImage = useSiteImage("unit-hero", "/media/generated/unit-training-assembly-hero.jpg");
  const values = [1, 2, 3, 4] as const;
  const process = [
    {
      icon: <CalendarLtr24Regular />,
      title: t("Pages_Unit_Index_GovernanceStep1Label"),
      body: t("Pages_Unit_Index_GovernanceStep1Body"),
    },
    {
      icon: <PeopleCommunity24Regular />,
      title: t("Pages_Unit_Index_GovernanceStep2Label"),
      body: t("Pages_Unit_Index_GovernanceStep2Body"),
    },
    {
      icon: <CheckmarkCircle24Regular />,
      title: t("Pages_Unit_Index_GovernanceStep3Label"),
      body: t("Pages_Unit_Index_GovernanceStep3Body"),
    },
  ];

  return (
    <div className="unit-page public-page">
      <PublicHero
        className="editorial-hero"
        style={{ "--hero-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
        kicker={t("Pages_Unit_Index_Kicker")}
        title={t("Pages_Unit_Index_Title")}
        description={t("Pages_Unit_Index_HeroText")}
        actions={
          <>
            <ActionLink variant="gold" to="/events/upcoming">
              {t("Pages_Unit_Index_ViewOperations")}
            </ActionLink>
            <ActionLink variant="glass" to="/join">
              {t("Pages_Unit_Index_JoinCommunity")}
            </ActionLink>
          </>
        }
        asideClassName="operating-model-panel"
        asideLabel={t("Pages_Unit_Index_OperatingModelLabel")}
        aside={
          <>
            <div className="public-hero__panel-heading">
              <PeopleCommunity24Regular />
              <div>
                <span>{t("Pages_Unit_Index_OperatingModel")}</span>
                <strong>{t("Pages_Unit_Index_OperatingModelValue")}</strong>
              </div>
            </div>
            <dl className="public-hero__status-list">
              <div>
                <dt>{t("Pages_Unit_Index_MissionAccessLabel")}</dt>
                <dd>{t("Pages_Unit_Index_MissionAccessValue")}</dd>
              </div>
              <div>
                <dt>{t("Pages_Unit_Index_CommunityCommsLabel")}</dt>
                <dd>{t("Pages_Unit_Index_CommunityCommsValue")}</dd>
              </div>
              <div>
                <dt>{t("Pages_Unit_Index_OnboardingLabel")}</dt>
                <dd>{t("Pages_Unit_Index_OnboardingValue")}</dd>
              </div>
            </dl>
          </>
        }
      />

      <section className="content-section public-section operating-model-section">
        <PageHeader
          kicker={t("Pages_Unit_Index_HowUtfWorks")}
          title={t("Pages_Unit_Index_OpenOperations")}
          headingLevel={2}
        />
        <div className="operating-model-grid">
          <article className="operating-model-statement">
            <h3>{t("Pages_Unit_Index_ParticipationHeadline")}</h3>
            <p>{t("Pages_Unit_Index_ParticipationBody1")}</p>
            <p>{t("Pages_Unit_Index_ParticipationBody2")}</p>
          </article>
          <ol className="public-process" aria-label={t("Pages_Unit_Index_GovernanceFlowLabel")}>
            {process.map((step, index) => (
              <li key={step.title}>
                <span className="public-process__number">0{index + 1}</span>
                <span className="public-process__icon" aria-hidden="true">
                  {step.icon}
                </span>
                <div>
                  <strong>{step.title}</strong>
                  <p>{step.body}</p>
                </div>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <section className="content-section public-section">
        <PageHeader
          kicker={t("Pages_Unit_Index_CommunityResourcesKicker")}
          title={t("Pages_Unit_Index_CommunityResourcesTitle")}
          headingLevel={2}
        />
        <div className="resource-links resource-links--three">
          <Link to="/events/upcoming">
            <CalendarLtr24Regular />
            <StatusBadge tone="success">{t("Pages_Unit_Index_StateActive")}</StatusBadge>
            <h3>{t("Pages_Unit_Index_OperationsBoardTitle")}</h3>
            <p>{t("Pages_Unit_Index_OperationsBoardBody")}</p>
            <ArrowRight20Regular />
          </Link>
          <Link to="/field-reports">
            <ShieldTask24Regular />
            <StatusBadge tone="success">{t("Pages_Unit_Index_StateActive")}</StatusBadge>
            <h3>{t("Pages_Unit_Index_FieldReportsTitle")}</h3>
            <p>{t("Pages_Unit_Index_FieldReportsBody")}</p>
            <ArrowRight20Regular />
          </Link>
          <Link to="/wiki">
            <BookOpen24Regular />
            <StatusBadge tone="success">{t("Pages_Unit_Index_StateActive")}</StatusBadge>
            <h3>{t("Pages_Unit_Index_WikiTitle")}</h3>
            <p>{t("Pages_Unit_Index_WikiBody")}</p>
            <ArrowRight20Regular />
          </Link>
        </div>
      </section>

      <section className="content-section public-section values-section">
        <PageHeader
          kicker={t("Pages_Unit_Index_ValuesKicker")}
          title={t("Pages_Unit_Index_ValuesTitle")}
          headingLevel={2}
        />
        <div className="value-cards value-cards--four">
          {values.map((value) => (
            <article key={value}>
              <span className="number-marker">0{value}</span>
              <h3>{t(`Pages_Unit_Index_Value${value}Title`)}</h3>
              <p>{t(`Pages_Unit_Index_Value${value}Body`)}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="content-section public-section split-feature">
        <div>
          <span className="kicker">{t("Pages_Unit_Index_InfrastructureKicker")}</span>
          <h2>{t("Pages_Unit_Index_InfrastructureTitle")}</h2>
          <p>{t("Pages_Unit_Index_InfrastructureBody")}</p>
          <StatusBadge tone="info">{t("Pages_Unit_Index_ControlPlaneFoundation")}</StatusBadge>
        </div>
        <div className="capability-list">
          <article>
            <CloudArrowUp24Regular />
            <div>
              <h3>{t("Pages_Unit_Index_InfrastructureCardOperations")}</h3>
              <p>{t("Pages_Unit_Index_InfrastructureCardOperationsBody")}</p>
            </div>
          </article>
          <article>
            <ShieldTask24Regular />
            <div>
              <h3>{t("Pages_Unit_Index_InfrastructureCardJobs")}</h3>
              <p>{t("Pages_Unit_Index_InfrastructureCardJobsBody")}</p>
            </div>
          </article>
          <article>
            <PeopleCommunity24Regular />
            <div>
              <h3>{t("Pages_Unit_Index_InfrastructureCardSubCommunities")}</h3>
              <p>{t("Pages_Unit_Index_InfrastructureCardSubCommunitiesBody")}</p>
            </div>
          </article>
        </div>
      </section>
    </div>
  );
}
