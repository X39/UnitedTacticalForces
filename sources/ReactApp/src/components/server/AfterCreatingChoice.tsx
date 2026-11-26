import { useTranslation } from "react-i18next";
import { RadioGroupField } from "@/components/FormFields";
import "@/components/server/create.css";

/** What happens to the server once it has been created: it stays off, or the start is confirmed next. */
export type AfterCreating = "keep-off" | "start";

/** Props of {@link AfterCreatingChoice}. */
export interface AfterCreatingChoiceProps {
  /** The chosen option. */
  value: AfterCreating;
  /**
   * Called with the option the user chose.
   *
   * @param value The chosen option.
   */
  onChange(value: AfterCreating): void;
  /** Whether starting is turned off for this server, which disables the start option and explains why. */
  startDisabled: boolean;
  /** Disables both options, for example while the create request runs. */
  disabled?: boolean | undefined;
}

/**
 * The After creating choice of the create page: keep the server off, or start it now.
 *
 * Starting is always an explicit choice. When launching is turned off the start option stays visible but disabled,
 * with the reason linked to it. Forcing keep off in that case is the page's job.
 */
export function AfterCreatingChoice({
  value,
  onChange,
  startDisabled,
  disabled,
}: AfterCreatingChoiceProps) {
  const { t } = useTranslation();
  return (
    <section className="create-section">
      <RadioGroupField
        legend={<h2 className="create-section__title">{t("Server_Create_After_Title")}</h2>}
        name="after-creating"
        value={value}
        options={[
          {
            value: "keep-off",
            label: t("Server_Create_After_KeepOff"),
            help: t("Server_Create_After_KeepOff_Help"),
          },
          {
            value: "start",
            label: t("Server_Create_After_Start"),
            help: t("Server_Create_After_Start_Help"),
            disabled: startDisabled,
          },
        ]}
        reason={startDisabled ? t("Server_Reason_LaunchDisabled") : undefined}
        disabled={disabled}
        onChange={(chosen) => {
          if (chosen === "keep-off" || chosen === "start") onChange(chosen);
        }}
      />
    </section>
  );
}
