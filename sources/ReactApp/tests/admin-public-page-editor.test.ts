import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import {
  createDefaultGdprContent,
  gdprContentToMarkdown,
  serializeGdprContent,
} from "@/features/admin/gdprContent";
import { createPublicPageRevision, normalizePublicPage } from "@/features/admin/publicPages";
import {
  createGdprEditorState,
  createPublicPageEditorSchema,
  createPublicPageEditorValues,
  createPublicPageRevisionPayload,
} from "@/features/admin/publicPageEditor";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

const defaults = {
  title: "New page",
  summary: "Summary",
  heroKicker: "Kicker",
  heroTitle: "Hero",
  heroDescription: "Description",
  markdown: "# New page",
};

describe("public-page revision editor model", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("starts an existing page from its active revision and never republishes implicitly", () => {
    const page = normalizePublicPage({
      slug: "operations",
      revision: {
        version: 4,
        title: "Operations",
        summary: "Current summary",
        heroKicker: "Field reports",
        heroTitle: "Latest operations",
        heroDescription: "After-action reporting",
        heroImageAssetFk: "10000000-0000-4000-8000-000000000001",
        heroImagePath: "media-assets/10000000-0000-4000-8000-000000000001/content",
        markdown: "# Latest operations",
        isPublished: true,
      },
    });

    expect(createPublicPageEditorValues(page, defaults)).toMatchObject({
      title: "Operations",
      heroImageAssetFk: "10000000-0000-4000-8000-000000000001",
      markdown: "# Latest operations",
      comment: "",
      publish: false,
    });
  });

  it("validates the same field limits and required gallery image as the API", () => {
    const message = "invalid";
    const schema = createPublicPageEditorSchema({
      title: message,
      summary: message,
      heroKicker: message,
      heroTitle: message,
      heroDescription: message,
      heroImage: message,
      markdown: message,
      comment: message,
    });
    const invalid = schema.safeParse({
      ...createPublicPageEditorValues(undefined, defaults),
      title: "",
      heroImageAssetFk: "",
    });
    const valid = schema.safeParse({
      ...createPublicPageEditorValues(undefined, defaults),
      heroImageAssetFk: "10000000-0000-4000-8000-000000000001",
    });

    expect(invalid.success).toBe(false);
    expect(valid.success).toBe(true);
  });

  it("preserves legacy GDPR markdown until the structured template is deliberately edited", () => {
    const revision = normalizePublicPage({
      slug: "gdpr",
      revision: { markdown: "# Legacy privacy notice", contentJson: "" },
    }).revision;
    const state = createGdprEditorState(revision);
    const values = {
      ...createPublicPageEditorValues(undefined, defaults),
      heroImageAssetFk: "10000000-0000-4000-8000-000000000001",
      markdown: "# Legacy privacy notice",
    };

    expect(state.preserveLegacyMarkdown).toBe(true);
    expect(createPublicPageRevisionPayload(values, true, state, revision)).toMatchObject({
      markdown: "# Legacy privacy notice",
      contentJson: "",
    });
  });

  it("generates canonical JSON and readable markdown from structured GDPR content", () => {
    const content = createDefaultGdprContent();
    content.intro.title = "Privacy operations";
    const values = {
      ...createPublicPageEditorValues(undefined, defaults),
      heroImageAssetFk: "10000000-0000-4000-8000-000000000001",
    };
    const payload = createPublicPageRevisionPayload(
      values,
      true,
      { content, preserveLegacyMarkdown: false },
      undefined,
    );

    expect(payload.markdown).toContain("# Privacy operations");
    expect(payload.markdown).toContain("## Data lifecycle");
    expect(payload.contentJson).toBe(serializeGdprContent(content));
    expect(gdprContentToMarkdown(content)).not.toContain("undefined");
  });

  it("posts a typed revision payload to the slug-scoped endpoint", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ slug: "operations", revisions: [{ version: 1 }] }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    const page = await createPublicPageRevision("operations", {
      title: "Operations",
      heroTitle: "Operations",
      heroImageAssetFk: "10000000-0000-4000-8000-000000000001",
      markdown: "# Operations",
      isPublished: false,
    });

    expect(page.slug).toBe("operations");
    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/public-pages/admin/operations/revisions");
    expect(init.method).toBe("POST");
    expect(JSON.parse(String(init.body))).toMatchObject({
      title: "Operations",
      isPublished: false,
    });
  });
});
