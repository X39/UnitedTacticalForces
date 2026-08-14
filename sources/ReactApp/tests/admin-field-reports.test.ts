import type { FullUser } from "@/api/types";
import {
  canCreateFieldReports,
  canDeleteFieldReports,
  canModifyFieldReports,
  canReadFieldReports,
} from "@/auth/claims";
import {
  createFieldReportFormSchema,
  createFieldReportSlug,
  fieldReportMatchesView,
  fieldReportPayload,
  fieldReportState,
  isGuid,
  normalizeCommandFieldReportsSnapshot,
  sortCommandFieldReports,
  type CommandFieldReport,
  type FieldReportFormValues,
} from "@/features/admin/fieldReports";

function claim(identifier: string, value: string | null = null): FullUser["claims"][number] {
  return {
    primaryKey: 1,
    identifier,
    value,
    title: identifier,
    category: "test",
    description: "",
    isPrefix: false,
  };
}

function user(...claims: FullUser["claims"]): FullUser {
  return {
    primaryKey: "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f",
    nickname: "Editor",
    isBanned: false,
    isVerified: true,
    isDeleted: false,
    isVereinMember: false,
    claims,
    roles: [],
  };
}

const report = (overrides: Partial<CommandFieldReport> = {}): CommandFieldReport => ({
  primaryKey: "11111111-1111-1111-1111-111111111111",
  slug: "operation-northstar",
  title: "Operation Northstar",
  excerpt: "After-action summary",
  category: "After Action",
  markdown: "# Report",
  heroImageAlt: "Night patrol",
  isFeatured: false,
  sortOrder: 0,
  isPublished: false,
  isDeleted: false,
  timeStampCreated: "2026-08-12T10:00:00Z",
  timeStampUpdated: "2026-08-12T10:00:00Z",
  auditRecordCount: 1,
  ...overrides,
});

const messages = {
  titleRequired: "title required",
  titleTooLong: "title too long",
  slugTooLong: "slug too long",
  excerptRequired: "excerpt required",
  excerptTooLong: "excerpt too long",
  categoryRequired: "category required",
  categoryTooLong: "category too long",
  bodyRequired: "body required",
  bodyTooLong: "body too long",
  heroAltTooLong: "alt too long",
  sortOrderInvalid: "sort invalid",
  publicationDateInvalid: "date invalid",
};

const validForm: FieldReportFormValues = {
  slug: "operation-northstar",
  title: "Operation Northstar",
  excerpt: "After-action summary",
  category: "After Action",
  markdown: "# Report",
  heroImageAssetFk: "",
  heroImagePath: "",
  heroImageAlt: "",
  isFeatured: false,
  sortOrder: 0,
  isPublished: false,
  publicationInput: "",
};

describe("field-report authorization", () => {
  it("keeps read/create, modify, and delete capabilities distinct", () => {
    const creator = user(claim("crt:wiki"));
    expect(canReadFieldReports(creator)).toBe(true);
    expect(canCreateFieldReports(creator)).toBe(true);
    expect(canModifyFieldReports(creator)).toBe(false);
    expect(canDeleteFieldReports(creator)).toBe(false);

    const modifier = user(claim("wiki:modify"));
    expect(canReadFieldReports(modifier)).toBe(false);
    expect(canModifyFieldReports(modifier)).toBe(true);
    expect(canDeleteFieldReports(modifier)).toBe(false);

    const wikiAdmin = user(claim("adm:wiki"));
    expect(canReadFieldReports(wikiAdmin)).toBe(true);
    expect(canCreateFieldReports(wikiAdmin)).toBe(true);
    expect(canModifyFieldReports(wikiAdmin)).toBe(true);
    expect(canDeleteFieldReports(wikiAdmin)).toBe(true);
  });

  it("does not treat resource-valued claims as base authority", () => {
    const scoped = user(claim("adm:wiki", "one-report"), claim("wiki:delete", "one-report"));
    expect(canReadFieldReports(scoped)).toBe(false);
    expect(canDeleteFieldReports(scoped)).toBe(false);
  });
});

describe("field-report administration model", () => {
  it("normalizes partial snapshots and removes rows without identifiers", () => {
    expect(
      normalizeCommandFieldReportsSnapshot({
        fieldReportCount: 2,
        reports: [{ title: "Incomplete" }, { primaryKey: "report-1", title: "Ready" }],
      }),
    ).toMatchObject({
      fieldReportCount: 2,
      publishedFieldReportCount: 0,
      reports: [{ primaryKey: "report-1", title: "Ready", isPublished: false }],
    });
  });

  it("preserves deleted, draft, scheduled, and published state precedence", () => {
    const now = Date.parse("2026-08-14T12:00:00Z");
    expect(fieldReportState(report({ isDeleted: true, isPublished: true }), now)).toBe("deleted");
    expect(fieldReportState(report(), now)).toBe("draft");
    expect(
      fieldReportState(
        report({ isPublished: true, timeStampPublished: "2026-08-15T12:00:00Z" }),
        now,
      ),
    ).toBe("scheduled");
    expect(
      fieldReportState(
        report({ isPublished: true, timeStampPublished: "2026-08-13T12:00:00Z" }),
        now,
      ),
    ).toBe("published");
  });

  it("filters and sorts like the classic ledger", () => {
    const reports = [
      report({ primaryKey: "deleted", isDeleted: true, sortOrder: -10 }),
      report({ primaryKey: "standard", sortOrder: 0 }),
      report({ primaryKey: "featured", isFeatured: true, sortOrder: 10 }),
    ];
    expect(sortCommandFieldReports(reports).map((item) => item.primaryKey)).toEqual([
      "featured",
      "standard",
      "deleted",
    ]);
    expect(reports.filter((item) => fieldReportMatchesView(item, "featured"))).toHaveLength(1);
    expect(reports.filter((item) => fieldReportMatchesView(item, "deleted"))).toHaveLength(1);
  });

  it("creates stable localized slugs and recognizes every Guid shape", () => {
    expect(createFieldReportSlug("  Übung: Nacht / Alpha  ")).toBe("übung-nacht-alpha");
    expect(createFieldReportSlug("***")).toBe("field-report");
    expect(isGuid("00000000-0000-0000-0000-000000000000")).toBe(true);
    expect(isGuid("not-a-guid")).toBe(false);
  });
});

describe("field-report form contract", () => {
  const schema = createFieldReportFormSchema(messages);

  it("validates required, length, integer, and publication-date constraints", () => {
    expect(schema.safeParse(validForm).success).toBe(true);
    expect(schema.safeParse({ ...validForm, title: "" }).error?.issues[0]?.message).toBe(
      "title required",
    );
    expect(schema.safeParse({ ...validForm, markdown: "x".repeat(20_001) }).success).toBe(false);
    expect(schema.safeParse({ ...validForm, sortOrder: 1.5 }).success).toBe(false);
    expect(
      schema
        .safeParse({
          ...validForm,
          isPublished: true,
          publicationInput: "not-a-date",
        })
        .error?.issues.some((issue) => issue.message === "date invalid"),
    ).toBe(true);
  });

  it("normalizes optional fields and emits ISO publication instants", () => {
    const payload = fieldReportPayload({
      ...validForm,
      slug: "  ",
      heroImageAlt: "  ",
      isPublished: true,
      publicationInput: "2026-08-14T12:30",
    });
    expect(payload.slug).toBeNull();
    expect(payload.heroImageAlt).toBeNull();
    expect(payload.timeStampPublished).toMatch(/^2026-08-14T/);
  });
});
