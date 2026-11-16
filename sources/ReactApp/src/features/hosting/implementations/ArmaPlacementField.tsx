import { Select } from "@fluentui/react-components";
import { useTranslation } from "react-i18next";
import type { ConfigurationContributionProps } from "./ConfigurationContribution";

export function ArmaPlacementField({
  id,
  value,
  disabled,
  required,
  allowUnset,
  invalid,
  describedBy,
  onChange,
}: ConfigurationContributionProps) {
  const { t } = useTranslation();
  return (
    <>
      <Select
        id={id}
        value={value}
        disabled={disabled}
        required={required}
        aria-invalid={invalid}
        aria-describedby={describedBy}
        onChange={(_, data) => onChange(data.value)}
      >
        {allowUnset && !required && (
          <option value="">{t("Hosting_ConfigurationProfiles_NotSet")}</option>
        )}
        <option value="same-host">{t("React_Placement_SameServer")}</option>
        <option value="separate-host">{t("React_Placement_SeparateServer")}</option>
      </Select>
      <small>{t("React_Placement_NextDeployment")}</small>
    </>
  );
}
