import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { SelectField, type SelectOption, type SelectOptionGroup } from "@/components/FormFields";
import { StatePanel } from "@/components/primitives";
import "@/components/server/create-fields.css";
import {
  customPresetValue,
  presetSelection,
  type CreatePreset,
  type ServerCreationFormValues,
} from "@/features/hosting/createServerModel";

/** Props of {@link PresetField}. */
export interface PresetFieldProps {
  /** The projected presets, starting with the built-in game defaults. */
  presets: readonly CreatePreset[];
  /** The select value derived from the two form fields the preset controls. */
  value: string;
  /** Whether the configuration profile and mod pack option lists are still loading. */
  loading: boolean;
  /** Whether loading one of the option lists failed. */
  failed: boolean;
  /** Whether to state that a preset creates the server and keeps it off. */
  showAppliedLine: boolean;
  /** Called when the member asks to load the option lists again. */
  onRetry(): void;
  /**
   * Called when the member chooses a preset.
   *
   * @param selection The two form values the preset stands for.
   * @param preset The chosen preset.
   */
  onChoose(
    selection: Pick<ServerCreationFormValues, "configurationProfileId" | "modPackDefinitionId">,
    preset: CreatePreset,
  ): void;
}

/**
 * The preset shortcut of the create form (D-01, D-02).
 *
 * One native select offers the game defaults, the configuration profiles and the mod packs. A choice is resolved
 * through `presetSelection`, so the untrusted DOM value only ever selects one of the passed presets. When the form
 * fields match no single preset the select shows a disabled Customized option. The field renders nothing when only
 * the built-in default exists, and an error panel with a retry button replaces the select when the option lists
 * failed to load.
 */
export function PresetField({
  presets,
  value,
  loading,
  failed,
  showAppliedLine,
  onRetry,
  onChoose,
}: PresetFieldProps) {
  const { t } = useTranslation();
  if (failed) {
    return (
      <StatePanel
        kind="warning"
        action={
          <ActionButton variant="ghost" onClick={onRetry}>
            {t("Server_Create_Retry")}
          </ActionButton>
        }
      >
        {t("Server_Create_Options_LoadFailed")}
      </StatePanel>
    );
  }
  if (!loading && presets.length <= 1) return null;

  const profileOptions: SelectOption[] = [];
  const modPackOptions: SelectOption[] = [];
  let defaultOption: SelectOption | undefined;
  for (const preset of presets) {
    switch (preset.kind) {
      case "default":
        defaultOption = { value: preset.value, label: t("Server_Create_Preset_Default") };
        break;
      case "profile":
        profileOptions.push({
          value: preset.value,
          label: t("Server_Create_Preset_ProfileOption", {
            title: preset.title,
            revision: preset.revision,
          }),
        });
        break;
      case "modPack":
        modPackOptions.push({ value: preset.value, label: preset.title });
        break;
    }
  }
  const isCustom = value === customPresetValue;
  const options: (SelectOption | SelectOptionGroup)[] = [];
  if (defaultOption !== undefined) options.push(defaultOption);
  if (profileOptions.length > 0) {
    options.push({ group: t("Server_Create_Preset_GroupProfiles"), options: profileOptions });
  }
  if (modPackOptions.length > 0) {
    options.push({ group: t("Server_Create_Preset_GroupModPacks"), options: modPackOptions });
  }
  if (isCustom) {
    options.push({
      value: customPresetValue,
      label: t("Server_Create_Preset_CustomOption"),
      disabled: true,
    });
  }

  return (
    <div className="create-field">
      <SelectField
        label={t("Server_Create_Preset_Label")}
        hint={t("Server_Create_Preset_Hint")}
        value={value}
        disabled={loading}
        options={options}
        onChange={(event) => {
          const selection = presetSelection(event.target.value, presets);
          const preset = presets.find((candidate) => candidate.value === event.target.value);
          if (selection !== undefined && preset !== undefined) onChoose(selection, preset);
        }}
      />
      {loading && <StatePanel kind="loading">{t("Hosting_Create_LoadingSelections")}</StatePanel>}
      {isCustom ? (
        <p className="create-field__note">{t("Server_Create_Preset_Customized")}</p>
      ) : (
        showAppliedLine && (
          <p className="create-field__note">{t("Server_Create_Preset_AppliedLine")}</p>
        )
      )}
    </div>
  );
}
