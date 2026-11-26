import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { useParams, useSearchParams } from "react-router";
import { localReactReturnPath, useAuth } from "@/auth/auth";
import { ActionButton } from "@/components/ActionControl";
import { PageStack } from "@/components/PageStack";
import { PageHeader } from "@/components/primitives";
import { LoadingState } from "@/components/StateViews";
import "@/components/shellLayout.css";

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
        <PageStack>
          <PageHeader title={t("Shared_NavMenu_Logout")} />
          <LoadingState />
        </PageStack>
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
        <PageStack className="authentication-options">
          {auth.providersLoading && <LoadingState />}
          {auth.providers.steam && (
            <ActionButton
              className="identity-button identity-button--steam"
              onClick={() => auth.login("steam", returnUrl)}
            >
              <span>Steam</span>
              <strong>{t("Shared_CommunityDialog_UseSteamIdentity")}</strong>
            </ActionButton>
          )}
          {auth.providers.discord && (
            <ActionButton
              className="identity-button identity-button--discord"
              onClick={() => auth.login("discord", returnUrl)}
            >
              <span>Discord</span>
              <strong>{t("Shared_CommunityDialog_UseDiscordIdentity")}</strong>
            </ActionButton>
          )}
          {!auth.providersLoading && !auth.providers.steam && !auth.providers.discord && (
            <p role="status">{t("Authentication_NoMethodsAvailable")}</p>
          )}
        </PageStack>
      </section>
    </div>
  );
}
