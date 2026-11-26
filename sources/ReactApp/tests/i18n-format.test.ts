import { describe, expect, it } from "vitest";
import { formatRelativeTime } from "@/app/format";
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

describe("relative time formatting", () => {
  const now = Date.parse("2026-11-19T12:00:00Z");

  it("formats past values in English with the matching unit", async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
    expect(formatRelativeTime(now - 4 * 60_000, now)).toBe("4 minutes ago");
    expect(formatRelativeTime(now - 30_000, now)).toBe("30 seconds ago");
    expect(formatRelativeTime(now - 3 * 3_600_000, now)).toBe("3 hours ago");
    expect(formatRelativeTime(now - 2 * 86_400_000, now)).toBe("2 days ago");
    expect(formatRelativeTime(new Date(now - 4 * 60_000).toISOString(), now)).toBe("4 minutes ago");
  });

  it("formats past values in German", async () => {
    await i18nReady;
    await i18n.changeLanguage("de");
    expect(formatRelativeTime(now - 4 * 60_000, now)).toBe("vor 4 Minuten");
    await i18n.changeLanguage("en");
  });

  it("returns an empty string for an unparsable value", () => {
    expect(formatRelativeTime("not a date", now)).toBe("");
  });
});
