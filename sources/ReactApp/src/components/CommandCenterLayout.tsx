import { ArrowExit20Regular, Dismiss24Regular, Navigation24Regular } from "@fluentui/react-icons";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, Outlet, useLocation } from "react-router";
import type { PublicConfiguration } from "@/app/config";
import { useDocumentTitle } from "@/app/documentTitle";
import { setCulture } from "@/app/i18n";
import { matchRoute, switchToClassic } from "@/app/routes";
import { useAuth } from "@/auth/auth";
import { canAccessCommandCenter } from "@/auth/claims";
import {
  getCommandNavigationContext,
  getCommandNavigationGroups,
  isCommandNavigationItemActive,
} from "@/components/commandNavigation";
import { StatePanel } from "@/components/primitives";
import { ProfileMenu } from "@/components/ProfileMenu";
import "@/components/commandCenter.css";

export function CommandCenterLayout({ configuration }: { configuration: PublicConfiguration }) {
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const location = useLocation();
  const currentRouteIsCertified = matchRoute(location.pathname)?.certified === true;
  const [drawerOpen, setDrawerOpen] = useState(false);
  const currentContext = getCommandNavigationContext(location.pathname);
  const currentAreaKey = currentContext?.titleKey ?? "Shared_CommandCenter_CurrentArea_Overview";
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

  const groups = getCommandNavigationGroups(auth.user);
  const CurrentAreaIcon = currentContext?.item.icon;
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
            <section key={group.id} className="command-nav__group">
              <span className="command-nav__group-label">{t(group.labelKey)}</span>
              {group.items.map((item) => {
                const active = isCommandNavigationItemActive(item, location.pathname);
                const ItemIcon = item.icon;
                return (
                  <Link
                    key={item.id}
                    to={item.to}
                    {...(active ? { className: "active", "aria-current": "page" as const } : {})}
                    onClick={() => setDrawerOpen(false)}
                  >
                    <ItemIcon aria-hidden="true" />
                    <span>{t(item.labelKey)}</span>
                  </Link>
                );
              })}
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
          {CurrentAreaIcon && (
            <span className="command-topbar__area-icon" aria-hidden="true">
              <CurrentAreaIcon />
            </span>
          )}
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
            <Link className="button button--ghost button--small command-topbar__site-link" to="/">
              <ArrowExit20Regular /> <span>{t("Shared_CommandCenter_BackToSite")}</span>
            </Link>
            <ProfileMenu mode="command" />
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
