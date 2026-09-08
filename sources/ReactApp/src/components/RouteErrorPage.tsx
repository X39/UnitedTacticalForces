import { useTranslation } from "react-i18next";
import { isRouteErrorResponse, useRouteError } from "react-router";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { NotFoundPage } from "@/components/NotFoundPage";
import { StatePanel } from "@/components/primitives";

export function RouteErrorPage() {
  const { t } = useTranslation();
  const error = useRouteError();

  if (isRouteErrorResponse(error) && error.status === 404) return <NotFoundPage />;

  return (
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
}
