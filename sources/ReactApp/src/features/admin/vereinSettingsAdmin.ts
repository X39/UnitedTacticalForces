import { getApiClient } from "@/api/client";
import { apiMethod, apiPath } from "@/api/contract";
import type { components } from "@/generated/api";

type VereinSettingsDto = components["schemas"]["VereinSettingsDto"];
export type VereinSettingsUpdate = components["schemas"]["VereinSettingsPayload"];

export interface VereinAdminSettings {
  membershipContributionMonthlyAmount: number | null;
  membershipContributionAnnualAmount: number | null;
  membershipContributionCurrency: string;
  membershipContributionPublicTextDe: string;
  membershipContributionPublicTextEn: string;
  timeStampUpdated: string | null;
}

export const vereinAdminQueryKeys = {
  settings: ["verein", "admin", "settings"] as const,
};

function normalizeSettings(settings: VereinSettingsDto | undefined): VereinAdminSettings {
  return {
    membershipContributionMonthlyAmount: settings?.membershipContributionMonthlyAmount ?? null,
    membershipContributionAnnualAmount: settings?.membershipContributionAnnualAmount ?? null,
    membershipContributionCurrency: settings?.membershipContributionCurrency || "EUR",
    membershipContributionPublicTextDe: settings?.membershipContributionPublicTextDe ?? "",
    membershipContributionPublicTextEn: settings?.membershipContributionPublicTextEn ?? "",
    timeStampUpdated: settings?.timeStampUpdated ?? null,
  };
}

export async function loadVereinAdminSettings(signal?: AbortSignal): Promise<VereinAdminSettings> {
  const settings = await getApiClient().request<VereinSettingsDto>(
    apiPath("/verein/admin/settings"),
    { signal },
  );
  return normalizeSettings(settings);
}

export async function updateVereinAdminSettings(
  payload: VereinSettingsUpdate,
  signal?: AbortSignal,
): Promise<VereinAdminSettings> {
  const path = "/verein/admin/settings" as const;
  const settings = await getApiClient().request<VereinSettingsDto>(apiPath(path), {
    method: apiMethod(path, "put"),
    body: payload,
    signal,
  });
  if (!settings) throw new Error("Verein settings update returned no response body.");
  return normalizeSettings(settings);
}

export function parseContributionAmount(value: string): number | null | undefined {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const comma = trimmed.lastIndexOf(",");
  const dot = trimmed.lastIndexOf(".");
  let normalized = trimmed.replaceAll(" ", "");
  if (comma >= 0 && dot >= 0) {
    normalized =
      comma > dot
        ? normalized.replaceAll(".", "").replace(",", ".")
        : normalized.replaceAll(",", "");
  } else if (comma >= 0) {
    normalized = normalized.replace(",", ".");
  }
  const parsed = Number(normalized);
  return Number.isFinite(parsed) && parsed > 0 ? Math.round(parsed * 100) / 100 : undefined;
}

export function createVereinSettingsPayload(values: {
  monthlyAmount: string;
  annualAmount: string;
  currency: string;
  publicTextDe: string;
  publicTextEn: string;
}): VereinSettingsUpdate | undefined {
  const monthlyAmount = parseContributionAmount(values.monthlyAmount);
  const annualAmount = parseContributionAmount(values.annualAmount);
  if (
    monthlyAmount === undefined ||
    annualAmount === undefined ||
    (monthlyAmount === null) !== (annualAmount === null)
  )
    return undefined;
  return {
    membershipContributionMonthlyAmount: monthlyAmount,
    membershipContributionAnnualAmount: annualAmount,
    membershipContributionCurrency: values.currency.trim().toUpperCase(),
    membershipContributionPublicTextDe: values.publicTextDe.trim(),
    membershipContributionPublicTextEn: values.publicTextEn.trim(),
  };
}
