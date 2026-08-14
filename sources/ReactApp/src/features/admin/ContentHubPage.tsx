import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { FullUser } from "@/api/types";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { canManagePublicPages, canManageVereinDocuments } from "@/auth/claims";
import { PageHeader, StatePanel } from "@/components/primitives";

interface ContentArea {
  id: "website" | "verein";
  to: string;
  kickerKey: TranslationKey;
  titleKey: TranslationKey;
  descriptionKey: TranslationKey;
  scopeKey: TranslationKey;
  routeKey: TranslationKey;
  actionKey: TranslationKey;
}

export function contentHubAreas(user: FullUser | undefined): ContentArea[] {
  const areas: ContentArea[] = [];
  if (canManagePublicPages(user))
    areas.push({
      id: "website",
      to: "/command/content/pages",
      kickerKey: "Pages_Command_Content_HubWebsiteKicker",
      titleKey: "Pages_Command_Content_HubWebsiteTitle",
      descriptionKey: "Pages_Command_Content_HubWebsiteDescription",
      scopeKey: "Pages_Command_Content_HubWebsiteScope",
      routeKey: "Pages_Command_Content_HubWebsiteRoute",
      actionKey: "Pages_Command_Content_HubWebsiteAction",
    });
  if (canManageVereinDocuments(user))
    areas.push({
      id: "verein",
      to: "/command/verein/documents",
      kickerKey: "Pages_Command_Content_HubRecordsKicker",
      titleKey: "Pages_Command_Content_HubRecordsTitle",
      descriptionKey: "Pages_Command_Content_HubRecordsDescription",
      scopeKey: "Pages_Command_Content_HubRecordsScope",
      routeKey: "Pages_Command_Content_HubRecordsRoute",
      actionKey: "Pages_Command_Content_HubRecordsAction",
    });
  return areas;
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const areas = contentHubAreas(user);
  return (
    <section className="command-content-hub">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_ContentAndDocuments")}
        title={t("Pages_Command_Content_HubTitle")}
        description={t("Pages_Command_Content_HubDescription")}
      />
      {areas.length === 0 ? (
        <StatePanel kind="forbidden" title={t("Pages_Command_Content_HubNoAccessTitle")}>
          {t("Pages_Command_Content_HubNoAccessDescription")}
        </StatePanel>
      ) : (
        <div className="command-content-hub__grid" aria-label={t("Pages_Command_Content_HubAria")}>
          {areas.map((area) => (
            <article key={area.id} className="command-content-hub__card">
              <span className="kicker">{t(area.kickerKey)}</span>
              <h2>{t(area.titleKey)}</h2>
              <p>{t(area.descriptionKey)}</p>
              <ul>
                <li>{t(area.scopeKey)}</li>
                <li>{t(area.routeKey)}</li>
              </ul>
              <Link className="button button--gold" to={area.to}>
                {t(area.actionKey)}
              </Link>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
