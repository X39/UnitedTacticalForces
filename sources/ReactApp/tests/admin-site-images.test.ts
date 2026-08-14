import { describe, expect, it } from "vitest";
import {
  mediaAssetMatchesUsageDomains,
  mediaAssetTagsEqual,
  normalizeSiteImageGroup,
  parseMediaAssetTags,
  siteImageGroups,
  type MediaAsset,
} from "@/features/admin/siteImages";

const asset: MediaAsset = {
  primaryKey: "asset-1",
  originalFileName: "briefing.webp",
  contentType: "image/webp",
  size: 1200,
  imagePath: "media-assets/asset-1/content",
  timeStampCreated: "2026-08-13T12:00:00Z",
  timeStampUpdated: "2026-08-13T12:00:00Z",
  createdByName: "Editor",
  tags: ["briefing"],
  usages: [],
};

describe("public image assignment model", () => {
  it("accepts every supported group and normalizes case", () => {
    for (const [group] of siteImageGroups) expect(normalizeSiteImageGroup(group)).toBe(group);
    expect(normalizeSiteImageGroup(" GDPR ")).toBe("gdpr");
  });

  it("falls back invalid and missing groups to home like the classic route", () => {
    expect(normalizeSiteImageGroup(undefined)).toBe("home");
    expect(normalizeSiteImageGroup("unknown")).toBe("home");
  });

  it("normalizes tag input with case-insensitive de-duplication", () => {
    expect(parseMediaAssetTags(" Briefing, night;briefing, terrain ")).toEqual([
      "Briefing",
      "night",
      "terrain",
    ]);
    expect(mediaAssetTagsEqual("NIGHT, briefing", ["briefing", "night"])).toBe(true);
    expect(mediaAssetTagsEqual("briefing", ["briefing", "night"])).toBe(false);
  });

  it("matches unused and tracked usage filters independently", () => {
    expect(mediaAssetMatchesUsageDomains(asset, new Set(["unused"]))).toBe(true);
    expect(mediaAssetMatchesUsageDomains(asset, new Set(["event"]))).toBe(false);
    const used = {
      ...asset,
      usages: [
        {
          domain: "event",
          domainKey: "event-1",
          displayName: "Operation Northstar",
          path: "/events/event-1",
        },
      ],
    };
    expect(mediaAssetMatchesUsageDomains(used, new Set(["event"]))).toBe(true);
    expect(mediaAssetMatchesUsageDomains(used, new Set(["unused"]))).toBe(false);
  });
});
