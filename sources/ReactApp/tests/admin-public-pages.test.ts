import { describe, expect, it } from "vitest";
import { latestRevision } from "@/features/admin/PublicPageDetailPage";
import { latestPublishedRevision, normalizePublicPage } from "@/features/admin/publicPages";

describe("public page administration projection", () => {
  it("normalizes omitted revision arrays", () => {
    expect(normalizePublicPage({ slug: "gdpr" })).toMatchObject({
      slug: "gdpr",
      revisions: [],
      isDeleted: false,
    });
  });

  it("selects the most recently published revision with a version tie-breaker", () => {
    const page = normalizePublicPage({
      slug: "gdpr",
      revisions: [
        { version: 1, isPublished: true, timeStampPublished: "2026-01-01T00:00:00Z" },
        { version: 2, isPublished: true, timeStampPublished: "2026-02-01T00:00:00Z" },
        { version: 3, isPublished: false, timeStampPublished: "2026-03-01T00:00:00Z" },
      ],
    });

    expect(latestPublishedRevision(page)?.version).toBe(2);
  });

  it("previews the explicit active revision before falling back to the latest version", () => {
    const explicit = normalizePublicPage({
      slug: "gdpr",
      revision: { version: 1, title: "Selected draft" },
      revisions: [{ version: 4, title: "Newest revision" }],
    });
    const fallback = normalizePublicPage({
      slug: "gdpr",
      revisions: [
        { version: 2, title: "Older revision" },
        { version: 5, title: "Newest revision" },
      ],
    });

    expect(latestRevision(explicit)?.title).toBe("Selected draft");
    expect(latestRevision(fallback)?.version).toBe(5);
  });
});
