import { useQueries } from "@tanstack/react-query";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";

/** One mod of a flattened mod pack: the stable key and the API-provided title. */
export interface ModPackItem {
  /** The stable key of the mod, `{source}:{identifier}`; two packs contain the same mod when the keys are equal. */
  key: string;
  /** The title of the mod as the API sends it. */
  title: string;
}

/** The difference between the mods of the current pack and the mods of the next pack. */
export interface ModPackDiffResult {
  /** The mods only the next pack contains, in the order of the next pack. */
  added: ModPackItem[];
  /** The mods only the current pack contains, in the order of the current pack. */
  removed: ModPackItem[];
  /** The mods both packs contain, in the order of the next pack. */
  unchanged: ModPackItem[];
}

/**
 * Compares the contents of two mod packs by key.
 *
 * Keys are compared as strings through a `Map`, never used to index an object. A key that occurs more than once in
 * one list counts once, with its first title.
 *
 * @param current The mods of the pack that is applied now.
 * @param next The mods of the pack that would replace it.
 * @returns The added, removed and unchanged mods.
 */
export function modPackDiff(
  current: readonly ModPackItem[],
  next: readonly ModPackItem[],
): ModPackDiffResult {
  const currentByKey = uniqueByKey(current);
  const nextByKey = uniqueByKey(next);
  const added: ModPackItem[] = [];
  const unchanged: ModPackItem[] = [];
  for (const [key, item] of nextByKey) {
    if (currentByKey.has(key)) unchanged.push(item);
    else added.push(item);
  }
  const removed: ModPackItem[] = [];
  for (const [key, item] of currentByKey) {
    if (!nextByKey.has(key)) removed.push(item);
  }
  return { added, removed, unchanged };
}

/**
 * Indexes a list of mods by key, keeping the order of first appearance.
 *
 * @param items The mods to index.
 * @returns A map from key to the first item with that key.
 */
function uniqueByKey(items: readonly ModPackItem[]): Map<string, ModPackItem> {
  const result = new Map<string, ModPackItem>();
  for (const item of items) {
    if (!result.has(item.key)) result.set(item.key, item);
  }
  return result;
}

/** The state of the comparison between the saved pack and the staged pack. */
export type ModPackComparisonStatus =
  /** No mod pack change is staged, so there is nothing to compare. */
  | "idle"
  /** At least one of the two contents requests is still pending. */
  | "loading"
  /** Both contents are known and the diff is meaningful. */
  | "ready"
  /** A contents request failed, so the packs cannot be compared. */
  | "failed"
  /** The API reports that the contents of a pack are not available. */
  | "unknown";

/** The result of {@link useModPackComparison}. */
export interface ModPackComparison {
  /** Whether the comparison is idle, loading, ready, failed or unknown. */
  status: ModPackComparisonStatus;
  /** The mods of the saved pack; empty for no pack or while unknown. */
  before: ModPackItem[];
  /** The mods of the staged pack; empty for no pack or while unknown. */
  after: ModPackItem[];
  /** The difference between {@link before} and {@link after}. */
  diff: ModPackDiffResult;
}

const noItems: ModPackItem[] = [];
const emptyDiff: ModPackDiffResult = { added: noItems, removed: noItems, unchanged: noItems };

/**
 * Loads the contents of the saved and the staged mod pack and compares them.
 *
 * One hook serves the review dialog extra and the Apply block, so the two cannot disagree. A null side (no mod pack)
 * counts as an empty list and needs no request. Contents are cached per server and pack; the panel's own contents
 * disclosure reads the same cache entries.
 *
 * @param serverId The server whose packs are compared.
 * @param savedId The saved mod pack, or null for none.
 * @param stagedId The staged mod pack, null for staging no pack, or undefined while no mod pack change is staged.
 * @returns The comparison state with both content lists and their diff.
 */
export function useModPackComparison(
  serverId: string,
  savedId: number | null,
  stagedId: number | null | undefined,
): ModPackComparison {
  const staging = stagedId !== undefined;
  const requested = staging ? [savedId, stagedId].filter((id): id is number => id !== null) : [];
  const results = useQueries({
    queries: requested.map((id) => ({
      queryKey: hostingQueryKeys.modPackContents(serverId, id),
      queryFn: ({ signal }) => hostingService.modPackContents(serverId, id, signal),
    })),
  });
  if (!staging) return { status: "idle", before: noItems, after: noItems, diff: emptyDiff };

  const sides = [savedId, stagedId].map((id) => {
    const result = id === null ? undefined : results[requested.indexOf(id)];
    const unavailable = result?.data?.isAvailable === false;
    return {
      pending: result !== undefined && result.isPending,
      failed: result !== undefined && result.isError,
      unavailable,
      items: unavailable ? noItems : (result?.data?.items ?? noItems),
    };
  });
  const [before, after] = [sides[0]?.items ?? noItems, sides[1]?.items ?? noItems];
  const status: ModPackComparisonStatus = sides.some((side) => side.pending)
    ? "loading"
    : sides.some((side) => side.failed)
      ? "failed"
      : sides.some((side) => side.unavailable)
        ? "unknown"
        : "ready";
  return {
    status,
    before,
    after,
    diff: status === "ready" ? modPackDiff(before, after) : emptyDiff,
  };
}
