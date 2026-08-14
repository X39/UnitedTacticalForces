import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const configuration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

describe("public configuration bootstrap", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => vi.unstubAllGlobals());

  it("uses the beta-local development fallback when the root response is not JSON", async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(
        new Response("<!doctype html><title>Classic</title>", {
          headers: { "Content-Type": "text/html" },
        }),
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify(configuration), {
          headers: { "Content-Type": "application/json" },
        }),
      );
    vi.stubGlobal("fetch", fetchMock);

    const { loadPublicConfiguration, resolveReactConfigurationUrl } = await import("@/app/config");

    await expect(loadPublicConfiguration()).resolves.toEqual(configuration);
    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      "/appsettings.json",
      expect.objectContaining({ cache: "no-store" }),
    );
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(resolveReactConfigurationUrl("/next/")).toBe("/next/appsettings.json");
  });

  it("rejects an API base with a non-HTTP scheme before rendering", async () => {
    const invalidConfiguration = {
      ...configuration,
      Api: { BaseUrl: "javascript:alert(1)" },
    };
    vi.stubGlobal(
      "fetch",
      vi.fn<typeof fetch>().mockResolvedValue(
        new Response(JSON.stringify(invalidConfiguration), {
          headers: { "Content-Type": "application/json" },
        }),
      ),
    );

    const { loadPublicConfiguration } = await import("@/app/config");

    await expect(loadPublicConfiguration()).rejects.toThrow(
      "United Tactical Forces could not start.",
    );
  });
});
