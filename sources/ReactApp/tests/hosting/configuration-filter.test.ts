import {
  chipKeys,
  countEntries,
  filterEntries,
  isFilterActive,
  isRequiredEmpty,
  type ConfigurationFilterDraft,
} from "@/components/server/configurationFilterRules";
import { configurationEntry } from "../helpers/configurationFixtures";

/* The pure rules behind the Configuration search and the Changed and Needs a value chips (D-10, T-70-17). */

const slots = configurationEntry({
  key: "slots",
  displayName: "Slots",
  group: "Basics",
  kind: "Integer",
  value: 64,
  source: "Server",
});
const terrain = configurationEntry({
  key: "terrain",
  displayName: "Map name",
  group: "Gelände",
  description: "Which terrain loads at start.",
  source: "Default",
});
const bare = configurationEntry({
  key: "bare",
  displayName: "Bare entry",
  group: "Misc",
  description: "",
  source: "Profile",
});
const password = configurationEntry({
  key: "password",
  displayName: "Admin password",
  group: "Security",
  sensitive: true,
  isRedacted: true,
  required: true,
  value: "hunter2",
  source: "Server",
});
const needed = configurationEntry({
  key: "needed",
  displayName: "Required name",
  group: "Basics",
  required: true,
  value: "  ",
  source: "Server",
});
const flag = configurationEntry({
  key: "flag",
  displayName: "Flag",
  group: "Basics",
  kind: "Boolean",
  required: true,
  value: "",
  source: "Server",
});
const all = [slots, terrain, bare, password, needed, flag];

const noDraft: ConfigurationFilterDraft = {
  values: new Map(),
  resets: new Set(),
  profile: null,
};

/** Builds a draft with the given staged values, resets and profile flag. */
function draftWith(
  values: Record<string, string> = {},
  resets: string[] = [],
  profile = false,
): ConfigurationFilterDraft {
  return {
    values: new Map(Object.entries(values)),
    resets: new Set(resets),
    profile: profile ? { profileId: "profile", revisionId: null } : null,
  };
}

/** The keys of the entries a filter returns, in order. */
function keys(entries: readonly { key: string }[]): string[] {
  return entries.map((entry) => entry.key);
}

describe("filterEntries query", () => {
  it("returns every entry in order for an empty or blank query in mode all", () => {
    expect(keys(filterEntries(all, "", "all", noDraft))).toEqual(keys(all));
    expect(keys(filterEntries(all, "   ", "all", noDraft))).toEqual(keys(all));
  });

  it("matches case-insensitively and ignores surrounding blanks", () => {
    expect(keys(filterEntries(all, "SLOTS", "all", noDraft))).toEqual(["slots"]);
    expect(keys(filterEntries(all, "  slots ", "all", noDraft))).toEqual(["slots"]);
  });

  it("matches a substring of the display name", () => {
    expect(keys(filterEntries(all, "lot", "all", noDraft))).toEqual(["slots"]);
  });

  it("ignores diacritics in the query and in the group name", () => {
    expect(keys(filterEntries(all, "gelande", "all", noDraft))).toEqual(["terrain"]);
    expect(keys(filterEntries(all, "GELÄNDE", "all", noDraft))).toEqual(["terrain"]);
  });

  it("matches case-insensitively whatever the browser locale is", () => {
    // A Turkish browser lower-cases "I" to a dotless i; the search must not depend on that default locale.
    const turkish = vi.spyOn(String.prototype, "toLocaleLowerCase").mockImplementation(function (
      this: string,
    ) {
      return String(this).replace(/I/g, "ı").toLowerCase();
    });
    const info = configurationEntry({ key: "info", displayName: "info line", group: "Basics" });
    try {
      expect(keys(filterEntries([info], "INFO", "all", noDraft))).toEqual(["info"]);
    } finally {
      turkish.mockRestore();
    }
  });

  it("matches the group name", () => {
    expect(keys(filterEntries(all, "basics", "all", noDraft))).toEqual(["slots", "needed", "flag"]);
  });

  it("matches the description only when the API supplies one", () => {
    expect(keys(filterEntries(all, "terrain loads", "all", noDraft))).toEqual(["terrain"]);
    const absent = { ...bare, description: null } as unknown as typeof bare;
    expect(filterEntries([absent], "null", "all", noDraft)).toEqual([]);
    expect(filterEntries([absent], "undefined", "all", noDraft)).toEqual([]);
    expect(filterEntries([bare], "null", "all", noDraft)).toEqual([]);
  });

  it("returns an empty list when nothing matches and does not mutate the input", () => {
    const input = [...all];
    expect(filterEntries(input, "zzz", "all", noDraft)).toEqual([]);
    expect(input).toEqual(all);
  });
});

describe("filterEntries and sensitive entries (T-70-17)", () => {
  it("never matches the value of a sensitive or redacted entry", () => {
    expect(filterEntries(all, "hunter2", "all", noDraft)).toEqual([]);
    expect(filterEntries([password], "hunter", "all", noDraft)).toEqual([]);
  });

  it("still matches a sensitive entry by its name, group and description", () => {
    expect(keys(filterEntries(all, "admin pass", "all", noDraft))).toEqual(["password"]);
    expect(keys(filterEntries(all, "security", "all", noDraft))).toEqual(["password"]);
    const described = { ...password, description: "Needed to log in remotely." };
    expect(keys(filterEntries([described], "log in", "all", noDraft))).toEqual(["password"]);
  });

  it("does not search the value of an ordinary entry either", () => {
    expect(filterEntries(all, "64", "all", noDraft)).toEqual([]);
  });

  it("never lists a sensitive or redacted entry as needing a value", () => {
    const emptied = { ...password, value: null };
    expect(keys(filterEntries([emptied], "", "attention", noDraft))).toEqual([]);
  });
});

describe("filterEntries mode changed", () => {
  it("returns staged and reset-staged entries", () => {
    const draft = draftWith({ slots: "70" }, ["terrain"]);
    expect(keys(filterEntries(all, "", "changed", draft))).toEqual(["slots", "terrain"]);
  });

  it("returns nothing when nothing is staged", () => {
    expect(filterEntries(all, "", "changed", noDraft)).toEqual([]);
  });

  it("adds every inherited entry while a profile change is staged", () => {
    const draft = draftWith({}, [], true);
    // Server-set entries keep their value; Default and Profile sources are inherited and can change.
    expect(keys(filterEntries(all, "", "changed", draft))).toEqual(["terrain", "bare"]);
  });

  it("combines with the query", () => {
    const draft = draftWith({ slots: "70" }, ["terrain"]);
    expect(keys(filterEntries(all, "map", "changed", draft))).toEqual(["terrain"]);
  });
});

describe("filterEntries mode attention", () => {
  it("returns required, typed, non-sensitive, non-boolean entries with an empty value", () => {
    expect(keys(filterEntries(all, "", "attention", noDraft))).toEqual(["needed"]);
  });

  it("judges the staged value over the saved one", () => {
    expect(keys(filterEntries(all, "", "attention", draftWith({ needed: "ok" })))).toEqual([]);
    const cleared = { ...slots, required: true };
    expect(keys(filterEntries([cleared], "", "attention", draftWith({ slots: " " })))).toEqual([
      "slots",
    ]);
  });
});

describe("filterEntries sticky keys", () => {
  it("keeps a sticky entry in the chip after it stops matching, without changing the pure rule", () => {
    const typed = draftWith({ needed: "ok" });
    expect(keys(filterEntries(all, "", "attention", typed))).toEqual([]);
    expect(keys(filterEntries(all, "", "attention", typed, new Set(["needed"])))).toEqual([
      "needed",
    ]);
  });

  it("keeps an un-staged entry in the Changed chip while its key is sticky", () => {
    expect(keys(filterEntries(all, "", "changed", noDraft, new Set(["slots"])))).toEqual(["slots"]);
    expect(filterEntries(all, "", "changed", noDraft, new Set())).toEqual([]);
  });

  it("still applies the query to a sticky entry and ignores the keys for the All chip", () => {
    const sticky = new Set(["needed", "slots"]);
    expect(keys(filterEntries(all, "required", "attention", noDraft, sticky))).toEqual(["needed"]);
    expect(keys(filterEntries(all, "slots", "all", noDraft, sticky))).toEqual(["slots"]);
  });

  it("lists the keys that satisfy a chip right now, and none for All", () => {
    expect(chipKeys(all, "attention", noDraft)).toEqual(["needed"]);
    expect(chipKeys(all, "changed", draftWith({ slots: "70" }, ["terrain"]))).toEqual([
      "slots",
      "terrain",
    ]);
    expect(chipKeys(all, "all", noDraft)).toEqual([]);
  });
});

describe("countEntries", () => {
  it("equals the lengths of the two filtered lists", () => {
    const draft = draftWith({ slots: "70" }, ["terrain"]);
    const counts = countEntries(all, draft);
    expect(counts.changed).toBe(filterEntries(all, "", "changed", draft).length);
    expect(counts.attention).toBe(filterEntries(all, "", "attention", draft).length);
    expect(counts).toEqual({ changed: 2, attention: 1 });
  });

  it("counts every entry regardless of any query", () => {
    expect(countEntries(all, noDraft)).toEqual({ changed: 0, attention: 1 });
  });
});

describe("isFilterActive and isRequiredEmpty", () => {
  it("is active for a query with text or a chip other than All", () => {
    expect(isFilterActive("", "all")).toBe(false);
    expect(isFilterActive("   ", "all")).toBe(false);
    expect(isFilterActive("a", "all")).toBe(true);
    expect(isFilterActive("", "changed")).toBe(true);
    expect(isFilterActive("", "attention")).toBe(true);
  });

  it("flags only a required, typed entry whose trimmed value is empty", () => {
    expect(isRequiredEmpty(needed, " ")).toBe(true);
    expect(isRequiredEmpty(needed, "x")).toBe(false);
    expect(isRequiredEmpty(flag, "")).toBe(false);
    expect(isRequiredEmpty(password, "")).toBe(false);
    expect(isRequiredEmpty(slots, "")).toBe(false);
  });
});
