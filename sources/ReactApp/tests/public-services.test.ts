import { initializeApiClient } from "@/api/client";
import { eventService, fieldReportService, publicPageService, vereinService } from "@/api/services";
import type { PublicConfiguration } from "@/app/config";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

const eventId = "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f";

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

/** The public reads that declare an expected none answer, keyed by a readable name. */
const noneReads: Record<string, () => Promise<unknown>> = {
  "eventService.detail": () => eventService.detail(eventId),
  "fieldReportService.detail": () => fieldReportService.detail("unknown-report"),
  "publicPageService.published": () => publicPageService.published("gdpr"),
};

describe("public reads that expect a none answer", () => {
  afterEach(() => vi.unstubAllGlobals());

  describe.each(Object.entries(noneReads))("%s", (_name, read) => {
    it("resolves null for a 404", async () => {
      answerWith(() => new Response(null, { status: 404 }));
      await expect(read()).resolves.toBeNull();
    });

    it("resolves null for a 204", async () => {
      answerWith(() => new Response(null, { status: 204 }));
      await expect(read()).resolves.toBeNull();
    });

    it("resolves the parsed body for a 200", async () => {
      answerWith(() => json({ title: "Present" }));
      await expect(read()).resolves.toEqual({ title: "Present" });
    });

    it("rejects with a forbidden ApiError for a 403", async () => {
      answerWith(() => new Response(null, { status: 403 }));
      await expect(read()).rejects.toMatchObject({ code: "forbidden", status: 403 });
    });

    it("rejects for a 500", async () => {
      answerWith(() => new Response(null, { status: 500 }));
      await expect(read()).rejects.toMatchObject({ code: "server", status: 500 });
    });
  });

  describe("Verein reads fixed earlier in the phase", () => {
    it.each([
      ["statutes", () => vereinService.statutes()],
      ["minute", () => vereinService.minute("7")],
      ["myMembershipRequest", () => vereinService.myMembershipRequest()],
    ])("%s resolves null for a 404", async (_name, read) => {
      answerWith(() => new Response(null, { status: 404 }));
      await expect(read()).resolves.toBeNull();
    });
  });
});
