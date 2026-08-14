import {
  ArrowRight20Regular,
  BookOpen24Regular,
  CloudArrowUp24Regular,
  PeopleCommunity24Regular,
  ShieldTask24Regular,
} from "@fluentui/react-icons";
import type { CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { useSiteImage } from "@/app/siteImages";
import { PageHeader, StatusBadge } from "@/components/primitives";

export function Component() {
  const { t } = useTranslation();
  const heroImage = useSiteImage("unit-hero", "/media/generated/unit-training-assembly-hero.jpg");
  const values = [1, 2, 3, 4] as const;
  return (
    <div className="unit-page">
      <section
        className="editorial-hero editorial-hero--unit"
        style={{ "--hero-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
      >
        <div>
          <span className="kicker">{t("Pages_Unit_Index_Kicker")}</span>
          <h1>{t("Pages_Unit_Index_Title")}</h1>
          <p>{t("Pages_Unit_Index_HeroText")}</p>
          <div>
            <Link className="button button--gold" to="/join">
              {t("Pages_Unit_Index_JoinCommunity")}
            </Link>
            <Link className="button button--glass" to="/events/upcoming">
              {t("Pages_Unit_Index_SeeCurrentOperations")}
            </Link>
          </div>
        </div>
      </section>
      <section className="content-section">
        <PageHeader
          kicker={t("Pages_Unit_Index_ValuesKicker")}
          title={t("Pages_Unit_Index_ValuesTitle")}
        />
        <div className="value-cards value-cards--four">
          {values.map((value) => (
            <article key={value}>
              <span className="number-marker">0{value}</span>
              <h2>{t(`Pages_Unit_Index_Value${value}Title`)}</h2>
              <p>{t(`Pages_Unit_Index_Value${value}Body`)}</p>
            </article>
          ))}
        </div>
      </section>
      <section className="content-section split-feature">
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
      <section className="content-section">
        <PageHeader
          kicker={t("Pages_Unit_Index_CommunityResourcesKicker")}
          title={t("Pages_Unit_Index_CommunityResourcesTitle")}
        />
        <div className="resource-links">
          <Link to="/events/upcoming">
            <h3>{t("Pages_Unit_Index_OperationsBoardTitle")}</h3>
            <p>{t("Pages_Unit_Index_OperationsBoardBody")}</p>
            <ArrowRight20Regular />
          </Link>
          <Link to="/wiki">
            <h3>{t("Pages_Unit_Index_WikiTitle")}</h3>
            <p>{t("Pages_Unit_Index_WikiBody")}</p>
            <ArrowRight20Regular />
          </Link>
          <Link to="/field-reports">
            <h3>{t("Pages_Unit_Index_FieldReportsTitle")}</h3>
            <p>{t("Pages_Unit_Index_FieldReportsBody")}</p>
            <ArrowRight20Regular />
          </Link>
          <Link to="/verein/satzung">
            <BookOpen24Regular />
            <h3>{t("Pages_Verein_Index_SatzungTitle")}</h3>
            <p>{t("Pages_Verein_Index_SatzungBody")}</p>
            <ArrowRight20Regular />
          </Link>
        </div>
      </section>
    </div>
  );
}
