import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import de from "@/locales/de.json";
import en from "@/locales/en.json";
import { navLabelContract } from "../support/navLabelContract";

const projectRoot = resolve(import.meta.dirname, "..", "..");
const english: Record<string, string> = en;
const german: Record<string, string> = de;

const navigationSources = [
  "src/components/commandNavigation.tsx",
  "src/components/CommunityLayout.tsx",
] as const;

/** Matches every navigation label key; accessible-name keys that end in `Aria` are not visible labels. */
const navLabelKeyPattern =
  /"((?:Shared_CommandCenter_(?:Nav|Group)_|Shared_CommunityNav_|Hosting_Nav_)[A-Za-z0-9]+)"/g;

function readSource(path: string): string {
  return readFileSync(join(projectRoot, path), "utf8");
}

/** Returns the navigation label keys in `source` that are not part of the closed list. */
function keysOutsideContract(source: string): string[] {
  const closed = new Set(navLabelContract.map((entry) => entry.key));
  const keys = [...source.matchAll(navLabelKeyPattern)].map((match) => match[1] as string);
  return [...new Set(keys)].filter((key) => !key.endsWith("Aria") && !closed.has(key));
}

describe("closed navigation label list", () => {
  it.each(navLabelContract.map((entry) => [entry.key, entry] as const))(
    "%s has exactly the contract English and German values",
    (key, entry) => {
      expect(english[key]).toBe(entry.en);
      expect(german[key]).toBe(entry.de);
    },
  );

  it("keeps every contract key unique", () => {
    const keys = navLabelContract.map((entry) => entry.key);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it.each(navigationSources)("%s uses only label keys from the closed list", (path) => {
    expect(keysOutsideContract(readSource(path))).toEqual([]);
  });

  it("detects a navigation label key outside the closed list", () => {
    expect(
      keysOutsideContract(
        'labelKey: "Shared_CommandCenter_Nav_Bogus", group: "Shared_CommandCenter_Group_Assets"',
      ),
    ).toEqual(["Shared_CommandCenter_Nav_Bogus"]);
    expect(keysOutsideContract('aria: "Shared_CommunityNav_ToggleNavigationAria"')).toEqual([]);
  });

  it("records the h1 exceptions for Overview, Contribution settings and Public images", () => {
    const exceptions = navLabelContract
      .filter((entry) => entry.h1Exception !== undefined)
      .map((entry) => entry.key)
      .sort();

    expect(exceptions).toEqual([
      "Shared_CommandCenter_Nav_Overview",
      "Shared_CommandCenter_Nav_PublicImages",
      "Shared_CommandCenter_Nav_VereinSettings",
    ]);
  });

  it("writes every English label in sentence case without an ampersand", () => {
    for (const entry of navLabelContract) {
      expect(entry.en).not.toContain("&");
      expect(entry.de).not.toContain("&");
    }
    expect(english["Shared_CommandCenter_Nav_ModPacks"]).toBe("Mod packs");
    expect(english["Shared_CommandCenter_Nav_FieldReports"]).toBe("Field reports");
  });

  it("keeps the long Contribution settings h1 apart from the short navigation label", () => {
    expect(english["Pages_Command_VereinSettings_PageTitle"]).toBe("Verein contribution settings");
    expect(german["Pages_Command_VereinSettings_PageTitle"]).toBe("Verein-Beitragseinstellungen");
    expect(english["Shared_CommandCenter_Nav_VereinSettings"]).toBe("Contribution settings");
  });
});
