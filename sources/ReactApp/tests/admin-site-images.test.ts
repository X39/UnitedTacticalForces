import { afterEach, describe, expect, it, vi } from "vitest";
import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import {
  loadMediaAsset,
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

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

/** Builds a JSON response with the given status and body. */
function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

/** Stubs fetch so that every request is answered by a fresh response from the factory. */
function answerWith(factory: () => Response): void {
  vi.stubGlobal(
    "fetch",
    vi.fn(() => Promise.resolve(factory())),
  );
  initializeApiClient(configuration);
}

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

describe("loadMediaAsset expected-none contract", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("resolves exactly null for a 404 so the caller needs no fallback", async () => {
    answerWith(() => new Response(null, { status: 404 }));
    await expect(loadMediaAsset("asset-9")).resolves.toBeNull();
  });

  it("resolves the normalized asset for a 200", async () => {
    answerWith(() =>
      json({
        primaryKey: "asset-9",
        originalFileName: "map.png",
        contentType: "image/png",
        size: 2048,
        imagePath: "media-assets/asset-9/content",
      }),
    );
    await expect(loadMediaAsset("asset-9")).resolves.toMatchObject({
      primaryKey: "asset-9",
      originalFileName: "map.png",
      contentType: "image/png",
      size: 2048,
      imagePath: "media-assets/asset-9/content",
      tags: [],
      usages: [],
    });
  });

  it("rejects with a forbidden ApiError for a 403", async () => {
    answerWith(() => new Response(null, { status: 403 }));
    await expect(loadMediaAsset("asset-9")).rejects.toMatchObject({
      code: "forbidden",
      status: 403,
    });
  });

  it("rejects with a server ApiError for a 500", async () => {
    answerWith(() => new Response(null, { status: 500 }));
    await expect(loadMediaAsset("asset-9")).rejects.toMatchObject({
      code: "server",
      status: 500,
    });
  });
});
