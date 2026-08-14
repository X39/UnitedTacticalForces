import {
  ArrowExit20Regular,
  Dismiss24Regular,
  Navigation24Regular,
  Person20Regular,
} from "@fluentui/react-icons";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, NavLink, Outlet, useLocation } from "react-router";
import type { PublicConfiguration } from "@/app/config";
import { useDocumentTitle } from "@/app/documentTitle";
import { setCulture } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { matchRoute, switchToClassic } from "@/app/routes";
import { useAuth } from "@/auth/auth";
import {
  canAccessCommandCenter,
  canAccessConfigurationProfiles,
  canAccessGameServers,
  canManageAuthority,
  canManageContent,
  canManageEvents,
  canManageModPacks,
  canManagePublicPages,
  canManageTerrains,
  canManageVereinLedger,
  canReadFieldReports,
  hasBaseClaim,
  hasClaim,
} from "@/auth/claims";
import { StatePanel } from "@/components/primitives";

interface CommandLink {
  to: string;
  labelKey: TranslationKey;
}

interface CommandGroup {
  labelKey: TranslationKey;
  links: CommandLink[];
}

function commandNavigation(user: ReturnType<typeof useAuth>["user"]): CommandGroup[] {
  const operations: CommandLink[] = [];
  if (canAccessGameServers(user))
    operations.push({ to: "/command/servers", labelKey: "Hosting_Nav_Servers" });
  if (canAccessConfigurationProfiles(user))
    operations.push({
      to: "/command/configuration-profiles",
      labelKey: "Shared_CommandCenter_Nav_CloudConfiguration",
    });
  if (canManageEvents(user))
    operations.push({ to: "/command/events", labelKey: "Shared_CommandCenter_Nav_Events" });

  const assets: CommandLink[] = [];
  if (canManageModPacks(user))
    assets.push({ to: "/command/mod-packs", labelKey: "Shared_CommandCenter_Nav_ModPacks" });
  if (canManageTerrains(user))
    assets.push({ to: "/command/terrains", labelKey: "Shared_CommandCenter_Nav_Terrains" });
  const canManageWikiMedia = canReadFieldReports(user);
  const canManagePages = canManagePublicPages(user);
  if (canManageWikiMedia)
    assets.push({
      to: "/command/field-reports",
      labelKey: "Shared_CommandCenter_Nav_FieldReports",
    });
  if (canManageWikiMedia || canManagePages)
    assets.push({
      to: "/command/content/gallery",
      labelKey: "Shared_CommandCenter_Nav_Gallery",
    });
  if (canManagePages)
    assets.push({
      to: "/command/content/images/home",
      labelKey: "Shared_CommandCenter_Nav_PublicImages",
    });

  const community: CommandLink[] = [];
  if (
    hasClaim(user, "adm", "adm:usr", "adm:verein") ||
    hasBaseClaim(user, "usr", "verein", "verein:members")
  )
    community.push({ to: "/command/members", labelKey: "Shared_CommandCenter_Nav_Members" });
  if (canManageAuthority(user)) {
    community.push(
      { to: "/command/roles", labelKey: "Shared_CommandCenter_Nav_Roles" },
      { to: "/command/claims", labelKey: "Shared_CommandCenter_Nav_Claims" },
    );
  }

  const governance: CommandLink[] = [];
  if (canManageContent(user))
    governance.push({
      to: "/command/content",
      labelKey: "Shared_CommandCenter_Nav_ContentAndDocuments",
    });
  if (
    hasClaim(user, "adm", "adm:usr", "adm:verein") ||
    hasBaseClaim(user, "usr", "verein", "verein:members")
  ) {
    governance.push(
      {
        to: "/command/verein/membership-requests",
        labelKey: "Shared_CommandCenter_Nav_VereinMembershipRequests",
      },
      {
        to: "/command/verein/settings",
        labelKey: "Shared_CommandCenter_Nav_VereinSettings",
      },
    );
  }
  if (canManageVereinLedger(user))
    governance.push({
      to: "/command/verein/kassenbuch",
      labelKey: "Shared_CommandCenter_Nav_VereinKassenbuch",
    });
  if (governance.length)
    governance.push({ to: "/command/settings", labelKey: "Shared_CommandCenter_Nav_Settings" });

  const finance: CommandLink[] = hasBaseClaim(user, "fin", "fin:view-all")
    ? [
        {
          to: "/command/hosting-finance",
          labelKey: "Shared_CommandCenter_Nav_HostingFinance",
        },
      ]
    : [];

  const groups: CommandGroup[] = [
    {
      labelKey: "Shared_CommandCenter_Group_CommandCenter",
      links: [{ to: "/command", labelKey: "Shared_CommandCenter_Nav_Overview" }],
    },
    { labelKey: "Shared_CommandCenter_Group_Operations", links: operations },
    { labelKey: "Shared_CommandCenter_Group_Finance", links: finance },
    { labelKey: "Shared_CommandCenter_Group_Assets", links: assets },
    { labelKey: "Shared_CommandCenter_Group_Community", links: community },
    { labelKey: "Shared_CommandCenter_Group_Governance", links: governance },
  ];
  return groups.filter((group) => group.links.length > 0);
}

export function CommandCenterLayout({ configuration }: { configuration: PublicConfiguration }) {
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const location = useLocation();
  const currentRouteIsCertified = matchRoute(location.pathname)?.certified === true;
  const [drawerOpen, setDrawerOpen] = useState(false);
  const commandPath = location.pathname.toLocaleLowerCase();
  const currentAreaKey =
    commandPath === "/command/settings"
      ? "Shared_CommandCenter_CurrentArea_Settings"
      : commandPath === "/command/claims"
        ? "Shared_CommandCenter_CurrentArea_Claims"
        : commandPath.startsWith("/command/members")
          ? "Shared_CommandCenter_Nav_Members"
          : commandPath === "/command/hosting-finance"
            ? "Shared_CommandCenter_Nav_HostingFinance"
            : commandPath === "/command/configuration-profiles"
              ? "Shared_CommandCenter_Nav_CloudConfiguration"
              : commandPath === "/command/servers"
                ? "Hosting_Nav_Servers"
                : commandPath.startsWith("/command/field-reports") ||
                    commandPath === "/command/wiki-media"
                  ? "Shared_CommandCenter_Nav_FieldReports"
                  : commandPath.startsWith("/command/events") || commandPath === "/events/all"
                    ? "Shared_CommandCenter_Nav_Events"
                    : commandPath.startsWith("/command/mod-packs") ||
                        commandPath.startsWith("/data/mod-packs")
                      ? commandPath === "/command/mod-packs" ||
                        commandPath.endsWith("/all") ||
                        commandPath.endsWith("/create")
                        ? "Shared_CommandCenter_CurrentArea_ModPacks"
                        : "Shared_CommandCenter_CurrentArea_ModPackDetail"
                      : commandPath.startsWith("/command/terrains") ||
                          commandPath.startsWith("/data/terrains")
                        ? "Shared_CommandCenter_CurrentArea_Terrains"
                        : commandPath.startsWith("/command/content")
                          ? "Shared_CommandCenter_Nav_ContentAndDocuments"
                          : "Shared_CommandCenter_CurrentArea_Overview";
  useDocumentTitle(t(currentAreaKey));

  if (auth.isLoading)
    return (
      <main className="command-access-state">
        <StatePanel kind="loading">{t("Common_LoadingData")}</StatePanel>
      </main>
    );

  if (!auth.isAuthenticated || !canAccessCommandCenter(auth.user))
    return (
      <main className="command-access-state">
        <StatePanel kind="forbidden" title={t("Auto_ConfigurationAccessRequired_aa38db73")}>
          <p>{t("Pages_Command_Content_HubNoAccessDescription")}</p>
          <Link className="button button--gold" to="/">
            {t("Shared_CommandCenter_BackToSite")}
          </Link>
        </StatePanel>
      </main>
    );

  const groups = commandNavigation(auth.user);
  return (
    <div className="command-shell">
      <a className="skip-link" href="#command-content">
        {t("React_UI_SkipContent")}
      </a>
      <aside
        id="command-navigation"
        className={drawerOpen ? "command-nav command-nav--open" : "command-nav"}
      >
        <div className="command-nav__brand">
          <img src="/Insignia-48px.png" alt="" width="42" height="42" />
          <span>
            <small>{t("Shared_CommandCenter_BrandName")}</small>
            <strong>{t("Shared_CommandCenter_HeaderTitle")}</strong>
          </span>
          <button
            className="icon-button command-nav__close"
            type="button"
            aria-label={t("Shared_CommandCenter_CloseNavigationAria")}
            onClick={() => setDrawerOpen(false)}
          >
            <Dismiss24Regular />
          </button>
        </div>
        <nav aria-label={t("Shared_CommandCenter_NavigationAria")}>
          {groups.map((group) => (
            <section key={group.labelKey} className="command-nav__group">
              <span>{t(group.labelKey)}</span>
              {group.links.map((link) => (
                <NavLink
                  key={link.to}
                  to={link.to}
                  end={link.to === "/command"}
                  onClick={() => setDrawerOpen(false)}
                >
                  {t(link.labelKey)}
                </NavLink>
              ))}
            </section>
          ))}
        </nav>
      </aside>
      {drawerOpen && (
        <button
          className="command-scrim"
          type="button"
          aria-label={t("Shared_CommandCenter_CloseNavigationAria")}
          onClick={() => setDrawerOpen(false)}
        />
      )}
      <div className="command-main">
        <header className="command-topbar">
          <button
            className="icon-button command-nav__toggle"
            type="button"
            aria-controls="command-navigation"
            aria-label={t("Shared_CommandCenter_OpenNavigationAria")}
            aria-expanded={drawerOpen}
            onClick={() => setDrawerOpen(true)}
          >
            <Navigation24Regular />
          </button>
          <div className="command-topbar__title">
            <span>{t("Shared_CommandCenter_HeaderTitle")}</span>
            <strong>{t(currentAreaKey)}</strong>
          </div>
          <div className="command-topbar__actions">
            <label className="language-select">
              <span className="sr-only">{t("React_UI_Language")}</span>
              <select
                value={i18n.resolvedLanguage?.startsWith("de") ? "de" : "en"}
                onChange={(event) => setCulture(event.target.value as "de" | "en")}
              >
                <option value="en">EN</option>
                <option value="de">DE</option>
              </select>
            </label>
            {configuration.Ui.ReactVariantEnabled && currentRouteIsCertified && (
              <button
                className="button button--ghost button--small"
                type="button"
                onClick={switchToClassic}
              >
                {t("React_UI_UseClassic")}
              </button>
            )}
            <Link className="button button--ghost button--small" to="/">
              <ArrowExit20Regular /> {t("Shared_CommandCenter_BackToSite")}
            </Link>
            <details className="profile-menu command-profile-menu">
              <summary>
                <Person20Regular /> <span>{auth.user?.nickname}</span>
              </summary>
              <div>
                <Link to="/settings">{t("Shared_NavMenu_UserSettings")}</Link>
                <button type="button" onClick={() => auth.logout()}>
                  {t("Shared_NavMenu_Logout")}
                </button>
              </div>
            </details>
          </div>
        </header>
        <main id="command-content" className="command-content" tabIndex={-1}>
          <Outlet context={configuration} />
        </main>
        <span className="command-route-context" aria-hidden="true">
          {location.pathname}
        </span>
      </div>
    </div>
  );
}
