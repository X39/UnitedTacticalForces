import {
  filesLocationSearch,
  filesLocationTo,
  maxFilesPathLength,
  parseFilesPath,
  readFilesLocation,
} from "@/features/hosting/filesLocation";

describe("parseFilesPath", () => {
  it("treats a missing and an empty value as the valid top level", () => {
    expect(parseFilesPath(null)).toEqual({ path: "", invalid: false });
    expect(parseFilesPath("")).toEqual({ path: "", invalid: false });
  });

  it("accepts a normal nested folder unchanged", () => {
    expect(parseFilesPath("profiles/server")).toEqual({ path: "profiles/server", invalid: false });
    expect(parseFilesPath("My Folder/a.b")).toEqual({ path: "My Folder/a.b", invalid: false });
    expect(parseFilesPath("a".repeat(maxFilesPathLength))).toEqual({
      path: "a".repeat(maxFilesPathLength),
      invalid: false,
    });
  });

  it.each([
    ["a leading slash", "/a"],
    ["a trailing slash", "a/"],
    ["a double slash", "a//b"],
    ["a dot segment", "a/./b"],
    ["a parent segment", "a/../b"],
    ["a lone parent segment", ".."],
    ["a lone dot segment", "."],
    ["a backslash", "a\\b"],
    ["a control character", "a\u0007b"],
    ["a delete character", "a\u007fb"],
    ["a line break", "a\nb"],
    ["a value over the maximum length", "a".repeat(maxFilesPathLength + 1)],
  ])("rejects %s and returns the top level", (_name, value) => {
    expect(parseFilesPath(value)).toEqual({ path: "", invalid: true });
  });
});

describe("readFilesLocation", () => {
  it("reads area=live as the live context", () => {
    expect(readFilesLocation(new URLSearchParams("area=live")).location).toEqual({
      path: "",
      context: "live",
    });
  });

  it("reads an absent or unknown area as persistent without an invalid flag", () => {
    expect(readFilesLocation(new URLSearchParams("")).location.context).toBe("persistent");
    const unknown = readFilesLocation(new URLSearchParams("area=__proto__&path=a"));
    expect(unknown.location).toEqual({ path: "a", context: "persistent" });
    expect(unknown.invalid).toBe(false);
  });

  it("flags an invalid path while still reading the context", () => {
    const result = readFilesLocation(new URLSearchParams("area=live&path=a/../b"));
    expect(result).toEqual({ location: { path: "", context: "live" }, invalid: true });
  });
});

describe("filesLocationSearch", () => {
  it("keeps only the current tab and omits the top level path and the persistent area", () => {
    const current = new URLSearchParams("tab=files&action=start&path=old");
    expect(filesLocationSearch(current, { path: "", context: "persistent" })).toBe("?tab=files");
    expect(filesLocationSearch(new URLSearchParams(), { path: "", context: "persistent" })).toBe(
      "",
    );
  });

  it("builds an encoded path and the live area", () => {
    const current = new URLSearchParams("tab=files");
    expect(filesLocationSearch(current, { path: "profiles/server", context: "live" })).toBe(
      "?tab=files&path=profiles%2Fserver&area=live",
    );
  });

  it("round-trips through readFilesLocation", () => {
    const current = new URLSearchParams("tab=files");
    for (const location of [
      { path: "", context: "persistent" as const },
      { path: "profiles/My Server & more", context: "persistent" as const },
      { path: "a/b/c", context: "live" as const },
    ]) {
      const search = filesLocationSearch(current, location);
      const read = readFilesLocation(new URLSearchParams(search));
      expect(read).toEqual({ location, invalid: false });
    }
  });

  it("builds a router location with the pathname", () => {
    expect(
      filesLocationTo("/command/servers/1", new URLSearchParams("tab=files"), {
        path: "x",
        context: "persistent",
      }),
    ).toEqual({ pathname: "/command/servers/1", search: "?tab=files&path=x" });
  });
});
