import type { TranslationKey } from "@/app/localizationTypes";

/** One section of the server workspace, selected by the `tab` search parameter. */
export type WorkspaceSection = "overview" | "activity" | "settings" | "files" | "machine";

/**
 * The closed registry of workspace sections in navigation order.
 *
 * Overview is the default and writes no `tab` parameter; every other id is the value of `?tab=`. Anything not in
 * this list, including every retired section id, opens Overview.
 */
export const workspaceTabIds: readonly WorkspaceSection[] = [
  "overview",
  "activity",
  "settings",
  "files",
  "machine",
];

/** The localization key of the visible label of each workspace section. */
export const workspaceSectionLabelKeys = {
  overview: "Server_Workspace_Section_Overview",
  activity: "Server_Workspace_Section_Activity",
  settings: "Server_Workspace_Section_Settings",
  files: "Server_Workspace_Section_Files",
  machine: "Server_Workspace_Section_Machine",
} satisfies Record<WorkspaceSection, TranslationKey>;

/**
 * Resolves the untrusted `?tab=` value of the URL to a workspace section.
 *
 * The value is accepted only through an exact `includes` match against {@link workspaceTabIds}. It is never used to
 * index an object, so prototype keys such as `__proto__` cannot select anything. Retired, unknown, empty and
 * differently cased values resolve to Overview without an error and without rewriting the URL.
 *
 * @param value The raw `tab` search parameter, or null when absent.
 * @returns The matching section, or `overview` for every other value.
 */
export function workspaceSectionFromSearch(value: string | null): WorkspaceSection {
  if (value === null) return "overview";
  const match = workspaceTabIds.find((id) => id === value);
  return match ?? "overview";
}

/**
 * Builds the search string of a section link.
 *
 * Only the `tab` parameter is written, so a stale `?action=` deep link is dropped when the viewer switches section
 * and can never fire again.
 *
 * @param section The target section.
 * @returns An empty string for Overview, otherwise `?tab=<id>`.
 */
export function sectionSearch(section: WorkspaceSection): string {
  return section === "overview" ? "" : `?tab=${section}`;
}

function serverPathname(serverId: string): string {
  return `/command/servers/${encodeURIComponent(serverId)}`;
}

/**
 * Builds the router location of a workspace section.
 *
 * @param serverId The server primary key.
 * @param section The target section.
 * @returns A location with the workspace pathname and the section's search string.
 */
export function sectionTo(
  serverId: string,
  section: WorkspaceSection,
): { pathname: string; search: string } {
  return { pathname: serverPathname(serverId), search: sectionSearch(section) };
}

/**
 * Tells whether a navigation leaves one workspace section.
 *
 * Sections are compared after resolution through {@link workspaceSectionFromSearch}, so a search-only change inside
 * the section (for example a replaced `?action=` or a changed `path=`) never counts as leaving it. An unknown `tab`
 * value resolves to Overview and is therefore compared as Overview.
 *
 * @param current The location the viewer is on.
 * @param next The location being navigated to.
 * @param section The guarded section.
 * @returns True when the pathname differs or the next location resolves to another section.
 */
export function leavesWorkspaceSection(
  current: { pathname: string; search: string },
  next: { pathname: string; search: string },
  section: WorkspaceSection,
): boolean {
  if (next.pathname !== current.pathname) return true;
  return workspaceSectionFromSearch(new URLSearchParams(next.search).get("tab")) !== section;
}

/**
 * Builds the fragment id of an activity timeline entry.
 *
 * @param type The kind of entry: a lifecycle job, a worker command log or an audit record.
 * @param primaryKey The primary key of the job, command or operation.
 * @returns The anchor id `activity-<type>-<primaryKey>`.
 */
export function activityEntryAnchor(type: "job" | "log" | "audit", primaryKey: string): string {
  return `activity-${type}-${primaryKey}`;
}

/**
 * Builds the router location that opens one activity entry.
 *
 * @param serverId The server primary key.
 * @param anchor An entry anchor from {@link activityEntryAnchor}.
 * @returns A location with the workspace pathname, `?tab=activity` and the anchor as the fragment.
 */
export function activityEntryTo(
  serverId: string,
  anchor: string,
): { pathname: string; search: string; hash: string } {
  return {
    pathname: serverPathname(serverId),
    search: sectionSearch("activity"),
    hash: `#${anchor}`,
  };
}
