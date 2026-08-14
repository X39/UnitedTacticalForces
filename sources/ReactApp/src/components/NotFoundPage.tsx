import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { StatePanel } from "@/components/primitives";

export function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <div className="narrow-page">
      <StatePanel kind="empty" title={t("React_NotFound_Title")}>
        <p>{t("React_NotFound_Body")}</p>
        <Link className="button button--gold" to="/">
          {t("React_NotFound_Home")}
        </Link>
      </StatePanel>
    </div>
  );
}
