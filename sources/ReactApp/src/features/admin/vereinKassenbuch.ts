import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath } from "@/api/contract";
import type { components } from "@/generated/api";

type KassenbuchEntryDto = components["schemas"]["KassenbuchEntryDto"];
type KassenbuchPageDto = components["schemas"]["KassenbuchPageDto"];
export type KassenbuchEntryPayload = components["schemas"]["KassenbuchEntryPayload"];

export type KassenbuchDirection = "Income" | "Expense";

export interface KassenbuchEntry {
  primaryKey: number;
  timeStampBooked: string;
  direction: KassenbuchDirection;
  amount: number;
  currency: string;
  category: string;
  description: string;
  reference: string | null;
  documentNumber: string | null;
  isDeleted: boolean;
  timeStampCreated: string;
  timeStampUpdated: string;
  createdByName: string | null;
  updatedByName: string | null;
}

export interface KassenbuchPage {
  entries: KassenbuchEntry[];
  totalCount: number;
  incomeTotal: number;
  expenseTotal: number;
  balance: number;
  currency: string;
}

export interface KassenbuchFilters {
  skip?: number;
  take?: number;
  from?: string;
  to?: string;
  search?: string;
  category?: string;
}

export interface KassenbuchFormValues {
  bookedOn: string;
  direction: KassenbuchDirection;
  amount: string;
  currency: string;
  category: string;
  description: string;
  reference: string;
  documentNumber: string;
}

export type KassenbuchValidationError =
  "date" | "amount" | "currency" | "category" | "description" | "reference";

export const kassenbuchQueryKeys = {
  all: ["verein", "admin", "kassenbuch"] as const,
  list: (filters: KassenbuchFilters) =>
    [
      ...kassenbuchQueryKeys.all,
      "list",
      filters.from ?? "",
      filters.to ?? "",
      filters.search ?? "",
      filters.category ?? "",
      filters.skip ?? 0,
      filters.take ?? 100,
    ] as const,
  entry: (entryId: number) => [...kassenbuchQueryKeys.all, "entry", entryId] as const,
  member: ["verein", "kassenbuch"] as const,
};

export function normalizeKassenbuchDirection(value: unknown): KassenbuchDirection {
  return value === "Expense" || value === 1 || value === "1" ? "Expense" : "Income";
}

export function normalizeKassenbuchEntry(dto: KassenbuchEntryDto): KassenbuchEntry {
  return {
    primaryKey: dto.primaryKey ?? 0,
    timeStampBooked: dto.timeStampBooked ?? "",
    direction: normalizeKassenbuchDirection(dto.direction),
    amount: dto.amount ?? 0,
    currency: dto.currency?.trim().toUpperCase() || "EUR",
    category: dto.category ?? "",
    description: dto.description ?? "",
    reference: dto.reference ?? null,
    documentNumber: dto.documentNumber ?? null,
    isDeleted: dto.isDeleted ?? false,
    timeStampCreated: dto.timeStampCreated ?? "",
    timeStampUpdated: dto.timeStampUpdated ?? "",
    createdByName: dto.createdByName ?? null,
    updatedByName: dto.updatedByName ?? null,
  };
}

export function normalizeKassenbuchPage(dto: KassenbuchPageDto | undefined): KassenbuchPage {
  return {
    entries: (dto?.entries ?? []).map(normalizeKassenbuchEntry),
    totalCount: dto?.totalCount ?? 0,
    incomeTotal: dto?.incomeTotal ?? 0,
    expenseTotal: dto?.expenseTotal ?? 0,
    balance: dto?.balance ?? 0,
    currency: dto?.currency?.trim().toUpperCase() || "EUR",
  };
}

function createKassenbuchQuery(filters: KassenbuchFilters): string {
  const query = new URLSearchParams({
    skip: String(filters.skip ?? 0),
    take: String(filters.take ?? 100),
  });
  if (filters.from) query.set("from", filters.from);
  if (filters.to) query.set("to", filters.to);
  if (filters.search?.trim()) query.set("search", filters.search.trim());
  if (filters.category?.trim()) query.set("category", filters.category.trim());
  return query.toString();
}

export async function loadKassenbuchPage(
  filters: KassenbuchFilters = {},
  signal?: AbortSignal,
): Promise<KassenbuchPage> {
  const dto = await getApiClient().request<KassenbuchPageDto>(
    apiPathWithQuery(apiPath("/verein/kassenbuch"), createKassenbuchQuery(filters)),
    { signal },
  );
  return normalizeKassenbuchPage(dto);
}

export async function findKassenbuchEntry(
  entryId: number,
  signal?: AbortSignal,
): Promise<KassenbuchEntry | undefined> {
  const pageSize = 200;
  for (let skip = 0; ; skip += pageSize) {
    const page = await loadKassenbuchPage({ skip, take: pageSize }, signal);
    const entry = page.entries.find((candidate) => candidate.primaryKey === entryId);
    if (entry) return entry;
    if (page.entries.length < pageSize || skip + page.entries.length >= page.totalCount)
      return undefined;
  }
}

export async function createKassenbuchEntry(
  payload: KassenbuchEntryPayload,
  signal?: AbortSignal,
): Promise<KassenbuchEntry> {
  const path = "/verein/kassenbuch" as const;
  const dto = await getApiClient().request<KassenbuchEntryDto>(apiPath(path), {
    method: apiMethod(path, "post"),
    body: payload,
    signal,
  });
  if (!dto) throw new Error("Kassenbuch entry creation returned no response body.");
  return normalizeKassenbuchEntry(dto);
}

export async function updateKassenbuchEntry(
  entryId: number,
  payload: KassenbuchEntryPayload,
  signal?: AbortSignal,
): Promise<KassenbuchEntry> {
  const path = "/verein/kassenbuch/{entryId}" as const;
  const dto = await getApiClient().request<KassenbuchEntryDto>(fillApiPath(path, { entryId }), {
    method: apiMethod(path, "put"),
    body: payload,
    signal,
  });
  if (!dto) throw new Error("Kassenbuch entry update returned no response body.");
  return normalizeKassenbuchEntry(dto);
}

export async function deleteKassenbuchEntry(
  entryId: number,
  signal?: AbortSignal,
): Promise<KassenbuchEntry> {
  const path = "/verein/kassenbuch/{entryId}" as const;
  const dto = await getApiClient().request<KassenbuchEntryDto>(fillApiPath(path, { entryId }), {
    method: apiMethod(path, "delete"),
    signal,
  });
  if (!dto) throw new Error("Kassenbuch entry deletion returned no response body.");
  return normalizeKassenbuchEntry(dto);
}

export function dateFilterInstant(value: string, endOfDay: boolean): string | undefined {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) return undefined;
  const year = Number(match[1]);
  const month = Number(match[2]) - 1;
  const day = Number(match[3]);
  const instant = new Date(year, month, day);
  instant.setFullYear(year);
  if (instant.getFullYear() !== year || instant.getMonth() !== month || instant.getDate() !== day)
    return undefined;
  if (endOfDay) instant.setDate(instant.getDate() + 1);
  if (endOfDay) instant.setMilliseconds(instant.getMilliseconds() - 1);
  return Number.isNaN(instant.getTime()) ? undefined : instant.toISOString();
}

export function dateInputValue(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const year = String(date.getFullYear()).padStart(4, "0");
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function todayInputValue(): string {
  return dateInputValue(new Date().toISOString());
}

export function validateKassenbuchForm(
  values: KassenbuchFormValues,
): KassenbuchValidationError | undefined {
  if (!dateFilterInstant(values.bookedOn, false)) return "date";
  if (!Number.isFinite(Number(values.amount)) || Number(values.amount) <= 0) return "amount";
  if (values.currency.trim().length !== 3) return "currency";
  if (!values.category.trim() || values.category.trim().length > 96) return "category";
  if (!values.description.trim() || values.description.trim().length > 512) return "description";
  if (values.reference.trim().length > 128 || values.documentNumber.trim().length > 128)
    return "reference";
  return undefined;
}

export function createKassenbuchPayload(values: KassenbuchFormValues): KassenbuchEntryPayload {
  return {
    timeStampBooked: dateFilterInstant(values.bookedOn, false)!,
    direction: values.direction,
    amount: Number(values.amount),
    currency: values.currency.trim().toUpperCase(),
    category: values.category.trim(),
    description: values.description.trim(),
    reference: values.reference.trim() || null,
    documentNumber: values.documentNumber.trim() || null,
  };
}

export function kassenbuchFormValues(entry: KassenbuchEntry): KassenbuchFormValues {
  return {
    bookedOn: dateInputValue(entry.timeStampBooked),
    direction: entry.direction,
    amount: String(entry.amount),
    currency: entry.currency,
    category: entry.category,
    description: entry.description,
    reference: entry.reference ?? "",
    documentNumber: entry.documentNumber ?? "",
  };
}
