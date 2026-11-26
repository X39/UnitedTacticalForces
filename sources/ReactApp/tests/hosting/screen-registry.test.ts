import { hostingRouteDefinitions } from "@/features/hosting/routes";
import { workspaceTabIds } from "@/features/hosting/workspaceSections";
import { adjacentPending, adjacentScreens } from "../e2e/support/screens/adjacentScreens";
import { fleetPending, fleetScreens } from "../e2e/support/screens/fleetScreens";
import {
  requiredRegistry,
  type RequiredRegistryItem,
  type ScreenEntry,
} from "../e2e/support/screens/registryTypes";
import { workspacePending, workspaceScreens } from "../e2e/support/screens/workspaceScreens";

/**
 * Completeness guard of the server screen registry (FND-06, D-12).
 *
 * The registry (`tests/e2e/support/serverScreens.ts`) is the single list every accessibility, German and baseline
 * audit iterates. A new hosting route, workspace tab, overlay or notice must therefore be either an entry in a slice
 * or declared pending there, and never both, so a screen cannot ship without an audit. Every failure names the id.
 *
 * The closing rule (plan 69-34) is that no slice may keep a pending id: every required screen is an audited entry.
 */

/** One slice of the registry: its entries and the ids it declares pending. */
interface Slice {
  name: RequiredRegistryItem["slice"];
  entries: readonly ScreenEntry[];
  pending: readonly string[];
}

const slices: readonly Slice[] = [
  { name: "fleet", entries: fleetScreens, pending: fleetPending },
  { name: "workspace", entries: workspaceScreens, pending: workspacePending },
  { name: "adjacent", entries: adjacentScreens, pending: adjacentPending },
];

const allEntries = slices.flatMap((slice) => slice.entries);
const requiredById = new Map(requiredRegistry.map((item) => [item.id, item]));

/**
 * Finds every required id that is not in exactly one of the entries or the pending list of its own slice.
 *
 * @param required The required manifest.
 * @param available The slices to check against.
 * @returns One message per problem, each naming the id.
 */
function coverageProblems(
  required: readonly RequiredRegistryItem[],
  available: readonly Slice[],
): string[] {
  const problems: string[] = [];
  for (const item of required) {
    const owner = available.find((slice) => slice.name === item.slice);
    const entered = available
      .flatMap((slice) => slice.entries)
      .filter((entry) => entry.id === item.id);
    const pending = available.flatMap((slice) => slice.pending).filter((id) => id === item.id);
    if (entered.length === 0 && pending.length === 0) {
      problems.push(`${item.id} has neither an entry nor a pending declaration`);
    }
    if (entered.length > 0 && pending.length > 0) {
      problems.push(`${item.id} is both an entry and declared pending`);
    }
    if (entered.length > 1) problems.push(`${item.id} has ${entered.length} entries`);
    if (pending.length > 1) problems.push(`${item.id} is declared pending ${pending.length} times`);
    if (owner && entered.length > 0 && !owner.entries.some((entry) => entry.id === item.id)) {
      problems.push(`${item.id} is an entry outside its slice ${item.slice}`);
    }
    if (owner && pending.length > 0 && !owner.pending.includes(item.id)) {
      problems.push(`${item.id} is declared pending outside its slice ${item.slice}`);
    }
  }
  return problems;
}

/** Every manifest pattern a required screen covers, including the patterns its states navigate to. */
function coveredPatterns(): string[] {
  return requiredRegistry.flatMap((item) => [
    ...(item.routePattern === undefined ? [] : [item.routePattern]),
    ...(item.alsoCovers ?? []),
  ]);
}

/**
 * Finds every id a slice still declares pending.
 *
 * @param available The slices to check.
 * @returns One message per pending id, each naming the id and its slice.
 */
function pendingProblems(available: readonly Slice[]): string[] {
  return available.flatMap((slice) =>
    slice.pending.map((id) => `${id} is still declared pending in the ${slice.name} slice`),
  );
}

describe("server screen registry completeness", () => {
  it("has a non-empty required manifest and at least one audited entry", () => {
    expect(requiredRegistry.length).toBeGreaterThan(0);
    expect(allEntries.length).toBeGreaterThan(0);
  });

  it("has unique required ids", () => {
    const ids = requiredRegistry.map((item) => item.id);
    const duplicates = ids.filter((id, index) => ids.indexOf(id) !== index);
    expect(duplicates).toEqual([]);
  });

  it("covers every hosting route pattern with a required screen", () => {
    const covered = new Set(coveredPatterns());
    const missing = hostingRouteDefinitions
      .map((route) => route.manifestPattern)
      .filter((pattern) => !covered.has(pattern));
    expect(missing, `hosting route patterns without a required registry screen`).toEqual([]);
  });

  it("names no route pattern that the hosting router does not define", () => {
    // The `/command` overview lives in App.tsx; every other hosting route comes from the shared table.
    const defined = new Set([
      "/command",
      ...hostingRouteDefinitions.map((route) => route.manifestPattern),
    ]);
    const stray = coveredPatterns().filter((pattern) => !defined.has(pattern));
    expect(stray).toEqual([]);
  });

  it("covers every workspace tab with a required screen", () => {
    const covered = new Set(requiredRegistry.map((item) => item.tab));
    const missing = workspaceTabIds.filter((tab) => !covered.has(tab));
    expect(missing, `workspace tabs without a required registry screen`).toEqual([]);
  });

  it("puts every required id in exactly one of the entries or the pending list of its slice", () => {
    expect(coverageProblems(requiredRegistry, slices)).toEqual([]);
  });

  it("declares no id pending in any slice (D-12 closure)", () => {
    expect(pendingProblems(slices)).toEqual([]);
  });

  it("names the id and slice when a slice still declares a pending id (live negative sample)", () => {
    const sample: readonly Slice[] = [
      { name: "fleet", entries: [], pending: [] },
      { name: "adjacent", entries: [], pending: ["S-09"] },
    ];

    expect(pendingProblems(sample)).toEqual([
      "S-09 is still declared pending in the adjacent slice",
    ]);
  });

  it("names the id when a required id has neither an entry nor a pending declaration", () => {
    const sample: readonly Slice[] = [{ name: "fleet", entries: [], pending: ["S-01"] }];
    const required: readonly RequiredRegistryItem[] = [
      { id: "S-01", kind: "screen", slice: "fleet" },
      { id: "S-02", kind: "screen", slice: "fleet" },
    ];
    expect(coverageProblems(required, sample)).toEqual([
      "S-02 has neither an entry nor a pending declaration",
    ]);
  });

  it("names the id when a required id is both an entry and declared pending", () => {
    const entry = { id: "S-01", kind: "screen", states: [] } as unknown as ScreenEntry;
    const sample: readonly Slice[] = [{ name: "fleet", entries: [entry], pending: ["S-01"] }];
    const required: readonly RequiredRegistryItem[] = [
      { id: "S-01", kind: "screen", slice: "fleet" },
    ];
    expect(coverageProblems(required, sample)).toEqual([
      "S-01 is both an entry and declared pending",
    ]);
  });

  it("declares no id that is not in the required manifest", () => {
    const stray = [
      ...allEntries.map((entry) => entry.id),
      ...slices.flatMap((slice) => slice.pending),
    ].filter((id) => !requiredById.has(id));
    expect(stray, `ids outside requiredRegistry`).toEqual([]);
  });

  it("keeps entry ids unique and equal to their required item", () => {
    const problems: string[] = [];
    const seen = new Set<string>();
    for (const entry of allEntries) {
      if (seen.has(entry.id)) problems.push(`${entry.id} is registered twice`);
      seen.add(entry.id);
      const item = requiredById.get(entry.id);
      if (!item) continue;
      if (entry.kind !== item.kind) {
        problems.push(`${entry.id} has kind ${entry.kind}, required ${item.kind}`);
      }
      if (entry.routePattern !== item.routePattern) {
        problems.push(
          `${entry.id} has routePattern ${entry.routePattern}, required ${item.routePattern}`,
        );
      }
      if (entry.tab !== item.tab) {
        problems.push(`${entry.id} has tab ${entry.tab}, required ${item.tab}`);
      }
      if (JSON.stringify(entry.alsoCovers ?? []) !== JSON.stringify(item.alsoCovers ?? [])) {
        problems.push(`${entry.id} has different alsoCovers than required`);
      }
      if (entry.states.length === 0) problems.push(`${entry.id} has no state`);
      if (!entry.route.startsWith("/")) problems.push(`${entry.id} route must start with a slash`);
    }
    expect(problems).toEqual([]);
  });
});
