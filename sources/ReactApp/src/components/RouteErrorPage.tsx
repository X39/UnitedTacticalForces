import { useTranslation } from "react-i18next";
import { isRouteErrorResponse, useRouteError } from "react-router";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { NotFoundPage } from "@/components/NotFoundPage";
import { StatePanel } from "@/components/primitives";

export function RouteErrorPage({ standalone = false }: { standalone?: boolean }) {
  const { t } = useTranslation();
  const error = useRouteError();

  const content =
    isRouteErrorResponse(error) && error.status === 404 ? (
      <NotFoundPage />
    ) : (
      <div className="narrow-page">
        <StatePanel
          kind="error"
          title={t("React_RouteError_Title")}
          action={
            <div className="route-error-actions">
              <ActionButton variant="gold" type="button" onClick={() => window.location.reload()}>
                {t("React_RouteError_Reload")}
              </ActionButton>
              <ActionLink variant="ghost" to="/">
                {t("React_NotFound_Home")}
              </ActionLink>
            </div>
          }
        >
          <p>{t("React_RouteError_Body")}</p>
        </StatePanel>
      </div>
    );

  return standalone ? <main>{content}</main> : content;
}
