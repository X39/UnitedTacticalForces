/**
 * Reads and writes the Files section location: the current folder in `?path=` and the live context in `?area=live`.
 *
 * The `path` parameter is untrusted input because anybody can share a link with any value. It must pass
 * {@link parseFilesPath} before it is used for a request or rendered as a link; the helpers in this module never
 * index an object with the raw values and never forward them unchecked.
 */
import type { HostingFileContext } from "@/features/hosting/types";

/** The longest accepted folder path in characters; the API's own path check stays authoritative. */
export const maxFilesPathLength = 1024;

/** The folder and context that the Files section shows. */
export interface FilesLocation {
  /** The server-relative folder; the empty string is the top level. */
  path: string;
  /** The file context: the persistent server data or the live worker files. */
  context: HostingFileContext;
}

/** Tells whether the text holds an ASCII control character (U+0000 to U+001F or U+007F). */
function hasControlCharacter(text: string): boolean {
  for (let index = 0; index < text.length; index += 1) {
    const code = text.charCodeAt(index);
    if (code <= 0x1f || code === 0x7f) return true;
  }
  return false;
}

/**
 * Parses the untrusted `?path=` value strictly.
 *
 * A path is accepted only when it is at most {@link maxFilesPathLength} characters, has no backslash or control
 * character and every segment between the `/` separators is non-empty and neither `.` nor `..`. The empty string
 * and a missing parameter are the valid top level. Nothing is normalized: a leading slash, a trailing slash and a
 * double slash are invalid so that one folder has exactly one URL.
 *
 * @param raw The raw search parameter, or null when absent.
 * @returns The accepted path, or the top level with `invalid` set when the value was rejected.
 */
export function parseFilesPath(raw: string | null): { path: string; invalid: boolean } {
  if (raw === null || raw === "") return { path: "", invalid: false };
  if (raw.length > maxFilesPathLength) return { path: "", invalid: true };
  if (raw.includes("\\") || hasControlCharacter(raw)) return { path: "", invalid: true };
  const rejected = raw
    .split("/")
    .some((segment) => segment === "" || segment === "." || segment === "..");
  return rejected ? { path: "", invalid: true } : { path: raw, invalid: false };
}

/**
 * Reads the Files location from the search parameters of the current URL.
 *
 * `area=live` selects the live context; a missing or any other `area` value selects the persistent context
 * without a notice. The path goes through {@link parseFilesPath}.
 *
 * @param search The search parameters of the current location.
 * @returns The location and whether the `path` value was rejected as invalid.
 */
export function readFilesLocation(search: URLSearchParams): {
  location: FilesLocation;
  invalid: boolean;
} {
  const { path, invalid } = parseFilesPath(search.get("path"));
  return {
    location: { path, context: search.get("area") === "live" ? "live" : "persistent" },
    invalid,
  };
}

/**
 * Builds the search string for a Files location.
 *
 * Only the current `tab` is carried over, so a stale `?action=` is dropped by a folder change. `path` is omitted at
 * the top level and `area` is written only for the live context.
 *
 * @param current The search parameters of the current location; only `tab` is read from them.
 * @param location The folder and context to link to; the path must already be trusted.
 * @returns An empty string or a `?`-prefixed search string.
 */
export function filesLocationSearch(current: URLSearchParams, location: FilesLocation): string {
  const next = new URLSearchParams();
  const tab = current.get("tab");
  if (tab !== null) next.set("tab", tab);
  if (location.path !== "") next.set("path", location.path);
  if (location.context === "live") next.set("area", "live");
  const text = next.toString();
  return text === "" ? "" : `?${text}`;
}

/**
 * Builds the router location of a Files folder.
 *
 * @param pathname The pathname of the workspace page.
 * @param current The search parameters of the current location.
 * @param location The folder and context to link to; the path must already be trusted.
 * @returns A router location with the workspace pathname and the Files search string.
 */
export function filesLocationTo(
  pathname: string,
  current: URLSearchParams,
  location: FilesLocation,
): { pathname: string; search: string } {
  return { pathname, search: filesLocationSearch(current, location) };
}
