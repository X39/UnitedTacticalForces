import { describe, expect, it } from "vitest";
import {
  normalizeSettingsSnapshot,
  settingsFeatureDetailKey,
  settingsFeatureTone,
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

  it("maps disabled, ready, and incomplete features to stable status tones", () => {
    expect(settingsFeatureTone({ isEnabled: false, isConfigured: false })).toBe("neutral");
    expect(settingsFeatureTone({ isEnabled: true, isConfigured: true })).toBe("success");
    expect(settingsFeatureTone({ isEnabled: true, isConfigured: false })).toBe("warning");
  });

  it("maps known server feature names to shared localization resources", () => {
    expect(settingsFeatureDetailKey("Steam")).toBe("Pages_Command_Settings_Feature_Steam_Detail");
    expect(settingsFeatureDetailKey("Discord OAuth")).toBe(
      "Pages_Command_Settings_Feature_DiscordOAuth_Detail",
    );
    expect(settingsFeatureDetailKey("future-provider")).toBeUndefined();
  });
});
