import { describe, expect, it } from "vitest";
import { settingTones, toneOf } from "@/components/statusTones";
import {
  normalizeSettingsSnapshot,
  settingsFeatureDetailKey,
  settingsFeatureState,
  settingsFeatureStatusKey,
} from "@/features/admin/SettingsPage";

describe("command settings projection", () => {
  it("normalizes omitted OpenAPI response fields without leaking undefined values", () => {
    expect(
      normalizeSettingsSnapshot({
        apiBaseUrlConfigured: true,
        features: [{ area: "Steam", isEnabled: true }],
      }),
    ).toEqual({
      apiBaseUrlConfigured: true,
      clientBaseUrlConfigured: false,
      basePathConfigured: false,
      autoVerifyNewUsers: false,
      features: [{ area: "Steam", detail: "", isEnabled: true, isConfigured: false }],
    });
  });

  it("maps disabled, ready, and incomplete features to stable states", () => {
    expect(settingsFeatureState({ isEnabled: false, isConfigured: false })).toBe("off");
    expect(settingsFeatureState({ isEnabled: false, isConfigured: true })).toBe("off");
    expect(settingsFeatureState({ isEnabled: true, isConfigured: true })).toBe("configured");
    expect(settingsFeatureState({ isEnabled: true, isConfigured: false })).toBe(
      "missing-configuration",
    );
  });

  it("takes the badge tone of each feature state from the settings lexicon", () => {
    const tone = (isEnabled: boolean, isConfigured: boolean) =>
      toneOf(settingTones, settingsFeatureState({ isEnabled, isConfigured }));

    expect(tone(false, false)).toBe("neutral");
    expect(tone(true, true)).toBe("success");
    expect(tone(true, false)).toBe("warning");
  });

  it("names a distinct status label for each feature state", () => {
    expect(settingsFeatureStatusKey("off")).toBe("Pages_Command_Settings_StatusDisabled");
    expect(settingsFeatureStatusKey("configured")).toBe("Pages_Command_Settings_StatusReady");
    expect(settingsFeatureStatusKey("missing-configuration")).toBe(
      "Pages_Command_Settings_StatusNeedsConfig",
    );
  });

  it("maps known server feature names to shared localization resources", () => {
    expect(settingsFeatureDetailKey("Steam")).toBe("Pages_Command_Settings_Feature_Steam_Detail");
    expect(settingsFeatureDetailKey("Discord OAuth")).toBe(
      "Pages_Command_Settings_Feature_DiscordOAuth_Detail",
    );
    expect(settingsFeatureDetailKey("future-provider")).toBeUndefined();
  });
});
