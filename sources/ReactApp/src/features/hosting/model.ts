import { z } from "zod";
import { currentLocale } from "@/app/format";
import type { TranslationKey } from "@/app/localizationTypes";
import type {
  ConfigurationDefinition,
  ConfigurationValue,
  RuntimeProfile,
  RuntimeStorageCapabilities,
  StorageDemand,
} from "@/features/hosting/types";

const bytesPerGiB = 1_073_741_824;
const byteUnits = ["B", "KiB", "MiB", "GiB", "TiB"] as const;

export type StorageStrategy = "IncludedDisk" | "AutomaticExternal" | "FixedExternal";

export function automaticExternalStorageGiB(
  demand: StorageDemand | undefined,
  storage: RuntimeStorageCapabilities | null | undefined,
): number | undefined {
  if (!demand || !storage || storage.capacityIncrementGiB <= 0) return undefined;
  const required = Math.ceil(demand.peakPreparationRequiredBytes / bytesPerGiB);
  const minimum = Math.max(required, storage.minimumCapacityGiB);
  return Math.ceil(minimum / storage.capacityIncrementGiB) * storage.capacityIncrementGiB;
}

export function selectedStorageBytes(
  strategy: StorageStrategy,
  profile: RuntimeProfile | undefined,
  demand: StorageDemand | undefined,
  storage: RuntimeStorageCapabilities | null | undefined,
  fixedExternalGiB: number | undefined,
): number | undefined {
  if (strategy === "IncludedDisk") return profile?.usableStorageBytes ?? undefined;
  const gib =
    strategy === "AutomaticExternal"
      ? automaticExternalStorageGiB(demand, storage)
      : fixedExternalGiB;
  return gib === undefined ? undefined : gib * bytesPerGiB;
}

export function isStorageSelectionValid(
  strategy: StorageStrategy,
  profile: RuntimeProfile | undefined,
  demand: StorageDemand | undefined,
  storage: RuntimeStorageCapabilities | null | undefined,
  fixedExternalGiB: number | undefined,
): boolean {
  if (!profile || !demand) return false;
  if (strategy === "IncludedDisk") {
    return (
      demand.runtimeProfiles.find(
        (fit) => fit.runtimeProfileIdentifier.toLowerCase() === profile.identifier.toLowerCase(),
      )?.isSufficient === true
    );
  }
  if (!storage?.supportsExternalBlockStorage) return false;
  const selectedGiB =
    strategy === "AutomaticExternal"
      ? automaticExternalStorageGiB(demand, storage)
      : fixedExternalGiB;
  return (
    selectedGiB !== undefined &&
    selectedGiB >= storage.minimumCapacityGiB &&
    selectedGiB <= storage.maximumCapacityGiB &&
    selectedGiB % storage.capacityIncrementGiB === 0 &&
    selectedGiB * bytesPerGiB >= demand.peakPreparationRequiredBytes
  );
}

export const serverCreationSchema = z.object({
  title: z.string().trim().min(1).max(256),
  gameIdentifier: z.string().min(1),
  runtimeProviderIdentifier: z.string().min(1),
  runtimeProfileIdentifier: z.string().min(1),
  runtimeStorageStrategy: z.enum(["IncludedDisk", "AutomaticExternal", "FixedExternal"]),
  externalStorageSizeGiB: z.number().int().positive().nullable(),
  modPackDefinitionId: z.number().int().positive().nullable(),
  configurationProfileId: z.string().uuid().nullable(),
  isLaunchDisabled: z.boolean(),
});

export type ServerCreationForm = z.infer<typeof serverCreationSchema>;

export function configurationDraftValue(
  definition: ConfigurationDefinition,
  value?: unknown,
): string {
  const actual = value === undefined ? definition.defaultValue : value;
  if (actual === undefined || actual === null) return "";
  if (definition.kind.toLowerCase() === "stringarray" && Array.isArray(actual)) {
    return actual.filter((item): item is string => typeof item === "string").join("\n");
  }
  return typeof actual === "string" ? actual : JSON.stringify(actual);
}

export type ConfigurationValidationCode =
  "required" | "integer" | "number" | "minimum" | "maximum" | "selection";

export interface ConfigurationValidationIssue {
  key: string;
  displayName: string;
  code: ConfigurationValidationCode;
  limit?: number;
}

export class ConfigurationValidationError extends Error {
  constructor(readonly issues: ConfigurationValidationIssue[]) {
    super("The configuration draft is invalid.");
    this.name = "ConfigurationValidationError";
  }
}

export function parseConfigurationValue(definition: ConfigurationDefinition, raw: string): unknown {
  const kind = definition.kind.toLowerCase();
  if (kind === "boolean") return raw === "true";
  if (kind === "integer") {
    return Number(raw);
  }
  if (kind === "number" || kind === "decimal") return Number(raw);
  if (kind === "stringarray") {
    return raw
      .split(/\r?\n/)
      .map((item) => item.trim())
      .filter(Boolean);
  }
  return raw;
}

export function validateConfigurationDraft(
  definitions: ConfigurationDefinition[],
  draft: Record<string, string>,
): ConfigurationValidationIssue[] {
  const issues: ConfigurationValidationIssue[] = [];
  for (const definition of definitions) {
    const raw = draft[definition.key];
    const trimmed = raw?.trim() ?? "";
    const kind = definition.kind.toLowerCase();
    const isEmpty =
      raw === undefined ||
      (kind === "stringarray"
        ? raw
            .split(/\r?\n/)
            .map((item) => item.trim())
            .filter(Boolean).length === 0
        : trimmed.length === 0);
    if (definition.required && isEmpty) {
      issues.push({
        key: definition.key,
        displayName: definition.displayName,
        code: "required",
      });
      continue;
    }
    if (isEmpty) continue;

    if (kind === "integer" || kind === "number" || kind === "decimal") {
      const value = Number(trimmed);
      if (!Number.isFinite(value)) {
        issues.push({
          key: definition.key,
          displayName: definition.displayName,
          code: kind === "integer" ? "integer" : "number",
        });
        continue;
      }
      if (kind === "integer" && !Number.isInteger(value)) {
        issues.push({
          key: definition.key,
          displayName: definition.displayName,
          code: "integer",
        });
        continue;
      }
      if (definition.minimum !== null && value < definition.minimum) {
        issues.push({
          key: definition.key,
          displayName: definition.displayName,
          code: "minimum",
          limit: definition.minimum,
        });
      } else if (definition.maximum !== null && value > definition.maximum) {
        issues.push({
          key: definition.key,
          displayName: definition.displayName,
          code: "maximum",
          limit: definition.maximum,
        });
      }
    }

    const choices = definition.choices ?? [];
    if (
      choices.length > 0 &&
      !choices.some((choice) => configurationDraftValue(definition, choice.value) === raw)
    ) {
      issues.push({
        key: definition.key,
        displayName: definition.displayName,
        code: "selection",
      });
    } else if (
      (definition.allowedValues?.length ?? 0) > 0 &&
      !definition.allowedValues.includes(raw)
    ) {
      issues.push({
        key: definition.key,
        displayName: definition.displayName,
        code: "selection",
      });
    }
  }
  return issues;
}

export function buildConfigurationValues(
  definitions: ConfigurationDefinition[],
  draft: Record<string, string>,
): ConfigurationValue[] {
  const issues = validateConfigurationDraft(definitions, draft);
  if (issues.length > 0) throw new ConfigurationValidationError(issues);
  return definitions
    .filter((definition) => draft[definition.key]?.trim())
    .map((definition) => ({
      key: definition.key,
      value: parseConfigurationValue(definition, draft[definition.key] ?? ""),
    }));
}

export function isConfigurationDraftDirty(
  definitions: ConfigurationDefinition[],
  baseline: Record<string, string>,
  draft: Record<string, string>,
): boolean {
  return definitions.some((definition) => {
    const current = draft[definition.key] ?? baseline[definition.key] ?? "";
    return current !== (baseline[definition.key] ?? "");
  });
}

export function formatBytes(bytes: number | null | undefined): string {
  if (bytes === null || bytes === undefined) return "—";
  const unitIndex = Math.min(
    Math.floor(Math.log(Math.max(Math.abs(bytes), 1)) / Math.log(1024)),
    byteUnits.length - 1,
  );
  const divisor = 1024 ** unitIndex;
  const formatted = new Intl.NumberFormat(currentLocale(), {
    maximumFractionDigits: unitIndex === 0 ? 0 : 1,
  }).format(bytes / divisor);
  return `${formatted} ${byteUnits[unitIndex]}`;
}

export function formatDate(value: string | null | undefined): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.valueOf())
    ? value
    : new Intl.DateTimeFormat(currentLocale(), {
        dateStyle: "short",
        timeStyle: "short",
      }).format(date);
}

export function formatMoney(value: number | null | undefined, currency = "EUR"): string {
  if (value === null || value === undefined) return "—";
  return new Intl.NumberFormat(currentLocale(), { style: "currency", currency }).format(value);
}

export function humanizeToken(value: string | null | undefined): string {
  if (!value) return "—";
  return value
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/[-_]+/g, " ")
    .replace(/^./, (character) => character.toUpperCase());
}

const deploymentStatusKeys: Record<string, TranslationKey> = {
  Provisioning: "Hosting_DeploymentStatus_Provisioning",
  WaitingForWorker: "Hosting_DeploymentStatus_WaitingForWorker",
  Preparing: "Hosting_DeploymentStatus_Preparing",
  Running: "Hosting_DeploymentStatus_Running",
  Stopping: "Hosting_DeploymentStatus_Stopping",
  PreservationFailed: "Hosting_DeploymentStatus_PreservationFailed",
  Terminating: "Hosting_DeploymentStatus_Terminating",
  Stopped: "Hosting_DeploymentStatus_Stopped",
  TerminatingDueToFault: "Hosting_DeploymentStatus_TerminatingDueToFault",
  TerminatedDueToFault: "Hosting_DeploymentStatus_TerminatedDueToFault",
  Failed: "Hosting_DeploymentStatus_Failed",
  Terminated: "Hosting_DeploymentStatus_Terminated",
};

const serverMessageKeys: Record<string, TranslationKey> = {
  "Server Operate permission is required.": "Hosting_ServerMessage_OperatePermissionRequired",
  "Another lifecycle operation is pending.": "Hosting_ServerMessage_OperationPending",
  "Server launch is disabled.": "Hosting_ServerMessage_LaunchDisabled",
  "This action is unavailable in the current deployment state.":
    "Hosting_ServerMessage_ActionUnavailableForState",
  "Termination is already in progress.": "Hosting_ServerMessage_TerminationPending",
  "Confirm this server action.": "Hosting_ServerMessage_ConfirmAction",
  "Live unpublished data will be lost.": "Hosting_ServerMessage_LiveDataWillBeLost",
};

export function deploymentStatusKey(value: string | null | undefined): TranslationKey | undefined {
  return value ? deploymentStatusKeys[value] : undefined;
}

export function serverMessageKey(value: string | null | undefined): TranslationKey | undefined {
  return value ? serverMessageKeys[value] : undefined;
}
