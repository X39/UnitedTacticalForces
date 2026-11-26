import { Navigation24Regular, ShieldLock24Regular } from "@fluentui/react-icons";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, NavLink, Outlet, useLocation } from "react-router";
import type { PublicConfiguration } from "@/app/config";
import { useDocumentTitle } from "@/app/documentTitle";
import { setCulture } from "@/app/i18n";
import { matchRoute, switchToClassic } from "@/app/routes";
import { canAccessCommandCenter } from "@/auth/claims";
import { identityService } from "@/api/services";
import { useAuth } from "@/auth/auth";
import { ActionButton } from "@/components/ActionControl";
import { SelectField, type SelectOption } from "@/components/FormFields";
import { ProfileMenu } from "@/components/ProfileMenu";
import { CostConfirmationProvider } from "@/components/server/CostConfirmationProvider";
import { useDrawerFocus } from "@/components/useDrawerFocus";
import { useResponsiveNavigation } from "@/components/useResponsiveNavigation";
import { FleetLiveUpdatesProvider } from "@/features/hosting/FleetLiveUpdatesProvider";
import "@/components/shellLayout.css";

const navigation = [
  ["/", "Shared_CommunityNav_Home"],
  ["/events/upcoming", "Shared_CommunityNav_Operations"],
  ["/unit", "Shared_CommunityNav_Unit"],
  ["/verein", "Shared_CommunityNav_Verein"],
  ["/wiki", "Shared_CommunityNav_Wiki"],
  ["/field-reports", "Shared_CommunityNav_FieldReports"],
  ["/join", "Shared_CommunityNav_Join"],
] as const;

/** The two interface languages; the labels are the language codes, which are the same in every language. */
const languageOptions: readonly SelectOption[] = [
  { value: "en", label: "EN" },
  { value: "de", label: "DE" },
];

export function CommunityLayout({ configuration }: { configuration: PublicConfiguration }) {
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const location = useLocation();
  const currentRouteIsCertified = matchRoute(location.pathname)?.certified === true;
  const [menuOpen, setMenuOpen] = useState(false);
  const [loginOpen, setLoginOpen] = useState(false);
  const loginDialog = useRef<HTMLDialogElement>(null);
  const menuToggle = useRef<HTMLButtonElement>(null);
  const menuNavigation = useRef<HTMLElement>(null);
  useResponsiveNavigation(menuOpen, setMenuOpen);
  useDrawerFocus({
    open: menuOpen,
    drawer: menuNavigation,
    opener: menuToggle,
    includeOpener: true,
  });
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
  const brandName = [
    t("Shared_CommunityBrand_NamePart1"),
    t("Shared_CommunityBrand_NamePart2"),
    t("Shared_CommunityBrand_NamePart3"),
  ].join(" ");
  const nav = [
    ...navigation,
    ...(canAccessCommandCenter(auth.user)
      ? [["/command", "Shared_CommunityNav_CommandCenter"] as const]
      : []),
  ];
  return (
    <CostConfirmationProvider>
      <FleetLiveUpdatesProvider configuration={configuration}>
        <div className="site-shell">
          <a className="skip-link" href="#main-content" inert={menuOpen}>
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
            <ActionButton
              ref={menuToggle}
              iconOnly
              variant="ghost"
              className="nav-toggle"
              aria-controls="community-navigation"
              aria-expanded={menuOpen}
              aria-label={t("Shared_CommunityNav_ToggleNavigationAria")}
              onClick={() => setMenuOpen((value) => !value)}
            >
              <Navigation24Regular />
            </ActionButton>
            <nav
              ref={menuNavigation}
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
              <div className="shell-language">
                <SelectField
                  label={<span className="sr-only">{t("React_UI_Language")}</span>}
                  value={i18n.resolvedLanguage?.startsWith("de") ? "de" : "en"}
                  options={languageOptions}
                  onChange={(event) => {
                    const language = event.target.value as "de" | "en";
                    setCulture(language);
                    // Fire and forget: the recorded language only decides the language of Discord and email cost alerts, so a failed
                    // request must neither show an error nor block the switch. A signed-out visitor has no record to write to.
                    if (auth.user)
                      void identityService.recordLocale(language).catch(() => undefined);
                  }}
                />
              </div>
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
            <ActionButton
              className="navigation-scrim"
              tabIndex={-1}
              aria-label={t("Shared_CommunityNav_CloseNavigationAria")}
              onClick={() => setMenuOpen(false)}
            >
              {null}
            </ActionButton>
          )}
          <main id="main-content" tabIndex={-1} inert={menuOpen}>
            <Outlet context={configuration} />
          </main>
          <footer className="site-footer" inert={menuOpen}>
            <div>
              <img src="/Insignia-48px.png" alt="" width="36" height="36" />
              <p>
                {brandName}
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
                <ActionButton variant="ghost" size="compact" onClick={switchToClassic}>
                  {t("React_UI_UseClassic")}
                </ActionButton>
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
                  <ActionButton
                    className="identity-button identity-button--steam"
                    onClick={() => auth.login("steam")}
                  >
                    Steam
                  </ActionButton>
                )}
                {auth.providers.discord && (
                  <ActionButton
                    className="identity-button identity-button--discord"
                    onClick={() => auth.login("discord")}
                  >
                    Discord
                  </ActionButton>
                )}
                {auth.providersLoading && <p role="status">{t("Common_LoadingData")}</p>}
                {!auth.providersLoading && !auth.providers.steam && !auth.providers.discord && (
                  <p>{t("React_UI_NoIdentityProviders")}</p>
                )}
              </div>
              <ActionButton variant="ghost" type="button" onClick={() => setLoginOpen(false)}>
                {t("Shared_CommunityDialog_Login_Dismiss")}
              </ActionButton>
            </form>
          </dialog>
          <span className="build-variant" title={`${t("React_UI_Beta")} · ${location.pathname}`}>
            <ShieldLock24Regular /> {t("React_UI_Beta")}
          </span>
        </div>
      </FleetLiveUpdatesProvider>
    </CostConfirmationProvider>
  );
}
