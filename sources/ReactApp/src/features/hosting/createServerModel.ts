import { z } from "zod";
import type {
  RuntimeProfile,
  RuntimeProvider,
  RuntimeStorageCapabilities,
  ServerCreationCostOwnerOption,
} from "@/features/hosting/types";

export interface ServerCreationFormValues {
  title: string;
  gameIdentifier: string;
  runtimeProviderIdentifier: string;
  runtimeProfileIdentifier: string;
  runtimeStorageStrategy: "IncludedDisk" | "AutomaticExternal" | "FixedExternal";
  externalStorageSizeGiB: number | null;
  modPackDefinitionId: number | null;
  configurationProfileId: string | null;
  costOwnerId: string | null;
  isLaunchDisabled: boolean;
}

export interface ServerCreationValidationMessages {
  titleRequired: string;
  titleTooLong: string;
  selectionRequired: string;
  invalidCapacity: string;
  invalidIdentifier: string;
}

export function createServerCreationSchema(messages: ServerCreationValidationMessages) {
  const requiredSelection = z.string().trim().min(1, messages.selectionRequired);
  return z.object({
    title: z.string().trim().min(1, messages.titleRequired).max(256, messages.titleTooLong),
    gameIdentifier: requiredSelection,
    runtimeProviderIdentifier: requiredSelection,
    runtimeProfileIdentifier: requiredSelection,
    runtimeStorageStrategy: z.enum(["IncludedDisk", "AutomaticExternal", "FixedExternal"]),
    externalStorageSizeGiB: z.number().int().positive(messages.invalidCapacity).nullable(),
    modPackDefinitionId: z.number().int().positive().nullable(),
    configurationProfileId: z.uuid(messages.invalidIdentifier).nullable(),
    costOwnerId: z.uuid(messages.invalidIdentifier).nullable(),
    isLaunchDisabled: z.boolean(),
  });
}

export function isProviderAvailable(provider: RuntimeProvider): boolean {
  return (
    provider.availabilityStatus.trim().toLocaleLowerCase() === "available" &&
    provider.profiles.length > 0
  );
}

export function orderProviders(providers: RuntimeProvider[]): RuntimeProvider[] {
  return [...providers].sort((left, right) =>
    left.identifier.localeCompare(right.identifier, undefined, { sensitivity: "base" }),
  );
}

export function isCostOwnerSelectionValid(
  costOwnerId: string | null,
  owners: ServerCreationCostOwnerOption[] | undefined,
): boolean {
  if (!owners) return false;
  if (costOwnerId) {
    const selected = owners.find((owner) => owner.costOwnerId === costOwnerId);
    return selected !== undefined && !selected.isFrozen;
  }
  return owners.find((owner) => owner.isCurrentUser)?.isFrozen !== true;
}

export function combinedHourlyCost(
  profile: RuntimeProfile | undefined,
  selectedStorageCapacityBytes: number | undefined,
  strategy: ServerCreationFormValues["runtimeStorageStrategy"],
  storage: RuntimeStorageCapabilities | null | undefined,
): number | undefined {
  if (profile?.estimatedHourlyCostEur == null) return undefined;
  const storageCost =
    strategy === "IncludedDisk"
      ? 0
      : ((selectedStorageCapacityBytes ?? 0) / 1_073_741_824) *
        (storage?.estimatedHourlyCostPerGiBEur ?? 0);
  return profile.estimatedHourlyCostEur + storageCost;
}
