import { describe, expect, it } from "vitest";
import {
  findLeaks,
  functionWords,
  leakCandidates,
  realLeakCandidates,
} from "../e2e/support/germanLeak";
import de from "@/locales/de.json";
import en from "@/locales/en.json";

const english: Record<string, string> = en;
const german: Record<string, string> = de;

/** Tells whether the real candidate set flags a text. */
function flags(text: string): boolean {
  return findLeaks([text], realLeakCandidates()).length > 0;
}

describe("german leak candidates", () => {
  it("is not vacuous: the real locale files yield a large candidate set", () => {
    expect(realLeakCandidates().length).toBeGreaterThan(1000);
  });

  it("keeps values whose German differs and drops identical, allow-listed, short and German-contained values", () => {
    const candidates = leakCandidates(
      {
        Differs: "Refresh the list",
        Same: "Server settings",
        Allowed: "Briefing room",
        Short: "Save",
        Inside: "Open servers",
        Template: "Greetings {{name}}, welcome back",
      },
      {
        Differs: "Liste aktualisieren",
        Same: "Server settings",
        Allowed: "Einsatzraum",
        Short: "Speichern",
        Inside: "Alle offenen Server und Open servers anzeigen",
        Template: "Grüße {{name}}, willkommen zurück",
      },
      new Set(["Allowed"]),
    );
    expect(candidates).toContain("refresh the list");
    expect(candidates).toContain("greetings");
    expect(candidates).toContain(", welcome back");
    expect(candidates).not.toContain("server settings");
    expect(candidates).not.toContain("briefing room");
    expect(candidates).not.toContain("save");
    expect(candidates).not.toContain("open servers");
  });

  it("treats a key without a German value as a leak candidate", () => {
    expect(leakCandidates({ Missing: "Missing translation text" }, {}, new Set())).toEqual([
      "missing translation text",
    ]);
  });
});

describe("english samples are detected (non-vacuity)", () => {
  it("flags English locale values verbatim", () => {
    const value = english.Server_Alerts_Page_LoadError;
    expect(value).toBeDefined();
    expect(german.Server_Alerts_Page_LoadError).not.toBe(value);
    expect(flags(value ?? "")).toBe(true);
  });

  it("flags an English locale value inside surrounding text, ignoring case", () => {
    const value = english.Server_Fleet_Refresh ?? "";
    expect(flags(`>> ${value.toUpperCase()} <<`)).toBe(true);
  });

  it("flags the sample 'The servers could not be loaded' through two function words", () => {
    const [leak] = findLeaks(["The servers could not be loaded"], []);
    expect(leak?.reason).toContain("function words");
    expect(leak?.reason).toContain("the");
    expect(leak?.reason).toContain("not");
  });

  it("flags the sample 'this is your server' through its function words", () => {
    const [leak] = findLeaks(["this is your server"], []);
    expect(leak?.reason).toContain("this, is, your");
  });

  it("does not flag a single function word, so 'Start the server now' needs a locale match to count", () => {
    expect(findLeaks(["Start the server now"], [])).toEqual([]);
  });
});

describe("german samples pass", () => {
  it.each([
    "Server starten",
    "Konfigurationsprofile",
    "1 Server verursacht Kosten oder könnte noch Kosten verursachen.",
    "Ich will an den Server",
    "Keine Warnungen eingerichtet",
  ])("passes %s", (text) => {
    expect(flags(text)).toBe(false);
  });

  it("does not flag short words shared with German, such as Name and Status", () => {
    expect(flags("Name")).toBe(false);
    expect(flags("Status")).toBe(false);
    expect(flags("Name Status")).toBe(false);
  });

  it("does not match a candidate inside a longer word", () => {
    expect(findLeaks(["Interactive"], ["active"])).toEqual([]);
    expect(findLeaks(["the active one"], ["active"])).toHaveLength(1);
  });

  it("does not match the tail of a colon-joined permission claim id", () => {
    expect(findLeaks(["sv:operate"], ["operate"])).toEqual([]);
    expect(findLeaks(["Entfernen sv:operate"], ["operate"])).toEqual([]);
    expect(findLeaks(["Please operate: now"], ["operate"])).toHaveLength(1);
    expect(findLeaks(["Hinweis: operate"], ["operate"])).toHaveLength(1);
  });
});

describe("function word list", () => {
  it("leaves out the ordinary German words 'an' and 'will'", () => {
    expect(functionWords).not.toContain("an");
    expect(functionWords).not.toContain("will");
  });

  it("lists the seventeen words of the false-positive review", () => {
    expect(functionWords).toHaveLength(17);
  });
});
