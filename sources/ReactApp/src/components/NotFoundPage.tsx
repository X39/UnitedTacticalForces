import { useTranslation } from "react-i18next";
import { ActionLink } from "@/components/ActionControl";
import { PageHeader, StatePanel } from "@/components/primitives";

/** The 404 page: the page level heading names the problem, the panel explains it and offers the way home. */
export function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <div className="narrow-page">
      <PageHeader title={t("React_NotFound_Title")} />
      <StatePanel
        kind="empty"
        action={
          <ActionLink variant="gold" to="/">
            {t("React_NotFound_Home")}
          </ActionLink>
        }
      >
        <p>{t("React_NotFound_Body")}</p>
      </StatePanel>
    </div>
  );
}
