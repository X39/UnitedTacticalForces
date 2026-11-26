import { modPackDiff, type ModPackItem } from "@/features/hosting/modPackDiff";

/* The pure contents diff of two mod packs (D-05). */

/** Builds mods whose key is `workshop:<id>` and whose title is the id. */
function mods(...ids: string[]): ModPackItem[] {
  return ids.map((id) => ({ key: `workshop:${id}`, title: id }));
}

/** The titles of a list of mods. */
function titles(items: ModPackItem[]): string[] {
  return items.map((item) => item.title);
}

describe("modPackDiff", () => {
  it("separates added, removed and unchanged mods by key", () => {
    const diff = modPackDiff(mods("A", "B"), mods("B", "C"));

    expect(titles(diff.added)).toEqual(["C"]);
    expect(titles(diff.removed)).toEqual(["A"]);
    expect(titles(diff.unchanged)).toEqual(["B"]);
  });

  it("keeps the order of the next pack for added and unchanged and of the current pack for removed", () => {
    const diff = modPackDiff(mods("R2", "K2", "R1", "K1"), mods("N2", "K1", "N1", "K2"));

    expect(titles(diff.added)).toEqual(["N2", "N1"]);
    expect(titles(diff.unchanged)).toEqual(["K1", "K2"]);
    expect(titles(diff.removed)).toEqual(["R2", "R1"]);
  });

  it("gives empty added and removed lists for identical contents", () => {
    const diff = modPackDiff(mods("A", "B"), mods("B", "A"));

    expect(diff.added).toEqual([]);
    expect(diff.removed).toEqual([]);
    expect(titles(diff.unchanged)).toEqual(["B", "A"]);
  });

  it("lists every mod as added without a current pack and as removed without a next pack", () => {
    expect(titles(modPackDiff([], mods("A", "B")).added)).toEqual(["A", "B"]);
    expect(titles(modPackDiff(mods("A", "B"), []).removed)).toEqual(["A", "B"]);
    expect(modPackDiff([], [])).toEqual({ added: [], removed: [], unchanged: [] });
  });

  it("compares the key, not the title, and counts a repeated key once", () => {
    const current = [{ key: "workshop:1", title: "Old title" }];
    const next = [
      { key: "workshop:1", title: "New title" },
      { key: "workshop:1", title: "Repeated" },
    ];
    const diff = modPackDiff(current, next);

    expect(diff.added).toEqual([]);
    expect(diff.removed).toEqual([]);
    expect(diff.unchanged).toEqual([{ key: "workshop:1", title: "New title" }]);
  });

  it("treats keys that look like object properties as plain strings", () => {
    const keys = ["__proto__", "constructor", "toString", "hasOwnProperty"];
    const current = keys.slice(0, 2).map((key) => ({ key, title: key }));
    const next = keys.slice(1).map((key) => ({ key, title: key }));
    const diff = modPackDiff(current, next);

    expect(titles(diff.removed)).toEqual(["__proto__"]);
    expect(titles(diff.unchanged)).toEqual(["constructor"]);
    expect(titles(diff.added)).toEqual(["toString", "hasOwnProperty"]);
  });
});
