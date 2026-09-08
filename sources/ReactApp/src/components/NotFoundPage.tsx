import { useTranslation } from "react-i18next";
import { ActionLink } from "@/components/ActionControl";
import { StatePanel } from "@/components/primitives";

export function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <div className="narrow-page">
      <StatePanel kind="empty" title={t("React_NotFound_Title")}>
        <p>{t("React_NotFound_Body")}</p>
        <ActionLink variant="gold" to="/">
          {t("React_NotFound_Home")}
        </ActionLink>
      </StatePanel>
    </div>
  );
}
