import { ArrowExit20Regular, Dismiss24Regular, Navigation24Regular } from "@fluentui/react-icons";
import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, Outlet, useLocation } from "react-router";
import type { PublicConfiguration } from "@/app/config";
import { useDocumentTitle } from "@/app/documentTitle";
import { setCulture } from "@/app/i18n";
import { matchRoute, switchToClassic } from "@/app/routes";
import { identityService } from "@/api/services";
import { useAuth } from "@/auth/auth";
import { canAccessCommandCenter } from "@/auth/claims";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import {
  getCommandNavigationContext,
  getCommandNavigationGroups,
  isCommandNavigationItemActive,
} from "@/components/commandNavigation";
import { SelectField, type SelectOption } from "@/components/FormFields";
import { PageHeader } from "@/components/primitives";
import { PageStack } from "@/components/PageStack";
import { ProfileMenu } from "@/components/ProfileMenu";
import { CostConfirmationProvider } from "@/components/server/CostConfirmationProvider";
import { ForbiddenState, LoadingState } from "@/components/StateViews";
import { useDrawerFocus } from "@/components/useDrawerFocus";
import { useResponsiveNavigation } from "@/components/useResponsiveNavigation";
import { FleetLiveUpdatesProvider } from "@/features/hosting/FleetLiveUpdatesProvider";
import "@/components/commandCenter.css";
import "@/components/shellLayout.css";

/** The two interface languages; the labels are the language codes, which are the same in every language. */
const languageOptions: readonly SelectOption[] = [
  { value: "en", label: "EN" },
  { value: "de", label: "DE" },
];

export function CommandCenterLayout({ configuration }: { configuration: PublicConfiguration }) {
  const { t, i18n } = useTranslation();
  const auth = useAuth();
  const location = useLocation();
  const currentRouteIsCertified = matchRoute(location.pathname)?.certified === true;
  const [drawerOpen, setDrawerOpen] = useState(false);
  const drawerToggle = useRef<HTMLButtonElement>(null);
  const drawer = useRef<HTMLElement>(null);
  const closeButton = useRef<HTMLButtonElement>(null);
  useResponsiveNavigation(drawerOpen, setDrawerOpen);
  useDrawerFocus({
    open: drawerOpen,
    drawer,
    opener: drawerToggle,
    initialFocus: closeButton,
  });
  const currentContext = getCommandNavigationContext(location.pathname);
  const currentAreaKey = currentContext?.titleKey ?? "Shared_CommandCenter_CurrentArea_Overview";
  useDocumentTitle(t(currentAreaKey));

  if (auth.isLoading)
    return (
      <main className="command-access-state">
        <PageStack>
          <PageHeader title={t("Shared_CommandCenter_HeaderTitle")} />
          <LoadingState />
        </PageStack>
      </main>
    );

  if (!auth.isAuthenticated || !canAccessCommandCenter(auth.user))
    return (
      <main className="command-access-state">
        <PageStack>
          <PageHeader title={t("Shared_CommandCenter_HeaderTitle")} />
          <ForbiddenState title={t("Auto_ConfigurationAccessRequired_aa38db73")}>
            <p>{t("Pages_Command_Content_HubNoAccessDescription")}</p>
            <ActionLink variant="gold" to="/">
              {t("Shared_CommandCenter_BackToSite")}
            </ActionLink>
          </ForbiddenState>
        </PageStack>
      </main>
    );

  const groups = getCommandNavigationGroups(auth.user);
  const CurrentAreaIcon = currentContext?.item.icon;
  return (
    <CostConfirmationProvider>
      <FleetLiveUpdatesProvider configuration={configuration}>
        <div className="command-shell">
          <a className="skip-link" href="#command-content" inert={drawerOpen}>
            {t("React_UI_SkipContent")}
          </a>
          <aside
            ref={drawer}
            id="command-navigation"
            className={drawerOpen ? "command-nav command-nav--open" : "command-nav"}
          >
            <div className="command-nav__brand">
              <img src="/Insignia-48px.png" alt="" width="42" height="42" />
              <span>
                <small>{t("Shared_CommandCenter_BrandName")}</small>
                <strong>{t("Shared_CommandCenter_HeaderTitle")}</strong>
              </span>
              <ActionButton
                ref={closeButton}
                iconOnly
                variant="ghost"
                className="command-nav__close"
                aria-label={t("Shared_CommandCenter_CloseNavigationAria")}
                onClick={() => setDrawerOpen(false)}
              >
                <Dismiss24Regular />
              </ActionButton>
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
                        {...(active
                          ? { className: "active", "aria-current": "page" as const }
                          : {})}
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
            <ActionButton
              className="command-scrim"
              tabIndex={-1}
              aria-label={t("Shared_CommandCenter_CloseNavigationAria")}
              onClick={() => setDrawerOpen(false)}
            >
              {null}
            </ActionButton>
          )}
          <div className="command-main" inert={drawerOpen}>
            <header className="command-topbar">
              <ActionButton
                ref={drawerToggle}
                iconOnly
                variant="ghost"
                className="command-nav__toggle"
                aria-controls="command-navigation"
                aria-label={t("Shared_CommandCenter_OpenNavigationAria")}
                aria-expanded={drawerOpen}
                onClick={() => setDrawerOpen(true)}
              >
                <Navigation24Regular />
              </ActionButton>
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
                {configuration.Ui.ReactVariantEnabled && currentRouteIsCertified && (
                  <ActionButton
                    variant="ghost"
                    size="compact"
                    type="button"
                    onClick={switchToClassic}
                  >
                    {t("React_UI_UseClassic")}
                  </ActionButton>
                )}
                <ActionLink
                  variant="ghost"
                  size="compact"
                  className="command-topbar__site-link"
                  to="/"
                >
                  <ArrowExit20Regular /> <span>{t("Shared_CommandCenter_BackToSite")}</span>
                </ActionLink>
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
      </FleetLiveUpdatesProvider>
    </CostConfirmationProvider>
  );
}
