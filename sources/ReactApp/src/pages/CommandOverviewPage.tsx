import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { FullUser } from "@/api/types";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import {
  canAccessGameServers,
  canManageContent,
  canManageEvents,
  canManageGallery,
  canManageModPacks,
  canManageTerrains,
  canManageVereinLedger,
} from "@/auth/claims";
import { PageHeader } from "@/components/primitives";

interface OverviewLink {
  to: string;
  labelKey: TranslationKey;
}

export function commandOverviewLinks(user: FullUser | undefined): OverviewLink[] {
  return [
    canAccessGameServers(user) && {
      to: "/command/servers",
      labelKey: "Hosting_Nav_Servers",
    },
    canManageEvents(user) && {
      to: "/command/events",
      labelKey: "Shared_CommandCenter_Nav_Events",
    },
    canManageModPacks(user) && {
      to: "/command/mod-packs",
      labelKey: "Shared_CommandCenter_Nav_ModPacks",
    },
    canManageTerrains(user) && {
      to: "/command/terrains",
      labelKey: "Shared_CommandCenter_Nav_Terrains",
    },
    canManageContent(user) && {
      to: "/command/content",
      labelKey: "Shared_CommandCenter_Nav_ContentAndDocuments",
    },
    canManageGallery(user) && {
      to: "/command/content/gallery",
      labelKey: "Shared_CommandCenter_Nav_Gallery",
    },
    canManageVereinLedger(user) && {
      to: "/command/verein/kassenbuch",
      labelKey: "Shared_CommandCenter_Nav_VereinKassenbuch",
    },
  ].filter((link): link is OverviewLink => Boolean(link));
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const links = commandOverviewLinks(user);
  return (
    <section className="command-overview-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_Overview")}
        title={t("Shared_CommandCenter_HeaderTitle")}
        description={t("Shared_CommandCenter_NavigationAria")}
      />
      <div className="command-overview-grid">
        {links.map((link) => (
          <Link key={link.to} className="command-overview-card" to={link.to}>
            <span>{t(link.labelKey)}</span>
            <strong aria-hidden="true">→</strong>
          </Link>
        ))}
      </div>
    </section>
  );
}
