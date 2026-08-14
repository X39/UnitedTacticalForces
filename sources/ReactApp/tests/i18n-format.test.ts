import { describe, expect, it } from "vitest";
import i18n, { formatResource, i18nReady } from "@/app/i18n";

describe("RESX composite formatting", () => {
  it("formats unqualified and N0/N2 numeric placeholders", async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
    expect(formatResource("{0} / {1:N0} / {2:N2}", "ready", 1200, 12.5)).toBe(
      "ready / 1,200 / 12.50",
    );
  });
});
