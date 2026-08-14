import { z } from "zod";
import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath } from "@/api/contract";
import type { components } from "@/generated/api";

type FieldReportDto = components["schemas"]["CommandCenterFieldReportDto"];
type FieldReportsSnapshotDto = components["schemas"]["CommandCenterFieldReportsSnapshotDto"];
type FieldReportPayloadDto = components["schemas"]["FieldReportUpsertPayload"];

export interface CommandFieldReport {
  primaryKey: string;
  slug: string;
  title: string;
  excerpt: string;
  category: string;
  markdown: string;
  heroImageAssetFk?: string;
  heroImagePath?: string;
  heroImageAlt: string;
  isFeatured: boolean;
  sortOrder: number;
  isPublished: boolean;
  isDeleted: boolean;
  timeStampPublished?: string;
  timeStampCreated: string;
  timeStampUpdated: string;
  createdByName?: string;
  updatedByName?: string;
  auditRecordCount: number;
}

export interface CommandFieldReportsSnapshot {
  fieldReportCount: number;
  publishedFieldReportCount: number;
  draftFieldReportCount: number;
  deletedFieldReportCount: number;
  reports: CommandFieldReport[];
}

export type FieldReportView = "all" | "published" | "drafts" | "featured" | "deleted";
export type FieldReportState = "published" | "scheduled" | "draft" | "deleted";
export type FieldReportAction = "publish" | "unpublish" | "delete" | "restore";

export interface FieldReportFormValues {
  slug: string;
  title: string;
  excerpt: string;
  category: string;
  markdown: string;
  heroImageAssetFk: string;
  heroImagePath: string;
  heroImageAlt: string;
  isFeatured: boolean;
  sortOrder: number;
  isPublished: boolean;
  publicationInput: string;
}

export interface FieldReportValidationMessages {
  titleRequired: string;
  titleTooLong: string;
  slugTooLong: string;
  excerptRequired: string;
  excerptTooLong: string;
  categoryRequired: string;
  categoryTooLong: string;
  bodyRequired: string;
  bodyTooLong: string;
  heroAltTooLong: string;
  sortOrderInvalid: string;
  publicationDateInvalid: string;
}

const integerMinimum = -2_147_483_648;
const integerMaximum = 2_147_483_647;

function optionalString(value: string | null | undefined): string | undefined {
  const normalized = value?.trim();
  return normalized ? normalized : undefined;
}

export function normalizeCommandFieldReport(report: FieldReportDto): CommandFieldReport {
  const heroImageAssetFk = optionalString(report.heroImageAssetFk);
  const heroImagePath = optionalString(report.heroImagePath);
  const timeStampPublished = optionalString(report.timeStampPublished);
  const createdByName = optionalString(report.createdByName);
  const updatedByName = optionalString(report.updatedByName);
  return {
    primaryKey: report.primaryKey ?? "",
    slug: report.slug ?? "",
    title: report.title ?? "",
    excerpt: report.excerpt ?? "",
    category: report.category ?? "",
    markdown: report.markdown ?? "",
    ...(heroImageAssetFk ? { heroImageAssetFk } : {}),
    ...(heroImagePath ? { heroImagePath } : {}),
    heroImageAlt: report.heroImageAlt ?? "",
    isFeatured: report.isFeatured ?? false,
    sortOrder: report.sortOrder ?? 0,
    isPublished: report.isPublished ?? false,
    isDeleted: report.isDeleted ?? false,
    ...(timeStampPublished ? { timeStampPublished } : {}),
    timeStampCreated: report.timeStampCreated ?? "",
    timeStampUpdated: report.timeStampUpdated ?? "",
    ...(createdByName ? { createdByName } : {}),
    ...(updatedByName ? { updatedByName } : {}),
    auditRecordCount: report.auditRecordCount ?? 0,
  };
}

export function normalizeCommandFieldReportsSnapshot(
  snapshot: FieldReportsSnapshotDto | undefined,
): CommandFieldReportsSnapshot {
  return {
    fieldReportCount: snapshot?.fieldReportCount ?? 0,
    publishedFieldReportCount: snapshot?.publishedFieldReportCount ?? 0,
    draftFieldReportCount: snapshot?.draftFieldReportCount ?? 0,
    deletedFieldReportCount: snapshot?.deletedFieldReportCount ?? 0,
    reports: (snapshot?.reports ?? [])
      .map(normalizeCommandFieldReport)
      .filter((report) => Boolean(report.primaryKey)),
  };
}

export function fieldReportState(report: CommandFieldReport, now = Date.now()): FieldReportState {
  if (report.isDeleted) return "deleted";
  if (!report.isPublished) return "draft";
  if (report.timeStampPublished && Date.parse(report.timeStampPublished) > now) return "scheduled";
  return "published";
}

export function fieldReportMatchesView(report: CommandFieldReport, view: FieldReportView): boolean {
  switch (view) {
    case "all":
      return true;
    case "published":
      return report.isPublished && !report.isDeleted;
    case "drafts":
      return !report.isPublished && !report.isDeleted;
    case "featured":
      return report.isFeatured && !report.isDeleted;
    case "deleted":
      return report.isDeleted;
  }
}

export function sortCommandFieldReports(
  reports: readonly CommandFieldReport[],
): CommandFieldReport[] {
  return [...reports].sort((left, right) => {
    if (left.isDeleted !== right.isDeleted) return Number(left.isDeleted) - Number(right.isDeleted);
    if (left.isFeatured !== right.isFeatured)
      return Number(right.isFeatured) - Number(left.isFeatured);
    if (left.sortOrder !== right.sortOrder) return left.sortOrder - right.sortOrder;
    const leftDate = Date.parse(left.timeStampPublished ?? left.timeStampUpdated) || 0;
    const rightDate = Date.parse(right.timeStampPublished ?? right.timeStampUpdated) || 0;
    return rightDate - leftDate || left.title.localeCompare(right.title);
  });
}

export function createFieldReportSlug(value: string): string {
  const slug = value
    .trim()
    .toLocaleLowerCase()
    .replace(/[^\p{L}\p{N}]+/gu, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 120)
    .replace(/-+$/g, "");
  return slug || "field-report";
}

export function isGuid(value: string | undefined): value is string {
  return Boolean(
    value && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value),
  );
}

export function formatLocalDateTimeInput(value: string | Date | undefined): string {
  if (!value) return "";
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

export function parseLocalDateTimeInput(value: string): string | undefined {
  if (!value.trim()) return undefined;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

export function createFieldReportFormSchema(messages: FieldReportValidationMessages) {
  return z
    .object({
      slug: z.string().trim().max(120, messages.slugTooLong),
      title: z.string().trim().min(1, messages.titleRequired).max(150, messages.titleTooLong),
      excerpt: z.string().trim().min(1, messages.excerptRequired).max(320, messages.excerptTooLong),
      category: z
        .string()
        .trim()
        .min(1, messages.categoryRequired)
        .max(64, messages.categoryTooLong),
      markdown: z.string().trim().min(1, messages.bodyRequired).max(20_000, messages.bodyTooLong),
      heroImageAssetFk: z.string(),
      heroImagePath: z.string(),
      heroImageAlt: z.string().trim().max(200, messages.heroAltTooLong),
      isFeatured: z.boolean(),
      sortOrder: z
        .number({ error: messages.sortOrderInvalid })
        .int(messages.sortOrderInvalid)
        .min(integerMinimum, messages.sortOrderInvalid)
        .max(integerMaximum, messages.sortOrderInvalid),
      isPublished: z.boolean(),
      publicationInput: z.string(),
    })
    .superRefine((value, context) => {
      if (
        value.isPublished &&
        value.publicationInput.trim() &&
        !parseLocalDateTimeInput(value.publicationInput)
      )
        context.addIssue({
          code: "custom",
          path: ["publicationInput"],
          message: messages.publicationDateInvalid,
        });
    });
}

export function createNewFieldReportForm(templateMarkdown: string): FieldReportFormValues {
  return {
    slug: "",
    title: "",
    excerpt: "",
    category: "After Action",
    markdown: templateMarkdown,
    heroImageAssetFk: "",
    heroImagePath: "",
    heroImageAlt: "",
    isFeatured: false,
    sortOrder: 0,
    isPublished: false,
    publicationInput: formatLocalDateTimeInput(new Date()),
  };
}

export function fieldReportToForm(report: CommandFieldReport): FieldReportFormValues {
  return {
    slug: report.slug,
    title: report.title,
    excerpt: report.excerpt,
    category: report.category,
    markdown: report.markdown,
    heroImageAssetFk: report.heroImageAssetFk ?? "",
    heroImagePath: report.heroImagePath ?? "",
    heroImageAlt: report.heroImageAlt,
    isFeatured: report.isFeatured,
    sortOrder: report.sortOrder,
    isPublished: report.isPublished,
    publicationInput: formatLocalDateTimeInput(report.timeStampPublished),
  };
}

export function fieldReportPayload(values: FieldReportFormValues): FieldReportPayloadDto {
  return {
    slug: optionalString(values.slug) ?? null,
    title: values.title.trim(),
    excerpt: values.excerpt.trim(),
    category: values.category.trim(),
    markdown: values.markdown.trim(),
    heroImageAssetFk: optionalString(values.heroImageAssetFk) ?? null,
    heroImageAlt: optionalString(values.heroImageAlt) ?? null,
    isFeatured: values.isFeatured,
    sortOrder: values.sortOrder,
    isPublished: values.isPublished,
    timeStampPublished: values.isPublished
      ? (parseLocalDateTimeInput(values.publicationInput) ?? new Date().toISOString())
      : null,
  };
}

export async function loadCommandFieldReports(
  search: string,
  signal?: AbortSignal,
): Promise<CommandFieldReportsSnapshot> {
  const query = new URLSearchParams({ take: "160" });
  if (search.trim()) query.set("search", search.trim());
  const snapshot = await getApiClient().request<FieldReportsSnapshotDto>(
    apiPathWithQuery(apiPath("/command-center/field-reports-snapshot"), query.toString()),
    { signal },
  );
  return normalizeCommandFieldReportsSnapshot(snapshot);
}

export async function loadCommandFieldReport(
  reportId: string,
  signal?: AbortSignal,
): Promise<CommandFieldReport | undefined> {
  const path = "/field-reports/admin/{reportId}" as const;
  const report = await getApiClient().request<FieldReportDto>(fillApiPath(path, { reportId }), {
    signal,
    expectedStatuses: [404],
  });
  return report ? normalizeCommandFieldReport(report) : undefined;
}

export async function saveCommandFieldReport(
  reportId: string | undefined,
  values: FieldReportFormValues,
  signal?: AbortSignal,
): Promise<CommandFieldReport> {
  let report: FieldReportDto | undefined;
  if (reportId) {
    const path = "/field-reports/admin/{reportId}" as const;
    report = await getApiClient().request<FieldReportDto>(fillApiPath(path, { reportId }), {
      method: apiMethod(path, "put"),
      body: fieldReportPayload(values),
      signal,
    });
  } else {
    const path = "/field-reports" as const;
    report = await getApiClient().request<FieldReportDto>(apiPath(path), {
      method: apiMethod(path, "post"),
      body: fieldReportPayload(values),
      signal,
    });
  }
  if (!report) throw new Error("Field report save returned no response body.");
  return normalizeCommandFieldReport(report);
}

export async function runFieldReportAction(
  reportId: string,
  action: FieldReportAction,
  signal?: AbortSignal,
): Promise<CommandFieldReport> {
  let report: FieldReportDto | undefined;
  switch (action) {
    case "publish": {
      const path = "/field-reports/admin/{reportId}/publish" as const;
      report = await getApiClient().request<FieldReportDto>(fillApiPath(path, { reportId }), {
        method: apiMethod(path, "post"),
        signal,
      });
      break;
    }
    case "unpublish": {
      const path = "/field-reports/admin/{reportId}/unpublish" as const;
      report = await getApiClient().request<FieldReportDto>(fillApiPath(path, { reportId }), {
        method: apiMethod(path, "post"),
        signal,
      });
      break;
    }
    case "delete": {
      const path = "/field-reports/admin/{reportId}" as const;
      report = await getApiClient().request<FieldReportDto>(fillApiPath(path, { reportId }), {
        method: apiMethod(path, "delete"),
        signal,
      });
      break;
    }
    case "restore": {
      const path = "/field-reports/admin/{reportId}/restore" as const;
      report = await getApiClient().request<FieldReportDto>(fillApiPath(path, { reportId }), {
        method: apiMethod(path, "post"),
        signal,
      });
      break;
    }
  }
  if (!report) throw new Error("Field report action returned no response body.");
  return normalizeCommandFieldReport(report);
}
