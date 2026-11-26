import { readdirSync, readFileSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { measureLegacyCss, type LegacyCssMeasure } from "../helpers/cssGuard";

/** The ceilings and the reason recorded for one legacy stylesheet. */
interface RatchetEntry extends LegacyCssMeasure {
  /** Why the stylesheet is still legacy. */
  reason: string;
}

const sourceRoot = resolve(import.meta.dirname, "..", "..", "src");
const ratchet = JSON.parse(
  readFileSync(join(import.meta.dirname, "legacy-ratchet.json"), "utf8"),
) as Record<string, RatchetEntry>;

/**
 * Stylesheets the ratchet does not hold: the guarded sheets (`components/server/*.css`,
 * `components/shared-primitives.css`, `components/action-control.css` and `components/search-picker.css`, scanned by
 * `server-style-guard.test.ts`) and `styles/tokens.css`, the token source of truth whose raw values are the point of
 * the file.
 */
function isGuarded(path: string): boolean {
  return (
    path.startsWith("components/server/") ||
    path === "components/shared-primitives.css" ||
    path === "components/action-control.css" ||
    path === "components/search-picker.css" ||
    path === "styles/tokens.css"
  );
}

/** Lists every stylesheet under `src` that the ratchet must hold, as POSIX paths relative to `src`. */
function legacyStylesheets(): string[] {
  return readdirSync(sourceRoot, { recursive: true })
    .map((name) => String(name).split("\\").join("/"))
    .filter((name) => name.endsWith(".css") && !isGuarded(name))
    .sort();
}

/**
 * Compares a measurement with its ceilings.
 *
 * @param measured The counts recomputed from the stylesheet.
 * @param ceiling The counts recorded in `legacy-ratchet.json`.
 * @returns One message per count that is above its ceiling; empty when the stylesheet is within bounds.
 */
function exceededCounts(measured: LegacyCssMeasure, ceiling: LegacyCssMeasure): string[] {
  return (["hex", "offGridPx", "outlineRemoval"] as const)
    .filter((key) => measured[key] > ceiling[key])
    .map((key) => `${key} is ${measured[key]}, above its ceiling of ${ceiling[key]}`);
}

/** The reason recorded for the Tier C public hero rules whose raw literals stay on the ratchet (D-AUTO-4). */
const tierCHeroReason = "Tier C hero (D-AUTO-4)";

/** The stylesheets that hold the Tier C hero literals. */
const tierCHeroStylesheets = ["pages/publicEvents.css", "styles.css"];

/** Measures every legacy stylesheet from disk, keyed by its POSIX path relative to `src`. */
function measureAll(): Map<string, LegacyCssMeasure> {
  return new Map(
    legacyStylesheets().map((name) => [
      name,
      measureLegacyCss(readFileSync(join(sourceRoot, name), "utf8")),
    ]),
  );
}

const updateRatchet = process.env["UTF_LEGACY_RATCHET_UPDATE"] === "1";

describe("legacy ratchet ceiling writer (opt-in with UTF_LEGACY_RATCHET_UPDATE=1)", () => {
  it.skipIf(!updateRatchet)(
    "lowers every ceiling to the measured count and never raises one",
    () => {
      const next: Record<string, RatchetEntry> = {};
      for (const [name, measured] of measureAll()) {
        const entry = ratchet[name];
        if (entry === undefined) throw new Error(`${name} is not listed in legacy-ratchet.json.`);
        const exceeded = exceededCounts(measured, entry);
        if (exceeded.length > 0)
          throw new Error(`Refusing to raise ${name}: ${exceeded.join("; ")}.`);
        next[name] = {
          hex: measured.hex,
          offGridPx: measured.offGridPx,
          outlineRemoval: measured.outlineRemoval,
          reason: entry.reason,
        };
      }
      writeFileSync(
        join(import.meta.dirname, "legacy-ratchet.json"),
        `${JSON.stringify(next, null, 2)}\n`,
      );
    },
  );
});

describe("legacy stylesheet ratchet", () => {
  it("finds the legacy stylesheets, styles.css among them", () => {
    expect(legacyStylesheets()).toContain("styles.css");
    expect(legacyStylesheets().length).toBeGreaterThan(5);
  });

  it("lists every legacy stylesheet in legacy-ratchet.json", () => {
    expect(legacyStylesheets().filter((name) => ratchet[name] === undefined)).toEqual([]);
  });

  it("records a reason and non-negative integer ceilings for every entry", () => {
    for (const [name, entry] of Object.entries(ratchet)) {
      expect(entry.reason.trim().length, `${name} has a reason`).toBeGreaterThan(0);
      for (const key of ["hex", "offGridPx", "outlineRemoval"] as const) {
        expect(Number.isInteger(entry[key]) && entry[key] >= 0, `${name} ${key}`).toBe(true);
      }
    }
  });

  it("never holds a ceiling for a guarded stylesheet", () => {
    expect(Object.keys(ratchet).filter(isGuarded)).toEqual([]);
  });

  it("reports every count above its ceiling and accepts counts at or below it", () => {
    const ceiling = { hex: 5, offGridPx: 5, outlineRemoval: 1 };

    expect(exceededCounts({ hex: 5, offGridPx: 4, outlineRemoval: 0 }, ceiling)).toEqual([]);
    expect(exceededCounts({ hex: 6, offGridPx: 9, outlineRemoval: 2 }, ceiling)).toEqual([
      "hex is 6, above its ceiling of 5",
      "offGridPx is 9, above its ceiling of 5",
      "outlineRemoval is 2, above its ceiling of 1",
    ]);
  });

  it.each(legacyStylesheets())(
    "keeps %s within its raw hex, off-grid pixel and outline-removal ceilings",
    (name) => {
      const ceiling = ratchet[name];
      expect(ceiling, `${name} is listed in legacy-ratchet.json`).toBeDefined();
      const measured = measureLegacyCss(readFileSync(join(sourceRoot, name), "utf8"));

      expect(exceededCounts(measured, ceiling as RatchetEntry)).toEqual([]);
    },
  );

  it("holds no outline removal in any stylesheet", () => {
    expect(
      Object.entries(ratchet)
        .filter(([, entry]) => entry.outlineRemoval !== 0)
        .map(([name]) => name),
    ).toEqual([]);
  });

  it("holds every ceiling at the current count so that a conversion must lower it", () => {
    const stale = [...measureAll()].flatMap(([name, measured]) => {
      const ceiling = ratchet[name] as RatchetEntry;
      return (["hex", "offGridPx", "outlineRemoval"] as const)
        .filter((key) => measured[key] < ceiling[key])
        .map((key) => `${name} ${key} is ${measured[key]}, below its ceiling of ${ceiling[key]}`);
    });

    expect(stale).toEqual([]);
  });

  it("records the Tier C hero reason on the hero stylesheets", () => {
    for (const name of tierCHeroStylesheets) {
      expect(ratchet[name]?.reason, `${name} reason`).toContain(tierCHeroReason);
    }
  });
});
