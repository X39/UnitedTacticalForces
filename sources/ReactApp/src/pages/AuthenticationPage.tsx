import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { useParams, useSearchParams } from "react-router";
import { localReactReturnPath, useAuth } from "@/auth/auth";
import { PageHeader, StatePanel } from "@/components/primitives";

export function Component() {
  const { t } = useTranslation();
  const { action = "login" } = useParams();
  const [searchParams] = useSearchParams();
  const auth = useAuth();
  const logoutSubmitted = useRef(false);
  const returnUrl = localReactReturnPath(searchParams.get("returnUrl"), "/next/");
  const isLogout = action.toLocaleLowerCase() === "logout";

  useEffect(() => {
    if (!isLogout || logoutSubmitted.current) return;
    logoutSubmitted.current = true;
    auth.logout(returnUrl);
  }, [auth, isLogout, returnUrl]);

  if (isLogout)
    return (
      <div className="narrow-page">
        <StatePanel kind="loading" title={t("Shared_NavMenu_Logout")}>
          {t("Common_LoadingData")}
        </StatePanel>
      </div>
    );

  return (
    <div className="narrow-page authentication-page">
      <section className="panel authentication-panel">
        <PageHeader
          kicker={t("Shared_CommunityDialog_AccountAccessKicker")}
          title={t("Shared_CommunityDialog_SignInTitle")}
          description={t("Shared_CommunityDialog_SignInSubtitle")}
        />
        <div className="authentication-options">
          {auth.providersLoading && (
            <StatePanel kind="loading">{t("Common_LoadingData")}</StatePanel>
          )}
          {auth.providers.steam && (
            <button
              className="identity-button identity-button--steam"
              type="button"
              onClick={() => auth.login("steam", returnUrl)}
            >
              <span>Steam</span>
              <strong>{t("Shared_CommunityDialog_UseSteamIdentity")}</strong>
            </button>
          )}
          {auth.providers.discord && (
            <button
              className="identity-button identity-button--discord"
              type="button"
              onClick={() => auth.login("discord", returnUrl)}
            >
              <span>Discord</span>
              <strong>{t("Shared_CommunityDialog_UseDiscordIdentity")}</strong>
            </button>
          )}
          {!auth.providersLoading && !auth.providers.steam && !auth.providers.discord && (
            <p role="status">{t("Authentication_NoMethodsAvailable")}</p>
          )}
        </div>
      </section>
    </div>
  );
}
