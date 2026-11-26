import type { StatusTone } from "@/components/statusTones";

/**
 * Lifecycle of a mod pack, a mod pack revision or a terrain: `active` is in use, `inactive` has been deactivated
 * (or, for a revision, is an older immutable one).
 */
export const assetStateTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["active", "success"],
  ["inactive", "neutral"],
]);

/**
 * Readiness of a creation step or an asset: `ready` has everything it needs, `needs-attention` is missing a
 * required value and `optional` is nothing to act on yet.
 */
export const assetReadinessTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["ready", "success"],
  ["needs-attention", "warning"],
  ["optional", "neutral"],
]);

/**
 * Origin of a mod chosen for a mod pack: a `catalog` entry that already exists is informational, a `new-entry`
 * that the save will create needs a second look.
 */
export const modOriginTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["catalog", "info"],
  ["new-entry", "warning"],
]);

/** State of unsaved edits on the mod pack workspace: `unsaved` and valid is `success`, `current` is `neutral`. */
export const editStateTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["unsaved", "success"],
  ["current", "neutral"],
]);
