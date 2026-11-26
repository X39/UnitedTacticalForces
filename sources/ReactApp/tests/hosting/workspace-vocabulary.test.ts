import de from "@/locales/de.json";
import en from "@/locales/en.json";

const english: Record<string, string> = en;
const german: Record<string, string> = de;

/** The key prefixes of every Phase 63 string, as listed by the 63-UI-SPEC cost-stop vocabulary contract. */
const phase63Prefixes = [
  "Server_Workspace_",
  "Server_Progress_",
  "Server_Failure_",
  "Server_Activity_",
  "Server_Live_",
  "Server_Overview_",
  "Server_Settings_",
  "Server_Machine_",
  "Shared_Copy_",
] as const;

/**
 * Synonyms that must never name the cost-stop action in Phase 63 copy. The one phrase is "Shut down and stop
 * billing" (German "Herunterfahren und Abrechnung beenden").
 */
const bannedSynonyms =
  /cost[- ]stop|deprovision|terminat|tear down|teardown|kostenstopp|terminierung|deprovisionier/i;

const languages: ReadonlyArray<readonly [string, Record<string, string>]> = [
  ["en", english],
  ["de", german],
];

/**
 * Phase 66 accrued cost copy lives under a Phase 63 prefix. It says "Cost stopped" about the state of a session's
 * cost (66-UI-SPEC), never as the name of the cost-stop action, so the action-name guard does not apply to it.
 */
const phase66AccruedPrefix = "Server_Workspace_Cost_Accrued";

function phase63Keys(source: Record<string, string>): string[] {
  return Object.keys(source)
    .filter((key) => phase63Prefixes.some((prefix) => key.startsWith(prefix)))
    .filter((key) => !key.startsWith(phase66AccruedPrefix))
    .sort();
}

function placeholderNames(value: string): string {
  const names = new Set<string>();
  for (const match of value.matchAll(/\{\{\s*([^{}\s]+)\s*\}\}/g)) {
    const name = match[1];
    if (name !== undefined) names.add(name);
  }
  return [...names].sort().join(",");
}

describe("Phase 63 cost-stop vocabulary guard", () => {
  it("proves the banned-synonym pattern is live on in-memory samples", () => {
    for (const sample of [
      "Start the cost-stop now",
      "Cost stop requested",
      "Deprovision the machine",
      "The machine is terminated",
      "Tear down the server",
      "Teardown finished",
      "Kostenstopp ausgelöst",
      "Terminierung läuft",
      "Deprovisionierung läuft",
    ]) {
      expect(bannedSynonyms.test(sample), sample).toBe(true);
    }
    expect(bannedSynonyms.test("Shut down and stop billing")).toBe(false);
  });

  it.each(phase63Prefixes)("finds at least one key with the prefix %s", (prefix) => {
    expect(Object.keys(english).some((key) => key.startsWith(prefix))).toBe(true);
    expect(Object.keys(german).some((key) => key.startsWith(prefix))).toBe(true);
  });

  it.each(languages)(
    "uses no banned cost-stop synonym in any Phase 63 value in %s",
    (_name, source) => {
      const keys = phase63Keys(source);
      const offending = keys.filter((key) => bannedSynonyms.test(source[key] ?? ""));

      expect(keys.length).toBeGreaterThan(150);
      expect(offending).toEqual([]);
    },
  );

  it("uses the one cost-stop phrase for the stop action in both languages", () => {
    expect(english["Server_Action_Stop_Label"]).toBe("Shut down and stop billing");
    expect(german["Server_Action_Stop_Label"]).toBe("Herunterfahren und Abrechnung beenden");
  });

  it("holds the same Phase 63 key set in both languages", () => {
    expect(phase63Keys(german)).toEqual(phase63Keys(english));
  });

  it("keeps the placeholder names of every Phase 63 key equal in both languages", () => {
    const mismatched = phase63Keys(english).filter(
      (key) => placeholderNames(english[key] ?? "") !== placeholderNames(german[key] ?? ""),
    );

    expect(mismatched).toEqual([]);
  });

  it.each([
    ["Server_Activity_Job_CancelledByCostStop", "stop billing", "Abrechnung beendet"],
    ["Server_Activity_Log_CancelledByCostStop", "stop billing", "Abrechnung beendet"],
  ])(
    "words the cancelled-by-cost-stop sentence %s with the billing phrase",
    (key, enPart, dePart) => {
      expect(english[key]).toContain(enPart);
      expect(german[key]).toContain(dePart);
    },
  );
});

/** The key prefixes of every Phase 68 string that is new in the automation and schedule copy (68-UI-SPEC). */
const phase68Prefixes = ["Server_Automation_", "Server_Activity_Trigger_"] as const;

function phase68Keys(source: Record<string, string>): string[] {
  return Object.keys(source)
    .filter((key) => phase68Prefixes.some((prefix) => key.startsWith(prefix)))
    .sort();
}

describe("Phase 68 cost-stop vocabulary guard", () => {
  it("proves the banned-synonym pattern covers the Phase 68 samples", () => {
    for (const sample of [
      "Automatic tear down at 18:00",
      "Automatic teardown finished",
      "Terminierung um 18:00",
      "Deprovisionierung geplant",
      "Kostenstopp um 18:00",
      "The cost stop runs at night",
    ]) {
      expect(bannedSynonyms.test(sample), sample).toBe(true);
    }
    expect(bannedSynonyms.test("Automatic shutdown at 18:00")).toBe(false);
  });

  it.each(phase68Prefixes)("finds at least one key with the prefix %s", (prefix) => {
    expect(Object.keys(english).some((key) => key.startsWith(prefix))).toBe(true);
    expect(Object.keys(german).some((key) => key.startsWith(prefix))).toBe(true);
  });

  it.each(languages)(
    "uses no banned cost-stop synonym in any Phase 68 value in %s",
    (_name, source) => {
      const keys = phase68Keys(source);
      const offending = keys.filter((key) => bannedSynonyms.test(source[key] ?? ""));

      expect(keys.length).toBeGreaterThanOrEqual(95);
      expect(offending).toEqual([]);
    },
  );

  it("holds the same Phase 68 key set in both languages", () => {
    expect(phase68Keys(german)).toEqual(phase68Keys(english));
  });

  it("keeps the placeholder names of every Phase 68 key equal in both languages", () => {
    const mismatched = phase68Keys(english).filter(
      (key) => placeholderNames(english[key] ?? "") !== placeholderNames(german[key] ?? ""),
    );

    expect(mismatched).toEqual([]);
  });

  it("describes an automatic stop with the one cost-stop phrase of the specification", () => {
    expect(english["Server_Action_Stop_Label"]).toBe("Shut down and stop billing");
    expect(german["Server_Action_Stop_Label"]).toBe("Herunterfahren und Abrechnung beenden");
    expect(english["Server_Automation_Rule_StopHelp"]).toContain("ends billing");
    expect(german["Server_Automation_Rule_StopHelp"]).toContain("beendet die Abrechnung");
  });
});
