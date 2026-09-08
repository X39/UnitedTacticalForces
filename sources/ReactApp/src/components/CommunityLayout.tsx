import { Navigation24Regular, ShieldLock24Regular } from "@fluentui/react-icons";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, NavLink, Outlet, useLocation } from "react-router";
import type { PublicConfiguration } from "@/app/config";
import { useDocumentTitle } from "@/app/documentTitle";
import { setCulture } from "@/app/i18n";
import { matchRoute, switchToClassic } from "@/app/routes";
import { canAccessCommandCenter } from "@/auth/claims";
import { useAuth } from "@/auth/auth";
import { ActionButton } from "@/components/ActionControl";
import { ProfileMenu } from "@/components/ProfileMenu";
import { useResponsiveNavigation } from "@/components/useResponsiveNavigation";

const navigation = [
  ["/", "Shared_CommunityNav_Home"],
  ["/events/upcoming", "Shared_CommunityNav_Operations"],
  ["/unit", "Shared_CommunityNav_Unit"],
  ["/verein", "Shared_CommunityNav_Verein"],
  ["/wiki", "Shared_CommunityNav_Wiki"],
  ["/field-reports", "Shared_CommunityNav_FieldReports"],
  ["/join", "Shared_CommunityNav_Join"],
] as const;

export function CommunityLayout({ configuration }: { configuration: PublicConfiguration }) {
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const location = useLocation();
  const currentRouteIsCertified = matchRoute(location.pathname)?.certified === true;
  const [menuOpen, setMenuOpen] = useState(false);
  const [loginOpen, setLoginOpen] = useState(false);
  const loginDialog = useRef<HTMLDialogElement>(null);
  useResponsiveNavigation(menuOpen, setMenuOpen);
  useEffect(() => {
    const dialog = loginDialog.current;
    if (!dialog) return;
    if (loginOpen && !dialog.open) dialog.showModal();
    if (!loginOpen && dialog.open) dialog.close();
  }, [loginOpen]);
  const pathname = location.pathname.toLocaleLowerCase();
  const titleKey =
    pathname === "/"
      ? "Pages_Public_Home_PageTitle"
      : pathname === "/events/upcoming"
        ? "Auto_UpcomingOperations_f2355c40"
        : pathname === "/field-reports" || pathname === "/media"
          ? "Pages_Public_Media_PageTitle"
          : pathname.startsWith("/field-reports/") || pathname.startsWith("/media/")
            ? undefined
            : pathname === "/wiki" || pathname.startsWith("/wiki/")
              ? "Pages_Wiki_Index_PageTitle"
              : pathname === "/unit"
                ? "Pages_Unit_Index_PageTitle"
                : pathname === "/gdpr"
                  ? "Pages_Public_GDPR_PageTitle"
                  : pathname === "/join" || pathname === "/members"
                    ? "Pages_Public_Join_PageTitle"
                    : pathname.endsWith("/satzung")
                      ? "Pages_Unit_Satzung_Index_PageTitle"
                      : pathname.endsWith("/beschluesse")
                        ? "Pages_Unit_Beschluesse_Index_PageTitle"
                        : /^\/(verein|unit)\/beschluesse\//.test(pathname)
                          ? undefined
                          : pathname === "/verein"
                            ? "Pages_Verein_Index_PageTitle"
                            : "Pages_Public_Home_PageTitle";
  useDocumentTitle(titleKey ? t(titleKey) : undefined);
  const nav = [
    ...navigation,
    ...(canAccessCommandCenter(auth.user)
      ? [["/command", "Shared_CommunityNav_CommandCenter"] as const]
      : []),
  ];
  return (
    <div className="site-shell">
      <a className="skip-link" href="#main-content">
        {t("React_UI_SkipContent")}
      </a>
      <header className="site-header">
        <Link className="brand" to="/" aria-label={t("Shared_CommunityNav_Home")}>
          <img src="/Insignia-48px.png" alt="" width="44" height="44" />
          <span className="brand__utf">UTF</span>
          <span className="brand__divider" aria-hidden="true" />
          <span className="brand__stack">
            <span>{t("Shared_CommunityBrand_NamePart1")}</span>
            <span>{t("Shared_CommunityBrand_NamePart2")}</span>
            <span>{t("Shared_CommunityBrand_NamePart3")}</span>
          </span>
        </Link>
        <button
          className="icon-button nav-toggle"
          type="button"
          aria-controls="community-navigation"
          aria-expanded={menuOpen}
          aria-label={t("Shared_CommunityNav_ToggleNavigationAria")}
          onClick={() => setMenuOpen((value) => !value)}
        >
          <Navigation24Regular />
        </button>
        <nav
          id="community-navigation"
          className={menuOpen ? "primary-nav primary-nav--open" : "primary-nav"}
          aria-label={t("Shared_CommunityNav_Aria")}
        >
          {nav.map(([to, key]) => (
            <NavLink key={to} to={to} end={to === "/"} onClick={() => setMenuOpen(false)}>
              {t(key)}
            </NavLink>
          ))}
        </nav>
        <div className="header-actions">
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
          {auth.isAuthenticated ? (
            <ProfileMenu mode="community" />
          ) : (
            <ActionButton
              variant="gold"
              size="compact"
              type="button"
              onClick={() => setLoginOpen(true)}
            >
              {t("Shared_CommunityNav_Login")}
            </ActionButton>
          )}
        </div>
      </header>
      {menuOpen && (
        <button
          className="navigation-scrim"
          type="button"
          aria-label={t("Shared_CommunityNav_ToggleNavigationAria")}
          onClick={() => setMenuOpen(false)}
        />
      )}
      <main id="main-content" tabIndex={-1}>
        <Outlet context={configuration} />
      </main>
      <footer className="site-footer">
        <div>
          <img src="/Insignia-48px.png" alt="" width="36" height="36" />
          <p>
            United Tactical Forces
            <br />
            <small>{t("React_UI_FooterTagline")}</small>
          </p>
        </div>
        <nav aria-label={t("Shared_CommunityFooterLinksAria")}>
          <Link to="/gdpr">{t("Shared_NavMenu_GDPR")}</Link>
          {configuration.Urls.TeamSpeak3 && (
            <a href={configuration.Urls.TeamSpeak3}>{t("Shared_NavMenu_TeamSpeak3")}</a>
          )}
          {configuration.Urls.Discord && (
            <a href={configuration.Urls.Discord}>{t("Shared_NavMenu_Discord")}</a>
          )}
          {configuration.Ui.ReactVariantEnabled && currentRouteIsCertified && (
            <button type="button" onClick={switchToClassic}>
              {t("React_UI_UseClassic")}
            </button>
          )}
        </nav>
      </footer>
      <dialog
        ref={loginDialog}
        className="dialog login-dialog"
        aria-labelledby="login-dialog-title"
        onCancel={() => setLoginOpen(false)}
        onClose={() => setLoginOpen(false)}
      >
        <form method="dialog">
          <span className="kicker">{t("Shared_CommunityDialog_AccountAccessKicker")}</span>
          <h2 id="login-dialog-title">{t("Shared_CommunityDialog_SignInTitle")}</h2>
          <p>{t("Shared_CommunityDialog_SignInSubtitle")}</p>
          <div className="login-options">
            {auth.providers.steam && (
              <button
                className="identity-button identity-button--steam"
                type="button"
                onClick={() => auth.login("steam")}
              >
                Steam
              </button>
            )}
            {auth.providers.discord && (
              <button
                className="identity-button identity-button--discord"
                type="button"
                onClick={() => auth.login("discord")}
              >
                Discord
              </button>
            )}
            {auth.providersLoading && <p role="status">{t("Common_LoadingData")}</p>}
            {!auth.providersLoading && !auth.providers.steam && !auth.providers.discord && (
              <p>{t("React_UI_NoIdentityProviders")}</p>
            )}
          </div>
          <ActionButton variant="ghost" type="button" onClick={() => setLoginOpen(false)}>
            {t("Shared_CommunityDialog_Cancel")}
          </ActionButton>
        </form>
      </dialog>
      <span className="build-variant" title={`${t("React_UI_Beta")} · ${location.pathname}`}>
        <ShieldLock24Regular /> {t("React_UI_Beta")}
      </span>
    </div>
  );
}
