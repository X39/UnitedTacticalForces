import de from "@/locales/de.json";
import allowed from "./de-glossary-allowed.json";

const german: Record<string, string> = de;
const allowList: Record<string, string> = allowed;

/**
 * Matches the retired German terms: Modpack and Modpaket in any case and as part of a compound, and Gelände as the
 * terrain noun. The closed glossary is Mod-Paket / Mod-Pakete and Karte / Karten.
 */
const retiredTermPattern = /modpack|modpaket|gelände/i;

/**
 * Finds the values that use a retired term.
 *
 * @param values The German values by key.
 * @param exceptions The documented exceptions by key; each maps to a non-empty reason.
 * @returns The offending keys with their values.
 */
function glossaryViolations(
  values: Record<string, string>,
  exceptions: Record<string, string>,
): string[] {
  return Object.entries(values)
    .filter(([key, value]) => retiredTermPattern.test(value) && !exceptions[key]?.trim())
    .map(([key, value]) => `${key}: ${value}`);
}

describe("German glossary", () => {
  it("uses Mod-Paket and Karte instead of Modpack, Modpaket and Gelände", () => {
    expect(glossaryViolations(german, allowList)).toEqual([]);
  });

  it("reports a retired term in a sample value, so the guard cannot pass vacuously", () => {
    const sample = { Sample_Filter: "Modpack-Filter", Sample_Terrain: "Alle Gelände" };

    expect(glossaryViolations(sample, {})).toEqual([
      "Sample_Filter: Modpack-Filter",
      "Sample_Terrain: Alle Gelände",
    ]);
  });

  it("accepts a sample value only through an allowlist entry with a reason", () => {
    const sample = { Sample_Filter: "Modpack-Filter" };

    expect(glossaryViolations(sample, { Sample_Filter: "Deliberate brand spelling" })).toEqual([]);
    expect(glossaryViolations(sample, { Sample_Filter: "  " })).toEqual([
      "Sample_Filter: Modpack-Filter",
    ]);
  });

  it("allows only keys that exist in de.json, with a reason", () => {
    for (const [key, reason] of Object.entries(allowList)) {
      expect(german, `stale allowlist entry ${key}`).toHaveProperty(key);
      expect(reason.trim(), `reason for ${key}`).not.toBe("");
    }
  });
});
