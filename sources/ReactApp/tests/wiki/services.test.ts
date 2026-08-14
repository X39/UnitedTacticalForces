import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import { wikiService } from "@/features/wiki/services";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

describe("wiki API service", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("requests deleted headers only for the caller-selected privileged view", async () => {
    const fetchMock = vi
      .fn()
      .mockImplementation(() =>
        Promise.resolve(
          new Response("[]", { status: 200, headers: { "Content-Type": "application/json" } }),
        ),
      );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await wikiService.headers(false);
    await wikiService.headers(true);

    expect((fetchMock.mock.calls[0]?.[0] as URL).pathname).toBe("/api/wiki/all/header");
    expect((fetchMock.mock.calls[1]?.[0] as URL).searchParams.get("showDeleted")).toBe("true");
  });

  it("uses form encoding for create, rename, and revision mutations", async () => {
    const fetchMock = vi.fn().mockImplementation(() =>
      Promise.resolve(
        new Response(JSON.stringify({ primaryKey: "page-1", revisions: [] }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await wikiService.create("Training/Radio", "# Radio", "Initial");
    await wikiService.rename("page-1", "Training/Comms");
    await wikiService.createRevision("page-1", "# Comms", "Rename references");

    const [, createInit] = fetchMock.mock.calls[0] as [URL, RequestInit];
    const [renameUrl, renameInit] = fetchMock.mock.calls[1] as [URL, RequestInit];
    const [revisionUrl, revisionInit] = fetchMock.mock.calls[2] as [URL, RequestInit];
    expect(createInit.method).toBe("POST");
    expect(createInit.body).toBeInstanceOf(FormData);
    expect((createInit.body as FormData).get("comment")).toBe("Initial");
    expect(renameUrl.pathname).toBe("/api/wiki/page-1");
    expect(renameInit.method).toBe("PUT");
    expect((renameInit.body as FormData).get("title")).toBe("Training/Comms");
    expect(revisionUrl.pathname).toBe("/api/wiki/page-1/revisions");
    expect(revisionInit.method).toBe("POST");
    expect(new Headers(revisionInit.headers).has("Content-Type")).toBe(false);
  });
});
